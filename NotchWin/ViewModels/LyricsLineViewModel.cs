using System;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NotchWin.ViewModels
{
    public partial class LyricsLineViewModel : ObservableObject
    {
        public TimeSpan Time { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsInterlude { get; set; }

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private double _fontSize = 12.0;

        [ObservableProperty]
        private FontWeight _fontWeight = FontWeights.Normal;

        [ObservableProperty]
        private Brush _foregroundBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170)); // #A1A1AA

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

        public void SetActive(bool active)
        {
            IsActive = active;
            if (active)
            {
                FontSize = 14.5;
                FontWeight = FontWeights.Bold;
                ForegroundBrush = Brushes.White;
                Opacity = 1.0;
            }
            else
            {
                FontSize = 12.0;
                FontWeight = FontWeights.Normal;
                ForegroundBrush = new SolidColorBrush(Color.FromRgb(161, 161, 170));
                Opacity = 0.50;
            }
        }

        public void UpdateCountdown(double remainingSec)
        {
        }
    }
}
