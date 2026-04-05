using LMLocal.Internal;
using NUnit.Framework;

namespace LMLocal.Tests.Unit.Internal
{
    [TestFixture]
    public class LMStudioClientTests
    {
        [TestCase("{\"error\":{\"message\":\"Something went wrong\"}}", "Something went wrong")]
        [TestCase("{\"error\":{}}", null)]
        [TestCase("{}", null)]
        [TestCase("", null)]
        [TestCase("invalid json", null)]
        public void TryExtractErrorMessage_HandlesVariousInputs(string raw, string expected)
        {
            // Testing the internal static method
            string result = LMStudioClient.TryExtractErrorMessage(raw);
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
