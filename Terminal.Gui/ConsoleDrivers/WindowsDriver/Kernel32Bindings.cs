using System;
using System.Runtime.InteropServices;

namespace Terminal.Gui {

	internal static class Kernel32Bindings {

		[DllImport ("kernel32.dll", SetLastError = true)]
		public static extern IntPtr GetStdHandle (int nStdHandle);

		[DllImport ("kernel32.dll", SetLastError = true)]
		public static extern bool CloseHandle (IntPtr handle);

		[DllImport ("kernel32.dll", EntryPoint = "ReadConsoleInputW", CharSet = CharSet.Unicode)]
		public static extern bool ReadConsoleInput (
			IntPtr hConsoleInput,
			[Out] IntPtr lpBuffer,
			uint nLength,
			out uint lpNumberOfEventsRead);

		[DllImport ("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		public static extern bool ReadConsoleOutput (
			IntPtr hConsoleOutput,
			[Out] WindowsConsole.CharInfo [] lpBuffer,
			WindowsConsole.Coord dwBufferSize,
			WindowsConsole.Coord dwBufferCoord,
			ref WindowsConsole.SmallRect lpReadRegion
		);

		[DllImport ("kernel32.dll", EntryPoint = "WriteConsoleOutput", SetLastError = true,
			CharSet = CharSet.Unicode)]
		public static extern bool WriteConsoleOutput (
			IntPtr hConsoleOutput,
			WindowsConsole.CharInfo [] lpBuffer,
			WindowsConsole.Coord dwBufferSize,
			WindowsConsole.Coord dwBufferCoord,
			ref WindowsConsole.SmallRect lpWriteRegion
		);

		[DllImport ("kernel32.dll")]
		public static extern bool SetConsoleCursorPosition (IntPtr hConsoleOutput,
			WindowsConsole.Coord dwCursorPosition);

		[DllImport ("kernel32.dll")]
		public static extern bool GetConsoleMode (IntPtr hConsoleHandle, out uint lpMode);

		[DllImport ("kernel32.dll")]
		public static extern bool SetConsoleMode (IntPtr hConsoleHandle, uint dwMode);

		[DllImport ("kernel32.dll", SetLastError = true)]
		public static extern IntPtr CreateConsoleScreenBuffer (
			WindowsConsole.DesiredAccess dwDesiredAccess,
			WindowsConsole.ShareMode dwShareMode,
			IntPtr securityAttributes,
			uint flags,
			IntPtr screenBufferData
		);

		[DllImport ("kernel32.dll", SetLastError = true)]
		public static extern bool SetConsoleActiveScreenBuffer (IntPtr handle);

		[DllImport ("kernel32.dll", SetLastError = true)]
		public static extern bool GetNumberOfConsoleInputEvents (IntPtr handle, out uint lpcNumberOfEvents);

	}

}
