using System;
using System.Runtime.InteropServices;

namespace NotchWin.Services.Notifications
{
    internal static class WinSqlite3Native
    {
        private const string LibraryName = "winsqlite3.dll";

        public const int SQLITE_OK = 0;
        public const int SQLITE_ROW = 100;
        public const int SQLITE_DONE = 101;
        public const int SQLITE_BUSY = 5;

        public const int SQLITE_OPEN_READONLY = 0x00000001;
        public const int SQLITE_OPEN_URI = 0x00000040;

        [DllImport(LibraryName, EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int sqlite3_open_v2(byte[] filenameUtf8, out IntPtr ppDb, int flags, IntPtr zVfs);

        [DllImport(LibraryName, EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int sqlite3_close(IntPtr db);

        [DllImport(LibraryName, EntryPoint = "sqlite3_busy_timeout", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int sqlite3_busy_timeout(IntPtr db, int ms);

        [DllImport(LibraryName, EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int sqlite3_prepare_v2(IntPtr db, byte[] zSqlUtf8, int nByte, out IntPtr ppStmt, IntPtr pzTail);

        [DllImport(LibraryName, EntryPoint = "sqlite3_bind_int64", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int sqlite3_bind_int64(IntPtr stmt, int index, long value);

        [DllImport(LibraryName, EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int sqlite3_step(IntPtr stmt);

        [DllImport(LibraryName, EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int sqlite3_finalize(IntPtr stmt);

        [DllImport(LibraryName, EntryPoint = "sqlite3_column_int64", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern long sqlite3_column_int64(IntPtr stmt, int iCol);

        [DllImport(LibraryName, EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int sqlite3_column_bytes(IntPtr stmt, int iCol);

        [DllImport(LibraryName, EntryPoint = "sqlite3_column_blob", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern IntPtr sqlite3_column_blob(IntPtr stmt, int iCol);

        [DllImport(LibraryName, EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern IntPtr sqlite3_column_text(IntPtr stmt, int iCol);

        [DllImport(LibraryName, EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern IntPtr sqlite3_errmsg(IntPtr db);

        public static string? GetErrorMessage(IntPtr db)
        {
            if (db == IntPtr.Zero) return null;
            var ptr = sqlite3_errmsg(db);
            return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
        }
    }
}
