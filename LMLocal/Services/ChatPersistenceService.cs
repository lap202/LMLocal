using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LMLocal.Common;
using LMLocal.Infrastructure;
using LMLocal.Models;
using Newtonsoft.Json;

namespace LMLocal.Services
{
    internal interface IChatPersistenceService
    {
        Task SaveLastMessageAsync(ChatMessage message, CancellationToken cancellationToken = default);
    }

    internal class ChatPersistenceService : IChatPersistenceService
    {
        private readonly string _chatHistoryDir;
        private readonly IFileSystem _fileSystem;
        private readonly ISettingsManager _settingsManager;
        private readonly System.Threading.SemaphoreSlim _writeSemaphore = new System.Threading.SemaphoreSlim(1, 1);

        public ChatPersistenceService(ISettingsManager settingsManager, IFileSystem fileSystem = null)
        {
            _settingsManager = settingsManager;
            _fileSystem = fileSystem ?? new DefaultFileSystem();
            _chatHistoryDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Defaults.LocalAppDataFolder,
                Defaults.ChatHistoryFolder
            );
            _fileSystem.CreateDirectory(_chatHistoryDir);
        }

        public async Task SaveLastMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            if (_settingsManager?.Current?.EnableChatLogging != true || message == null) return;
            
            await _writeSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                string fileName = $"{Defaults.ChatHistoryFilePrefix}{DateTime.UtcNow:yyyyMMdd_HH}.jsonl";
                string filePath = Path.Combine(_chatHistoryDir, fileName);

                var entry = new
                {
                    timestamp = DateTime.UtcNow,
                    role = message.Role,
                    content = message.Content
                };

                string jsonLine = JsonConvert.SerializeObject(entry) + Environment.NewLine;
                byte[] data = Encoding.UTF8.GetBytes(jsonLine);

                if (_fileSystem.FileExists(filePath))
                {
                    await _fileSystem.AppendAllBytesAsync(filePath, data, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await _fileSystem.WriteAllBytesAsync(filePath, data, cancellationToken).ConfigureAwait(false);
                }

                InternalLogger.Debug($"Chat message appended to {fileName}");
            }
            catch (Exception ex)
            {
                InternalLogger.Error("Failed to save chat message history, trying again", ex);

                try
                {
                    string fileName = $"{Defaults.ChatHistoryFilePrefix}{DateTime.UtcNow:yyyyMMdd_HH}_{Guid.NewGuid():N}.jsonl";
                    string filePath = Path.Combine(_chatHistoryDir, fileName);

                    var entry = new
                    {
                        timestamp = DateTime.UtcNow,
                        role = message.Role,
                        content = message.Content
                    };

                    string jsonLine = JsonConvert.SerializeObject(entry) + Environment.NewLine;
                    byte[] data = Encoding.UTF8.GetBytes(jsonLine);

                    await _fileSystem.WriteAllBytesAsync(filePath, data, cancellationToken).ConfigureAwait(false);
                    InternalLogger.Debug($"Chat message saved to new file {fileName}");
                }
                catch (Exception ex2)
                {
                    InternalLogger.Error("Failed to save chat message history", ex2);
                }
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }
    }
}
