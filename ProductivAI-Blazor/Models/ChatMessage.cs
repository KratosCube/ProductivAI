namespace ProductivAI_Blazor.Models;

public enum ChatMessageSender
{
    User,
    Assistant,
    System // For system messages, if needed
}

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ChatMessageSender Sender { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? ModelUsed { get; set; } // Optional: To store which model generated the assistant message
    public bool IsStreaming { get; set; } = false; // Added for UI state
    public bool IsError { get; set; } = false; // Added for UI state
    public AiTaskSuggestion? AttachedTaskSuggestion { get; set; } // For task suggestions linked to this message
    public ProjectSuggestionModel? AttachedProjectSuggestion { get; set; } // Added for project suggestions
    public SuggestedTaskIdeasListModel? AttachedTaskIdeasListSuggestion { get; set; } // Added for bulk task idea suggestions

    // For OpenRouter API request structure
    public string Role => Sender switch
    {
        ChatMessageSender.User => "user",
        ChatMessageSender.Assistant => "assistant",
        ChatMessageSender.System => "system",
        _ => "user"
    };

    public ChatMessage() { }

    public ChatMessage(ChatMessageSender sender, string content, string? modelUsed = null)
    {
        Sender = sender;
        Content = content;
        ModelUsed = modelUsed;
        // Note: IsStreaming and IsError default to false.
        // They should be set explicitly when a message's state changes.
    }

    // Optional: Add a constructor that includes these new flags if needed for convenience
    public ChatMessage(ChatMessageSender sender, string content, string? modelUsed, Guid id, string role, bool isError, bool isStreaming, AiTaskSuggestion? attachedTaskSuggestion = null) : this(sender, content, modelUsed)
    {
        Id = id;
        // Role is a computed property, so not setting it here directly unless it becomes a settable field.
        IsError = isError;
        IsStreaming = isStreaming;
        AttachedTaskSuggestion = attachedTaskSuggestion;
    }
} 