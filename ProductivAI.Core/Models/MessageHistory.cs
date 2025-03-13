using System;

namespace ProductivAI.Core.Models
{
    public class MessageHistory
    {
        public string Content { get; set; }
        public bool IsUserMessage { get; set; }
        public string ReasoningContent { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Utility method to convert from a UI message
        public static MessageHistory FromChatMessage(object chatMessage)
        {
            // This is a dynamic conversion that needs to be tailored
            // based on your ChatMessage class in AIChat.razor
            dynamic message = chatMessage;
            return new MessageHistory
            {
                Content = message.Content,
                IsUserMessage = message.IsFromUser,
                ReasoningContent = message.HasReasoning ? message.ReasoningContent : null,
                Timestamp = DateTime.Now
            };
        }
    }
}