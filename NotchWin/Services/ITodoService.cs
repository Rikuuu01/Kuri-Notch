using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface ITodoService
    {
        IReadOnlyList<TodoItem> Items { get; }
        event EventHandler? ItemsChanged;

        Task<IReadOnlyList<TodoItem>> LoadAsync();
        Task AddItemAsync(string title);
        Task ToggleItemAsync(Guid id);
        Task DeleteItemAsync(Guid id);
    }
}
