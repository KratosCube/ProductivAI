using ProductivAI.Core.Models;
using ProductivAI.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace ProductivAI.Application.Services
{
    public interface ITaskService
    {
        Task<TaskItem> GetTaskByIdAsync(Guid id);
        Task<IEnumerable<TaskItem>> GetAllTasksAsync();
        Task<IEnumerable<TaskItem>> GetActiveTasksAsync();
        Task<IEnumerable<TaskItem>> GetCompletedTasksAsync();
        Task<TaskItem> CreateTaskAsync(string title, string description, DateTime? dueDate = null, int priority = 0);
        Task UpdateTaskAsync(TaskItem task);
        Task CompleteTaskAsync(Guid id);
        Task ReactivateTaskAsync(Guid id);
        Task DeleteTaskAsync(Guid id);
        Task<SubTask> AddSubTaskAsync(Guid taskId, string description);
        Task CompleteSubTaskAsync(Guid taskId, Guid subTaskId);
        Task<IEnumerable<TaskItem>> GetOverdueTasksAsync();
        Task<IEnumerable<TaskItem>> PrioritizeTasksAsync(UserContext userContext);
        Task<string> GetTaskSuggestionsAsync(UserContext userContext);
        Task<IEnumerable<TaskItem>> GetTasksByDueDateRangeAsync(DateTime startDate, DateTime endDate);
    }

    public class TaskService : ITaskService
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IAIService _aiService;

        public TaskService(ITaskRepository taskRepository, IAIService aiService)
        {
            _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        }

        public async Task<TaskItem> GetTaskByIdAsync(Guid id)
        {
            return await _taskRepository.GetByIdAsync(id);
        }

        public async Task<IEnumerable<TaskItem>> GetAllTasksAsync()
        {
            return await _taskRepository.GetAllAsync();
        }

        public async Task<IEnumerable<TaskItem>> GetActiveTasksAsync()
        {
            return await _taskRepository.GetActiveTasksAsync();
        }

        public async Task<IEnumerable<TaskItem>> GetCompletedTasksAsync()
        {
            return await _taskRepository.GetCompletedTasksAsync();
        }

        public async Task<TaskItem> CreateTaskAsync(string title, string description, DateTime? dueDate = null, int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Task title cannot be empty", nameof(title));

            var task = new TaskItem
            {
                Title = title,
                Description = description ?? string.Empty,
                CreatedDate = DateTime.Now,
                DueDate = dueDate,
                Priority = priority,
                IsCompleted = false
            };

            return await _taskRepository.AddAsync(task);
        }

        public async Task UpdateTaskAsync(TaskItem task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            await _taskRepository.UpdateAsync(task);
        }

        public async Task CompleteTaskAsync(Guid id)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task == null)
                throw new ArgumentException($"Task with ID {id} not found", nameof(id));

            task.IsCompleted = true;
            await _taskRepository.UpdateAsync(task);

            // If task is recurring, create next instance
            if (task.IsRecurring && !string.IsNullOrEmpty(task.RecurrencePattern))
            {
                await CreateRecurringTaskInstanceAsync(task);
            }
        }

        public async Task ReactivateTaskAsync(Guid id)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task == null)
                throw new ArgumentException($"Task with ID {id} not found", nameof(id));

            task.IsCompleted = false;
            await _taskRepository.UpdateAsync(task);
        }

        public async Task DeleteTaskAsync(Guid id)
        {
            await _taskRepository.DeleteAsync(id);
        }

        public async Task<SubTask> AddSubTaskAsync(Guid taskId, string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Subtask description cannot be empty", nameof(description));

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
                throw new ArgumentException($"Task with ID {taskId} not found", nameof(taskId));

            var subTask = new SubTask
            {
                Description = description,
                IsCompleted = false
            };

            task.SubTasks.Add(subTask);
            await _taskRepository.UpdateAsync(task);

            return subTask;
        }

        public async Task CompleteSubTaskAsync(Guid taskId, Guid subTaskId)
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
                throw new ArgumentException($"Task with ID {taskId} not found", nameof(taskId));

            var subTask = task.SubTasks.FirstOrDefault(st => st.Id == subTaskId);
            if (subTask == null)
                throw new ArgumentException($"Subtask with ID {subTaskId} not found", nameof(subTaskId));

            subTask.IsCompleted = true;
            await _taskRepository.UpdateAsync(task);

            // Check if all subtasks are completed, and auto-complete the task if configured
            if (task.SubTasks.Count > 0 && task.SubTasks.All(st => st.IsCompleted))
            {
                // Could add a configuration option to auto-complete tasks when all subtasks are done
                // For now, we'll leave the task incomplete until explicitly completed
            }
        }

        public async Task<IEnumerable<TaskItem>> GetOverdueTasksAsync()
        {
            var activeTasks = await _taskRepository.GetActiveTasksAsync();
            return activeTasks.Where(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.Today);
        }

        public async Task<IEnumerable<TaskItem>> PrioritizeTasksAsync(UserContext userContext)
        {
            var activeTasks = await _taskRepository.GetActiveTasksAsync();
            var tasksList = activeTasks.ToList();

            // First apply basic sorting based on user preferences
            tasksList = userContext.SortPreference switch
            {
                TaskSortPreference.DueDate => tasksList
                    .OrderBy(t => t.DueDate.HasValue ? t.DueDate.Value : DateTime.MaxValue)
                    .ToList(),
                TaskSortPreference.Priority => tasksList
                    .OrderByDescending(t => t.Priority)
                    .ToList(),
                TaskSortPreference.CreationDate => tasksList
                    .OrderBy(t => t.CreatedDate)
                    .ToList(),
                _ => tasksList
            };

            // Then apply AI prioritization if available
            try
            {
                var prioritizedTasks = await _aiService.PrioritizeTasksAsync(tasksList, userContext);
                return prioritizedTasks;
            }
            catch (Exception)
            {
                // If AI prioritization fails, return the basic sorted list
                return tasksList;
            }
        }

        public async Task<string> GetTaskSuggestionsAsync(UserContext userContext)
        {
            return await _aiService.GenerateTaskSuggestionsAsync(userContext);
        }

        public async Task<IEnumerable<TaskItem>> GetTasksByDueDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _taskRepository.GetTasksByDueDateAsync(startDate, endDate);
        }

        // Private helper methods
        private async Task<TaskItem> CreateRecurringTaskInstanceAsync(TaskItem completedTask)
        {
            // Parse recurrence pattern and create next instance
            // This is a simplified implementation - you'd need more complex logic for various recurrence patterns

            var nextDueDate = CalculateNextDueDate(completedTask.DueDate, completedTask.RecurrencePattern);

            if (!nextDueDate.HasValue)
                return null;

            var newTask = new TaskItem
            {
                Title = completedTask.Title,
                Description = completedTask.Description,
                CreatedDate = DateTime.Now,
                DueDate = nextDueDate,
                Priority = completedTask.Priority,
                IsCompleted = false,
                IsRecurring = true,
                RecurrencePattern = completedTask.RecurrencePattern
            };

            // Copy subtasks (but reset their completion status)
            foreach (var subtask in completedTask.SubTasks)
            {
                newTask.SubTasks.Add(new SubTask
                {
                    Description = subtask.Description,
                    IsCompleted = false
                });
            }

            return await _taskRepository.AddAsync(newTask);
        }

        private DateTime? CalculateNextDueDate(DateTime? previousDueDate, string recurrencePattern)
        {
            if (!previousDueDate.HasValue || string.IsNullOrEmpty(recurrencePattern))
                return null;

            // Simple parsing of basic patterns
            // In a real implementation, you'd want a more robust recurrence engine
            if (recurrencePattern.Equals("daily", StringComparison.OrdinalIgnoreCase))
            {
                return previousDueDate.Value.AddDays(1);
            }
            else if (recurrencePattern.Equals("weekly", StringComparison.OrdinalIgnoreCase))
            {
                return previousDueDate.Value.AddDays(7);
            }
            else if (recurrencePattern.Equals("monthly", StringComparison.OrdinalIgnoreCase))
            {
                return previousDueDate.Value.AddMonths(1);
            }
            else if (recurrencePattern.Equals("yearly", StringComparison.OrdinalIgnoreCase))
            {
                return previousDueDate.Value.AddYears(1);
            }
            else if (recurrencePattern.StartsWith("every", StringComparison.OrdinalIgnoreCase))
            {
                // Parse "every X days/weeks/months" format
                var parts = recurrencePattern.Split(' ');
                if (parts.Length >= 3 && int.TryParse(parts[1], out int interval))
                {
                    var unit = parts[2].ToLowerInvariant();
                    return unit switch
                    {
                        "day" or "days" => previousDueDate.Value.AddDays(interval),
                        "week" or "weeks" => previousDueDate.Value.AddDays(interval * 7),
                        "month" or "months" => previousDueDate.Value.AddMonths(interval),
                        "year" or "years" => previousDueDate.Value.AddYears(interval),
                        _ => null
                    };
                }
            }

            // Couldn't parse the pattern
            return null;
        }
    }
}