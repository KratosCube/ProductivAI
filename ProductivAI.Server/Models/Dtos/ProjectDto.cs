namespace ProductivAI.Server.Models.Dtos;

public class ProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    // No TaskItems collection here to break the cycle for the Task list view
} 