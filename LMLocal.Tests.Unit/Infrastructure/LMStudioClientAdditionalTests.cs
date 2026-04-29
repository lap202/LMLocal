using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Infrastructure.Lm;
using LMLocal.Models;
using NUnit.Framework;

namespace LMLocal.Tests.Unit.Infrastructure
{
    class FakeHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public FakeHandler(HttpResponseMessage response) { _response = response; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    [TestFixture]
    public class LMStudioClientAdditionalTests
    {
        [Test]
        public async Task SendChatAsync_ParsesContent()
        {
            var json = "{ \"choices\": [ { \"message\": { \"content\": \"hello world\" } } ] }";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };
            var client = new HttpClient(new FakeHandler(response));
            var lm = new LMStudioClient(client, "http://localhost:1234");
            var messageContext = new MessageContext(new List<ChatMessage>());
            var modelContext = new ModelContext("test-model");
            var result = await lm.SendChatAsync(messageContext, modelContext, CancellationToken.None);
            Assert.That(result?.Choices?[0]?.Message?.Content, Is.EqualTo("hello world"));
        }

        [Test]
        public void SendChatAsync_ErrorResponse_ThrowsWithMessage()
        {
            var json = "{ \"error\": { \"message\": \"Bad things\" } }";
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(json)
            };
            var client = new HttpClient(new FakeHandler(response));
            var lm = new LMStudioClient(client, "http://localhost:1234");
            var messageContext = new MessageContext(new List<ChatMessage>());
            var modelContext = new ModelContext("test-model");
            Assert.ThrowsAsync<HttpRequestException>(async () => await lm.SendChatAsync(messageContext, modelContext, CancellationToken.None));
        }
    }
}
