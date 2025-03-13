namespace ProductivAI.Core.Models
{
    public class NoteItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool IsImportant { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }
}