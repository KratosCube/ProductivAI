namespace ProductivAI_Blazor.Models
{
    public class ProjectSuggestionModel
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsActioned { get; set; } = false; // To track if the user has interacted (created/dismissed)
    }
} 