using System;
using System.Collections.Generic;

namespace ProductivAI.Core.Models
{
    public class TaskDetectionResult
    {
        public bool IsTaskLike { get; set; }
        public double Confidence { get; set; }
        public string SuggestedTitle { get; set; }
        public string SuggestedDescription { get; set; }
        public int SuggestedPriority { get; set; } = 3;
        public DateTime? SuggestedDueDate { get; set; }
        public List<string> SuggestedSubTasks { get; set; } = new List<string>();
    }
}