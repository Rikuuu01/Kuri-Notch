using System.Collections.Generic;
using System.Collections.ObjectModel;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface IShelfService
    {
        ObservableCollection<ShelfItem> Items { get; }
        int AddFiles(IEnumerable<string> filePaths);
        void RemoveItem(ShelfItem item);
        void ClearAll();
        void OpenItem(ShelfItem item);
        void ShowInExplorer(ShelfItem item);
        void CopyItem(ShelfItem item);
        void CopyAllToClipboard();
    }
}
