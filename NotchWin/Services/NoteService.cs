using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class NoteService : INoteService
    {
        private readonly string _notesPath;
        private readonly string _remindersPath;
        private readonly List<NoteItem> _notes = new();
        private readonly List<ReminderItem> _reminders = new();
        private readonly DispatcherTimer _reminderTimer;

        public event EventHandler<ReminderItem>? ReminderDue;
        public event EventHandler? NotesChanged;
        public event EventHandler? RemindersChanged;

        public IReadOnlyList<NoteItem> Notes
        {
            get
            {
                lock (_notes) return _notes.ToList();
            }
        }

        public IReadOnlyList<ReminderItem> Reminders
        {
            get
            {
                lock (_reminders) return _reminders.ToList();
            }
        }

        public NoteService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "NotchWin");
            Directory.CreateDirectory(dir);
            _notesPath = Path.Combine(dir, "notes.json");
            _remindersPath = Path.Combine(dir, "reminders.json");

            LoadNotes();
            LoadReminders();

            if (_notes.Count == 0)
            {
                _notes.Add(new NoteItem { Title = "Note 1", Content = string.Empty });
                SaveNotesInternal();
            }

            _reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _reminderTimer.Tick += OnReminderTimerTick;
            _reminderTimer.Start();
        }

        public NoteItem CreateNote()
        {
            NoteItem note;
            lock (_notes)
            {
                int count = _notes.Count + 1;
                note = new NoteItem
                {
                    Title = $"Note {count}",
                    Content = string.Empty,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _notes.Add(note);
            }
            SaveNotesInternal();
            NotesChanged?.Invoke(this, EventArgs.Empty);
            return note;
        }

        public void SaveNote(NoteItem note)
        {
            lock (_notes)
            {
                var existing = _notes.FirstOrDefault(n => n.Id == note.Id);
                if (existing != null)
                {
                    existing.Title = note.Title;
                    existing.Content = note.Content;
                    existing.UpdatedAt = DateTime.Now;
                }
                else
                {
                    _notes.Add(note);
                }
            }
            SaveNotesInternal();
            NotesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteNote(string id)
        {
            lock (_notes)
            {
                _notes.RemoveAll(n => n.Id == id);
                if (_notes.Count == 0)
                {
                    _notes.Add(new NoteItem { Title = "Note 1", Content = string.Empty });
                }
                else
                {
                    for (int i = 0; i < _notes.Count; i++)
                    {
                        if (_notes[i].Title.StartsWith("Note"))
                        {
                            _notes[i].Title = $"Note {i + 1}";
                        }
                    }
                }
            }
            SaveNotesInternal();
            NotesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddReminder(ReminderItem reminder)
        {
            lock (_reminders)
            {
                _reminders.Insert(0, reminder);
            }
            SaveRemindersInternal();
            RemindersChanged?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteReminder(string id)
        {
            lock (_reminders)
            {
                _reminders.RemoveAll(r => r.Id == id);
            }
            SaveRemindersInternal();
            RemindersChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleReminder(string id)
        {
            lock (_reminders)
            {
                var rem = _reminders.FirstOrDefault(r => r.Id == id);
                if (rem != null)
                {
                    rem.IsCompleted = !rem.IsCompleted;
                }
            }
            SaveRemindersInternal();
            RemindersChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnReminderTimerTick(object? sender, EventArgs e)
        {
            var now = DateTime.Now;
            List<ReminderItem> due = new();
            bool hasExpiredReminders = false;

            lock (_reminders)
            {
                // Auto-cleanup: remove reminders whose due time has passed by 5 minutes or more
                int removed = _reminders.RemoveAll(r => (now - r.DueTime).TotalMinutes >= 5);
                if (removed > 0)
                {
                    hasExpiredReminders = true;
                }

                foreach (var rem in _reminders)
                {
                    if (!rem.IsCompleted && !rem.IsNotified && rem.DueTime <= now)
                    {
                        rem.IsNotified = true;
                        due.Add(rem);
                    }
                }
            }

            if (due.Count > 0 || hasExpiredReminders)
            {
                SaveRemindersInternal();
                foreach (var d in due)
                {
                    ReminderDue?.Invoke(this, d);
                }
                RemindersChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void LoadNotes()
        {
            try
            {
                if (File.Exists(_notesPath))
                {
                    string json = File.ReadAllText(_notesPath);
                    var items = JsonSerializer.Deserialize<List<NoteItem>>(json);
                    if (items != null)
                    {
                        lock (_notes)
                        {
                            _notes.Clear();
                            _notes.AddRange(items);

                            // Clean up redundant empty notes if all notes are empty
                            var nonEmpty = _notes.Where(n => !string.IsNullOrWhiteSpace(n.Content) || !string.IsNullOrWhiteSpace(n.PlainText)).ToList();
                            if (nonEmpty.Count > 0)
                            {
                                _notes.Clear();
                                _notes.AddRange(nonEmpty);
                            }
                            else
                            {
                                _notes.Clear();
                                _notes.Add(new NoteItem { Title = "Note 1", Content = string.Empty });
                            }

                            for (int i = 0; i < _notes.Count; i++)
                            {
                                if (_notes[i].Title.StartsWith("Note"))
                                {
                                    _notes[i].Title = $"Note {i + 1}";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load notes");
            }
        }

        private void SaveNotesInternal()
        {
            try
            {
                List<NoteItem> copy;
                lock (_notes) copy = _notes.ToList();
                string json = JsonSerializer.Serialize(copy, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(_notesPath, json);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to save notes");
            }
        }

        private void LoadReminders()
        {
            try
            {
                if (File.Exists(_remindersPath))
                {
                    string json = File.ReadAllText(_remindersPath);
                    var items = JsonSerializer.Deserialize<List<ReminderItem>>(json);
                    if (items != null)
                    {
                        var now = DateTime.Now;
                        bool hadExpired = false;
                        lock (_reminders)
                        {
                            _reminders.Clear();
                            foreach (var item in items)
                            {
                                if ((now - item.DueTime).TotalMinutes < 5)
                                {
                                    _reminders.Add(item);
                                }
                                else
                                {
                                    hadExpired = true;
                                }
                            }
                        }

                        if (hadExpired)
                        {
                            SaveRemindersInternal();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load reminders");
            }
        }

        private void SaveRemindersInternal()
        {
            try
            {
                List<ReminderItem> copy;
                lock (_reminders) copy = _reminders.Take(50).ToList();
                string json = JsonSerializer.Serialize(copy, new JsonSerializerOptions { WriteIndented = false });
                File.WriteAllText(_remindersPath, json);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to save reminders");
            }
        }
    }
}
