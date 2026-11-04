using System;
using System.IO;
using System.Threading;
using System.Windows;
using NotchWin.Views;
using Serilog;

namespace NotchWin
{
    public partial class App : Application
    {
        private static Mutex? _singleInstanceMutex;
        private const string MutexName = "Local\\NotchWin_SingleInstance_Mutex_56ab85f1";
        private const string WakeEventName = "Local\\NotchWin_WakeEvent_56ab85f1";
        private static EventWaitHandle? _wakeEvent;
        private static RegisteredWaitHandle? _registeredWaitHandle;

        public static string LogDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NotchWin",
            "logs"
        );

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Directory.CreateDirectory(LogDirectory);

            bool isAutostartHelper = TryGetAutostartTaskCommand(e.Args, out string? autostartCommand);

            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Debug()
#else
                .MinimumLevel.Information()
#endif
                .WriteTo.File(
                    Path.Combine(LogDirectory, isAutostartHelper ? "autostart-task-.log" : "notchwin-.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    buffered: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(2),
                    fileSizeLimitBytes: 20 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: 7
                )
                .CreateLogger();

            Log.Information("=== NotchWin is starting up ===");

            if (isAutostartHelper)
            {
                int exitCode = HandleAutostartTaskCommand(autostartCommand!);
                Log.Information("Autostart task helper finished with exit code {ExitCode}", exitCode);
                Log.CloseAndFlush();
                Environment.Exit(exitCode);
                return;
            }

            // Enforce single instance application lifecycle
            try
            {
                _singleInstanceMutex = new Mutex(true, MutexName, out bool isNewInstance);
                if (!isNewInstance)
                {
                    Log.Information("Another instance of NotchWin is already active. Signalling active instance and exiting quietly.");

                    // Signal active instance to wake up and expand notch
                    try
                    {
                        if (EventWaitHandle.TryOpenExisting(WakeEventName, out var existingWakeEvent))
                        {
                            existingWakeEvent.Set();
                            existingWakeEvent.Dispose();
                        }
                    }
                    catch (Exception signalEx)
                    {
                        Log.Debug(signalEx, "Could not signal existing NotchWin instance");
                    }

                    // Silent exit: no annoying popup modal during startup or duplicate launch
                    Shutdown();
                    return;
                }

                // Register listener for wake signals from secondary instances
                _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeEventName);
                _registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                    _wakeEvent,
                    OnWakeSignalReceived,
                    null,
                    -1,
                    false
                );
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not acquire single-instance mutex; continuing anyway");
            }

            // Keep application alive as background overlay until explicitly closed
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Log.Fatal(args.ExceptionObject as Exception, "AppDomain Unhandled Exception: {ExceptionObject}", args.ExceptionObject);
                Log.CloseAndFlush();
            };

            DispatcherUnhandledException += (s, args) =>
            {
                Log.Error(args.Exception, "Dispatcher Unhandled Exception: {Message}", args.Exception.Message);
                args.Handled = true;
            };

            try
            {
                var settingsService = new Services.SettingsService();
                Services.ThemeManager.ApplyAccentColor(settingsService.CurrentSettings.AccentColor);
                Services.FontScaleService.Initialize(settingsService.CurrentSettings.FontScale);

                Log.Information("Initializing NotchWindow...");
                var window = new NotchWindow();
                MainWindow = window;

                window.Closed += (s, args) =>
                {
                    Log.Information("NotchWindow Closed event triggered. Initiating Shutdown...");
                    Shutdown();
                };

                window.Show();
                Log.Information("NotchWindow.Show() executed successfully.");

                if (window.DataContext is ViewModels.NotchViewModel notchViewModel)
                {
                    notchViewModel.PlayStartupSoundIfEnabled();
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize or show NotchWindow");
                Shutdown();
            }
        }

        private static bool TryGetAutostartTaskCommand(string[] args, out string? command)
        {
            const string prefix = "--autostart-task=";
            command = null;

            foreach (string arg in args)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    command = arg.Substring(prefix.Length).Trim().ToLowerInvariant();
                    return true;
                }
            }

            return false;
        }

        private static int HandleAutostartTaskCommand(string command)
        {
            try
            {
                switch (command)
                {
                    case "enable":
                        return Services.AutostartService.RegisterLogonTask() ? 0 : 1;
                    case "disable":
                        return Services.AutostartService.UnregisterLogonTask() ? 0 : 1;
                    case "status":
                        return Services.AutostartService.IsLogonTaskRegistered() ? 0 : 1;
                    default:
                        Log.Error("Unknown autostart task command: {Command}", command);
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Autostart task helper failed for command {Command}", command);
                return 1;
            }
        }

        private void OnWakeSignalReceived(object? state, bool timedOut)
        {
            if (timedOut) return;

            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    Log.Information("Wake signal received from secondary instance. Expanding Notch Window.");
                    if (MainWindow is NotchWindow notchWindow)
                    {
                        notchWindow.Show();
                        notchWindow.Activate();
                        if (notchWindow.DataContext is ViewModels.NotchViewModel vm)
                        {
                            vm.Expand();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to expand notch upon receiving wake signal");
                }
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("NotchWin is exiting with code {ExitCode}", e.ApplicationExitCode);
            Log.CloseAndFlush();

            _registeredWaitHandle?.Unregister(null);
            _registeredWaitHandle = null;

            if (_wakeEvent != null)
            {
                _wakeEvent.Dispose();
                _wakeEvent = null;
            }

            if (_singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch { }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            base.OnExit(e);
        }
    }
}
