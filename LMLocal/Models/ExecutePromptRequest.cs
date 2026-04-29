namespace LMLocal.Models
{
    public class ExecutePromptRequest
    {
        public string Prompt { get; set; }
        public bool IncludeContent { get; set; }
        public string AdditionalPrompt { get; set; }
        public string ModelId { get; set; }
    }
}
