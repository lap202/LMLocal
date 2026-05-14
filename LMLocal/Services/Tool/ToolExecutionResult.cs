namespace LMLocal.Services.Tool
{
    /// <summary>
    /// Result of tool execution.
    /// </summary>
    internal class ToolExecutionResult
    {
        /// <summary>
        /// Unique tool call ID (correlates with tool invocation).
        /// </summary>
        public string ToolId { get; set; }

        /// <summary>
        /// Tool function name.
        /// </summary>
        public string ToolName { get; set; }

        /// <summary>
        /// Execution result (can be any object - string, JSON, etc).
        /// Null if execution failed.
        /// </summary>
        public object Result { get; set; }

        /// <summary>
        /// Error message if execution failed.
        /// Null if successful.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Completion message summarizing the tool execution result.
        /// </summary>
        public string CompletionMessage { get; set; }

        /// <summary>
        /// True if tool executed successfully.
        /// Computed property based on Error and Result.
        /// </summary>
        public bool IsSuccess => string.IsNullOrEmpty(Error) && Result != null;
    }
}
