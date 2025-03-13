namespace ProductivAI.Core.Models
{
    public class TaskItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? DueDate { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsRecurring { get; set; }
        public string RecurrencePattern { get; set; }
        public int Priority { get; set; }
        public List<SubTask> SubTasks { get; set; } = new List<SubTask>();
    }

    public class SubTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Description { get; set; }
        public bool IsCompleted { get; set; }
    }
}