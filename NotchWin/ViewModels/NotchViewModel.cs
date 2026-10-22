using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NotchWin.Models;

namespace NotchWin.ViewModels
{
    public partial class NotchViewModel : ObservableObject
    {
        // Default geometry constants (will later be configurable via AppSettings)
        public const double CollapsedWidth = 190.0;
        public const double CollapsedHeight = 32.0;
        public const double ExpandedWidth = 380.0;
        public const double ExpandedHeight = 150.0;

        // Radius for bottom-left and bottom-right corners (top is flush against bezel)
        public const double CollapsedCornerRadius = 16.0;
        public const double ExpandedCornerRadius = 22.0;

        private CancellationTokenSource? _collapseCts;
        private readonly TimeSpan _collapseDelay = TimeSpan.FromMilliseconds(450);

        [ObservableProperty]
        private NotchState _currentState = NotchState.Collapsed;

        [ObservableProperty]
        private bool _isLockedExpanded;

        [ObservableProperty]
        private double _targetWidth = CollapsedWidth;

        [ObservableProperty]
        private double _targetHeight = CollapsedHeight;

        [ObservableProperty]
        private double _targetCornerRadius = CollapsedCornerRadius;

        [ObservableProperty]
        private bool _isContentVisible;

        [ObservableProperty]
        private string _statusText = "NotchWin";

        public event EventHandler? RequestCloseEvent;
        public event EventHandler? StateAnimationRequested;

        public NotchViewModel()
        {
            UpdateGeometryForState(NotchState.Collapsed);
        }

        [RelayCommand]
        public void OnMouseEnter()
        {
            CancelPendingCollapse();
            if (CurrentState == NotchState.Collapsed)
            {
                Expand(isLock: false);
            }
        }

        [RelayCommand]
        public void OnMouseLeave()
        {
            if (IsLockedExpanded)
            {
                // Locked by click, do not auto-collapse on mouse leave
                return;
            }

            ScheduleDelayedCollapse();
        }

        [RelayCommand]
        public void OnPillClicked()
        {
            CancelPendingCollapse();

            if (CurrentState == NotchState.Collapsed)
            {
                // Open and lock
                Expand(isLock: true);
            }
            else
            {
                // Currently expanded:
                if (!IsLockedExpanded)
                {
                    // Was expanded by hover -> click locks/pins it open
                    IsLockedExpanded = true;
                }
                else
                {
                    // Was already locked open -> click unlocks and collapses it
                    Collapse();
                }
            }
        }

        public void Expand(bool isLock = false)
        {
            CancelPendingCollapse();

            if (isLock)
            {
                IsLockedExpanded = true;
            }

            if (CurrentState != NotchState.Expanded)
            {
                CurrentState = NotchState.Expanded;
                UpdateGeometryForState(NotchState.Expanded);
                StateAnimationRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Collapse()
        {
            CancelPendingCollapse();
            IsLockedExpanded = false;

            if (CurrentState != NotchState.Collapsed)
            {
                CurrentState = NotchState.Collapsed;
                UpdateGeometryForState(NotchState.Collapsed);
                StateAnimationRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        [RelayCommand]
        public void RequestClose()
        {
            RequestCloseEvent?.Invoke(this, EventArgs.Empty);
        }

        private void ScheduleDelayedCollapse()
        {
            CancelPendingCollapse();
            _collapseCts = new CancellationTokenSource();
            var token = _collapseCts.Token;

            Task.Delay(_collapseDelay, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        if (!IsLockedExpanded)
                        {
                            Collapse();
                        }
                    });
                }
            }, TaskScheduler.Default);
        }

        private void CancelPendingCollapse()
        {
            if (_collapseCts != null)
            {
                _collapseCts.Cancel();
                _collapseCts.Dispose();
                _collapseCts = null;
            }
        }

        private void UpdateGeometryForState(NotchState state)
        {
            if (state == NotchState.Expanded)
            {
                TargetWidth = ExpandedWidth;
                TargetHeight = ExpandedHeight;
                TargetCornerRadius = ExpandedCornerRadius;
            }
            else
            {
                TargetWidth = CollapsedWidth;
                TargetHeight = CollapsedHeight;
                TargetCornerRadius = CollapsedCornerRadius;
            }
        }
    }
}
