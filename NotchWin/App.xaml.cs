using System;
using System.IO;
using System.Windows;
using NotchWin.Views;
using Serilog;

namespace NotchWin
{
    public partial class App : Application
    {
        public static string LogDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NotchWin",
            "logs"
        );

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Keep application alive as background overlay until explicitly closed
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            Directory.CreateDirectory(LogDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(LogDirectory, "notchwin-.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            Log.Information("=== NotchWin is starting up ===");

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
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize or show NotchWindow");
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("NotchWin is exiting with code {ExitCode}", e.ApplicationExitCode);
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
