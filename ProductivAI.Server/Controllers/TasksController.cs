using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProductivAI.Server.Data;
using ProductivAI.Server.Models;
using ProductivAI.Server.Models.Dtos;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProductivAI.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TasksController> _logger;

        public TasksController(ApplicationDbContext context, ILogger<TasksController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Tasks
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaskItemDto>>> GetTaskItems([FromQuery] bool includeCompleted = false)
        {
            _logger.LogInformation("Attempting to get task items. includeCompleted: {IncludeCompleted}", includeCompleted);
            try
            {
                var query = _context.TaskItems
                                    .Include(t => t.Project)
                                    .Include(t => t.Subtasks)
                                    .AsQueryable();

                _logger.LogInformation("Base query created. Applying includeCompleted filter.");

                if (!includeCompleted)
                {
                    query = query.Where(t => !t.IsCompleted);
                    _logger.LogInformation("Filter for active tasks applied.");
                }
                else
                {
                    _logger.LogInformation("Filter for active tasks NOT applied (includeCompleted is true).");
                }

                var tasks = await query.OrderByDescending(t => t.Importance)
                                      .ThenBy(t => t.CreatedAt)
                                      .Select(t => new TaskItemDto
                                      {
                                          Id = t.Id,
                                          Name = t.Name,
                                          AiContext = t.AiContext,
                                          ContextDetails = t.ContextDetails,
                                          Importance = t.Importance,
                                          DueDate = t.DueDate,
                                          IsCompleted = t.IsCompleted,
                                          IsRecurring = t.IsRecurring,
                                          IsIdea = t.IsIdea,
                                          CreatedAt = t.CreatedAt,
                                          CompletedAt = t.CompletedAt,
                                          UpdatedAt = t.UpdatedAt,
                                          ProjectId = t.ProjectId,
                                          Project = t.Project == null ? null : new ProjectDto
                                          {
                                              Id = t.Project.Id,
                                              Name = t.Project.Name,
                                              CreatedAt = t.Project.CreatedAt
                                          },
                                          Subtasks = t.Subtasks.Select(s => new SubtaskDto
                                          {
                                              Id = s.Id,
                                              Name = s.Name,
                                              Context = s.Context,
                                              DueDate = s.DueDate,
                                              Importance = s.Importance,
                                              IsCompleted = s.IsCompleted,
                                              CreatedAt = s.CreatedAt,
                                              CompletedAt = s.CompletedAt,
                                              UpdatedAt = s.UpdatedAt
                                          }).ToList()
                                      })
                                      .ToListAsync();
                
                _logger.LogInformation("Successfully fetched {TaskCount} DTO tasks from database.", tasks.Count);
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in GetTaskItems.");
                return StatusCode(500, "An internal error occurred while fetching tasks.");
            }
        }

        // GET: api/Tasks/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TaskItemDto>> GetTaskItem(int id)
        {
            var taskItem = await _context.TaskItems
                                         .Include(t => t.Project)
                                         .Include(t => t.Subtasks)
                                         .FirstOrDefaultAsync(t => t.Id == id);

            if (taskItem == null)
            {
                return NotFound();
            }

            var taskItemDto = new TaskItemDto
            {
                Id = taskItem.Id,
                Name = taskItem.Name,
                AiContext = taskItem.AiContext,
                ContextDetails = taskItem.ContextDetails,
                Importance = taskItem.Importance,
                DueDate = taskItem.DueDate,
                IsCompleted = taskItem.IsCompleted,
                IsRecurring = taskItem.IsRecurring,
                IsIdea = taskItem.IsIdea,
                CreatedAt = taskItem.CreatedAt,
                CompletedAt = taskItem.CompletedAt,
                UpdatedAt = taskItem.UpdatedAt,
                ProjectId = taskItem.ProjectId,
                Project = taskItem.Project == null ? null : new ProjectDto
                {
                    Id = taskItem.Project.Id,
                    Name = taskItem.Project.Name,
                    CreatedAt = taskItem.Project.CreatedAt
                },
                Subtasks = taskItem.Subtasks.Select(s => new SubtaskDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Context = s.Context,
                    DueDate = s.DueDate,
                    Importance = s.Importance,
                    IsCompleted = s.IsCompleted,
                    CreatedAt = s.CreatedAt,
                    CompletedAt = s.CompletedAt,
                    UpdatedAt = s.UpdatedAt
                }).ToList()
            };

            return Ok(taskItemDto);
        }

        // PUT: api/Tasks/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTaskItem(int id, TaskItemDto taskDto)
        {
            if (id != taskDto.Id)
            {
                return BadRequest("ID mismatch");
            }

            var entityToUpdate = await _context.TaskItems
                                               .Include(t => t.Subtasks)
                                               .FirstOrDefaultAsync(t => t.Id == id);

            if (entityToUpdate == null)
            {
                return NotFound();
            }

            entityToUpdate.Name = taskDto.Name;
            entityToUpdate.AiContext = taskDto.AiContext;
            entityToUpdate.ContextDetails = taskDto.ContextDetails;
            entityToUpdate.Importance = taskDto.Importance;
            entityToUpdate.IsRecurring = taskDto.IsRecurring;
            entityToUpdate.IsIdea = taskDto.IsIdea;
            entityToUpdate.ProjectId = taskDto.ProjectId;
            entityToUpdate.UpdatedAt = DateTime.UtcNow;

            if (taskDto.DueDate.HasValue)
            {
                entityToUpdate.DueDate = taskDto.DueDate.Value.Kind == DateTimeKind.Unspecified 
                    ? DateTime.SpecifyKind(taskDto.DueDate.Value, DateTimeKind.Utc) 
                    : taskDto.DueDate.Value;
            }
            else
            {
                entityToUpdate.DueDate = null;
            }

            if (taskDto.IsCompleted && !entityToUpdate.IsCompleted) 
            {
                entityToUpdate.CompletedAt = DateTime.UtcNow;
            }
            else if (!taskDto.IsCompleted && entityToUpdate.IsCompleted)
            {
                entityToUpdate.CompletedAt = null; 
            }
            entityToUpdate.IsCompleted = taskDto.IsCompleted;

            var subtaskIdsInDto = taskDto.Subtasks.Select(s => s.Id).Where(sId => sId != 0).ToList();
            var subtasksToRemove = entityToUpdate.Subtasks.Where(s => !subtaskIdsInDto.Contains(s.Id)).ToList();
            _context.Subtasks.RemoveRange(subtasksToRemove);

            foreach (var subtaskDto in taskDto.Subtasks)
            {
                var existingSubtask = entityToUpdate.Subtasks.FirstOrDefault(s => s.Id == subtaskDto.Id && s.Id != 0);
                if (existingSubtask != null)
                {
                    existingSubtask.Name = subtaskDto.Name;
                    existingSubtask.Context = subtaskDto.Context;
                    existingSubtask.Importance = subtaskDto.Importance;
                    existingSubtask.IsCompleted = subtaskDto.IsCompleted;
                    existingSubtask.UpdatedAt = DateTime.UtcNow;
                    if (subtaskDto.IsCompleted && !existingSubtask.IsCompleted) existingSubtask.CompletedAt = DateTime.UtcNow;
                    else if (!subtaskDto.IsCompleted && existingSubtask.IsCompleted) existingSubtask.CompletedAt = null;

                    if (subtaskDto.DueDate.HasValue)
                    {
                        existingSubtask.DueDate = subtaskDto.DueDate.Value.Kind == DateTimeKind.Unspecified
                            ? DateTime.SpecifyKind(subtaskDto.DueDate.Value, DateTimeKind.Utc)
                            : subtaskDto.DueDate.Value;
                    }
                    else
                    {
                        existingSubtask.DueDate = null;
                    }
                }
                else
                {
                    var newSubtask = new Subtask
                    {
                        Name = subtaskDto.Name,
                        Context = subtaskDto.Context,
                        Importance = subtaskDto.Importance,
                        IsCompleted = subtaskDto.IsCompleted,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CompletedAt = subtaskDto.IsCompleted ? DateTime.UtcNow : (DateTime?)null,
                        DueDate = subtaskDto.DueDate.HasValue 
                                    ? (subtaskDto.DueDate.Value.Kind == DateTimeKind.Unspecified 
                                        ? DateTime.SpecifyKind(subtaskDto.DueDate.Value, DateTimeKind.Utc) 
                                        : subtaskDto.DueDate.Value) 
                                    : null
                    };
                    entityToUpdate.Subtasks.Add(newSubtask);
                }
            }
            
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TaskItemExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch(Exception ex) 
            {
                _logger.LogError(ex, "Internal server error updating task {TaskId}", id);
                return StatusCode(500, "Internal server error updating task: " + ex.Message);
            }

            return NoContent();
        }

        // POST: api/Tasks
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<TaskItemDto>> PostTaskItem(TaskItemDto taskDto)
        {
            _logger.LogInformation("Attempting to process POST /api/tasks request with DTO.");

            if (!ModelState.IsValid)
            {
                _logger.LogError("Model state is invalid (DTO). Errors: {ModelStateErrors}", 
                    string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(taskDto.Name))
            {
                return BadRequest("Task name cannot be empty.");
            }

            var taskItemEntity = new TaskItem
            {
                Name = taskDto.Name,
                AiContext = taskDto.AiContext,
                ContextDetails = taskDto.ContextDetails,
                Importance = taskDto.Importance,
                IsRecurring = taskDto.IsRecurring,
                IsIdea = taskDto.IsIdea,
                ProjectId = taskDto.ProjectId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsCompleted = false,
                CompletedAt = null
            };

            if (taskDto.DueDate.HasValue)
            {
                taskItemEntity.DueDate = taskDto.DueDate.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(taskDto.DueDate.Value, DateTimeKind.Utc)
                    : taskDto.DueDate.Value;
            }

            if (taskDto.Subtasks != null)
            {
                foreach(var subtaskDto_post in taskDto.Subtasks)
                {
                    var subtaskEntity = new Subtask
                    {
                        Name = subtaskDto_post.Name,
                        Context = subtaskDto_post.Context,
                        Importance = subtaskDto_post.Importance,
                        IsCompleted = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CompletedAt = null,
                        DueDate = subtaskDto_post.DueDate.HasValue
                                    ? (subtaskDto_post.DueDate.Value.Kind == DateTimeKind.Unspecified
                                        ? DateTime.SpecifyKind(subtaskDto_post.DueDate.Value, DateTimeKind.Utc)
                                        : subtaskDto_post.DueDate.Value)
                                    : null
                    };
                    taskItemEntity.Subtasks.Add(subtaskEntity);
                }
            }

            _context.TaskItems.Add(taskItemEntity);
            await _context.SaveChangesAsync();

            var createdTaskDto = new TaskItemDto
            {
                Id = taskItemEntity.Id,
                Name = taskItemEntity.Name,
                AiContext = taskItemEntity.AiContext,
                ContextDetails = taskItemEntity.ContextDetails,
                Importance = taskItemEntity.Importance,
                DueDate = taskItemEntity.DueDate,
                IsCompleted = taskItemEntity.IsCompleted,
                IsRecurring = taskItemEntity.IsRecurring,
                IsIdea = taskItemEntity.IsIdea,
                CreatedAt = taskItemEntity.CreatedAt,
                CompletedAt = taskItemEntity.CompletedAt,
                UpdatedAt = taskItemEntity.UpdatedAt,
                ProjectId = taskItemEntity.ProjectId,
                Subtasks = taskItemEntity.Subtasks.Select(s => new SubtaskDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Context = s.Context,
                    DueDate = s.DueDate,
                    Importance = s.Importance,
                    IsCompleted = s.IsCompleted,
                    CreatedAt = s.CreatedAt,
                    CompletedAt = s.CompletedAt,
                    UpdatedAt = s.UpdatedAt
                }).ToList()
            };

            if (createdTaskDto.ProjectId.HasValue && createdTaskDto.Project == null)
            {
                var projectEntity = await _context.Projects.FindAsync(createdTaskDto.ProjectId.Value);
                if (projectEntity != null)
                {
                    createdTaskDto.Project = new ProjectDto 
                    { 
                        Id = projectEntity.Id, 
                        Name = projectEntity.Name, 
                        CreatedAt = projectEntity.CreatedAt 
                    };
                }
            }

            return CreatedAtAction(nameof(GetTaskItem), new { id = createdTaskDto.Id }, createdTaskDto);
        }

        // DELETE: api/Tasks/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTaskItem(int id)
        {
            var taskItem = await _context.TaskItems.FindAsync(id);
            if (taskItem == null)
            {
                return NotFound();
            }

            _context.TaskItems.Remove(taskItem);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // We could add specific endpoints for subtasks if needed, e.g.,
        // POST /api/tasks/{taskId}/subtasks
        // PUT /api/tasks/{taskId}/subtasks/{subtaskId}
        // DELETE /api/tasks/{taskId}/subtasks/{subtaskId}

        private bool TaskItemExists(int id)
        {
            return _context.TaskItems.Any(e => e.Id == id);
        }
    }
} 