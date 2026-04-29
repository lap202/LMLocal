using Newtonsoft.Json.Linq;

namespace LMLocal.Infrastructure.WebView
{
    internal class GetStatusResponse
    {
        public string Status { get; set; }
        public string ModelName { get; set; }
        public int MaxContext { get; set; }
        public string ErrorMessage { get; set; }
        public string ModelId { get; set; }
        public JObject ModelDetails { get; set; }
    }
}
