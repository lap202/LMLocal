namespace LMLocal.Models
{
    internal class ChatMessage
    {
        public string Role { get; }
        public string Content { get; }
        public ChatMessage(string role, string content) => (Role, Content) = (role, content);
    }
}
