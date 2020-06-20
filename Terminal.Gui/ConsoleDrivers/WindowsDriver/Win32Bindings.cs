using System;
using System.Runtime.InteropServices;

namespace Terminal.Gui {

	internal static class Win32Bindings {

		public static GetStdHandleFunc GetStdHandle;
		public static CloseHandleFunc CloseHandle;
		public static ReadConsoleInputFunc ReadConsoleInput;
		public static ReadConsoleOutputFunc ReadConsoleOutput;
		public static WriteConsoleOutputFunc WriteConsoleOutput;
		public static SetConsoleCursorPositionFunc SetConsoleCursorPosition;
		public static GetConsoleModeFunc GetConsoleMode;
		public static SetConsoleModeFunc SetConsoleMode;
		public static CreateConsoleScreenBufferFunc CreateConsoleScreenBuffer;
		public static SetConsoleActiveScreenBufferFunc SetConsoleActiveScreenBuffer;
		public static GetNumberOfConsoleInputEventsFunc GetNumberOfConsoleInputEvents;

		public delegate IntPtr GetStdHandleFunc (int nStdHandle);

		public delegate bool CloseHandleFunc (IntPtr handle);

		public delegate bool ReadConsoleInputFunc (
			IntPtr hConsoleInput,
			[Out] IntPtr lpBuffer,
			uint nLength,
			out uint lpNumberOfEventsRead);

		public delegate bool ReadConsoleOutputFunc (
			IntPtr hConsoleOutput,
			[Out] WindowsConsole.CharInfo [] lpBuffer,
			WindowsConsole.Coord dwBufferSize,
			WindowsConsole.Coord dwBufferCoord,
			ref WindowsConsole.SmallRect lpReadRegion
		);

		public delegate bool WriteConsoleOutputFunc (
			IntPtr hConsoleOutput,
			WindowsConsole.CharInfo [] lpBuffer,
			WindowsConsole.Coord dwBufferSize,
			WindowsConsole.Coord dwBufferCoord,
			ref WindowsConsole.SmallRect lpWriteRegion
		);

		public delegate bool SetConsoleCursorPositionFunc (IntPtr hConsoleOutput,
			WindowsConsole.Coord dwCursorPosition);

		public delegate bool GetConsoleModeFunc (IntPtr hConsoleHandle, out uint lpMode);

		public delegate bool SetConsoleModeFunc (IntPtr hConsoleHandle, uint dwMode);

		public delegate IntPtr CreateConsoleScreenBufferFunc (
			WindowsConsole.DesiredAccess dwDesiredAccess,
			WindowsConsole.ShareMode dwShareMode,
			IntPtr securityAttributes,
			uint flags,
			IntPtr screenBufferData
		);

		public delegate bool SetConsoleActiveScreenBufferFunc (IntPtr handle);

		public delegate bool GetNumberOfConsoleInputEventsFunc (IntPtr handle, out uint lpcNumberOfEvents);

		static Win32Bindings ()
		{
			var isUwp = Environment.OSVersion.Version.Major >= 10;
			if (isUwp) {
				GetStdHandle = UwpBindings.GetStdHandle;
				CloseHandle = UwpBindings.CloseHandle;
				ReadConsoleInput = UwpBindings.ReadConsoleInput;
				ReadConsoleOutput = UwpBindings.ReadConsoleOutput;
				WriteConsoleOutput = UwpBindings.WriteConsoleOutput;
				SetConsoleCursorPosition = UwpBindings.SetConsoleCursorPosition;
				GetConsoleMode = UwpBindings.GetConsoleMode;
				SetConsoleMode = UwpBindings.SetConsoleMode;
				CreateConsoleScreenBuffer = UwpBindings.CreateConsoleScreenBuffer;
				SetConsoleActiveScreenBuffer = UwpBindings.SetConsoleActiveScreenBuffer;
				GetNumberOfConsoleInputEvents = UwpBindings.GetNumberOfConsoleInputEvents;
			} else {
				GetStdHandle = Kernel32Bindings.GetStdHandle;
				CloseHandle = Kernel32Bindings.CloseHandle;
				ReadConsoleInput = Kernel32Bindings.ReadConsoleInput;
				ReadConsoleOutput = Kernel32Bindings.ReadConsoleOutput;
				WriteConsoleOutput = Kernel32Bindings.WriteConsoleOutput;
				SetConsoleCursorPosition = Kernel32Bindings.SetConsoleCursorPosition;
				GetConsoleMode = Kernel32Bindings.GetConsoleMode;
				SetConsoleMode = Kernel32Bindings.SetConsoleMode;
				CreateConsoleScreenBuffer = Kernel32Bindings.CreateConsoleScreenBuffer;
				SetConsoleActiveScreenBuffer = Kernel32Bindings.SetConsoleActiveScreenBuffer;
				GetNumberOfConsoleInputEvents = Kernel32Bindings.GetNumberOfConsoleInputEvents;
			}
		}

	}

}
