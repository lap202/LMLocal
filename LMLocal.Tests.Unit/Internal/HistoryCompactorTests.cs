using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LMLocal.Services;
using LMLocal.Models;
using Moq;
using NUnit.Framework;

namespace LMLocal.Tests.Unit.Internal
{
    [TestFixture]
    public class HistoryCompactorTests
    {
        [Test]
        public void NeedsCompaction_ReturnsTrue_WhenOverThreshold()
        {
            var mockSettings = new Mock<ISettingsManager>();
            mockSettings.Setup(s => s.Current).Returns(new AppSettings { EnableHistoryCompaction = true });

            var history = new ChatHistoryManager("", null, mockSettings.Object);
            for (int i = 0; i < 100; i++)
            {
                history.AddUserMessage(new string('a', 100));
            }

            var compactor = new HistoryCompactor(history,  null, mockSettings.Object);
            compactor.SetMaxContext(1000);

            var needs = compactor.NeedsCompaction();

            Assert.That(needs, Is.True);
        }

        [Test]
        public void NeedsCompaction_ReturnsFalse_WhenBelowThreshold()
        {
            var mockSettings = new Mock<ISettingsManager>();
            mockSettings.Setup(s => s.Current).Returns(new AppSettings { EnableHistoryCompaction = true });

            var history = new ChatHistoryManager("", null, mockSettings.Object);
            history.AddUserMessage("short");
            var compactor = new HistoryCompactor(history,  null, mockSettings.Object);
            compactor.SetMaxContext(10000);

            var needs = compactor.NeedsCompaction();

            Assert.That(needs, Is.False);
        }

        [Test]
        public void NeedsCompaction_WithDynamicSettings_ReadsCurrentSetting()
        {
            var mockHistory = new Mock<IChatHistoryManager>();
            mockHistory.Setup(h => h.GetHistoryCopy()).Returns(new List<ChatMessage>());
            var mockSettings = new Mock<ISettingsManager>();
            mockSettings.Setup(s => s.Current).Returns(new AppSettings { EnableHistoryCompaction = true });

            var compactor = new HistoryCompactor(mockHistory.Object, null, mockSettings.Object);
            compactor.SetMaxContext(100);

            var result = compactor.NeedsCompaction();

            Assert.That(result, Is.False);
        }

        [Test]
        public void NeedsCompaction_WithSettingsDisabled_ReturnsFalse()
        {
            var mockHistory = new Mock<IChatHistoryManager>();
            mockHistory.Setup(h => h.GetHistoryCopy()).Returns(
                Enumerable.Range(0, 100).Select(i => new ChatMessage("user", "x")).ToList()
            );
            var mockSettings = new Mock<ISettingsManager>();
            mockSettings.Setup(s => s.Current).Returns(new AppSettings { EnableHistoryCompaction = false });

            var compactor = new HistoryCompactor(mockHistory.Object,  null, mockSettings.Object);
            compactor.SetMaxContext(100);

            var result = compactor.NeedsCompaction();

            Assert.That(result, Is.False);
        }
    }
}
