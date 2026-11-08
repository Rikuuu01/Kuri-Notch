using System;
using System.Collections.Generic;

namespace NotchWin.Models
{
    public class ShelfState
    {
        public int Version { get; set; } = 1;
        public List<ShelfEntryRecord> Items { get; set; } = new();
    }

    public class ShelfEntryRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Path { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; } = DateTime.Now;
        public bool IsDirectory { get; set; }
    }
}
