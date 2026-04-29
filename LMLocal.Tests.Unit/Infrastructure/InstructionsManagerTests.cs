using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Infrastructure;
using LMLocal.Services;
using NUnit.Framework;

namespace LMLocal.Tests.Unit.Infrastructure
{
    [TestFixture]
    public class InstructionsManagerTests
    {
        [Test]
        public async Task GetAsync_ReturnsEmptyObject_WhenFileMissing()
        {
            var fs = new InMemoryFileSystem();
            var path = "instructions.json";
            var manager = new InstructionsManager(path, fs);
            var result = await manager.GetAsync();
            Assert.That(result, Is.EqualTo("{}"));
        }

        [Test]
        public async Task GetAsync_ReturnsContent_WhenFileExists()
        {
            var fs = new InMemoryFileSystem();
            var path = "instructions.json";
            var content = "{\"k\":\"v\"}";
            await fs.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes(content));
            var manager = new InstructionsManager(path, fs);
            var result = await manager.GetAsync();
            Assert.That(result, Is.EqualTo(content));
        }

        [Test]
        public async Task GetAsync_ReturnsEmptyObject_OnReadError()
        {
            var fs = new ThrowingReadFileSystem();
            var path = "instructions.json";
            await fs.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes("{}"));
            var manager = new InstructionsManager(path, fs);
            var result = await manager.GetAsync();
            Assert.That(result, Is.EqualTo("{}"));
        }

        [Test]
        public async Task UpdateAsync_WritesContent_WhenValidJson()
        {
            var fs = new InMemoryFileSystem();
            var path = "instructions.json";
            var manager = new InstructionsManager(path, fs);
            var json = "{\"a\":1}";
            await manager.UpdateAsync(json);
            var stored = fs.ReadAllText(path);
            Assert.That(stored, Is.EqualTo(json));
        }

        [Test]
        public async Task UpdateAsync_WritesEmptyObject_WhenInputNullOrWhitespace()
        {
            var fs = new InMemoryFileSystem();
            var path = "instructions.json";
            var manager = new InstructionsManager(path, fs);
            await manager.UpdateAsync(null);
            var stored = fs.ReadAllText(path);
            Assert.That(stored, Is.EqualTo("{}"));
            await manager.UpdateAsync("   ");
            stored = fs.ReadAllText(path);
            Assert.That(stored, Is.EqualTo("{}"));
        }

        [Test]
        public void UpdateAsync_ThrowsInvalidOperationException_OnInvalidJson()
        {
            var fs = new InMemoryFileSystem();
            var path = "instructions.json";
            var manager = new InstructionsManager(path, fs);
            Assert.That(async () => await manager.UpdateAsync("not json"), Throws.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public void Constructor_ValidatesPath_And_EnsuresDirectory()
        {
            var spy = new SpyFileSystem();
            var path = "instructions.json";
            var _ = new InstructionsManager(path, spy);
            Assert.That(spy.ValidateCalled, Is.True);
            Assert.That(spy.EnsureDirectoryCalled, Is.True);
        }

        private class ThrowingReadFileSystem : IFileSystem
        {
            private readonly InMemoryFileSystem _inner = new InMemoryFileSystem();
            public void CreateDirectory(string path) { }
            public bool FileExists(string path) => _inner.FileExists(path);
            public string ReadAllText(string path) => throw new Exception("read error");
            public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) => throw new Exception("read error");
            public Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default) => _inner.WriteAllBytesAsync(path, data, cancellationToken);
            public Task AppendAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default) => _inner.AppendAllBytesAsync(path, data, cancellationToken);
            public void Replace(string sourceFileName, string destinationFileName) => _inner.Replace(sourceFileName, destinationFileName);
            public void Move(string sourceFileName, string destinationFileName) => _inner.Move(sourceFileName, destinationFileName);
            public void Delete(string path) => _inner.Delete(path);
            public System.Collections.Generic.IEnumerable<string> GetAllFiles() => _inner.GetAllFiles();
            public void ValidateFilePath(string filePath) { }
            public void EnsureDirectoryExistsForFile(string filePath) { }
        }

        private class SpyFileSystem : IFileSystem
        {
            public bool ValidateCalled { get; private set; }
            public bool EnsureDirectoryCalled { get; private set; }
            public void CreateDirectory(string path) { }
            public bool FileExists(string path) => false;
            public string ReadAllText(string path) => throw new System.IO.FileNotFoundException();
            public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(ReadAllText(path));
            public Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default) { return Task.CompletedTask; }
            public Task AppendAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default) { return Task.CompletedTask; }
            public void Replace(string sourceFileName, string destinationFileName) { }
            public void Move(string sourceFileName, string destinationFileName) { }
            public void Delete(string path) { }
            public System.Collections.Generic.IEnumerable<string> GetAllFiles() { yield break; }
            public void ValidateFilePath(string filePath) { ValidateCalled = true; }
            public void EnsureDirectoryExistsForFile(string filePath) { EnsureDirectoryCalled = true; }
        }
    }
}
