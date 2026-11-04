using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using NotchWin.Interop;
using NotchWin.Models;

namespace NotchWin.Services
{
    public class MascotAlertEventArgs : EventArgs
    {
        public MascotState State { get; }
        public TimeSpan? Duration { get; }

        public MascotAlertEventArgs(MascotState state, TimeSpan? duration = null)
        {
            State = state;
            Duration = duration;
        }
    }

    public enum PetState
    {
        Normal,
        Happy,
        Wink,
        Sleeping
    }

    /// <summary>
    /// Background controller for the dynamic notch pet companion.
    /// Manages cursor tracking, smooth gaze physics, sine-wave breathing & bobbing,
    /// music groove reactivity, multiple character styles, and tactile states.
    /// </summary>
    public class PetCompanionService
    {
        public static PetCompanionService Instance { get; } = new PetCompanionService();

        private readonly DispatcherTimer _loopTimer;
        private readonly Random _rand = new Random();

        // Cursor & Gaze Physics
        private NativeMethods.POINT _lastCursorPos;
        private double _targetPupilX;
        private double _targetPupilY;
        private double _currentPupilX;
        private double _currentPupilY;

        // Blinking
        private double _eyeScaleY = 1.0;
        private bool _isBlinking;
        private int _blinkPhase; // 0 = closing, 1 = opening
        private double _blinkElapsedMs;
        private double _nextBlinkDelaySeconds;

        // Idle & Sleep (Adaptive Timings)
        private DateTime _lastMouseMoveTime = DateTime.UtcNow;
        private DateTime _lastTickTime = DateTime.MinValue;

        /// <summary>No cursor movement for this long (seconds) before the pet falls asleep.</summary>
        public const double IdleSleepAfterSeconds = 300.0;

        private bool _isNotchExpanded = false;
        public bool IsNotchExpanded
        {
            get => _isNotchExpanded;
            set
            {
                if (_isNotchExpanded != value)
                {
                    _isNotchExpanded = value;
                    NotchExpandedChanged?.Invoke(this, value);
                }
            }
        }

        public event EventHandler<bool>? NotchExpandedChanged;

        // Sine-Wave Motion & Physics
        private double _motionTimeSeconds;
        private double _floatBobY;
        private double _bodySwayAngle;
        private double _bodyScale = 1.0;
        private double _tailAngle;
        private double _slimeSquishY;
        private double _headphoneScale;

        // Music Reactivity
        private bool _isMusicPlaying;

        // Style
        private PetStyle _style = PetStyle.PastelMochi;

        // State Machine
        private PetState _state = PetState.Normal;
        private int _temporaryStateTicks;

        // Screen center reference for notch
        private double _notchScreenX = 960.0;
        private double _notchScreenY = 15.0;

        // Events for UI controls to invalidate
        public event EventHandler? FrameUpdated;
        public event EventHandler? StateChanged;
        public event EventHandler? StyleChanged;
        public event EventHandler<MascotAlertEventArgs>? MascotAlertTriggered;
        public event EventHandler<bool>? MusicStateChanged;

        private MascotState? _currentAlert;
        private TimeSpan? _currentAlertDuration;
        private MascotState? _persistentAlert;
        private DispatcherTimer? _alertExpiryTimer;

        public MascotState? CurrentAlert => _persistentAlert ?? _currentAlert;
        public TimeSpan? CurrentAlertDuration => _persistentAlert.HasValue ? Timeout.InfiniteTimeSpan : _currentAlertDuration;

        public event EventHandler? MascotAlertCleared;

        public void SetPersistentAlert(MascotState? state)
        {
            if (_persistentAlert == state) return;
            _persistentAlert = state;

            if (state.HasValue)
            {
                _currentAlert = null;
                _currentAlertDuration = null;
                _alertExpiryTimer?.Stop();
                MascotAlertTriggered?.Invoke(this, new MascotAlertEventArgs(state.Value, Timeout.InfiniteTimeSpan));
            }
            else
            {
                _currentAlert = null;
                _currentAlertDuration = null;
                MascotAlertCleared?.Invoke(this, EventArgs.Empty);
            }
        }

        public void TriggerAlert(MascotState state, TimeSpan? duration = null)
        {
            if (!_isEnabled || _persistentAlert.HasValue) return;
            _currentAlert = state;
            _currentAlertDuration = duration;

            _alertExpiryTimer?.Stop();
            if (duration.HasValue && duration.Value > TimeSpan.Zero)
            {
                _alertExpiryTimer ??= new DispatcherTimer();
                _alertExpiryTimer.Interval = duration.Value;
                _alertExpiryTimer.Tick -= OnAlertExpired;
                _alertExpiryTimer.Tick += OnAlertExpired;
                _alertExpiryTimer.Start();
            }

            MascotAlertTriggered?.Invoke(this, new MascotAlertEventArgs(state, duration));
        }

        private void OnAlertExpired(object? sender, EventArgs e)
        {
            _alertExpiryTimer?.Stop();
            _currentAlert = null;
            _currentAlertDuration = null;
        }

        public PetStyle Style
        {
            get => _style;
            set
            {
                if (_style != value)
                {
                    _style = value;
                    StyleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public PetState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool IsMusicPlaying
        {
            get => _isMusicPlaying;
            set
            {
                if (_isMusicPlaying != value)
                {
                    _isMusicPlaying = value;
                    MusicStateChanged?.Invoke(this, value);
                }
            }
        }

        // Real-time animation properties for UI
        public double PupilOffsetX => _currentPupilX;
        public double PupilOffsetY => _currentPupilY;
        public double EyeScaleY => _eyeScaleY;
        public bool IsWinkingLeft => _state == PetState.Wink;

        public double FloatBobY => _floatBobY;
        public double BodySwayAngle => _bodySwayAngle;
        public double BodyScale => _bodyScale;
        public double HeadphoneScale => _headphoneScale;
        public double TailAngle => _tailAngle;
        public double SlimeSquishY => _slimeSquishY;

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    if (_isEnabled)
                    {
                        Start();
                    }
                    else
                    {
                        Stop();
                    }
                }
            }
        }

        private PetCompanionService()
        {
            _loopTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _loopTimer.Tick += OnLoopTick;

            _nextBlinkDelaySeconds = GetRandomBlinkInterval();
            Start();
        }

        public void Start()
        {
            if (!_loopTimer.IsEnabled && _isEnabled)
            {
                _loopTimer.Start();
            }
        }

        public void Stop()
        {
            if (_loopTimer.IsEnabled)
            {
                _loopTimer.Stop();
            }
        }

        public void SetNotchScreenPosition(double screenX, double screenY)
        {
            _notchScreenX = screenX;
            _notchScreenY = screenY;
        }

        public void TriggerHappy(bool isHappy)
        {
            if (isHappy)
            {
                _temporaryStateTicks = 0;
                State = PetState.Happy;
            }
            else if (State == PetState.Happy)
            {
                State = PetState.Normal;
            }
        }

        public void TriggerPoke()
        {
            // Wink and tactile reaction on click
            State = PetState.Wink;
            _temporaryStateTicks = 28; // ~700ms wink
            _lastMouseMoveTime = DateTime.UtcNow;
        }

        private void OnLoopTick(object? sender, EventArgs e)
        {
            if (!_isEnabled) return;

            DateTime now = DateTime.UtcNow;
            double dt = _lastTickTime == DateTime.MinValue ? 0.033 : Math.Min((now - _lastTickTime).TotalSeconds, 0.2);
            _lastTickTime = now;
            _motionTimeSeconds += dt;

            // 1. Get Global Cursor Position & Idle Detection
            if (NativeMethods.GetCursorPos(out NativeMethods.POINT cursorPt))
            {
                if (cursorPt.X != _lastCursorPos.X || cursorPt.Y != _lastCursorPos.Y)
                {
                    _lastCursorPos = cursorPt;
                    _lastMouseMoveTime = now;

                    // Wake up immediately if asleep
                    if (State == PetState.Sleeping)
                    {
                        State = PetState.Normal;
                        TriggerBlink();
                    }
                }
                else
                {
                    if ((now - _lastMouseMoveTime).TotalSeconds >= IdleSleepAfterSeconds && State == PetState.Normal)
                    {
                        State = PetState.Sleeping;
                    }
                }
            }

            // Adaptive Framerate Regulation
            TimeSpan targetInterval;
            if (State == PetState.Sleeping)
            {
                targetInterval = TimeSpan.FromMilliseconds(300); // Low CPU while AFK
            }
            else if (_isBlinking)
            {
                targetInterval = TimeSpan.FromMilliseconds(33); // 30 FPS: Smooth eye blink
            }
            else if (!IsNotchExpanded)
            {
                // In collapsed notch (22x16px), 150ms (6.6 FPS) is smooth and prevents layered window thrashing
                targetInterval = (now - _lastMouseMoveTime).TotalSeconds >= 1.5 && !_isMusicPlaying
                    ? TimeSpan.FromMilliseconds(250) // Mouse stationary: zero-render check
                    : TimeSpan.FromMilliseconds(150); // Mouse moving
            }
            else
            {
                // Interactive expanded notch: full 30 FPS while active,
                // drop to ~12 FPS once the cursor has been idle for a moment
                bool idleExpanded = (now - _lastMouseMoveTime).TotalSeconds >= 2.0 && !_isMusicPlaying;
                targetInterval = idleExpanded
                    ? TimeSpan.FromMilliseconds(83)
                    : TimeSpan.FromMilliseconds(33);
            }

            if (_loopTimer.Interval != targetInterval)
            {
                _loopTimer.Interval = targetInterval;
            }

            // 2. Handle Temporary States (Wink/Poke)
            if (_temporaryStateTicks > 0)
            {
                _temporaryStateTicks--;
                if (_temporaryStateTicks == 0 && State == PetState.Wink)
                {
                    State = PetState.Normal;
                }
            }

            // 3. Gaze & Pupil Tracking
            if (State == PetState.Normal || State == PetState.Wink)
            {
                double deltaX = _lastCursorPos.X - _notchScreenX;
                double deltaY = _lastCursorPos.Y - _notchScreenY;
                double angle = Math.Atan2(deltaY, deltaX);
                double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                // Max elliptical displacement (horizontal 2.2px, vertical 3.4px)
                double maxRx = 2.2;
                double maxRy = 3.4;

                // Logarithmic compression for distance sensitivity
                double factor = Math.Min(distance / 160.0, 1.0);

                _targetPupilX = Math.Cos(angle) * (maxRx * factor);
                _targetPupilY = Math.Sin(angle) * (maxRy * factor);

                if (!IsNotchExpanded)
                {
                    // In collapsed notch, snap when close to target to stop micro-lerp invalidations
                    if (Math.Abs(_targetPupilX - _currentPupilX) < 0.12) _currentPupilX = _targetPupilX;
                    else _currentPupilX += (_targetPupilX - _currentPupilX) * 0.45;

                    if (Math.Abs(_targetPupilY - _currentPupilY) < 0.12) _currentPupilY = _targetPupilY;
                    else _currentPupilY += (_targetPupilY - _currentPupilY) * 0.45;
                }
                else
                {
                    // Smooth linear interpolation (lerp damping)
                    _currentPupilX += (_targetPupilX - _currentPupilX) * 0.28;
                    _currentPupilY += (_targetPupilY - _currentPupilY) * 0.28;
                }
            }
            else
            {
                // Center pupils when happy or sleeping
                _currentPupilX += (0.0 - _currentPupilX) * 0.35;
                _currentPupilY += (0.0 - _currentPupilY) * 0.35;
                if (Math.Abs(_currentPupilX) < 0.05) _currentPupilX = 0.0;
                if (Math.Abs(_currentPupilY) < 0.05) _currentPupilY = 0.0;
            }

            // 4. Sine-Wave Motion & Music Reactivity Physics
            bool isGrooving = _isMusicPlaying && State != PetState.Sleeping;
            double targetHeadphone = isGrooving ? 1.0 : 0.0;
            _headphoneScale += (targetHeadphone - _headphoneScale) * 0.18;
            if (Math.Abs(_headphoneScale - targetHeadphone) < 0.01) _headphoneScale = targetHeadphone;

            if (State == PetState.Sleeping)
            {
                // Sleep: stationary neutral body
                _floatBobY = 0.0;
                _bodySwayAngle = 0.0;
                _bodyScale = 1.0;
                _tailAngle = 0.0;
                _slimeSquishY = 0.0;
            }
            else if (isGrooving)
            {
                // Upbeat rhythmic music groove (tempo ~0.68s period)
                double beatHalfPeriod = 0.68;
                double beatSine = (1.0 - Math.Cos(Math.PI * (_motionTimeSeconds % (beatHalfPeriod * 2.0)) / beatHalfPeriod)) / 2.0;
                _floatBobY = -2.6 * beatSine;
                _bodySwayAngle = 3.5 * Math.Sin(_motionTimeSeconds * 4.6);
                _bodyScale = 1.0 + 0.045 * beatSine;
                _tailAngle = 18.0 * Math.Sin(_motionTimeSeconds * 5.2);
                _slimeSquishY = 0.14 * Math.Sin(_motionTimeSeconds * 5.5);
            }
            else if (!IsNotchExpanded)
            {
                // In collapsed notch: body is completely stationary at rest! (Eliminates continuous layered window software redraws)
                _floatBobY = 0.0;
                _bodySwayAngle = 0.0;
                _bodyScale = 1.0;
                _tailAngle = 0.0;
                _slimeSquishY = 0.0;
            }
            else
            {
                // Gentle idle breathing & floating ONLY when notch is expanded
                double idleHalfPeriod = 2.4;
                double idleSine = (1.0 - Math.Cos(Math.PI * (_motionTimeSeconds % (idleHalfPeriod * 2.0)) / idleHalfPeriod)) / 2.0;
                _floatBobY = -1.6 * idleSine;
                _bodySwayAngle = 1.5 * Math.Sin(_motionTimeSeconds * 1.6);
                _bodyScale = 1.0 + 0.025 * idleSine;
                _tailAngle = 8.0 * Math.Sin(_motionTimeSeconds * 2.0);
                _slimeSquishY = 0.05 * Math.Sin(_motionTimeSeconds * 2.2);
            }

            // 5. Blinking & Sleeping Eye Scale
            if (State == PetState.Sleeping)
            {
                _eyeScaleY = 0.10; // Static closed eyes
            }
            else if (_isBlinking)
            {
                _blinkElapsedMs += dt * 1000.0;
                if (_blinkPhase == 0) // Closing eye
                {
                    _eyeScaleY -= 14.0 * dt;
                    if (_eyeScaleY <= 0.06)
                    {
                        _eyeScaleY = 0.06;
                        _blinkPhase = 1;
                    }
                }
                else // Opening eye
                {
                    _eyeScaleY += 13.0 * dt;
                    if (_eyeScaleY >= 1.0)
                    {
                        _eyeScaleY = 1.0;
                        _isBlinking = false;
                        _nextBlinkDelaySeconds = GetRandomBlinkInterval();
                    }
                }
            }
            else
            {
                _eyeScaleY = 1.0;

                // Check periodic blink trigger
                _nextBlinkDelaySeconds -= dt;
                if (_nextBlinkDelaySeconds <= 0 && State == PetState.Normal)
                {
                    TriggerBlink();
                }
            }

            // 6. Notify UI only if visual state changed
            if (HasVisualStateChanged())
            {
                FrameUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        private double _prevPupilX = -999, _prevPupilY = -999;
        private double _prevEyeScaleY = -999;
        private double _prevFloatBobY = -999, _prevBodySwayAngle = -999;
        private double _prevHeadphoneScale = -999;
        private PetState _prevState = (PetState)(-1);

        private bool HasVisualStateChanged()
        {
            bool changed = false;
            if (State != _prevState)
            {
                _prevState = State;
                changed = true;
            }
            if (Math.Abs(_currentPupilX - _prevPupilX) > 0.04)
            {
                _prevPupilX = _currentPupilX;
                changed = true;
            }
            if (Math.Abs(_currentPupilY - _prevPupilY) > 0.04)
            {
                _prevPupilY = _currentPupilY;
                changed = true;
            }
            if (Math.Abs(_eyeScaleY - _prevEyeScaleY) > 0.02)
            {
                _prevEyeScaleY = _eyeScaleY;
                changed = true;
            }
            if (Math.Abs(_floatBobY - _prevFloatBobY) > 0.04)
            {
                _prevFloatBobY = _floatBobY;
                changed = true;
            }
            if (Math.Abs(_bodySwayAngle - _prevBodySwayAngle) > 0.04)
            {
                _prevBodySwayAngle = _bodySwayAngle;
                changed = true;
            }
            if (Math.Abs(_headphoneScale - _prevHeadphoneScale) > 0.04)
            {
                _prevHeadphoneScale = _headphoneScale;
                changed = true;
            }
            return changed;
        }

        private void TriggerBlink()
        {
            _isBlinking = true;
            _blinkPhase = 0;
            _blinkElapsedMs = 0;
        }

        private double GetRandomBlinkInterval()
        {
            // Random interval between 2.8 and 6.2 seconds
            return 2.8 + _rand.NextDouble() * 3.4;
        }
    }
}
