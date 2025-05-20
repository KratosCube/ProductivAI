using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ProductivAI.Server.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property for related tasks
    public virtual ICollection<TaskItem> TaskItems { get; set; } = new List<TaskItem>();
} 