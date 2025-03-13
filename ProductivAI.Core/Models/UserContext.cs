using System.Collections.Generic;

namespace ProductivAI.Core.Models
{
    // In UserContext.cs
    public class UserContext
    {
        public string WorkDescription { get; set; }
        public List<string> FocusAreas { get; set; } = new List<string>();
        public List<string> LongTermGoals { get; set; } = new List<string>();
        public string PreferredAIModel { get; set; } = "Son35";
        public TaskSortPreference SortPreference { get; set; } = TaskSortPreference.DueDate;
        public bool UseReasoning { get; set; } = true; // Add this property
    }

    public enum TaskSortPreference
    {
        DueDate,
        Priority,
        CreationDate
    }
}