using Microsoft.JSInterop;
using ProductivAI.Core.Interfaces;
using ProductivAI.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProductivAI.Infrastructure.Repositories
{
    public class LocalStorageTaskRepository : LocalStorageRepository<TaskItem>, ITaskRepository
    {
        public LocalStorageTaskRepository(IJSRuntime jsRuntime)
            : base(jsRuntime, "productivai_tasks")
        {
        }

        public async Task<TaskItem> AddAsync(TaskItem entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var tasks = await GetItemsFromStorageAsync();

            // Ensure the task has a valid ID
            if (entity.Id == Guid.Empty)
                entity.Id = Guid.NewGuid();

            // Ensure CreatedDate is set
            if (entity.CreatedDate == default)
                entity.CreatedDate = DateTime.Now;

            tasks.Add(entity);
            await SaveItemsToStorageAsync(tasks);
            return entity;
        }

        public async Task DeleteAsync(Guid id)
        {
            var tasks = await GetItemsFromStorageAsync();
            var taskToRemove = tasks.FirstOrDefault(t => t.Id == id);

            if (taskToRemove != null)
            {
                tasks.Remove(taskToRemove);
                await SaveItemsToStorageAsync(tasks);
            }
        }

        public async Task<IEnumerable<TaskItem>> GetActiveTasksAsync()
        {
            var tasks = await GetItemsFromStorageAsync();
            return tasks.Where(t => !t.IsCompleted);
        }

        public async Task<IEnumerable<TaskItem>> GetAllAsync()
        {
            return await GetItemsFromStorageAsync();
        }

        public async Task<TaskItem> GetByIdAsync(Guid id)
        {
            var tasks = await GetItemsFromStorageAsync();
            return tasks.FirstOrDefault(t => t.Id == id);
        }

        public async Task<IEnumerable<TaskItem>> GetCompletedTasksAsync()
        {
            var tasks = await GetItemsFromStorageAsync();
            return tasks.Where(t => t.IsCompleted);
        }

        public async Task<IEnumerable<TaskItem>> GetTasksByDueDateAsync(DateTime startDate, DateTime endDate)
        {
            var tasks = await GetItemsFromStorageAsync();
            return tasks.Where(t => t.DueDate.HasValue &&
                                  t.DueDate.Value >= startDate &&
                                  t.DueDate.Value <= endDate);
        }

        public async Task<IEnumerable<TaskItem>> GetTasksByPriorityAsync(int minimumPriority)
        {
            var tasks = await GetItemsFromStorageAsync();
            return tasks.Where(t => t.Priority >= minimumPriority);
        }

        public async Task UpdateAsync(TaskItem entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var tasks = await GetItemsFromStorageAsync();
            var existingTaskIndex = tasks.FindIndex(t => t.Id == entity.Id);

            if (existingTaskIndex != -1)
            {
                tasks[existingTaskIndex] = entity;
                await SaveItemsToStorageAsync(tasks);
            }
            else
            {
                throw new KeyNotFoundException($"Task with ID {entity.Id} not found.");
            }
        }
    }
}