// In ProductivAI.Core/Models/MessageHistory.cs
using System;
using System.Text.Json.Serialization;

namespace ProductivAI.Core.Models
{
    public class MessageHistory
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("isUserMessage")]
        public bool IsUserMessage { get; set; }

        [JsonPropertyName("reasoningContent")]
        public string ReasoningContent { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [JsonPropertyName("isSystemMessage")]
        public bool IsSystemMessage { get; set; } = false;

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