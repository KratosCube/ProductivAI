using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductivAI.Server.Models;

public class TaskItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? AiContext { get; set; }

    [StringLength(2000)]
    public string? ContextDetails { get; set; }

    public int Importance { get; set; } // 0-100
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; } = false;
    public bool IsRecurring { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsIdea { get; set; } = false;

    // Foreign Key for Project
    public int? ProjectId { get; set; }
    [ForeignKey("ProjectId")]
    public virtual Project? Project { get; set; }

    // Navigation property for related subtasks
    public virtual ICollection<Subtask> Subtasks { get; set; } = new List<Subtask>();
} 