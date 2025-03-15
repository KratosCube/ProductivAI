using System;
using System.Collections.Generic;

namespace ProductivAI.Core.Models
{
    public class ConversationSummary
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; }
        public string Summary { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public List<string> Tags { get; set; } = new List<string>();
        public List<Guid> RelatedTaskIds { get; set; } = new List<Guid>();
    }
}