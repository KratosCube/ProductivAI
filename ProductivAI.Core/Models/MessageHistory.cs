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

        [JsonPropertyName("hasReasoning")]
        public bool HasReasoning { get; set; } = false;

        [JsonPropertyName("formattedContent")]
        public string FormattedContent { get; set; }

        /// <summary>
        /// Creates a MessageHistory object from a ChatMessage or similar object
        /// </summary>
        /// <param name="chatMessage">The chat message to convert (typically from Home.razor or AIChat.razor)</param>
        /// <returns>A new MessageHistory with properties copied from the chat message</returns>
        public static MessageHistory FromChatMessage(object chatMessage)
        {
            try
            {
                // This is a dynamic conversion that handles different property names
                dynamic message = chatMessage;

                var history = new MessageHistory
                {
                    Content = message.Content,
                    IsUserMessage = message.IsFromUser,
                    Timestamp = DateTime.Now,
                    IsSystemMessage = false // Default to regular message
                };

                // Handle reasoning content if available
                if (HasProperty(message, "ReasoningContent"))
                {
                    history.ReasoningContent = message.ReasoningContent;
                    history.HasReasoning = !string.IsNullOrEmpty(message.ReasoningContent);
                }
                else if (HasProperty(message, "HasReasoning") && message.HasReasoning)
                {
                    // If HasReasoning is true but no content, use empty string
                    history.HasReasoning = true;
                    history.ReasoningContent = "";
                }

                // Handle formatted content if available
                if (HasProperty(message, "FormattedContent"))
                {
                    history.FormattedContent = message.FormattedContent;
                }

                return history;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting chat message to history: {ex.Message}");

                // Return a basic message history with the error
                return new MessageHistory
                {
                    Content = "Error converting message",
                    IsUserMessage = false,
                    Timestamp = DateTime.Now
                };
            }
        }

        /// <summary>
        /// Helper method to check if a dynamic object has a property
        /// </summary>
        private static bool HasProperty(dynamic obj, string propertyName)
        {
            try
            {
                var value = obj.GetType().GetProperty(propertyName)?.GetValue(obj);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}