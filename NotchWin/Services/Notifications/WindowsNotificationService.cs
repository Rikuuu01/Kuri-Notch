using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace NotchWin.Services.Notifications
{
    public class WindowsNotificationService : IWindowsNotificationService
    {
        private readonly string _dbPath;
        private readonly object _lock = new();
        private readonly Dictionary<long, long> _knownToasts = new();
        private FileSystemWatcher? _watcher;
        private Timer? _debounceTimer;
        private Timer? _pollTimer;
        private bool _isDisposed;
        private bool _isRunning;

        public event EventHandler<WindowsNotificationModel>? NotificationReceived;

        public bool IsRunning => _isRunning;

        public WindowsNotificationService()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _dbPath = Path.Combine(localAppData, "Microsoft", "Windows", "Notifications", "wpndatabase.db");
        }

        public void Start()
        {
            lock (_lock)
            {
                if (_isRunning || _isDisposed) return;

                if (!File.Exists(_dbPath))
                {
                    Log.Warning("[WindowsNotificationService] Database not found at {Path}", _dbPath);
                    return;
                }

                // Initial baseline: populate known toast IDs and arrival times so past notifications don't replay
                LoadExistingNotificationIds();

                // Setup FileSystemWatcher on Notifications directory
                string? dir = Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    try
                    {
                        _watcher = new FileSystemWatcher(dir)
                        {
                            Filter = "wpndatabase.db*",
                            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                            EnableRaisingEvents = true
                        };

                        _watcher.Changed += OnDatabaseFileChanged;
                        _watcher.Created += OnDatabaseFileChanged;
                        _watcher.Renamed += (s, e) => OnDatabaseFileChanged(s, e);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[WindowsNotificationService] Failed to start FileSystemWatcher");
                    }
                }

                _pollTimer = new Timer(_ =>
                {
                    QueryNewNotifications();
                }, null, 400, 400);

                _isRunning = true;
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning) return;

                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                    _watcher = null;
                }

                _debounceTimer?.Dispose();
                _debounceTimer = null;

                _pollTimer?.Dispose();
                _pollTimer = null;

                _isRunning = false;
                Log.Information("[WindowsNotificationService] Stopped.");
            }
        }

        private void OnDatabaseFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_isDisposed || !_isRunning) return;

            lock (_lock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(_ =>
                {
                    Task.Run(() => QueryNewNotifications());
                }, null, 150, Timeout.Infinite);
            }
        }

        private void LoadExistingNotificationIds()
        {
            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try
            {
                string uri = $"file:{_dbPath.Replace('\\', '/')}?mode=ro";
                byte[] uriBytes = Encoding.UTF8.GetBytes(uri + "\0");

                int rc = WinSqlite3Native.sqlite3_open_v2(
                    uriBytes,
                    out db,
                    WinSqlite3Native.SQLITE_OPEN_READONLY | WinSqlite3Native.SQLITE_OPEN_URI,
                    IntPtr.Zero);

                if (rc != WinSqlite3Native.SQLITE_OK || db == IntPtr.Zero)
                {
                    Log.Warning("[WindowsNotificationService] sqlite3_open_v2 failed with code {Rc}: {Msg}", rc, WinSqlite3Native.GetErrorMessage(db));
                    return;
                }

                WinSqlite3Native.sqlite3_busy_timeout(db, 1000);

                string sql = "SELECT Id, ArrivalTime FROM Notification WHERE Type = 'toast'";
                byte[] sqlBytes = Encoding.UTF8.GetBytes(sql + "\0");

                rc = WinSqlite3Native.sqlite3_prepare_v2(db, sqlBytes, sqlBytes.Length, out stmt, IntPtr.Zero);
                if (rc == WinSqlite3Native.SQLITE_OK && stmt != IntPtr.Zero)
                {
                    _knownToasts.Clear();
                    while (WinSqlite3Native.sqlite3_step(stmt) == WinSqlite3Native.SQLITE_ROW)
                    {
                        long id = WinSqlite3Native.sqlite3_column_int64(stmt, 0);
                        long arrival = WinSqlite3Native.sqlite3_column_int64(stmt, 1);
                        _knownToasts[id] = arrival;
                    }
                    Log.Information("[WindowsNotificationService] Started. Loaded {Count} baseline toasts.", _knownToasts.Count);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[WindowsNotificationService] Failed to load baseline notification IDs");
            }
            finally
            {
                if (stmt != IntPtr.Zero) WinSqlite3Native.sqlite3_finalize(stmt);
                if (db != IntPtr.Zero) WinSqlite3Native.sqlite3_close(db);
            }
        }

        private void QueryNewNotifications()
        {
            if (_isDisposed || !_isRunning) return;

            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;

            lock (_lock)
            {
                try
                {
                    string uri = $"file:{_dbPath.Replace('\\', '/')}?mode=ro";
                    byte[] uriBytes = Encoding.UTF8.GetBytes(uri + "\0");

                    int rc = WinSqlite3Native.sqlite3_open_v2(
                        uriBytes,
                        out db,
                        WinSqlite3Native.SQLITE_OPEN_READONLY | WinSqlite3Native.SQLITE_OPEN_URI,
                        IntPtr.Zero);

                    if (rc != WinSqlite3Native.SQLITE_OK || db == IntPtr.Zero)
                    {
                        return;
                    }

                    WinSqlite3Native.sqlite3_busy_timeout(db, 1000);

                    string sql = @"
                        SELECT n.[Order], n.Id, n.ArrivalTime, n.Payload, h.PrimaryId
                        FROM Notification n
                        LEFT JOIN NotificationHandler h ON h.RecordId = n.HandlerId
                        WHERE n.Type = 'toast' AND n.Payload IS NOT NULL
                        ORDER BY n.ArrivalTime ASC";

                    byte[] sqlBytes = Encoding.UTF8.GetBytes(sql + "\0");

                    rc = WinSqlite3Native.sqlite3_prepare_v2(db, sqlBytes, sqlBytes.Length, out stmt, IntPtr.Zero);
                    if (rc != WinSqlite3Native.SQLITE_OK || stmt == IntPtr.Zero)
                    {
                        return;
                    }

                    var newItems = new List<WindowsNotificationModel>();
                    var currentIds = new HashSet<long>();

                    while (WinSqlite3Native.sqlite3_step(stmt) == WinSqlite3Native.SQLITE_ROW)
                    {
                        long order = WinSqlite3Native.sqlite3_column_int64(stmt, 0);
                        long id = WinSqlite3Native.sqlite3_column_int64(stmt, 1);
                        long arrivalTime = WinSqlite3Native.sqlite3_column_int64(stmt, 2);

                        currentIds.Add(id);

                        int blobBytes = WinSqlite3Native.sqlite3_column_bytes(stmt, 3);
                        IntPtr blobPtr = WinSqlite3Native.sqlite3_column_blob(stmt, 3);

                        IntPtr appIdPtr = WinSqlite3Native.sqlite3_column_text(stmt, 4);
                        string appId = appIdPtr != IntPtr.Zero ? Marshal.PtrToStringUTF8(appIdPtr) ?? string.Empty : string.Empty;

                        bool isNew = !_knownToasts.TryGetValue(id, out long prevArrival);
                        bool isUpdated = !isNew && arrivalTime > prevArrival;

                        if (!isNew && !isUpdated)
                        {
                            continue;
                        }

                        _knownToasts[id] = arrivalTime;

                        // Ignore notifications originated from Kuri itself if any
                        if (appId.Contains("NotchWin", StringComparison.OrdinalIgnoreCase) ||
                            appId.Contains("Kuri", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (blobBytes > 0 && blobPtr != IntPtr.Zero)
                        {
                            byte[] payloadBytes = new byte[blobBytes];
                            Marshal.Copy(blobPtr, payloadBytes, 0, blobBytes);

                            var (title, message, imageSrc) = NotificationPayloadParser.Parse(payloadBytes);
                            var (appName, icon) = NotificationAppResolver.Resolve(appId);

                            if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(message))
                            {
                                var item = new WindowsNotificationModel
                                {
                                    Order = order,
                                    Id = id,
                                    AppId = appId,
                                    AppName = appName,
                                    Icon = icon,
                                    Title = title,
                                    Message = message,
                                    ImagePath = imageSrc,
                                    Timestamp = DateTime.Now
                                };

                                Log.Information("[WindowsNotificationService] Toast detected (isNew={IsNew}, isUpdated={IsUpdated}): [{AppName}] {Title}: {Message}", isNew, isUpdated, appName, title, message);
                                newItems.Add(item);
                            }
                        }
                    }

                    // Prune known toasts that have been deleted from Windows
                    if (_knownToasts.Count > 100)
                    {
                        var toRemove = new List<long>();
                        foreach (var kvp in _knownToasts)
                        {
                            if (!currentIds.Contains(kvp.Key))
                            {
                                toRemove.Add(kvp.Key);
                            }
                        }
                        foreach (var id in toRemove)
                        {
                            _knownToasts.Remove(id);
                        }
                    }

                    // Dispatch all new/updated notifications
                    foreach (var item in newItems)
                    {
                        NotificationReceived?.Invoke(this, item);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[WindowsNotificationService] Exception while querying new notifications");
                }
                finally
                {
                    if (stmt != IntPtr.Zero) WinSqlite3Native.sqlite3_finalize(stmt);
                    if (db != IntPtr.Zero) WinSqlite3Native.sqlite3_close(db);
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            Stop();
        }
    }
}
