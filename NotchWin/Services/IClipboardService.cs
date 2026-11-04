using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NotchWin.Models;

namespace NotchWin.Services
{
    public interface IClipboardService
    {
        event EventHandler<ClipboardItem>? ItemAdded;
        event EventHandler? HistoryChanged;

        IReadOnlyList<ClipboardItem> History { get; }

        void Initialize(IntPtr windowHandle);
        void ProcessClipboardUpdate();
        Task CopyToClipboardAsync(ClipboardItem item);
        void DeleteItem(string id);
        void TogglePin(string id);
        void ClearAll();
        void ClearImages();
        void ClearTexts();
    }
}
