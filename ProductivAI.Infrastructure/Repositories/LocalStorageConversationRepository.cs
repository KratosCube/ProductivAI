using Microsoft.JSInterop;
using ProductivAI.Core.Interfaces;
using ProductivAI.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProductivAI.Infrastructure.Repositories
{
    public class LocalStorageConversationRepository : LocalStorageRepository<ConversationSummary>, IConversationRepository
    {
        public LocalStorageConversationRepository(IJSRuntime jsRuntime)
            : base(jsRuntime, "productivai_archived_conversations")
        {
        }

        // Required method implementations from IRepository<ConversationSummary>
        public async Task<ConversationSummary> AddAsync(ConversationSummary entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var conversations = await GetItemsFromStorageAsync();

            // Ensure the entity has a valid ID
            if (entity.Id == Guid.Empty)
                entity.Id = Guid.NewGuid();

            // Ensure CreatedDate is set
            if (entity.CreatedDate == default)
                entity.CreatedDate = DateTime.Now;

            conversations.Add(entity);
            await SaveItemsToStorageAsync(conversations);
            return entity;
        }

        public async Task DeleteAsync(Guid id)
        {
            var conversations = await GetItemsFromStorageAsync();
            var conversationToRemove = conversations.FirstOrDefault(c => c.Id == id);

            if (conversationToRemove != null)
            {
                conversations.Remove(conversationToRemove);
                await SaveItemsToStorageAsync(conversations);
            }
        }

        public async Task<IEnumerable<ConversationSummary>> GetAllAsync()
        {
            var conversations = await GetItemsFromStorageAsync();
            return conversations.OrderByDescending(c => c.CreatedDate);
        }

        public async Task<ConversationSummary> GetByIdAsync(Guid id)
        {
            var conversations = await GetItemsFromStorageAsync();
            return conversations.FirstOrDefault(c => c.Id == id);
        }

        public async Task UpdateAsync(ConversationSummary entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var conversations = await GetItemsFromStorageAsync();
            var existingIndex = conversations.FindIndex(c => c.Id == entity.Id);

            if (existingIndex != -1)
            {
                conversations[existingIndex] = entity;
                await SaveItemsToStorageAsync(conversations);
            }
            else
            {
                throw new KeyNotFoundException($"Conversation with ID {entity.Id} not found.");
            }
        }

        // IConversationRepository specific methods
        public async Task<List<ConversationSummary>> SearchByTagAsync(string tag)
        {
            var all = await GetItemsFromStorageAsync();
            return all.Where(c => c.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        public async Task<List<ConversationSummary>> SearchByTaskIdAsync(Guid taskId)
        {
            var all = await GetItemsFromStorageAsync();
            return all.Where(c => c.RelatedTaskIds.Contains(taskId)).ToList();
        }

        public async Task<List<ConversationSummary>> SearchByKeywordsAsync(List<string> keywords)
        {
            if (keywords == null || !keywords.Any())
                return new List<ConversationSummary>();

            var all = await GetItemsFromStorageAsync();
            return all.Where(c =>
                keywords.Any(k =>
                    c.Title.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                    c.Summary.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                    c.Tags.Any(t => t.Contains(k, StringComparison.OrdinalIgnoreCase))
                )
            ).ToList();
        }
    }
}