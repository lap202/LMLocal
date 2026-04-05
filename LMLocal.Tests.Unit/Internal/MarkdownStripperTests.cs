using LMLocal.Internal;
using NUnit.Framework;

namespace LMLocal.Tests.Unit.Internal
{
    [TestFixture]
    public class MarkdownStripperTests
    {
        [Test]
        public void Strip_RemovesMarkdownSyntax_ReturnsPlainText()
        {
            // Arrange
            var input = "# Header\n**bold** _italic_ [link](url)\n- item";
            var expected = "Header\nbold italic link\nitem";

            // Act
            var result = MarkdownStripper.Strip(input);

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Strip_EmptyOrNull_ReturnsInput()
        {
            // Arrange/Act/Assert
            Assert.That(MarkdownStripper.Strip(null), Is.Null);
            Assert.That(MarkdownStripper.Strip(""), Is.EqualTo(""));
        }
    }
}
