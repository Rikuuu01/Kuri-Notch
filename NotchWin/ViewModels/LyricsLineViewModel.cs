using System;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NotchWin.ViewModels
{
    public partial class LyricsLineViewModel : ObservableObject
    {
        private static readonly Brush InactiveBrush = CreateFrozenBrush(Color.FromRgb(161, 161, 170));
        private static readonly Brush KaraokeBaseBrush = CreateFrozenBrush(Color.FromRgb(148, 148, 158));

        private double _baseFontSize = 12.0;
        private double _activeFontSize = 14.5;
        private bool _useBold = true;

        public TimeSpan Time { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsInterlude { get; set; }
        public System.Collections.Generic.List<NotchWin.Models.SyllableItem> Syllables { get; set; } = new();
        public bool HasSyllables => Syllables != null && Syllables.Count > 0;

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private double _fontSize = 12.0;

        [ObservableProperty]
        private FontWeight _fontWeight = FontWeights.Normal;

        [ObservableProperty]
        private Brush _foregroundBrush = InactiveBrush;

        [ObservableProperty]
        private double _opacity = 0.55;

        public double Dot1Opacity { get; set; } = 0.35;
        public double Dot2Opacity { get; set; } = 0.35;
        public double Dot3Opacity { get; set; } = 0.35;

        public LyricsLineViewModel() { }

        public LyricsLineViewModel(TimeSpan time, string text, bool isInterlude = false)
        {
            Time = time;
            Text = text;
            IsInterlude = isInterlude;
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public void Configure(double baseFontSize, bool useBold)
        {
            _baseFontSize = Math.Clamp(baseFontSize, 10.0, 14.0);
            _activeFontSize = _baseFontSize + 2.5;
            _useBold = useBold;

            FontSize = IsActive ? _activeFontSize : _baseFontSize;
            FontWeight = IsActive && _useBold ? FontWeights.Bold : FontWeights.Normal;
        }

        public void SetActive(bool active, bool karaokeFill = false)
        {
            IsActive = active;
            if (active)
            {
                FontSize = _activeFontSize;
                FontWeight = _useBold ? FontWeights.Bold : FontWeights.Normal;
                ForegroundBrush = karaokeFill ? KaraokeBaseBrush : Brushes.White;
                Opacity = 1.0;
            }
            else
            {
                FontSize = _baseFontSize;
                FontWeight = FontWeights.Normal;
                ForegroundBrush = InactiveBrush;
                Opacity = 0.50;
            }
        }

        public void UpdateCountdown(double remainingSec)
        {
        }
    }
}
