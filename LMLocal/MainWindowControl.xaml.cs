using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LMLocal.Common;
using LMLocal.Infrastructure.WebView;
using LMLocal.Services;
using Microsoft.Web.WebView2.Core;

namespace LMLocal
{
    /// <summary>
    /// Interaction logic for MainWindowControl.
    /// </summary>
    public partial class MainWindowControl : UserControl, IDisposable
    {
        private static CoreWebView2Environment sharedEnvironment;
        private static readonly SemaphoreSlim _envLock = new SemaphoreSlim(1, 1);

        private bool _disposed;
        private bool _webViewInitialized;
        private bool _webViewInitializing;

        public MainWindowControl()
        {
            this.InitializeComponent();
            this.Focusable = true;
            this.Loaded += OnControlLoaded;
        }

        private async Task OnControlLoadedAsync(object sender, RoutedEventArgs e)
        {
            if (_webViewInitialized || _webViewInitializing)
            {
                return;
            }

            _webViewInitializing = true;

            try
            {
                await _envLock.WaitAsync();

                var settingsManager = new SettingsManager();
                await settingsManager.LoadAsync();

                try
                {
                    if (sharedEnvironment == null)
                    {
                        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        string userDataFolder = Path.Combine(localAppData, Defaults.LocalAppDataFolder, Defaults.WebViewUserDataFolder);
                        Directory.CreateDirectory(userDataFolder);
                        sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                    }
                }
                finally
                {
                    _envLock.Release();
                }


                await chatBrowser.EnsureCoreWebView2Async(sharedEnvironment);

                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string resourcesPath = Path.Combine(assemblyDir, "Resources");
                chatBrowser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    Defaults.VirtualHostName,
                    resourcesPath,
                    CoreWebView2HostResourceAccessKind.Allow
                );

                chatBrowser.CoreWebView2.AddHostObjectToScript("bridge", new WebViewBridge(chatBrowser, settingsManager));

                chatBrowser.HorizontalAlignment = HorizontalAlignment.Stretch;
                chatBrowser.VerticalAlignment = VerticalAlignment.Stretch;

#if !DEBUG
                chatBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
                chatBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
#endif


                chatBrowser.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

                string html = GetHtmlFromResource(Defaults.HtmlResourcePath);
                chatBrowser.NavigateToString(html);

                _webViewInitialized = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 Error: {ex.Message}");
            }
            finally
            {
                _webViewInitializing = false;
            }
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            _ = OnControlLoadedAsync(sender, e);
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                chatBrowser.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                _ = chatBrowser.CoreWebView2.ExecuteScriptAsync("window.lmInit()");
            }
        }

        private string GetHtmlFromResource(string resourceName)
        {
            var uri = new Uri($"/{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name};component/{resourceName}", UriKind.Relative);
            var streamInfo = Application.GetResourceStream(uri) ?? throw new InvalidOperationException("Resource not found: " + resourceName);
            using (var reader = new System.IO.StreamReader(streamInfo.Stream))
            {
                return reader.ReadToEnd();
            }
        }

        public void SendKeyToWebView(int keyCode)
        {
            if (chatBrowser?.CoreWebView2 == null || !_webViewInitialized)
                return;

            chatBrowser.Focus();

            string keyName = GetKeyName(keyCode);

            if (keyName != null)
            {
                string script = $@"
                    (function() {{
                        const textarea = document.getElementById('userInput');
                        if (!textarea) return;

                        const currentPos = textarea.selectionStart;
                        const textLength = textarea.value.length;

                        if ('{keyName}' === 'Home') {{
                            const lineStart = textarea.value.lastIndexOf('\n', currentPos - 1) + 1;
                            textarea.setSelectionRange(lineStart, lineStart);
                        }} else if ('{keyName}' === 'End') {{
                            const lineEnd = textarea.value.indexOf('\n', currentPos);
                            const actualLineEnd = lineEnd === -1 ? textLength : lineEnd;
                            textarea.setSelectionRange(actualLineEnd, actualLineEnd);
                        }} else if ('{keyName}' === 'ArrowLeft') {{
                            if (currentPos > 0) {{
                                textarea.setSelectionRange(currentPos - 1, currentPos - 1);
                            }}
                        }} else if ('{keyName}' === 'ArrowRight') {{
                            if (currentPos < textLength) {{
                                textarea.setSelectionRange(currentPos + 1, currentPos + 1);
                            }}
                        }}
                    }})();
                ";
                _ = chatBrowser.CoreWebView2.ExecuteScriptAsync(script);
            }
        }

        private string GetKeyName(int keyCode)
        {
            if (keyCode == 0x24) return "Home";
            if (keyCode == 0x23) return "End";
            if (keyCode == 0x25) return "ArrowLeft";
            if (keyCode == 0x27) return "ArrowRight";
            return null;
        }

        /// <summary>
        /// Releases all resources used by the <see cref="MainWindowControl"/> instance.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            this.Loaded -= OnControlLoaded;
            if (chatBrowser != null)
            {
                try
                {
                    chatBrowser.Dispose();
                }
                catch (Exception ex)
                {
                    InternalLogger.Error("Failed to dispose WebView2 control.", ex);
                }
            }
            GC.SuppressFinalize(this);
        }
    }
}
