using NUnit.Framework;
using LMLocal.Infrastructure.Api;

namespace LMLocal.Tests.Unit.Infrastructure
{
    [TestFixture]
    public class ProviderResolverTests
    {
        [Test]
        public void ResolveProvider_ReturnsLmStudio_ForPort1234()
        {
            var p = ProviderResolver.ResolveProvider("http://localhost:1234");
            Assert.That(p, Is.EqualTo(ModelProvider.LmStudio));
        }

        [Test]
        public void ResolveProvider_ReturnsOllama_ForPort11434()
        {
            var p = ProviderResolver.ResolveProvider("http://localhost:11434");
            Assert.That(p, Is.EqualTo(ModelProvider.Ollama));
        }

        [Test]
        public void ResolveProvider_ReturnsOpenAi_ForOtherOrInvalid()
        {
            var p1 = ProviderResolver.ResolveProvider("http://example.com:8080");
            Assert.That(p1, Is.EqualTo(ModelProvider.OpenAi));

            var p2 = ProviderResolver.ResolveProvider("not a url");
            Assert.That(p2, Is.EqualTo(ModelProvider.OpenAi));

            var p3 = ProviderResolver.ResolveProvider(null);
            Assert.That(p3, Is.EqualTo(ModelProvider.OpenAi));
        }
    }
}
