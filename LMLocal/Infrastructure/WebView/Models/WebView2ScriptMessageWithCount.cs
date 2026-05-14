namespace LMLocal.Infrastructure.WebView
{
    /// <summary>
    /// Message with token count and generation speed statistics.
    /// Used for StreamThought and StreamContent messages.
    /// </summary>
    internal class WebView2ScriptMessageWithCount : WebView2ScriptMessage
    {
        /// <summary>
        /// Total tokens generated so far in this stream.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Current generation speed in tokens per second.
        /// </summary>
        public double TokensPerSecond { get; set; }
    }
}
