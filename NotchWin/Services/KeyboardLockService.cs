using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Serilog;

namespace NotchWin.Services
{
    public class KeyboardLockService : IKeyboardLockService
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_CAPITAL = 0x14;
        private const int VK_NUMLOCK = 0x90;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private readonly LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        private bool _isCapsLockOn;
        private bool _isNumLockOn;
        private bool _isCapsKeyDown;
        private bool _isNumKeyDown;
        private bool _isStarted;

        public bool IsCapsLockOn => _isCapsLockOn;
        public bool IsNumLockOn => _isNumLockOn;

        public event EventHandler<bool>? CapsLockChanged;
        public event EventHandler<bool>? NumLockChanged;

        public KeyboardLockService()
        {
            _proc = HookCallback;
            _isCapsLockOn = ReadCapsLockState();
            _isNumLockOn = ReadNumLockState();
        }

        public void Start()
        {
            if (_isStarted) return;
            try
            {
                IntPtr moduleHandle = IntPtr.Zero;
                try
                {
                    using var curProcess = Process.GetCurrentProcess();
                    using var curModule = curProcess.MainModule;
                    if (curModule != null)
                    {
                        moduleHandle = GetModuleHandle(curModule.ModuleName);
                    }
                }
                catch {}

                if (moduleHandle == IntPtr.Zero)
                {
                    moduleHandle = GetModuleHandle(null);
                }

                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, moduleHandle, 0);
                _isStarted = _hookId != IntPtr.Zero;
                Log.Information("KeyboardLockService: Hook installed, hookId={HookId}, CapsLock={Caps}, NumLock={Num}", _hookId, _isCapsLockOn, _isNumLockOn);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "KeyboardLockService: Failed to install low level keyboard hook");
            }
        }

        public void Stop()
        {
            if (!_isStarted) return;
            try
            {
                if (_hookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookId);
                    _hookId = IntPtr.Zero;
                }
                _isStarted = false;
                Log.Information("KeyboardLockService: Hook uninstalled");
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "KeyboardLockService: Error uninstalling hook");
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                int vkCode = Marshal.ReadInt32(lParam);

                if (vkCode == VK_CAPITAL)
                {
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        if (!_isCapsKeyDown)
                        {
                            _isCapsKeyDown = true;
                            bool newState = !_isCapsLockOn;
                            _isCapsLockOn = newState;
                            Log.Information("KeyboardLockService: Caps Lock KeyDown -> {State}", newState);
                            SafeDispatch(() => CapsLockChanged?.Invoke(this, newState));
                        }
                    }
                    else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                    {
                        _isCapsKeyDown = false;
                        bool actualState = ReadCapsLockState();
                        if (_isCapsLockOn != actualState)
                        {
                            _isCapsLockOn = actualState;
                            Log.Information("KeyboardLockService: Caps Lock KeyUp sync -> {State}", actualState);
                            SafeDispatch(() => CapsLockChanged?.Invoke(this, actualState));
                        }
                    }
                }
                else if (vkCode == VK_NUMLOCK)
                {
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        if (!_isNumKeyDown)
                        {
                            _isNumKeyDown = true;
                            bool newState = !_isNumLockOn;
                            _isNumLockOn = newState;
                            Log.Information("KeyboardLockService: Num Lock KeyDown -> {State}", newState);
                            SafeDispatch(() => NumLockChanged?.Invoke(this, newState));
                        }
                    }
                    else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                    {
                        _isNumKeyDown = false;
                        bool actualState = ReadNumLockState();
                        if (_isNumLockOn != actualState)
                        {
                            _isNumLockOn = actualState;
                            Log.Information("KeyboardLockService: Num Lock KeyUp sync -> {State}", actualState);
                            SafeDispatch(() => NumLockChanged?.Invoke(this, actualState));
                        }
                    }
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public static bool ReadCapsLockState()
        {
            return (GetKeyState(VK_CAPITAL) & 1) != 0;
        }

        public static bool ReadNumLockState()
        {
            return (GetKeyState(VK_NUMLOCK) & 1) != 0;
        }

        private static void SafeDispatch(Action action)
        {
            var app = Application.Current;
            if (app == null || app.Dispatcher.HasShutdownStarted) return;
            app.Dispatcher.BeginInvoke(action);
        }

        public void Dispose()
        {
            Stop();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern short GetKeyState(int keyCode);
    }
}
