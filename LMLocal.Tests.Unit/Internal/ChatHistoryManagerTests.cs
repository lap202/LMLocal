using LMLocal.Internal;
using NUnit.Framework;

namespace LMLocal.Tests.Unit.Internal
{
    [TestFixture]
    public class ChatHistoryManagerTests
    {
        [Test]
        public void AddUserMessage_AddsMessageToHistory()
        {
            // Arrange
            var manager = new ChatHistoryManager("sys");

            // Act
            manager.AddUserMessage("hello");
            var history = manager.GetHistoryCopy();

            // Assert
            Assert.That(history.Count, Is.EqualTo(1));
            Assert.That(history[0].Role, Is.EqualTo("user"));
            Assert.That(history[0].Content, Is.EqualTo("hello"));
        }

        [Test]
        public void AddAssistantMessage_DoesNotAddEmpty()
        {
            // Arrange
            var manager = new ChatHistoryManager("sys");

            // Act
            manager.AddAssistantMessage("");
            var history = manager.GetHistoryCopy();

            // Assert
            Assert.That(history.Count, Is.EqualTo(0));
        }

        [Test]
        public void Clear_RemovesAllMessages()
        {
            // Arrange
            var manager = new ChatHistoryManager("sys");
            manager.AddUserMessage("a");
            manager.AddAssistantMessage("b");

            // Act
            manager.Clear();
            var history = manager.GetHistoryCopy();

            // Assert
            Assert.That(history.Count, Is.EqualTo(0));
        }
    }
}
