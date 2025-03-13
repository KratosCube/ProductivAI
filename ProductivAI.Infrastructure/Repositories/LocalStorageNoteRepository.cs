using Microsoft.JSInterop;
using ProductivAI.Core.Interfaces;
using ProductivAI.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProductivAI.Infrastructure.Repositories
{
    public class LocalStorageNoteRepository : LocalStorageRepository<NoteItem>, INoteRepository
    {
        public LocalStorageNoteRepository(IJSRuntime jsRuntime)
            : base(jsRuntime, "productivai_notes")
        {
        }

        public async Task<NoteItem> AddAsync(NoteItem entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var notes = await GetItemsFromStorageAsync();

            // Ensure the note has a valid ID
            if (entity.Id == Guid.Empty)
                entity.Id = Guid.NewGuid();

            // Ensure CreatedDate is set
            if (entity.CreatedDate == default)
                entity.CreatedDate = DateTime.Now;

            notes.Add(entity);
            await SaveItemsToStorageAsync(notes);
            return entity;
        }

        public async Task DeleteAsync(Guid id)
        {
            var notes = await GetItemsFromStorageAsync();
            var noteToRemove = notes.FirstOrDefault(n => n.Id == id);

            if (noteToRemove != null)
            {
                notes.Remove(noteToRemove);
                await SaveItemsToStorageAsync(notes);
            }
        }

        public async Task<IEnumerable<NoteItem>> GetAllAsync()
        {
            var notes = await GetItemsFromStorageAsync();
            // Return notes sorted by creation date (newest first)
            return notes.OrderByDescending(n => n.CreatedDate);
        }

        public async Task<NoteItem> GetByIdAsync(Guid id)
        {
            var notes = await GetItemsFromStorageAsync();
            return notes.FirstOrDefault(n => n.Id == id);
        }

        public async Task<IEnumerable<NoteItem>> GetImportantNotesAsync()
        {
            var notes = await GetItemsFromStorageAsync();
            return notes
                .Where(n => n.IsImportant)
                .OrderByDescending(n => n.CreatedDate);
        }

        public async Task<IEnumerable<NoteItem>> GetNotesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            var notes = await GetItemsFromStorageAsync();
            return notes
                .Where(n => n.CreatedDate >= startDate && n.CreatedDate <= endDate)
                .OrderByDescending(n => n.CreatedDate);
        }

        public async Task<IEnumerable<NoteItem>> GetNotesByTagAsync(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return Enumerable.Empty<NoteItem>();

            var notes = await GetItemsFromStorageAsync();
            return notes
                .Where(n => n.Tags != null && n.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(n => n.CreatedDate);
        }

        public async Task<IEnumerable<NoteItem>> SearchNotesAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return await GetAllAsync();

            searchTerm = searchTerm.ToLower();
            var notes = await GetItemsFromStorageAsync();

            return notes
                .Where(n =>
                    (n.Title != null && n.Title.ToLower().Contains(searchTerm)) ||
                    (n.Content != null && n.Content.ToLower().Contains(searchTerm)) ||
                    (n.Tags != null && n.Tags.Any(t => t.ToLower().Contains(searchTerm)))
                )
                .OrderByDescending(n => n.CreatedDate);
        }

        public async Task UpdateAsync(NoteItem entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var notes = await GetItemsFromStorageAsync();
            var existingNoteIndex = notes.FindIndex(n => n.Id == entity.Id);

            if (existingNoteIndex != -1)
            {
                notes[existingNoteIndex] = entity;
                await SaveItemsToStorageAsync(notes);
            }
            else
            {
                throw new KeyNotFoundException($"Note with ID {entity.Id} not found.");
            }
        }

        // Additional helper methods that are appropriate for a repository

        public async Task<IEnumerable<string>> GetAllTagsAsync()
        {
            var notes = await GetItemsFromStorageAsync();

            // Extract all unique tags from all notes
            return notes
                .Where(n => n.Tags != null)
                .SelectMany(n => n.Tags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag);
        }

        public async Task<int> GetNoteCountAsync()
        {
            var notes = await GetItemsFromStorageAsync();
            return notes.Count;
        }

        public async Task<IEnumerable<NoteItem>> GetRecentNotesAsync(int count)
        {
            var notes = await GetItemsFromStorageAsync();
            return notes
                .OrderByDescending(n => n.CreatedDate)
                .Take(count);
        }

        public async Task AddTagToNoteAsync(Guid noteId, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return;

            var note = await GetByIdAsync(noteId);
            if (note != null)
            {
                if (note.Tags == null)
                    note.Tags = new List<string>();

                // Only add the tag if it doesn't already exist
                if (!note.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                {
                    note.Tags.Add(tag);
                    await UpdateAsync(note);
                }
            }
        }

        public async Task RemoveTagFromNoteAsync(Guid noteId, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return;

            var note = await GetByIdAsync(noteId);
            if (note != null && note.Tags != null)
            {
                // Find the tag with case-insensitive comparison
                var existingTag = note.Tags.FirstOrDefault(t =>
                    string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));

                if (existingTag != null)
                {
                    note.Tags.Remove(existingTag);
                    await UpdateAsync(note);
                }
            }
        }

        public async Task ToggleNoteImportanceAsync(Guid noteId)
        {
            var note = await GetByIdAsync(noteId);
            if (note != null)
            {
                note.IsImportant = !note.IsImportant;
                await UpdateAsync(note);
            }
        }

        public async Task ClearAllNotesAsync()
        {
            await SaveItemsToStorageAsync(new List<NoteItem>());
        }
    }
}