using System;
using System.ComponentModel.DataAnnotations;

namespace ProductivAI_Blazor.Models;

public class SubtaskModel
{
    public int Id { get; set; } // Changed from string to int, should be 0 for new subtasks

    [Required(ErrorMessage = "Subtask name is required.")]
    [StringLength(200, ErrorMessage = "Subtask name cannot be longer than 200 characters.")]
    public string Name { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }
    public int Importance { get; set; } = 50; // 0-100

    [StringLength(1000, ErrorMessage = "Subtask context cannot be longer than 1000 characters.")]
    public string? Context { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid TempIdForClientEditing { get; set; } = Guid.NewGuid();
    // public string ParentTaskId { get; set; } // Implicitly part of TaskItemModel's list for now
}