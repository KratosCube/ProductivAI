using Microsoft.JSInterop;
using ProductivAI.Core.Interfaces;
using ProductivAI.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProductivAI.Application.Services
{
    public interface IConversationService
    {
        Task<List<MessageHistory>> LoadActiveConversationAsync();
        Task SaveActiveConversationAsync(List<MessageHistory> messages);
        Task<ConversationSummary> EndAndArchiveConversationAsync(List<MessageHistory> messages, UserContext userContext);
        Task<List<ConversationSummary>> FindRelevantConversationsAsync(string query, Guid? taskId = null);
    }

    public class ConversationService : IConversationService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly IConversationRepository _conversationRepository;
        private readonly IAIService _aiService;

        public ConversationService(
            IJSRuntime jsRuntime,
            IConversationRepository conversationRepository,
            IAIService aiService)
        {
            _jsRuntime = jsRuntime;
            _conversationRepository = conversationRepository;
            _aiService = aiService;
        }

        public async Task<List<MessageHistory>> LoadActiveConversationAsync()
        {
            var json = await _jsRuntime.InvokeAsync<string>(
                "localStorage.getItem",
                "productivai_active_conversation"
            );

            if (string.IsNullOrEmpty(json))
                return new List<MessageHistory>();

            try
            {
                // Add explicit JSON options to handle property names correctly
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = false
                };

                return JsonSerializer.Deserialize<List<MessageHistory>>(json, options);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error deserializing conversation: {ex.Message}");
                return new List<MessageHistory>();
            }
        }

        public async Task SaveActiveConversationAsync(List<MessageHistory> messages)
        {
            try
            {
                // Add explicit JSON options to ensure property names are preserved
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false
                };

                var json = JsonSerializer.Serialize(messages, options);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "productivai_active_conversation", json);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error saving conversation: {ex.Message}");
            }
        }

        public async Task<ConversationSummary> EndAndArchiveConversationAsync(
            List<MessageHistory> messages,
            UserContext userContext)
        {
            // Generate summary using AI
            var summaryPrompt = "Create a concise summary (3-5 sentences) of this conversation. " +
                "Then list 3-5 tags representing key topics, formatted as comma-separated values.";

            var messagesForSummary = messages.Select(m =>
                new { role = m.IsUserMessage ? "user" : "assistant", content = m.Content })
                .ToList();

            var summaryResult = await _aiService.ProcessQueryAsync(
                summaryPrompt + "\n\n" + JsonSerializer.Serialize(messagesForSummary),
                userContext
            );

            // Parse the summary and extract tags (simplified implementation)
            var lines = summaryResult.Split("\n", StringSplitOptions.RemoveEmptyEntries);
            string summary = string.Join("\n", lines.TakeWhile(l => !l.StartsWith("Tags:")));

            var tags = new List<string>();
            var tagsLine = lines.FirstOrDefault(l => l.StartsWith("Tags:"));
            if (tagsLine != null)
            {
                tags = tagsLine.Substring(5)
                    .Split(',')
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();
            }

            // Create title from first user message or first few words of summary
            string title = messages.FirstOrDefault(m => m.IsUserMessage)?.Content?.Substring(0,
                Math.Min(50, messages.First(m => m.IsUserMessage).Content.Length)) ??
                summary.Substring(0, Math.Min(50, summary.Length));

            if (title.Length == 50)
                title += "...";

            // Create summary object
            var conversationSummary = new ConversationSummary
            {
                Title = title,
                Summary = summary,
                Tags = tags,
                CreatedDate = DateTime.Now,
                RelatedTaskIds = ExtractTaskIds(messages)
            };

            // Save to repository
            await _conversationRepository.AddAsync(conversationSummary);

            return conversationSummary;
        }

        public async Task<List<ConversationSummary>> FindRelevantConversationsAsync(string query, Guid? taskId = null)
        {
            // First check for task ID match if provided
            if (taskId.HasValue)
            {
                var taskMatches = await _conversationRepository.SearchByTaskIdAsync(taskId.Value);
                if (taskMatches.Any())
                    return taskMatches;
            }

            // Extract keywords from query (simplified implementation)
            var keywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)
                .Take(5)
                .ToList();

            return await _conversationRepository.SearchByKeywordsAsync(keywords);
        }

        // Helper method to extract task IDs from conversation
        private List<Guid> ExtractTaskIds(List<MessageHistory> messages)
        {
            // In a real implementation, you would parse messages to find task references
            // This is a simplified placeholder
            return new List<Guid>();
        }
    }
}