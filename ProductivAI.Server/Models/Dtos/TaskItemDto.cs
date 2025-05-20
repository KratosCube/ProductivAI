namespace ProductivAI.Server.Models.Dtos;

public class TaskItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AiContext { get; set; }
    public string? ContextDetails { get; set; }
    public int Importance { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsRecurring { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? ProjectId { get; set; }
    public bool IsIdea { get; set; }
    public ProjectDto? Project { get; set; } 
    public ICollection<SubtaskDto> Subtasks { get; set; } = new List<SubtaskDto>();
} 