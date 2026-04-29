using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Common;
using LMLocal.Infrastructure;
using LMLocal.Models;
using LMLocal.Services;
using Moq;
using NUnit.Framework;

namespace LMLocal.Tests.Unit.Infrastructure
{
    [TestFixture]
    public class ChatPersistenceServiceTests
    {
        [Test]
        public async Task SaveLastMessageAsync_WhenLoggingDisabled_DoesNotWrite()
        {
            var mockSettings = new Mock<ISettingsManager>();
            mockSettings.Setup(s => s.Current).Returns(new AppSettings { EnableChatLogging = false });
            var mockFileSystem = new Mock<IFileSystem>();

            var service = new ChatPersistenceService(mockSettings.Object, mockFileSystem.Object);
            var message = new ChatMessage("user", "test");

            await service.SaveLastMessageAsync(message);

            mockFileSystem.Verify(fs => fs.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
            mockFileSystem.Verify(fs => fs.AppendAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task SaveLastMessageAsync_WhenLoggingEnabled_WritesFile()
        {
            var mockSettings = new Mock<ISettingsManager>();
            mockSettings.Setup(s => s.Current).Returns(new AppSettings { EnableChatLogging = true });
            var mockFileSystem = new Mock<IFileSystem>();

            var service = new ChatPersistenceService(mockSettings.Object, mockFileSystem.Object);
            var message = new ChatMessage("user", "hello");

            mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

            await service.SaveLastMessageAsync(message);

            mockFileSystem.Verify(fs => fs.WriteAllBytesAsync(
                It.Is<string>(p => p.Contains(Defaults.ChatHistoryFilePrefix)), 
                It.IsAny<byte[]>(), 
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Test]
        public async Task SaveLastMessageAsync_WithNullMessage_DoesNotWrite()
        {
            var mockSettings = new Mock<ISettingsManager>();
            mockSettings.Setup(s => s.Current).Returns(new AppSettings { EnableChatLogging = true });
            var mockFileSystem = new Mock<IFileSystem>();

            var service = new ChatPersistenceService(mockSettings.Object, mockFileSystem.Object);
            ChatMessage message = null;

            await service.SaveLastMessageAsync(message);

            mockFileSystem.Verify(fs => fs.WriteAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
            mockFileSystem.Verify(fs => fs.AppendAllBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void SaveChatAsync_CreatesDirectoryIfNotExists()
        {
            var mockSettings = new Mock<ISettingsManager>();
            mockSettings.Setup(s => s.Current).Returns(new AppSettings { EnableChatLogging = true });
            var mockFileSystem = new Mock<IFileSystem>();

            new ChatPersistenceService(mockSettings.Object, mockFileSystem.Object);

            mockFileSystem.Verify(fs => fs.CreateDirectory(It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task SaveLastMessageAsync_WithRealFileSystem_WritesValidJsonl()
        {
            var mockSettings = new Mock<ISettingsManager>();
            mockSettings.Setup(s => s.Current).Returns(new AppSettings { EnableChatLogging = true });
            var fileSystem = new InMemoryFileSystem();

            var service = new ChatPersistenceService(mockSettings.Object, fileSystem);
            var message = new ChatMessage("user", "test message");

            await service.SaveLastMessageAsync(message);

            var files = fileSystem.GetAllFiles().ToList();
            Assert.That(files.Count, Is.GreaterThan(0));
            var content = fileSystem.ReadAllText(files.First());
            Assert.That(content, Does.Contain("test message"));
            Assert.That(content, Does.Contain("timestamp"));
        }
    }
}
