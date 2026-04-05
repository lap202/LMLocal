using System.Collections.Generic;
using System.Threading.Tasks;
using LMLocal.Internal;
using NUnit.Framework;

namespace LMLocal.Tests.Unit.Internal
{
    [TestFixture]
    public class HistoryCompactorTests
    {
        [Test]
        public void NeedsCompaction_ReturnsTrue_WhenOverThreshold()
        {
            // Arrange
            var history = new ChatHistoryManager("");
            for (int i = 0; i < 100; i++)
            {
                history.AddUserMessage(new string('a', 100));
            }

            var compactor = new HistoryCompactor(history, "sys");
            compactor.SetMaxContext(1000);

            // Act
            var needs = compactor.NeedsCompaction();

            // Assert
            Assert.That(needs, Is.True);
        }

        [Test]
        public void NeedsCompaction_ReturnsFalse_WhenBelowThreshold()
        {
            // Arrange
            var history = new ChatHistoryManager("");
            history.AddUserMessage("short");
            var compactor = new HistoryCompactor(history, "sys");
            compactor.SetMaxContext(10000);

            // Act
            var needs = compactor.NeedsCompaction();

            // Assert
            Assert.That(needs, Is.False);
        }
    }
}
