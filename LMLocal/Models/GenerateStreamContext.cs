namespace LMLocal.Models
{
    public class GenerateStreamContext
    {
        public string Prompt { get; set; }
        public string ActiveDocumentContent { get; set; }
        public string AdditionalPrompt { get; set; }
        public string ModelId { get; set; }
    }
}
