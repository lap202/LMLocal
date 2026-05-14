using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LMLocal.Tests.E2E.VSIX
{
    internal static class VsLauncher
    {
        private const string DevenvPath = @"D:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe";

        public static async Task<Process> StartExperimentalInstanceAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(DevenvPath))
                throw new InvalidOperationException($"devenv.exe not found at '{DevenvPath}'");

            var startInfo = new ProcessStartInfo(DevenvPath, $"/rootsuffix Exp ")
            {
                UseShellExecute = false,
                CreateNoWindow = false
            };
            startInfo.EnvironmentVariables["LMLocal_IPC"] = "1";

            var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start VS");

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            return process;
        }
    }
}
