using ProductivAI_Blazor.Models;
using System;

namespace ProductivAI_Blazor.Services
{
    public class NavigationStateService
    {
        public int? PendingProjectIdToLoad { get; set; }

        private TaskItemModel? _ideaToDiscuss;
        public TaskItemModel? IdeaToDiscuss
        {
            get => _ideaToDiscuss;
            set
            {
                if (_ideaToDiscuss != value)
                {
                    _ideaToDiscuss = value;
                    OnIdeaToDiscussChanged?.Invoke();
                }
            }
        }
        public event Action? OnIdeaToDiscussChanged;
    }
} 