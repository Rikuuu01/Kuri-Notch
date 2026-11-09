using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NotchWin.Models;
using NotchWin.Services;

namespace NotchWin.Controls
{
    public partial class MochiPetControl : UserControl
    {
        private readonly PetCompanionService _pet = PetCompanionService.Instance;

        private bool _isPressed;

        public MochiPetControl()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnIsVisibleChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _pet.StyleChanged += Pet_StyleChanged;

            UpdateStyleVisibility();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _pet.StyleChanged -= Pet_StyleChanged;
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                UpdateStyleVisibility();
            }
        }

        private void Pet_StyleChanged(object? sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(UpdateStyleVisibility);
        }

        private void UpdateStyleVisibility()
        {
            PastelMascot.Visibility = (_pet.Style == PetStyle.PastelMochi) ? Visibility.Visible : Visibility.Collapsed;
            AstroMascot.Visibility = (_pet.Style == PetStyle.AstroMascot) ? Visibility.Visible : Visibility.Collapsed;
            EggMascot.Visibility = (_pet.Style == PetStyle.EggMascot) ? Visibility.Visible : Visibility.Collapsed;
            NotchEyes.Visibility = (_pet.Style == PetStyle.NotchEyes) ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Sapaan startup untuk gaya aktif; gaya tanpa animasi khusus memakai reaksi Happy.</summary>
        public void PlayWelcome(TimeSpan? duration = null)
        {
            var d = duration ?? TimeSpan.FromSeconds(3.4);
            switch (_pet.Style)
            {
                case PetStyle.AstroMascot when AstroMascot != null:
                    AstroMascot.ShowAlert(MascotState.Welcome, d);
                    break;
                case PetStyle.EggMascot when EggMascot != null:
                    EggMascot.PlayWelcome(d);
                    break;
                case PetStyle.PastelMochi when PastelMascot != null:
                    PastelMascot.ShowAlert(MascotState.Welcome, d);
                    break;
                case PetStyle.NotchEyes when NotchEyes != null:
                    NotchEyes.PlayWelcome();
                    break;
            }
        }

        /// <summary>Hentikan sapaan startup pada instance overlay (dipakai saat greeting dibatalkan).</summary>
        public void HideGreeting()
        {
            PastelMascot?.DismissAlert();
            AstroMascot?.DismissAlert();
            EggMascot?.DismissAlert();
            NotchEyes?.DismissAlert();
        }

        private void Control_MouseEnter(object sender, MouseEventArgs e)
        {
            _pet.TriggerHappy(true);
            if (!_isPressed)
            {
                AnimateScale(1.05, 1.05, 160);
            }
        }

        private void Control_MouseLeave(object sender, MouseEventArgs e)
        {
            _isPressed = false;
            _pet.TriggerHappy(false);
            AnimateScale(1.0, 1.0, 200);
        }

        private void Control_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isPressed = true;
            _pet.TriggerPoke();

            // Tactile Squash on press
            AnimateScale(1.10, 0.90, 80);
            e.Handled = true;
        }

        private void Control_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPressed)
            {
                _isPressed = false;
                // Elastic bounce on release
                AnimateBounce(1.0, 260);
                if (_pet.Style == PetStyle.PastelMochi && PastelMascot != null)
                {
                    PastelMascot.ShowAlert(MascotState.Happy);
                }
                else if (_pet.Style == PetStyle.AstroMascot && AstroMascot != null)
                {
                    AstroMascot.ShowAlert(MascotState.Happy);
                }
                else if (_pet.Style == PetStyle.EggMascot && EggMascot != null)
                {
                    EggMascot.ShowAlert(MascotState.Happy);
                }
                else if (_pet.Style == PetStyle.NotchEyes && NotchEyes != null)
                {
                    NotchEyes.ShowAlert(MascotState.Happy);
                }
                e.Handled = true;
            }
        }

        private void AnimateScale(double targetX, double targetY, double durationMs)
        {
            var animX = new DoubleAnimation(targetX, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var animY = new DoubleAnimation(targetY, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            SquashScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
            SquashScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animY);
        }

        private void AnimateBounce(double targetScale, double durationMs)
        {
            var animX = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(durationMs)
            };
            animX.KeyFrames.Add(new EasingDoubleKeyFrame(1.08, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.4)), new CubicEase { EasingMode = EasingMode.EaseOut }));
            animX.KeyFrames.Add(new EasingDoubleKeyFrame(targetScale, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)), new CubicEase { EasingMode = EasingMode.EaseInOut }));

            var animY = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(durationMs)
            };
            animY.KeyFrames.Add(new EasingDoubleKeyFrame(1.08, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs * 0.4)), new CubicEase { EasingMode = EasingMode.EaseOut }));
            animY.KeyFrames.Add(new EasingDoubleKeyFrame(targetScale, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(durationMs)), new CubicEase { EasingMode = EasingMode.EaseInOut }));

            SquashScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animX);
            SquashScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animY);
        }
    }
}
