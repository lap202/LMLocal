using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Threading.Tasks;

namespace LMLocal.Internal
{
    /// <summary>
    /// Executes JavaScript code or posts messages in a WebView2 control.
    /// </summary>
    internal class WebView2ScriptExecutor
    {
        private readonly Microsoft.Web.WebView2.Wpf.WebView2 _webView;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebView2ScriptExecutor"/> class.
        /// </summary>
        public WebView2ScriptExecutor(Microsoft.Web.WebView2.Wpf.WebView2 webView)
        {
            _webView = webView;
        }

        /// <summary>
        /// Posts a message to the WebView2 control as JSON. This does not wait for a response.
        /// </summary>
        public async Task PostMessageAsJsonAsync(object message)
        {
            try
            {
                string json = JsonConvert.SerializeObject(message);
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (_webView?.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.PostWebMessageAsJson(json);
                }
                else
                {
                    InternalLogger.Warn("WebView2ScriptExecutor: CoreWebView2 is null, cannot post message.");
                }
            }
            catch (Exception ex)
            {
                InternalLogger.Error("WebView2ScriptExecutor.PostMessageAsJsonAsync failed", ex);
            }
        }
    }

    public enum WebView2MessageType
    {
        StreamThought,
        StreamContent,
        StreamEnd,
        StreamError,
        CompactionStart,
        CompactionEnd
    }
    public class WebView2ScriptMessage
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public WebView2MessageType Type { get; set; }
        public string Payload { get; set; }
    }

    public class WebView2ScriptMessageWithCount : WebView2ScriptMessage
    {
        public int Count { get; set; }
        public double TokensPerSecond { get; set; }
    }
}
