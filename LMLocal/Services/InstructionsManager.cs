using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Common;
using LMLocal.Infrastructure;
using Newtonsoft.Json.Linq;


namespace LMLocal.Services
{
    /// <summary>
    /// Simple manager for instructions stored in a local JSON file.
    /// </summary>
    public interface IInstructionsManager
    {
        Task<string> GetAsync(CancellationToken cancellationToken = default);
        Task UpdateAsync(string jsonInstructions, CancellationToken cancellationToken = default);
    }

    internal class InstructionsManager : IInstructionsManager
    {
        private readonly string _filePath;
        private readonly IFileSystem _fileSystem;

        public InstructionsManager()
            : this(
                  Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Defaults.LocalAppDataFolder,
                    Defaults.LocalAppInstructionsFileName
                  ),
                  null
              )
        {
        }

        public InstructionsManager(string filePath)
            : this(filePath, null)
        {
        }

        public InstructionsManager(string filePath, IFileSystem fileSystem)
        {
            _fileSystem = fileSystem ?? new DefaultFileSystem();
            _fileSystem.ValidateFilePath(filePath);
            _fileSystem.EnsureDirectoryExistsForFile(filePath);
            _filePath = filePath;
        }

        public async Task<string> GetAsync(CancellationToken cancellationToken = default)
        {
            if (!_fileSystem.FileExists(_filePath))
            {
                return "{}";
            }

            try
            {
                return await _fileSystem.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading instructions: {ex}");
                return "{}";
            }
        }

        public async Task UpdateAsync(string jsonInstructions, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(jsonInstructions))
            {
                jsonInstructions = "{}";
            }

            try
            {
                JObject.Parse(jsonInstructions);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Invalid JSON format: {ex.Message}", ex);
            }

            byte[] data = Encoding.UTF8.GetBytes(jsonInstructions);
            await _fileSystem.WriteAllBytesAsync(_filePath, data, cancellationToken).ConfigureAwait(false);
        }
    }
}
