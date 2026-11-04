using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NotchWin.Controls
{
    /// <summary>
    /// Aesthetic 3-bar music visualizer widget that dynamically expands and pulses
    /// symmetrically from the center horizontal axis and middle bar (epicenter).
    /// </summary>
    public partial class MusicVisualizer : UserControl
    {
        public static readonly DependencyProperty IsPlayingProperty =
            DependencyProperty.Register(
                nameof(IsPlaying),
                typeof(bool),
                typeof(MusicVisualizer),
                new PropertyMetadata(false, OnIsPlayingChanged));

        public static readonly DependencyProperty BarBrushProperty =
            DependencyProperty.Register(
                nameof(BarBrush),
                typeof(Brush),
                typeof(MusicVisualizer),
                new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender, OnBarBrushChanged));

        public bool IsPlaying
        {
            get => (bool)GetValue(IsPlayingProperty);
            set => SetValue(IsPlayingProperty, value);
        }

        public Brush BarBrush
        {
            get => (Brush)GetValue(BarBrushProperty);
            set => SetValue(BarBrushProperty, value);
        }

        private bool _isAnimating = false;

        public MusicVisualizer()
        {
            InitializeComponent();

            // Default brush to dynamic theme AccentLightBrush if present
            if (Application.Current != null && Application.Current.Resources.Contains("AccentLightBrush"))
            {
                SetResourceReference(BarBrushProperty, "AccentLightBrush");
            }

            Loaded += (s, e) => UpdateAnimationState();
            Unloaded += (s, e) => StopAnimation();
            IsVisibleChanged += (s, e) =>
            {
                if (!IsVisible)
                {
                    StopAnimation();
                }
                else if (IsPlaying)
                {
                    StartAnimation();
                }
                else
                {
                    PauseAnimation();
                }
            };
        }

        private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MusicVisualizer visualizer)
            {
                visualizer.UpdateAnimationState();
            }
        }

        private static void OnBarBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MusicVisualizer visualizer && e.NewValue is Brush brush)
            {
                if (visualizer.Bar1 != null) visualizer.Bar1.Background = brush;
                if (visualizer.Bar2 != null) visualizer.Bar2.Background = brush;
                if (visualizer.Bar3 != null) visualizer.Bar3.Background = brush;
            }
        }

        public void UpdateAnimationState()
        {
            if (!IsLoaded || !IsVisible)
            {
                if (_isAnimating)
                {
                    PauseAnimation();
                }
                return;
            }

            if (IsPlaying)
            {
                StartAnimation();
            }
            else
            {
                PauseAnimation();
            }
        }

        private void StartAnimation()
        {
            if (_isAnimating)
                return;

            _isAnimating = true;

            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };

            // Bar 2 (Center): The primary beat driver radiating from the center
            var animCenter = new DoubleAnimation
            {
                From = 0.35,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(360),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = ease
            };

            // Bar 1 (Left): Outward harmonic wave
            var animLeft = new DoubleAnimation
            {
                From = 0.25,
                To = 0.75,
                Duration = TimeSpan.FromMilliseconds(480),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = ease
            };

            // Bar 3 (Right): Outward harmonic wave
            var animRight = new DoubleAnimation
            {
                From = 0.22,
                To = 0.80,
                Duration = TimeSpan.FromMilliseconds(420),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = ease
            };

            Bar2Scale.BeginAnimation(ScaleTransform.ScaleYProperty, animCenter);
            Bar1Scale.BeginAnimation(ScaleTransform.ScaleYProperty, animLeft);
            Bar3Scale.BeginAnimation(ScaleTransform.ScaleYProperty, animRight);
        }

        private void PauseAnimation()
        {
            _isAnimating = false;

            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var restAnim = new DoubleAnimation
            {
                To = 0.22,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = easeOut
            };

            Bar1Scale.BeginAnimation(ScaleTransform.ScaleYProperty, restAnim);
            Bar2Scale.BeginAnimation(ScaleTransform.ScaleYProperty, restAnim);
            Bar3Scale.BeginAnimation(ScaleTransform.ScaleYProperty, restAnim);
        }

        private void StopAnimation()
        {
            _isAnimating = false;
            Bar1Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            Bar2Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            Bar3Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            Bar1Scale.ScaleY = 0.22;
            Bar2Scale.ScaleY = 0.22;
            Bar3Scale.ScaleY = 0.22;
        }
    }
}
