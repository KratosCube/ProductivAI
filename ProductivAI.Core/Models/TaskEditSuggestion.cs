namespace ProductivAI.Core.Models
{
    public class TaskEditSuggestion
    {
        public string OriginalId { get; set; }
        public TaskItem Edited { get; set; }
        public TaskItem Original { get; set; }
    }
}