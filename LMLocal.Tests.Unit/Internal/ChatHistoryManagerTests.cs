using LMLocal.Models;
using LMLocal.Services;
using Moq;
using NUnit.Framework;

namespace LMLocal.Tests.Unit.Internal
{
    [TestFixture]
    public class ChatHistoryManagerTests
    {
        [Test]
        public void AddUserMessage_AddsMessageToHistory()
        {
            var manager = new ChatHistoryManager("sys");

            manager.AddUserMessage("hello");
            var history = manager.GetHistoryCopy();

            Assert.That(history.Count, Is.EqualTo(1));
            Assert.That(history[0].Role, Is.EqualTo("user"));
            Assert.That(history[0].Content, Is.EqualTo("hello"));
        }

        [Test]
        public void AddAssistantMessage_DoesNotAddEmpty()
        {
            var manager = new ChatHistoryManager("sys");

            manager.AddAssistantMessage("");
            var history = manager.GetHistoryCopy();

            Assert.That(history.Count, Is.EqualTo(0));
        }

        [Test]
        public void AddAssistantMessage_CallsPersistenceService()
        {
            var mockPersistence = new Mock<IChatPersistenceService>();
            var manager = new ChatHistoryManager("sys", mockPersistence.Object);

            manager.AddUserMessage("hello");
            manager.AddAssistantMessage("response");

            mockPersistence.Verify(p => p.SaveLastMessageAsync(It.IsAny<LMLocal.Models.ChatMessage>(), It.IsAny<System.Threading.CancellationToken>()), Times.Exactly(2));
        }

        [Test]
        public void AddAssistantMessage_WithNullPersistence_DoesNotThrow()
        {
            var manager = new ChatHistoryManager("sys", null);

            manager.AddUserMessage("hello");
            manager.AddAssistantMessage("response");

            var history = manager.GetHistoryCopy();
            Assert.That(history.Count, Is.EqualTo(2));
        }

        [Test]
        public void AddUserMessage_WithDynamicSettings_UsesCurrentCompressionSetting()
        {
            var mockSettings = new Mock<ISettingsManager>();
            mockSettings.Setup(s => s.Current).Returns(new AppSettings { EnableHistoryCompression = true });
            var manager = new ChatHistoryManager("sys", null, mockSettings.Object);

            manager.AddUserMessage("**bold**");
            var history = manager.GetHistoryCopy();

            Assert.That(history[0].Content, Does.Not.Contain("**"));
        }

        [Test]
        public void AddAssistantMessage_WithDynamicSettings_UsesCurrentCompressionSetting()
        {
            var mockSettings = new Mock<ISettingsManager>();
            mockSettings.Setup(s => s.Current).Returns(new AppSettings { EnableHistoryCompression = true });
            var manager = new ChatHistoryManager("sys", null, mockSettings.Object);

            manager.AddAssistantMessage("**bold**");
            var history = manager.GetHistoryCopy();

            Assert.That(history[0].Content, Does.Not.Contain("**"));
        }

        [Test]
        public void Clear_RemovesAllMessages()
        {
            var manager = new ChatHistoryManager("sys");
            manager.AddUserMessage("a");
            manager.AddAssistantMessage("b");

            manager.Clear();
            var history = manager.GetHistoryCopy();

            Assert.That(history.Count, Is.EqualTo(0));
        }
    }
}
