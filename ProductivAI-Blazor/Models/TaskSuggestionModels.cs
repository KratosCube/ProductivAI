using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProductivAI_Blazor.Models
{
    public class AiTaskSuggestion
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("importance")]
        public int Importance { get; set; } = 50; // Default importance

        [JsonPropertyName("dueDate")]
        public string? DueDate { get; set; } // Expected format "YYYY-MM-DD"

        [JsonPropertyName("aiContext")]
        public string? AiContext { get; set; }

        [JsonPropertyName("contextDetails")]
        public string? ContextDetails { get; set; }

        // Added based on the likelihood of project context in suggestions
        [JsonPropertyName("projectId")]
        public string? ProjectId { get; set; } // Can be string or int depending on your system, string is safer for ID

        [JsonPropertyName("subtasks")]
        public List<AiSubtaskSuggestion> Subtasks { get; set; } = new List<AiSubtaskSuggestion>();

        public bool IsActioned { get; set; } = false;
    }

    public class AiSubtaskSuggestion
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("importance")]
        public int Importance { get; set; } = 50; // Default importance

        [JsonPropertyName("dueDate")]
        public string? DueDate { get; set; } // Expected format "YYYY-MM-DD"

        [JsonPropertyName("context")]
        public string? Context { get; set; }
    }
} 