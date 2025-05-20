using System.ComponentModel.DataAnnotations;

namespace ProductivAI_Blazor.Models
{
    public class UserContextModel
    {
        [StringLength(2000, ErrorMessage = "Work description is too long.")]
        public string? WorkDescription { get; set; }

        [StringLength(1000, ErrorMessage = "Short-term focus is too long.")]
        public string? ShortTermFocus { get; set; }

        [StringLength(1000, ErrorMessage = "Long-term goals are too long.")]
        public string? LongTermGoals { get; set; }

        [StringLength(2000, ErrorMessage = "Other context is too long.")]
        public string? OtherContext { get; set; }

        public string? SortingPreference { get; set; } = "manual"; // Default value

        public string? SelectedAiModel { get; set; } 
        // This will likely be populated from OpenRouterService or a shared app state

        // Placeholder for project management data if integrated directly later
        public List<ProjectModel> Projects { get; set; } = new List<ProjectModel>();
    }
} 