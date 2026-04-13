using LMLocal.Internal;
using NUnit.Framework;
using System;

namespace LMLocal.Tests.Unit.Internal
{
    [TestFixture]
    public class LlmSseParserTests
    {
        [Test]
        public void ExtractDelta_ReturnsNull_OnDoneLine()
        {
            // Should return null for the [DONE] marker
            int tokens = 0;
            var result = LlmSseParser.ExtractDelta("data: [DONE]", ref tokens);
            Assert.That(result.IsEmpty, Is.True);
        }

        [Test]
        public void ExtractDelta_ParsesDeltaAndIncrementsTokens()
        {
            // Should extract delta and increment tokens if usage is not present
            int tokens = 0;
            var json = "data: {\"choices\":[{\"delta\":{\"content\":\"hi\"}}]}";
            var result = LlmSseParser.ExtractDelta(json, ref tokens);
            Assert.That(result.Text, Is.EqualTo("hi"));
            Assert.That(tokens, Is.EqualTo(1));
        }

        [Test]
        public void ExtractDelta_UpdatesTokensFromUsage()
        {
            // Should update tokens from usage if present
            int tokens = 0;
            var json = "data: {\"choices\":[{\"delta\":{\"content\":\"hi\"}}],\"usage\":{\"total_tokens\":42}}";
            var result = LlmSseParser.ExtractDelta(json, ref tokens);
            Assert.That(result.Text, Is.EqualTo("hi"));
            Assert.That(tokens, Is.EqualTo(42));
        }

        [Test]
        public void ExtractDelta_ReturnsNull_OnMalformedJson()
        {
            // Should throw or return null on invalid JSON
            int tokens = 0;
            Assert.That(() =>
                LlmSseParser.ExtractDelta("data: {not a json}", ref tokens),
                Throws.InstanceOf<Exception>());
        }

        [Test]
        public void ExtractDelta_ReturnsNull_WhenNoChoices()
        {
            // Should return null if choices array is missing or empty
            int tokens = 0;
            var json = "data: {\"choices\":[]}";
            var result = LlmSseParser.ExtractDelta(json, ref tokens);
            Assert.That(result.IsEmpty, Is.True);
        }
    }
}
