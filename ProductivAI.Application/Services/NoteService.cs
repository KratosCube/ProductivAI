using ProductivAI.Core.Models;
using ProductivAI.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace ProductivAI.Application.Services
{
    public interface INoteService
    {
        Task<NoteItem> GetNoteByIdAsync(Guid id);
        Task<IEnumerable<NoteItem>> GetAllNotesAsync();
        Task<IEnumerable<NoteItem>> GetImportantNotesAsync();
        Task<NoteItem> CreateNoteAsync(string title, string content, bool isImportant = false, List<string> tags = null);
        Task UpdateNoteAsync(NoteItem note);
        Task DeleteNoteAsync(Guid id);
        Task<IEnumerable<NoteItem>> SearchNotesAsync(string searchTerm);
        Task<string> EnhanceNoteWithAIAsync(Guid noteId, UserContext userContext);
    }

    public class NoteService : INoteService
    {
        private readonly INoteRepository _noteRepository;
        private readonly IAIService _aiService;

        public NoteService(INoteRepository noteRepository, IAIService aiService)
        {
            _noteRepository = noteRepository;
            _aiService = aiService;
        }

        public async Task<NoteItem> GetNoteByIdAsync(Guid id)
        {
            return await _noteRepository.GetByIdAsync(id);
        }

        public async Task<IEnumerable<NoteItem>> GetAllNotesAsync()
        {
            return await _noteRepository.GetAllAsync();
        }

        public async Task<IEnumerable<NoteItem>> GetImportantNotesAsync()
        {
            return await _noteRepository.GetImportantNotesAsync();
        }

        public async Task<NoteItem> CreateNoteAsync(string title, string content, bool isImportant = false, List<string> tags = null)
        {
            var note = new NoteItem
            {
                Title = title,
                Content = content,
                IsImportant = isImportant,
                Tags = tags ?? new List<string>(),
                CreatedDate = DateTime.Now
            };

            return await _noteRepository.AddAsync(note);
        }

        public async Task UpdateNoteAsync(NoteItem note)
        {
            await _noteRepository.UpdateAsync(note);
        }

        public async Task DeleteNoteAsync(Guid id)
        {
            await _noteRepository.DeleteAsync(id);
        }

        public async Task<IEnumerable<NoteItem>> SearchNotesAsync(string searchTerm)
        {
            return await _noteRepository.SearchNotesAsync(searchTerm);
        }

        public async Task<string> EnhanceNoteWithAIAsync(Guid noteId, UserContext userContext)
        {
            var note = await _noteRepository.GetByIdAsync(noteId);
            if (note == null)
                throw new ArgumentException("Note not found", nameof(noteId));

            var enhancedContent = await _aiService.EnhanceNoteAsync(note.Content, userContext);
            return enhancedContent;
        }
    }
}