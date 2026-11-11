using System;
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
        private FileSystemWatcher? _watcher;
        private Timer? _debounceTimer;
        private Timer? _pollTimer;
        private long _lastSeenOrder = 0;
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

                // Initial query: get the current max [Order] so we do not spam past notifications
                _lastSeenOrder = GetMaxOrder();
                Log.Information("[WindowsNotificationService] Started. Initial LastSeenOrder: {Order}", _lastSeenOrder);

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

            // Debounce by 180ms to allow Windows Push Notification Service to finish WAL commit
            lock (_lock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(_ =>
                {
                    Task.Run(() => QueryNewNotifications());
                }, null, 180, Timeout.Infinite);
            }
        }

        private long GetMaxOrder()
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
                    return 0;
                }

                WinSqlite3Native.sqlite3_busy_timeout(db, 1000);

                string sql = "SELECT MAX([Order]) FROM Notification WHERE Type = 'toast'";
                byte[] sqlBytes = Encoding.UTF8.GetBytes(sql + "\0");

                rc = WinSqlite3Native.sqlite3_prepare_v2(db, sqlBytes, sqlBytes.Length, out stmt, IntPtr.Zero);
                if (rc == WinSqlite3Native.SQLITE_OK && stmt != IntPtr.Zero)
                {
                    if (WinSqlite3Native.sqlite3_step(stmt) == WinSqlite3Native.SQLITE_ROW)
                    {
                        return WinSqlite3Native.sqlite3_column_int64(stmt, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[WindowsNotificationService] Failed to get MaxOrder");
            }
            finally
            {
                if (stmt != IntPtr.Zero) WinSqlite3Native.sqlite3_finalize(stmt);
                if (db != IntPtr.Zero) WinSqlite3Native.sqlite3_close(db);
            }

            return 0;
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
                        Log.Debug("[WindowsNotificationService] Could not open db: code {Rc}", rc);
                        return;
                    }

                    WinSqlite3Native.sqlite3_busy_timeout(db, 1000);

                    string sql = @"
                        SELECT n.[Order], n.Id, n.Payload, h.PrimaryId
                        FROM Notification n
                        LEFT JOIN NotificationHandler h ON h.RecordId = n.HandlerId
                        WHERE n.Type = 'toast' AND n.[Order] > ?1 AND n.Payload IS NOT NULL
                        ORDER BY n.[Order] ASC";

                    byte[] sqlBytes = Encoding.UTF8.GetBytes(sql + "\0");

                    rc = WinSqlite3Native.sqlite3_prepare_v2(db, sqlBytes, sqlBytes.Length, out stmt, IntPtr.Zero);
                    if (rc != WinSqlite3Native.SQLITE_OK || stmt == IntPtr.Zero)
                    {
                        Log.Warning("[WindowsNotificationService] prepare_v2 failed: {Msg}", WinSqlite3Native.GetErrorMessage(db));
                        return;
                    }

                    WinSqlite3Native.sqlite3_bind_int64(stmt, 1, _lastSeenOrder);

                    while (WinSqlite3Native.sqlite3_step(stmt) == WinSqlite3Native.SQLITE_ROW)
                    {
                        long order = WinSqlite3Native.sqlite3_column_int64(stmt, 0);
                        long id = WinSqlite3Native.sqlite3_column_int64(stmt, 1);

                        int blobBytes = WinSqlite3Native.sqlite3_column_bytes(stmt, 2);
                        IntPtr blobPtr = WinSqlite3Native.sqlite3_column_blob(stmt, 2);

                        IntPtr appIdPtr = WinSqlite3Native.sqlite3_column_text(stmt, 3);
                        string appId = appIdPtr != IntPtr.Zero ? Marshal.PtrToStringUTF8(appIdPtr) ?? string.Empty : string.Empty;

                        if (order > _lastSeenOrder)
                        {
                            _lastSeenOrder = order;
                        }

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

                                Log.Information("[WindowsNotificationService] Toast detected: [{AppName}] {Title}: {Message}", appName, title, message);
                                NotificationReceived?.Invoke(this, item);
                            }
                        }
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
