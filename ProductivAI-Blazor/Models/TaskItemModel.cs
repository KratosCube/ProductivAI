using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProductivAI_Blazor.Models;

public class TaskItemModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Task name is required.")]
    [StringLength(200, ErrorMessage = "Task name cannot be longer than 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "AI context cannot be longer than 1000 characters.")]
    public string? AiContext { get; set; } // Optional

    public int Importance { get; set; } = 60;
    public int? ProjectId { get; set; } // Changed from Guid? to int?, Optional

    [StringLength(2000, ErrorMessage = "Context details cannot be longer than 2000 characters.")]
    public string? ContextDetails { get; set; } // Was required in original, but making optional for now for easier add.

    public DateTime? DueDate { get; set; }
    public bool IsRecurring { get; set; }
    public bool IsCompleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public bool IsIdea { get; set; } = false; // True if the task is just an idea, not a fully planned task

    // For future use with subtasks, etc.
    public List<SubtaskModel> Subtasks { get; set; } = new List<SubtaskModel>();
} 