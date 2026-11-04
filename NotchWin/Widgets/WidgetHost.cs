using System;
using System.Collections.Generic;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace NotchWin.Widgets
{
    public partial class WidgetHost : ObservableObject
    {
        private readonly Dictionary<WidgetType, INotchWidget> _widgets = new();
        private readonly DispatcherTimer _transientTimer;

        [ObservableProperty]
        private WidgetType _currentWidgetType = WidgetType.Clock;

        [ObservableProperty]
        private bool _isTransientActive;

        public Func<WidgetType, bool>? WidgetEnabledPredicate { get; set; }

        public event EventHandler<WidgetType>? ActiveWidgetChanged;
        public event EventHandler<WidgetType>? TransientTriggered;

        public WidgetHost()
        {
            _transientTimer = new DispatcherTimer();
            _transientTimer.Tick += OnTransientTimerTick;
        }

        public void RegisterWidget(INotchWidget widget)
        {
            _widgets[widget.Type] = widget;
        }

        public INotchWidget? GetWidget(WidgetType type)
        {
            return _widgets.GetValueOrDefault(type);
        }

        /// <summary>
        /// Temporarily displays a transient widget (e.g. Volume HUD or Charger Plugged-in),
        /// then automatically returns to the highest-priority active widget.
        /// </summary>
        public void TriggerTransient(WidgetType type, TimeSpan duration)
        {
            if (WidgetEnabledPredicate != null && !WidgetEnabledPredicate(type))
            {
                return;
            }

            _transientTimer.Stop();
            _transientTimer.Interval = duration;

            IsTransientActive = true;
            SetCurrentWidget(type);

            TransientTriggered?.Invoke(this, type);

            _transientTimer.Start();
        }

        /// <summary>
        /// Displays a widget until it is dismissed explicitly (no auto-hide timer).
        /// Used by the water reminder alarm that must persist until the user acts.
        /// </summary>
        public void TriggerPersistent(WidgetType type)
        {
            _transientTimer.Stop();

            IsTransientActive = true;
            SetCurrentWidget(type);

            TransientTriggered?.Invoke(this, type);
        }

        public void DismissTransient()
        {
            _transientTimer.Stop();
            IsTransientActive = false;
            EvaluateHighestPriorityWidget();
        }

        private void OnTransientTimerTick(object? sender, EventArgs e)
        {
            _transientTimer.Stop();
            IsTransientActive = false;
            EvaluateHighestPriorityWidget();
        }

        public void EvaluateHighestPriorityWidget()
        {
            if (IsTransientActive)
            {
                // Don't interrupt an active transient notification
                return;
            }

            INotchWidget? best = null;
            foreach (var widget in _widgets.Values)
            {
                if (WidgetEnabledPredicate != null && !WidgetEnabledPredicate(widget.Type))
                {
                    continue;
                }

                if (widget.IsActive)
                {
                    if (best == null || widget.Priority > best.Priority)
                    {
                        best = widget;
                    }
                }
            }

            WidgetType target = best?.Type ?? WidgetType.Clock;
            SetCurrentWidget(target);
        }

        private void SetCurrentWidget(WidgetType type)
        {
            if (CurrentWidgetType != type)
            {
                CurrentWidgetType = type;
                Log.Debug("WidgetHost: Active widget switched to {WidgetType}", type);
                ActiveWidgetChanged?.Invoke(this, type);
            }
        }
    }
}
