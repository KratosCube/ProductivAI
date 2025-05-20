namespace ProductivAI.Server.Models.Dtos;

public class SubtaskDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Context { get; set; }
    public DateTime? DueDate { get; set; }
    public int Importance { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // TaskItemId is not strictly needed if Subtasks are always part of a TaskItemDto
} 