using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using NotchWin.Interop;
using Serilog;

namespace NotchWin.Services
{
    /// <summary>
    /// Reads the total size of active Chromium-family downloads from the
    /// browser History database. Only the downloads table is queried, read-only,
    /// and only size columns are used; no URLs or history entries are read.
    /// </summary>
    internal static class ChromiumDownloadProgressReader
    {
        private const string Query =
            "SELECT current_path, target_path, total_bytes FROM downloads WHERE state = 0 LIMIT 100;";

        public static Dictionary<string, long> ReadTotals(IEnumerable<string> historyDatabases)
        {
            var totals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            foreach (string database in historyDatabases)
            {
                try
                {
                    ReadDatabase(database, totals);
                }
                catch (DllNotFoundException)
                {
                    return totals;
                }
                catch (EntryPointNotFoundException)
                {
                    return totals;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "DownloadMonitor: could not read download sizes from {Db}", database);
                }
            }

            return totals;
        }

        private static void ReadDatabase(string path, Dictionary<string, long> totals)
        {
            if (!File.Exists(path)) return;

            int rc = SqliteNative.OpenV2(
                path,
                out IntPtr db,
                SqliteNative.OpenReadOnly | SqliteNative.OpenNoMutex,
                IntPtr.Zero);

            if (rc != SqliteNative.Ok || db == IntPtr.Zero)
            {
                if (db != IntPtr.Zero) SqliteNative.CloseV2(db);
                return;
            }

            IntPtr statement = IntPtr.Zero;
            try
            {
                // The browser may be writing; never block the UI thread.
                SqliteNative.BusyTimeout(db, 40);

                rc = SqliteNative.PrepareV2(db, Query, -1, out statement, IntPtr.Zero);
                if (rc != SqliteNative.Ok || statement == IntPtr.Zero) return;

                while (SqliteNative.Step(statement) == SqliteNative.Row)
                {
                    long total = SqliteNative.ColumnInt64(statement, 2);
                    if (total <= 0) continue;

                    AddKey(totals, ReadText(statement, 0), total);
                    AddKey(totals, ReadText(statement, 1), total);
                }
            }
            finally
            {
                if (statement != IntPtr.Zero) SqliteNative.Finalize(statement);
                SqliteNative.CloseV2(db);
            }
        }

        private static string ReadText(IntPtr statement, int index)
        {
            IntPtr text = SqliteNative.ColumnText(statement, index);
            return text == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(text) ?? string.Empty;
        }

        private static void AddKey(Dictionary<string, long> totals, string path, long total)
        {
            string key = NormalizePath(path);
            if (key.Length == 0) return;
            if (!totals.TryGetValue(key, out long existing) || total > existing)
            {
                totals[key] = total;
            }
        }

        public static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            string normalized = path.Trim().Trim('"');
            if (normalized.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(4);
            }

            return normalized.TrimEnd('\\', '/');
        }
    }
}
