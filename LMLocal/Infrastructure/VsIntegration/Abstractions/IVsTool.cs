namespace LMLocal.Infrastructure.Vs.Abstractions
{
    public interface IVsTool
    {
        /// <summary>
        /// Returns metadata about this tool for the LLM model.
        /// </summary>
        ToolDefinition GetToolInfo();

        /// <summary>
        /// Unique identifier for this tool.
        /// </summary>
        string ToolName { get; }
    }
}
