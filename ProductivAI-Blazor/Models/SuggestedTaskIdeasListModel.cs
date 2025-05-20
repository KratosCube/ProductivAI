using System.Collections.Generic;

namespace ProductivAI_Blazor.Models
{
    public class SuggestedTaskIdeasListModel
    {
        public int ProjectId { get; set; }
        public List<string> IdeaNames { get; set; } = new List<string>();
        public bool IsActioned { get; set; } = false;
    }
} 