using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotchWin.Models;
using NotchWin.Services;

namespace NotchWin.ViewModels
{
    public partial class NoteViewModel : ObservableObject
    {
        private readonly INoteService _noteService;
        private readonly DispatcherTimer _autoSaveTimer;
        private readonly DispatcherTimer _focusTimer;
        private bool _isUpdatingInternally = false;

        public event EventHandler<string>? ToastRequested;

        #region Notes Properties
        public ObservableCollection<NoteItem> Notes { get; } = new();

        [ObservableProperty]
        private NoteItem? _selectedNote;

        [ObservableProperty]
        private string _noteContent = string.Empty;

        public bool HasNotes => Notes.Count > 0;
        public bool CanDeleteNotes => Notes.Count > 1;
        #endregion

        #region Focus Timer Properties
        [ObservableProperty]
        private TimeSpan _timerTotal = TimeSpan.FromMinutes(5);

        [ObservableProperty]
        private TimeSpan _timerRemaining = TimeSpan.FromMinutes(5);

        [ObservableProperty]
        private bool _isTimerRunning;

        [ObservableProperty]
        private double _timerProgress = 1.0;

        public string TimerDisplay
        {
            get
            {
                if (TimerRemaining.TotalHours >= 1)
                    return TimerRemaining.ToString(@"h\:mm\:ss");
                return TimerRemaining.ToString(@"m\:ss");
            }
        }
        #endregion

        #region Reminders Properties
        public ObservableCollection<ReminderItem> Reminders { get; } = new();

        [ObservableProperty]
        private string _reminderInput = string.Empty;

        [ObservableProperty]
        private string _alarmHour = DateTime.Now.AddHours(1).ToString("HH");

        [ObservableProperty]
        private string _alarmMinute = "00";

        public bool HasReminders => Reminders.Count > 0;
        #endregion

        private readonly ISoundNotificationService? _soundNotificationService;

        public NoteViewModel(INoteService noteService, ISoundNotificationService? soundNotificationService = null)
        {
            _noteService = noteService;
            _soundNotificationService = soundNotificationService;

            _noteService.NotesChanged += (s, e) => RefreshNotes();
            _noteService.RemindersChanged += (s, e) => RefreshReminders();
            _noteService.ReminderDue += OnReminderDue;

            // Debounced autosave timer (350ms)
            _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _autoSaveTimer.Tick += (s, e) =>
            {
                _autoSaveTimer.Stop();
                SaveActiveNoteContent();
            };

            // Focus countdown timer (1 second ticks)
            _focusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _focusTimer.Tick += OnFocusTimerTick;

            RefreshNotes();
            RefreshReminders();

            SelectedNote = Notes.FirstOrDefault();
            if (SelectedNote != null)
            {
                NoteContent = SelectedNote.Content;
            }
        }

        #region Notes Implementation
        private void RefreshNotes()
        {
            _isUpdatingInternally = true;
            try
            {
                var curId = SelectedNote?.Id;
                Notes.Clear();
                foreach (var n in _noteService.Notes)
                {
                    Notes.Add(n);
                }

                if (curId != null)
                {
                    SelectedNote = Notes.FirstOrDefault(x => x.Id == curId) ?? Notes.FirstOrDefault();
                }
                else if (SelectedNote == null && Notes.Count > 0)
                {
                    SelectedNote = Notes[0];
                }

                foreach (var n in Notes)
                {
                    n.IsSelected = (n.Id == SelectedNote?.Id);
                }

                if (SelectedNote != null && NoteContent != SelectedNote.Content)
                {
                    NoteContent = SelectedNote.Content;
                }

                OnPropertyChanged(nameof(HasNotes));
                OnPropertyChanged(nameof(CanDeleteNotes));
            }
            finally
            {
                _isUpdatingInternally = false;
            }
        }

        partial void OnSelectedNoteChanged(NoteItem? value)
        {
            if (_isUpdatingInternally) return;
            foreach (var n in Notes)
            {
                n.IsSelected = (n.Id == value?.Id);
            }
            if (value == null) return;
            NoteContent = value.Content;
        }

        partial void OnNoteContentChanged(string value)
        {
            if (_isUpdatingInternally || SelectedNote == null) return;
            SelectedNote.Content = value;
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        private void SaveActiveNoteContent()
        {
            if (SelectedNote != null)
            {
                _noteService.SaveNote(SelectedNote);
            }
        }

        [RelayCommand]
        public void SelectNote(NoteItem? note)
        {
            if (note == null) return;
            SelectedNote = note;
            foreach (var n in Notes)
            {
                n.IsSelected = (n.Id == note.Id);
                n.IsConfirmingDelete = false;
            }
            NoteContent = note.Content;
        }

        [RelayCommand]
        public void AddNote()
        {
            var created = _noteService.CreateNote();
            RefreshNotes();
            SelectedNote = Notes.FirstOrDefault(x => x.Id == created.Id) ?? created;
            foreach (var n in Notes)
            {
                n.IsSelected = (n.Id == SelectedNote?.Id);
                n.IsConfirmingDelete = false;
            }
            NoteContent = string.Empty;
            ToastRequested?.Invoke(this, LocalizationService.Instance["Notification.NoteCreated"]);
        }

        [RelayCommand]
        public void RequestDeleteNote(NoteItem? item)
        {
            if (item == null) return;
            if (Notes.Count <= 1)
            {
                item.Content = string.Empty;
                item.PlainText = string.Empty;
                NoteContent = string.Empty;
                SaveActiveNoteContent();
                ToastRequested?.Invoke(this, LocalizationService.Instance["Notification.NoteCleared"]);
                return;
            }

            foreach (var n in Notes)
            {
                n.IsConfirmingDelete = (n.Id == item.Id);
            }
        }

        [RelayCommand]
        public void ConfirmDeleteNote(NoteItem? item)
        {
            if (item == null) return;
            string id = item.Id;
            _noteService.DeleteNote(id);
            RefreshNotes();
            SelectedNote = Notes.FirstOrDefault();
            foreach (var n in Notes)
            {
                n.IsSelected = (n.Id == SelectedNote?.Id);
                n.IsConfirmingDelete = false;
            }
            NoteContent = SelectedNote?.Content ?? string.Empty;
            ToastRequested?.Invoke(this, LocalizationService.Instance["Notification.NoteDeleted"]);
        }

        [RelayCommand]
        public void CancelDeleteNote(NoteItem? item)
        {
            if (item == null) return;
            item.IsConfirmingDelete = false;
        }

        [RelayCommand]
        public void DeleteCurrentNote()
        {
            if (SelectedNote == null) return;
            if (Notes.Count <= 1)
            {
                SelectedNote.Content = string.Empty;
                SelectedNote.PlainText = string.Empty;
                NoteContent = string.Empty;
                SaveActiveNoteContent();
                ToastRequested?.Invoke(this, LocalizationService.Instance["Notification.NoteCleared"]);
                return;
            }
            RequestDeleteNote(SelectedNote);
        }

        public void TriggerToast(string message)
        {
            ToastRequested?.Invoke(this, message);
        }

        public void UpdateContentFromEditor(string content, string plainText)
        {
            if (_isUpdatingInternally || SelectedNote == null) return;
            SelectedNote.Content = content;
            SelectedNote.PlainText = plainText;
            NoteContent = content;
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        [RelayCommand]
        public void CopyNote()
        {
            string toCopy = !string.IsNullOrWhiteSpace(SelectedNote?.PlainText)
                ? SelectedNote.PlainText
                : NoteContent;

            if (string.IsNullOrEmpty(toCopy)) return;
            try
            {
                Clipboard.SetText(toCopy.TrimEnd());
                ToastRequested?.Invoke(this, LocalizationService.Instance["Notification.NoteCopied"]);
            }
            catch { }
        }

        [RelayCommand]
        public void FormatBold() => InsertMarkdownWrap("**", "**");

        [RelayCommand]
        public void FormatItalic() => InsertMarkdownWrap("*", "*");

        [RelayCommand]
        public void FormatStrike() => InsertMarkdownWrap("~~", "~~");

        [RelayCommand]
        public void FormatCode() => InsertMarkdownWrap("`", "`");

        [RelayCommand]
        public void FormatBullet() => AppendLinePrefix("• ");

        [RelayCommand]
        public void FormatNumber() => AppendLinePrefix("1. ");

        [RelayCommand]
        public void FormatChecklist() => AppendLinePrefix("[ ] ");

        private void InsertMarkdownWrap(string open, string close)
        {
            if (string.IsNullOrEmpty(NoteContent))
            {
                NoteContent = $"{open}teks{close}";
            }
            else
            {
                NoteContent += $" {open}teks{close}";
            }
        }

        private void AppendLinePrefix(string prefix)
        {
            if (string.IsNullOrEmpty(NoteContent))
            {
                NoteContent = prefix;
            }
            else
            {
                NoteContent += (NoteContent.EndsWith("\n") ? "" : "\n") + prefix;
            }
        }
        #endregion

        #region Focus Timer Implementation
        [RelayCommand]
        public void StartPauseTimer()
        {
            if (IsTimerRunning)
            {
                _focusTimer.Stop();
                IsTimerRunning = false;
            }
            else
            {
                if (TimerRemaining <= TimeSpan.Zero)
                {
                    TimerRemaining = TimerTotal > TimeSpan.Zero ? TimerTotal : TimeSpan.FromMinutes(5);
                }
                _focusTimer.Start();
                IsTimerRunning = true;
            }
        }

        [RelayCommand]
        public void SetPresetTimer(object? param)
        {
            if (param != null && int.TryParse(param.ToString(), out int mins) && mins > 0)
            {
                _focusTimer.Stop();
                IsTimerRunning = false;
                TimerTotal = TimeSpan.FromMinutes(mins);
                TimerRemaining = TimerTotal;
                TimerProgress = 1.0;
                OnPropertyChanged(nameof(TimerDisplay));
            }
        }

        [RelayCommand]
        public void CustomTimer()
        {
            // Rotate commonly requested custom amounts: 20m -> 30m -> 50m -> 5m
            int currentMins = (int)TimerTotal.TotalMinutes;
            int nextMins = currentMins switch
            {
                5 => 20,
                20 => 30,
                30 => 50,
                50 => 60,
                _ => 5
            };
            SetPresetTimer(nextMins);
            ToastRequested?.Invoke(this, LocalizationService.Instance.Get("Notification.TimerMinutes", nextMins));
        }

        private void OnFocusTimerTick(object? sender, EventArgs e)
        {
            if (TimerRemaining > TimeSpan.FromSeconds(1))
            {
                TimerRemaining -= TimeSpan.FromSeconds(1);
                TimerProgress = TimerTotal.TotalSeconds > 0
                    ? Math.Clamp(TimerRemaining.TotalSeconds / TimerTotal.TotalSeconds, 0, 1)
                    : 0;
                OnPropertyChanged(nameof(TimerDisplay));
            }
            else
            {
                TimerRemaining = TimeSpan.Zero;
                TimerProgress = 0;
                IsTimerRunning = false;
                _focusTimer.Stop();
                OnPropertyChanged(nameof(TimerDisplay));

                try { (_soundNotificationService ?? new SoundNotificationService()).PlayTimerAlarm(); } catch { }
                ToastRequested?.Invoke(this, LocalizationService.Instance["Notification.FocusTimeComplete"]);
            }
        }
        #endregion

        #region Reminders Implementation
        private void RefreshReminders()
        {
            Reminders.Clear();
            foreach (var r in _noteService.Reminders.Take(25))
            {
                Reminders.Add(r);
            }
            OnPropertyChanged(nameof(HasReminders));
        }

        [RelayCommand]
        public void SetQuickAlarm(string preset)
        {
            var now = DateTime.Now;
            DateTime target = preset switch
            {
                "+15m" => now.AddMinutes(15),
                "+30m" => now.AddMinutes(30),
                "+1h" => now.AddHours(1),
                "Tomorrow9" => DateTime.Today.AddDays(1).AddHours(9),
                _ => now.AddMinutes(30)
            };
            AlarmHour = target.ToString("HH");
            AlarmMinute = target.ToString("mm");
            ToastRequested?.Invoke(this, LocalizationService.Instance.Get("Notification.AlarmTimeSet", AlarmHour, AlarmMinute));
        }

        [RelayCommand]
        public void AddReminder()
        {
            var now = DateTime.Now;
            int hour = (now.Hour + 1) % 24;
            int minute = 0;

            if (int.TryParse(AlarmHour, out int parsedHour))
            {
                hour = Math.Clamp(parsedHour, 0, 23);
            }
            if (int.TryParse(AlarmMinute, out int parsedMinute))
            {
                minute = Math.Clamp(parsedMinute, 0, 59);
            }

            var dueTime = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
            if (dueTime <= now)
            {
                dueTime = dueTime.AddDays(1);
            }

            string taskText = string.IsNullOrWhiteSpace(ReminderInput) ? "Reminder Alarm" : ReminderInput.Trim();

            if (!string.IsNullOrWhiteSpace(ReminderInput))
            {
                var (parsedText, parsedDue) = ParseNaturalLanguageReminder(ReminderInput.Trim());
                if (parsedDue > now && Math.Abs((parsedDue - now.AddMinutes(30)).TotalSeconds) > 2)
                {
                    dueTime = parsedDue;
                    taskText = parsedText;
                }
            }

            var item = new ReminderItem
            {
                Text = taskText,
                DueTime = dueTime,
                IsCompleted = false,
                IsNotified = false,
                IsAlarmEnabled = true
            };

            _noteService.AddReminder(item);
            ReminderInput = string.Empty;
            string day = dueTime.Date == DateTime.Today
                ? LocalizationService.Instance["Notification.Today"]
                : LocalizationService.Instance["Notification.Tomorrow"];
            string alarmMsg = LocalizationService.Instance.Get("Notification.AlarmSet", dueTime.ToString("HH:mm"), day);
            ToastRequested?.Invoke(this, $"⏰ {alarmMsg}");
        }

        [RelayCommand]
        public void ToggleReminder(ReminderItem? item)
        {
            if (item == null) return;
            _noteService.ToggleReminder(item.Id);
        }

        [RelayCommand]
        public void DeleteReminder(ReminderItem? item)
        {
            if (item == null) return;
            _noteService.DeleteReminder(item.Id);
        }

        private (string Task, DateTime Due) ParseNaturalLanguageReminder(string input)
        {
            var now = DateTime.Now;
            DateTime due = now.AddMinutes(30); // Default fallback: 30 minutes
            string clean = input;

            // Pattern 1: "in X minutes" or "dalam X menit"
            var m1 = Regex.Match(input, @"(?:in|dalam)\s+(\d+)\s*(?:minutes|minute|menit|mnt|m)\b", RegexOptions.IgnoreCase);
            if (m1.Success && int.TryParse(m1.Groups[1].Value, out int minVal))
            {
                due = now.AddMinutes(minVal);
                clean = input.Replace(m1.Value, "").Trim();
            }

            // Pattern 2: "in X hours" or "dalam X jam"
            var m2 = Regex.Match(input, @"(?:in|dalam)\s+(\d+)\s*(?:hours|hour|jam|j)\b", RegexOptions.IgnoreCase);
            if (m2.Success && int.TryParse(m2.Groups[1].Value, out int hrVal))
            {
                due = now.AddHours(hrVal);
                clean = input.Replace(m2.Value, "").Trim();
            }

            // Pattern 3: "tomorrow at HH:mm" or "besok jam HH:mm"
            var m3 = Regex.Match(input, @"(?:tomorrow|besok)(?:\s+(?:at|jam))?\s+(\d{1,2})(?:[:.](\d{2}))?", RegexOptions.IgnoreCase);
            if (m3.Success && int.TryParse(m3.Groups[1].Value, out int tomHr))
            {
                int min = m3.Groups[2].Success && int.TryParse(m3.Groups[2].Value, out int mVal) ? mVal : 0;
                due = now.Date.AddDays(1).AddHours(tomHr).AddMinutes(min);
                clean = input.Replace(m3.Value, "").Trim();
            }

            // Pattern 4: "at HH:mm" or "jam HH:mm"
            var m4 = Regex.Match(input, @"(?:at|jam)\s+(\d{1,2})[:.](\d{2})", RegexOptions.IgnoreCase);
            if (m4.Success && int.TryParse(m4.Groups[1].Value, out int hr) && int.TryParse(m4.Groups[2].Value, out int mn))
            {
                due = new DateTime(now.Year, now.Month, now.Day, hr, mn, 0);
                if (due < now) due = due.AddDays(1);
                clean = input.Replace(m4.Value, "").Trim();
            }

            if (string.IsNullOrWhiteSpace(clean)) clean = input;
            clean = clean.Trim(',', '.', ' ', '"', '\'');
            return (clean, due);
        }

        private void OnReminderDue(object? sender, ReminderItem reminder)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                try { (_soundNotificationService ?? new SoundNotificationService()).PlayTimerAlarm(); } catch { }
                string remMsg = LocalizationService.Instance.Get("Notification.Reminder", reminder.Text);
                ToastRequested?.Invoke(this, $"⏰ {remMsg}");
            });
        }
        #endregion
    }
}
