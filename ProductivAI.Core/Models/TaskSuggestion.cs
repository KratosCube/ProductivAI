// Add to your Models folder: ProductivAI.Core/Models/TaskSuggestion.cs
public class TaskSuggestion
{
    public string Title { get; set; }
    public string Description { get; set; }
    public int Priority { get; set; } = 3;
    public DateTime? DueDate { get; set; }
    public List<string> Subtasks { get; set; }
}