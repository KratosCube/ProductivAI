using ProductivAI.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProductivAI.Core.Interfaces
{
    public interface ITaskRepository : IRepository<TaskItem>
    {
        Task<IEnumerable<TaskItem>> GetCompletedTasksAsync();
        Task<IEnumerable<TaskItem>> GetActiveTasksAsync();
        Task<IEnumerable<TaskItem>> GetTasksByDueDateAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<TaskItem>> GetTasksByPriorityAsync(int minimumPriority);
    }
}