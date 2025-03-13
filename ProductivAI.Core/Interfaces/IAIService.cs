using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProductivAI.Core.Models;

namespace ProductivAI.Core.Interfaces
{
    public interface IAIService
    {
        Task<string> ProcessQueryAsync(string query, UserContext context);
        Task<List<TaskItem>> PrioritizeTasksAsync(List<TaskItem> tasks, UserContext context);
        Task<string> GenerateTaskSuggestionsAsync(UserContext context);
        Task<string> EnhanceNoteAsync(string noteContent, UserContext context);
        Task<string> ParseCommandAsync(string command, UserContext context);
        string GetModelName();

        // Original method (keep this for backward compatibility)
        Task ProcessQueryWithStreamingAsync(
            string query,
            UserContext context,
            StreamingResponseCallback callback,
            CancellationToken cancellationToken = default);

        // New method with conversation history
        Task ProcessQueryWithStreamingWithHistoryAsync(
            string query,
            UserContext context,
            List<MessageHistory> conversationHistory,
            StreamingResponseCallback callback,
            CancellationToken cancellationToken = default);
    }
}