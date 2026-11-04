using System;
using System.Runtime.InteropServices;

namespace NotchWin.Interop
{
    /// <summary>
    /// Minimal read-only bindings to the SQLite engine shipped with Windows
    /// (winsqlite3.dll). Used only to read download sizes; no writes.
    /// </summary>
    internal static class SqliteNative
    {
        private const string Library = "winsqlite3.dll";

        public const int Ok = 0;
        public const int Row = 100;
        public const int Done = 101;

        public const int OpenReadOnly = 0x00000001;
        public const int OpenNoMutex = 0x00008000;

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_open_v2")]
        public static extern int OpenV2(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string filename,
            out IntPtr db,
            int flags,
            IntPtr vfs);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_close_v2")]
        public static extern int CloseV2(IntPtr db);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_busy_timeout")]
        public static extern int BusyTimeout(IntPtr db, int milliseconds);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_prepare_v2")]
        public static extern int PrepareV2(
            IntPtr db,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string sql,
            int byteCount,
            out IntPtr statement,
            IntPtr tail);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_step")]
        public static extern int Step(IntPtr statement);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_int64")]
        public static extern long ColumnInt64(IntPtr statement, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_column_text")]
        public static extern IntPtr ColumnText(IntPtr statement, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl, EntryPoint = "sqlite3_finalize")]
        public static extern int Finalize(IntPtr statement);
    }
}
