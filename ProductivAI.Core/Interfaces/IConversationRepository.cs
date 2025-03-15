using ProductivAI.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProductivAI.Core.Interfaces
{
    public interface IConversationRepository : IRepository<ConversationSummary>
    {
        Task<List<ConversationSummary>> SearchByTagAsync(string tag);
        Task<List<ConversationSummary>> SearchByTaskIdAsync(Guid taskId);
        Task<List<ConversationSummary>> SearchByKeywordsAsync(List<string> keywords);
    }
}