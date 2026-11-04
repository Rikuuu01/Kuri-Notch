using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NotchWin.Models;
using Serilog;

namespace NotchWin.Services
{
    public class TodoService : ITodoService
    {
        private static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NotchWin"
        );
        private static readonly string FilePath = Path.Combine(DataDirectory, "todos.json");

        private readonly List<TodoItem> _items = new();
        public IReadOnlyList<TodoItem> Items => _items.AsReadOnly();
        public event EventHandler? ItemsChanged;

        public TodoService()
        {
            LoadSynchronous();
        }

        private void LoadSynchronous()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var loaded = JsonSerializer.Deserialize<List<TodoItem>>(json);
                    if (loaded != null)
                    {
                        _items.Clear();
                        _items.AddRange(loaded);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load todos from file");
            }

            // Default sample items if empty
            if (_items.Count == 0)
            {
                _items.Add(new TodoItem { Title = "Cek email & pesan penting", IsCompleted = true });
                _items.Add(new TodoItem { Title = "Review task Dynamic Notch", IsCompleted = false });
                _items.Add(new TodoItem { Title = "Minum 1 gelas air putih", IsCompleted = false });
                Save();
            }
        }

        public Task<IReadOnlyList<TodoItem>> LoadAsync()
        {
            LoadSynchronous();
            return Task.FromResult<IReadOnlyList<TodoItem>>(_items);
        }

        public Task AddItemAsync(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return Task.CompletedTask;

            _items.Insert(0, new TodoItem
            {
                Title = title.Trim(),
                IsCompleted = false,
                CreatedAt = DateTime.Now
            });

            Save();
            ItemsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task ToggleItemAsync(Guid id)
        {
            var item = _items.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                item.IsCompleted = !item.IsCompleted;
                Save();
                ItemsChanged?.Invoke(this, EventArgs.Empty);
            }
            return Task.CompletedTask;
        }

        public Task DeleteItemAsync(Guid id)
        {
            var item = _items.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                _items.Remove(item);
                Save();
                ItemsChanged?.Invoke(this, EventArgs.Empty);
            }
            return Task.CompletedTask;
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                string json = JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to save todos to file");
            }
        }
    }
}
