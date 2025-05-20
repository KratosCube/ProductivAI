using System.Text.Json.Serialization;

namespace ProductivAI_Blazor.Models;

public class ApiChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    public ApiChatMessage() { }

    public ApiChatMessage(string role, string content)
    {
        Role = role;
        Content = content;
    }
} 