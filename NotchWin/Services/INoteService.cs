using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface INoteService
    {
        event EventHandler<ReminderItem>? ReminderDue;
        event EventHandler? NotesChanged;
        event EventHandler? RemindersChanged;

        IReadOnlyList<NoteItem> Notes { get; }
        IReadOnlyList<ReminderItem> Reminders { get; }

        void SaveNote(NoteItem note);
        NoteItem CreateNote();
        void DeleteNote(string id);

        void AddReminder(ReminderItem reminder);
        void DeleteReminder(string id);
        void ToggleReminder(string id);
    }
}
