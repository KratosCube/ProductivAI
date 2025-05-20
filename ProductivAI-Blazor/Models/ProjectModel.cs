using System;
using System.ComponentModel.DataAnnotations;

namespace ProductivAI_Blazor.Models;

public class ProjectModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Project name is required.")]
    [StringLength(100, ErrorMessage = "Project name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    public string Color { get; set; } = "#808080"; // Default color, kept for client UI

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // We might not always populate this from the client-side perspective, 
    // but it could be useful if the API returns tasks associated with a project.
    // For basic project CRUD, it might not be needed in request DTOs.
    // public List<TaskItemModel> TaskItems { get; set; } = new List<TaskItemModel>();
} 