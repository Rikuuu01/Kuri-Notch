using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotchWin.Models;
using NotchWin.Services;

namespace NotchWin.ViewModels
{
    public partial class TodoItemViewModel : ObservableObject
    {
        public Guid Id { get; }

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private bool _isCompleted;

        public TodoItemViewModel(TodoItem item)
        {
            Id = item.Id;
            _title = item.Title;
            _isCompleted = item.IsCompleted;
        }
    }

    public partial class TodoViewModel : ObservableObject
    {
        private readonly ITodoService _todoService;

        [ObservableProperty]
        private string _newTaskTitle = string.Empty;

        [ObservableProperty]
        private string _summaryText = "0/0 Done";

        [ObservableProperty]
        private double _progressPercentage = 0.0;

        public bool HasTasks => Tasks.Count > 0;
        public bool HasNoTasks => Tasks.Count == 0;

        public event EventHandler? TasksChanged;

        public ObservableCollection<TodoItemViewModel> Tasks { get; } = new();

        public TodoViewModel(ITodoService todoService)
        {
            _todoService = todoService;
            _todoService.ItemsChanged += (_, _) => Reload();
            LocalizationService.Instance.LanguageChanged += (_, _) => UpdateSummary();
            Reload();
        }

        private void Reload()
        {
            Tasks.Clear();
            foreach (var item in _todoService.Items)
            {
                Tasks.Add(new TodoItemViewModel(item));
            }
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int total = Tasks.Count;
            int completed = Tasks.Count(t => t.IsCompleted);
            SummaryText = LocalizationService.Instance.Get("Todo.Summary", completed, total);
            ProgressPercentage = total > 0 ? (double)completed / total : 0.0;
            OnPropertyChanged(nameof(HasTasks));
            OnPropertyChanged(nameof(HasNoTasks));
            TasksChanged?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        public async Task AddTaskAsync()
        {
            if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;
            string title = NewTaskTitle;
            NewTaskTitle = string.Empty;
            await _todoService.AddItemAsync(title);
        }

        [RelayCommand]
        public async Task ToggleTaskAsync(object? param)
        {
            if (param is Guid id)
            {
                await _todoService.ToggleItemAsync(id);
            }
            else if (param is TodoItemViewModel vm)
            {
                await _todoService.ToggleItemAsync(vm.Id);
            }
        }

        [RelayCommand]
        public async Task DeleteTaskAsync(object? param)
        {
            if (param is Guid id)
            {
                await _todoService.DeleteItemAsync(id);
            }
            else if (param is TodoItemViewModel vm)
            {
                await _todoService.DeleteItemAsync(vm.Id);
            }
        }
    }
}
