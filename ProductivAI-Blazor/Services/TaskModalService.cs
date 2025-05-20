using ProductivAI_Blazor.Models;
using System;
using System.Threading.Tasks;

namespace ProductivAI_Blazor.Services
{
    public class TaskModalService
    {
        public event Action<AiTaskSuggestion?>? OnOpenTaskModalRequested;
        public event Action? OnCloseTaskModalRequested;
        public event Func<AiTaskSuggestion, Task>? OnTaskSuccessfullySavedFromSuggestion;

        public event Action<TaskItemModel?>? OnOpenStandardTaskModalRequested;
        public event Func<TaskItemModel, Task>? OnStandardTaskSaved;

        public event Func<int, Task>? OnTaskIdeasBatchSaved;

        public void RequestOpenModal(AiTaskSuggestion? suggestion = null)
        {
            OnOpenTaskModalRequested?.Invoke(suggestion);
        }

        public void RequestCloseModal()
        {
            OnCloseTaskModalRequested?.Invoke();
        }

        public async Task NotifyTaskSavedFromSuggestion(AiTaskSuggestion suggestion)
        {
            if (OnTaskSuccessfullySavedFromSuggestion != null)
            {
                await OnTaskSuccessfullySavedFromSuggestion.Invoke(suggestion);
            }
        }

        public void RequestOpenStandardModal(TaskItemModel? task = null)
        {
            Console.WriteLine($"[TaskModalService] RequestOpenStandardModal called. Task ID: {task?.Id}");
            OnOpenStandardTaskModalRequested?.Invoke(task);
        }

        public async Task NotifyStandardTaskSaved(TaskItemModel task) => await (OnStandardTaskSaved?.Invoke(task) ?? Task.CompletedTask);

        public async Task NotifyTaskSuccessfullySavedFromSuggestion(AiTaskSuggestion task) => await (OnTaskSuccessfullySavedFromSuggestion?.Invoke(task) ?? Task.CompletedTask);

        public async Task NotifyTaskIdeasBatchSaved(int projectId) => await (OnTaskIdeasBatchSaved?.Invoke(projectId) ?? Task.CompletedTask);
    }
} 