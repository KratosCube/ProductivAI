using Microsoft.EntityFrameworkCore;
using ProductivAI.Server.Models; // This should now correctly resolve

namespace ProductivAI.Server.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects { get; set; } = null!;
    public DbSet<TaskItem> TaskItems { get; set; } = null!;
    public DbSet<Subtask> Subtasks { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TaskItem>()
            .HasOne(t => t.Project)
            .WithMany(p => p.TaskItems)
            .HasForeignKey(t => t.ProjectId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Subtask>()
            .HasOne(s => s.TaskItem)
            .WithMany(t => t.Subtasks)
            .HasForeignKey(s => s.TaskItemId)
            .IsRequired(true)
            .OnDelete(DeleteBehavior.Cascade);
    }
} 