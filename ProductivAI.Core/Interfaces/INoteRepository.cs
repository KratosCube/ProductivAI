using ProductivAI.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProductivAI.Core.Interfaces
{
    public interface INoteRepository : IRepository<NoteItem>
    {
        Task<IEnumerable<NoteItem>> GetImportantNotesAsync();
        Task<IEnumerable<NoteItem>> GetNotesByTagAsync(string tag);
        Task<IEnumerable<NoteItem>> GetNotesByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<NoteItem>> SearchNotesAsync(string searchTerm);
    }
}