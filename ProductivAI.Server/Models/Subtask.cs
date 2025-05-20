using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProductivAI.Server.Models;

public class Subtask
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Context { get; set; }

    public DateTime? DueDate { get; set; }
    public int Importance { get; set; } // 0-100
    public bool IsCompleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Foreign Key for TaskItem
    public int TaskItemId { get; set; }
    [ForeignKey("TaskItemId")]
    public virtual TaskItem? TaskItem { get; set; }
} 