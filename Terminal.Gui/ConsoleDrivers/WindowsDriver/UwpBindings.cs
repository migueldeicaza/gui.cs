using System;
using System.Runtime.InteropServices;

namespace Terminal.Gui {

	internal static class UwpBindings {

		[DllImport ("api-ms-win-core-processenvironment-l1-1-0.dll", SetLastError = true)]
		public static extern IntPtr GetStdHandle (int nStdHandle);

		[DllImport ("api-ms-win-core-handle-l1-1-0.dll", SetLastError = true)]
		public static extern bool CloseHandle (IntPtr handle);

		[DllImport ("api-ms-win-core-console-l1-1-0.dll", EntryPoint = "ReadConsoleInputW",
			CharSet = CharSet.Unicode)]
		public static extern bool ReadConsoleInput (
			IntPtr hConsoleInput,
			[Out] IntPtr lpBuffer,
			uint nLength,
			out uint lpNumberOfEventsRead);

		[DllImport ("api-ms-win-core-console-l2-1-0.dll", EntryPoint = "ReadConsoleOutputW",
			SetLastError = true, CharSet = CharSet.Unicode)]
		public static extern bool ReadConsoleOutput (
			IntPtr hConsoleOutput,
			[Out] WindowsConsole.CharInfo [] lpBuffer,
			WindowsConsole.Coord dwBufferSize,
			WindowsConsole.Coord dwBufferCoord,
			ref WindowsConsole.SmallRect lpReadRegion
		);

		[DllImport ("api-ms-win-core-console-l2-1-0.dll", EntryPoint = "WriteConsoleOutputW",
			SetLastError = true, CharSet = CharSet.Unicode)]
		public static extern bool WriteConsoleOutput (
			IntPtr hConsoleOutput,
			WindowsConsole.CharInfo [] lpBuffer,
			WindowsConsole.Coord dwBufferSize,
			WindowsConsole.Coord dwBufferCoord,
			ref WindowsConsole.SmallRect lpWriteRegion
		);

		[DllImport ("api-ms-win-core-console-l2-1-0.dll")]
		public static extern bool SetConsoleCursorPosition (IntPtr hConsoleOutput,
			WindowsConsole.Coord dwCursorPosition);

		[DllImport ("api-ms-win-core-console-l1-1-0.dll")]
		public static extern bool GetConsoleMode (IntPtr hConsoleHandle, out uint lpMode);

		[DllImport ("api-ms-win-core-console-l1-1-0.dll")]
		public static extern bool SetConsoleMode (IntPtr hConsoleHandle, uint dwMode);

		[DllImport ("api-ms-win-core-console-l2-1-0.dll", SetLastError = true)]
		public static extern IntPtr CreateConsoleScreenBuffer (
			WindowsConsole.DesiredAccess dwDesiredAccess,
			WindowsConsole.ShareMode dwShareMode,
			IntPtr secutiryAttributes,
			UInt32 flags,
			IntPtr screenBufferData
		);

		[DllImport ("api-ms-win-core-console-l2-1-0.dll", SetLastError = true)]
		public static extern bool SetConsoleActiveScreenBuffer (IntPtr Handle);

		[DllImport ("api-ms-win-core-console-l1-1-0.dll", SetLastError = true)]
		public static extern bool GetNumberOfConsoleInputEvents (IntPtr handle, out uint lpcNumberOfEvents);

	}

}
