using System;
using System.Diagnostics;
using System.Runtime;
using System.Windows.Threading;
using NotchWin.Interop;
using Serilog;

namespace NotchWin.Services
{
    /// <summary>
    /// Reclaims one-time startup and known heavy-operation garbage.
    /// No periodic or idle GC: repeated forced collections only churn the GC
    /// and cost more CPU than they save in memory.
    /// </summary>
    public sealed class MemoryManagementService
    {
        private static readonly Lazy<MemoryManagementService> _instance = new(() => new MemoryManagementService());
        public static MemoryManagementService Instance => _instance.Value;

        private static readonly TimeSpan MinTrimInterval = TimeSpan.FromSeconds(90);

        private readonly DispatcherTimer _startupTimer;
        private bool _isInitialized;
        private DateTime _lastTrimUtc = DateTime.MinValue;

        private MemoryManagementService()
        {
            // Initial post-startup flush to reclaim one-time XAML & font cache compilation memory
            _startupTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(4.5)
            };
            _startupTimer.Tick += OnStartupTimerTick;
        }

        public void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            _startupTimer.Start();
            Log.Debug("MemoryManagementService initialized");
        }

        private void OnStartupTimerTick(object? sender, EventArgs e)
        {
            _startupTimer.Stop();
            TrimMemory("Post-Startup Initialization Flush", force: true);
        }

        /// <summary>
        /// Non-blocking, gentle compaction and working set trimming.
        /// Throttled so callers cannot trigger repeated GC churn.
        /// </summary>
        public void TrimMemory(string reason, bool force = false)
        {
            var now = DateTime.UtcNow;
            if (!force && now - _lastTrimUtc < MinTrimInterval) return;
            _lastTrimUtc = now;

            try
            {
                // Compact the Large Object Heap once to eliminate fragmentation from bitmaps/audio buffers
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

                // Non-blocking, optimized generation 2 collection
                GC.Collect(2, GCCollectionMode.Optimized, blocking: false, compacting: true);

                // Reclaim uncommitted virtual memory pages back to Windows
                var handle = Process.GetCurrentProcess().Handle;
                if (handle != IntPtr.Zero)
                {
                    NativeMethods.EmptyWorkingSet(handle);
                }

                Log.Debug("MemoryManagementService: Trimmed working set ({Reason})", reason);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "MemoryManagementService: Error trimming memory");
            }
        }
    }
}
