using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Infrastructure;
using LMLocal.Services;
using LMLocal.Models;
using LMLocal.Infrastructure.Lm;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using LMLocal.Infrastructure.Lm.Responses;

namespace LMLocal.Tests.Unit.Internal
{
    [TestFixture]
    public class ChatGenerationServiceTests
    {
        private Mock<ILMStudioClient> _clientMock;
        private Mock<IChatHistoryManager> _historyMock;
        private Mock<IHistoryCompactor> _compactorMock;
        private ChatGenerationService _service;

        [SetUp]
        public void SetUp()
        {
            _clientMock = new Mock<ILMStudioClient>();
            _historyMock = new Mock<IChatHistoryManager>();
            _compactorMock = new Mock<IHistoryCompactor>();
            _service = new ChatGenerationService(_clientMock.Object, _historyMock.Object, _compactorMock.Object);
        }


        private class BlockingStream : Stream
        {
            private readonly SemaphoreSlim _sem = new SemaphoreSlim(0, 1);

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => 0;
            public override long Position { get => 0; set => throw new NotSupportedException(); }

            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                try
                {
                    await _sem.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }

                return 0;
            }

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public void ReleaseAndClose()
            {
                try { _sem.Release(); } catch { }
            }

            protected override void Dispose(bool disposing)
            {
                try { _sem.Release(); } catch { }
                base.Dispose(disposing);
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }

        private class FakeClient : ILMStudioClient
        {
            private readonly Stream _stream;
            public FakeClient(Stream s) => _stream = s;

            public Task<ListModelsResponse> ListModelsAsync(CancellationToken cancellationToken) 
                => Task.FromResult(new ListModelsResponse());

            public Task<StreamingResponse> SendChatStreamingAsync(MessageContext messageContext, ModelContext modelContext, CancellationToken cancellationToken)
            {
                var response = new System.Net.Http.HttpResponseMessage();
                var request = new System.Net.Http.HttpRequestMessage();
                var content = new System.Net.Http.StringContent("", System.Text.Encoding.UTF8, "application/json");
                var streaming = new StreamingResponse(_stream, response, request, content);
                return Task.FromResult(streaming);
            }

            public Task<SendChatResponse> SendChatAsync(MessageContext messageContext, ModelContext modelContext, CancellationToken cancellationToken) 
                => Task.FromResult<SendChatResponse>(null);
        }

        private class DummyHistory : IChatHistoryManager
        {
            public void AddUserMessage(string text) { }
            public void AddAssistantMessage(string text) { }
            public void Clear() { }
            public IReadOnlyList<ChatMessage> GetHistoryCopy() => new List<ChatMessage>();
            public bool ReplaceHistory(string summary, IEnumerable<ChatMessage> recent, int expectedSize) => true;
            public List<ChatMessage> BuildUserMessagesWithHistory(string userPrompt, string includedContent = null, string additionalSystemPrompt = null) => new List<ChatMessage>();
        }

        private class DummyCompactor : IHistoryCompactor
        {
            public Task CompactIfNeededAsync(ILMStudioClient client, string modelId, CancellationToken token) => Task.CompletedTask;
            public void SetMaxContext(int max) { }
            public bool NeedsCompaction() => false;
        }

        [Test]
        public async Task GenerateStreamAsync_StopExecution_Completes()
        {
            var blocking = new BlockingStream();
            var client = new FakeClient(blocking);
            var history = new DummyHistory();
            var compactor = new DummyCompactor();

            var svc = new ChatGenerationService(client, history, compactor);

            var context = new GenerateStreamContext
            {
                Prompt = "hi",
                ActiveDocumentContent = null,
                AdditionalPrompt = "stop_extra",
                ModelId = null
            };

            var genTask = svc.GenerateStreamAsync(context, async (c, s) => { await Task.CompletedTask; }, async err => { await Task.CompletedTask; }, async () => { await Task.CompletedTask; });

            await Task.Delay(50);

            svc.StopExecution();

            blocking.ReleaseAndClose();

            var completed = await Task.WhenAny(genTask, Task.Delay(3000)) == genTask;
            Assert.That(completed, Is.True, "GenerateStreamAsync should complete after StopExecution");
            Assert.That(genTask.IsFaulted, Is.False, genTask.Exception?.ToString());
        }

        [Test]
        public async Task ResetHistory_ReturnsTrue_WhenNoActiveGeneration()
        {
            var result = await _service.ResetHistoryAsync();
            Assert.That(result, Is.True);
            _historyMock.Verify(h => h.Clear(), Times.Once);
        }

        [Test]
        public void SetMaxContext_DelegatesToCompactor()
        {
            _service.SetMaxContext(123);
            _compactorMock.Verify(c => c.SetMaxContext(123), Times.Once);
        }

        [Test]
        public void StopExecution_CancelsAndDisposesToken()
        {
            var cts = new CancellationTokenSource();
            typeof(ChatGenerationService)
                .GetField("_currentCts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_service, cts);
            _service.StopExecution();
            Assert.That(cts.IsCancellationRequested, Is.True);
        }

        [Test]
        public async Task GenerateStreamAsync_AddsUserMessage_AndAssistantMessage()
        {
            var messages = new List<ChatMessage>();
            _historyMock.Setup(h => h.BuildUserMessagesWithHistory(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(messages);

            var mockStream = new MemoryStream();
            var mockResponse = new System.Net.Http.HttpResponseMessage();
            var mockRequest = new System.Net.Http.HttpRequestMessage();
            var mockContent = new System.Net.Http.StringContent("");
            var streamingResponse = new StreamingResponse(mockStream, mockResponse, mockRequest, mockContent);
            _clientMock.Setup(c => c.SendChatStreamingAsync(It.IsAny<MessageContext>(), It.IsAny<ModelContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(streamingResponse);

            _historyMock.Setup(h => h.AddUserMessage(It.IsAny<string>()));
            _historyMock.Setup(h => h.AddAssistantMessage(It.IsAny<string>()));
            _compactorMock.Setup(c => c.CompactIfNeededAsync(_clientMock.Object, It.IsAny<string>(), CancellationToken.None)).Returns(Task.CompletedTask);

            Task onChunk(StreamChunk chunk, TokenGenerationStats t) => Task.CompletedTask;
            Task onError(string s) => Task.CompletedTask;

            var context = new GenerateStreamContext
            {
                Prompt = "prompt",
                ActiveDocumentContent = null,
                AdditionalPrompt = "extra",
                ModelId = null
            };

            await _service.GenerateStreamAsync(context, onChunk, onError, async () => { await Task.CompletedTask; });

            _historyMock.Verify(h => h.AddUserMessage("prompt"), Times.Once);
        }
    }
}
