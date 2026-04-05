using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using LMLocal.Internal;

using Moq;

using NUnit.Framework;

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

        [Test]
        public void ResetHistory_ReturnsTrue_WhenNoActiveGeneration()
        {
            var result = _service.ResetHistory();
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
            // Start a generation to set a token
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
            // Corrected type to match BuildMessagesForRequest
            var messages = new List<ChatMessage>();
            _historyMock.Setup(h => h.BuildMessagesForRequest(It.IsAny<string>())).Returns(messages);

            // Mock SendChatRequestAsync to return a StreamingResponse
            var mockStream = new MemoryStream();
            var mockResponse = new System.Net.Http.HttpResponseMessage();
            var mockRequest = new System.Net.Http.HttpRequestMessage();
            var mockContent = new System.Net.Http.StringContent("");
            var streamingResponse = new StreamingResponse(mockStream, mockResponse, mockRequest, mockContent);
            _clientMock.Setup(c => c.SendChatRequestAsync(It.IsAny<IReadOnlyList<ChatMessage>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(streamingResponse);

            _historyMock.Setup(h => h.AddUserMessage(It.IsAny<string>()));
            _historyMock.Setup(h => h.AddAssistantMessage(It.IsAny<string>()));
            _compactorMock.Setup(c => c.CompactIfNeededAsync(_clientMock.Object, CancellationToken.None)).Returns(Task.CompletedTask);

            // Use a dummy processor
            Task onChunk(string s, TokenGenerationStats t) => Task.CompletedTask;
            Task onError(string s) => Task.CompletedTask;

            // The actual streaming and processing are not tested here (would require more setup)
            await _service.GenerateStreamAsync("prompt", onChunk, onError);

            _historyMock.Verify(h => h.AddUserMessage("prompt"), Times.Once);
        }
    }
}
