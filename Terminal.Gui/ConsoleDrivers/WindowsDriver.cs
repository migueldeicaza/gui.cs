//
// WindowsDriver.cs: Windows specific driver
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//   Nick Van Dyck (vandyck.nick@outlook.com)
//
// Copyright (c) 2018
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//
using NStack;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Terminal.Gui {

	internal class WindowsConsole {
		public const int STD_OUTPUT_HANDLE = -11;
		public const int STD_INPUT_HANDLE = -10;
		public const int STD_ERROR_HANDLE = -12;

		internal IntPtr InputHandle, OutputHandle;
		IntPtr ScreenBuffer;
		uint originalConsoleMode;
		CursorVisibility? initialCursorVisibility = null;
		CursorVisibility? currentCursorVisibility = null;
		CursorVisibility? pendingCursorVisibility = null;

		public WindowsConsole ()
		{
			InputHandle = GetStdHandle (STD_INPUT_HANDLE);
			OutputHandle = GetStdHandle (STD_OUTPUT_HANDLE);
			originalConsoleMode = ConsoleMode;
			var newConsoleMode = originalConsoleMode;
			newConsoleMode |= (uint)(ConsoleModes.EnableMouseInput | ConsoleModes.EnableExtendedFlags);
			newConsoleMode &= ~(uint)ConsoleModes.EnableQuickEditMode;
			newConsoleMode &= ~(uint)ConsoleModes.EnableProcessedInput;
			ConsoleMode = newConsoleMode;
		}

		public CharInfo [] OriginalStdOutChars;

		public bool WriteToConsole (CharInfo [] charInfoBuffer, Coord coords, SmallRect window)
		{
			if (ScreenBuffer == IntPtr.Zero) {
				ReadFromConsoleOutput (new Size (Console.WindowWidth, Console.WindowHeight), coords, ref window);
			}

			return WriteConsoleOutput (ScreenBuffer, charInfoBuffer, coords, new Coord () { X = window.Left, Y = window.Top }, ref window);
		}

		public void ReadFromConsoleOutput (Size size, Coord coords, ref SmallRect window)
		{
			ScreenBuffer = CreateConsoleScreenBuffer (
				DesiredAccess.GenericRead | DesiredAccess.GenericWrite,
				ShareMode.FileShareRead | ShareMode.FileShareWrite,
				IntPtr.Zero,
				1,
				IntPtr.Zero
			);
			if (ScreenBuffer == INVALID_HANDLE_VALUE) {
				var err = Marshal.GetLastWin32Error ();

				if (err != 0)
					throw new System.ComponentModel.Win32Exception (err);
			}

			if (!initialCursorVisibility.HasValue && GetCursorVisibility (out CursorVisibility visibility)) {
				initialCursorVisibility = visibility;
			}

			if (!SetConsoleActiveScreenBuffer (ScreenBuffer)) {
				var err = Marshal.GetLastWin32Error ();
				throw new System.ComponentModel.Win32Exception (err);
			}

			OriginalStdOutChars = new CharInfo [size.Height * size.Width];

			ReadConsoleOutput (OutputHandle, OriginalStdOutChars, coords, new Coord () { X = 0, Y = 0 }, ref window);
		}

		public bool SetCursorPosition (Coord position)
		{
			return SetConsoleCursorPosition (ScreenBuffer, position);
		}

		public void SetInitialCursorVisibility ()
		{
			if (initialCursorVisibility.HasValue == false && GetCursorVisibility (out CursorVisibility visibility)) {
				initialCursorVisibility = visibility;
			}
		}

		public bool GetCursorVisibility (out CursorVisibility visibility)
		{
			if (!GetConsoleCursorInfo (ScreenBuffer, out ConsoleCursorInfo info)) {
				var err = Marshal.GetLastWin32Error ();
				if (err != 0) {
					throw new System.ComponentModel.Win32Exception (err);
				}
				visibility = Gui.CursorVisibility.Default;

				return false;
			}

			if (!info.bVisible)
				visibility = CursorVisibility.Invisible;
			else if (info.dwSize > 50)
				visibility = CursorVisibility.Box;
			else
				visibility = CursorVisibility.Underline;

			return true;
		}

		public bool EnsureCursorVisibility ()
		{
			if (initialCursorVisibility.HasValue && pendingCursorVisibility.HasValue && SetCursorVisibility (pendingCursorVisibility.Value)) {
				pendingCursorVisibility = null;

				return true;
			}

			return false;
		}

		public void ForceRefreshCursorVisibility ()
		{
			if (currentCursorVisibility.HasValue) {
				pendingCursorVisibility = currentCursorVisibility;
				currentCursorVisibility = null;
			}
		}

		public bool SetCursorVisibility (CursorVisibility visibility)
		{
			if (initialCursorVisibility.HasValue == false) {
				pendingCursorVisibility = visibility;

				return false;
			}

			if (currentCursorVisibility.HasValue == false || currentCursorVisibility.Value != visibility) {
				ConsoleCursorInfo info = new ConsoleCursorInfo {
					dwSize = (uint)visibility & 0x00FF,
					bVisible = ((uint)visibility & 0xFF00) != 0
				};

				if (!SetConsoleCursorInfo (ScreenBuffer, ref info))
					return false;

				currentCursorVisibility = visibility;
			}

			return true;
		}

		public void Cleanup ()
		{
			if (initialCursorVisibility.HasValue) {
				SetCursorVisibility (initialCursorVisibility.Value);
			}

			ConsoleMode = originalConsoleMode;
			//ContinueListeningForConsoleEvents = false;
			if (!SetConsoleActiveScreenBuffer (OutputHandle)) {
				var err = Marshal.GetLastWin32Error ();
				Console.WriteLine ("Error: {0}", err);
			}

			if (ScreenBuffer != IntPtr.Zero)
				CloseHandle (ScreenBuffer);

			ScreenBuffer = IntPtr.Zero;
		}

		//bool ContinueListeningForConsoleEvents = true;

		public uint ConsoleMode {
			get {
				uint v;
				GetConsoleMode (InputHandle, out v);
				return v;
			}

			set {
				SetConsoleMode (InputHandle, value);
			}
		}

		public bool HeightAsBuffer { get; set; }

		/// <summary>
		/// Underscore - Character Attributes
		/// </summary>
		public const ushort COMMON_LVB_UNDERSCORE = 0x8000;

		[Flags]
		public enum ConsoleModes : uint {
			EnableProcessedInput = 1,
			EnableMouseInput = 16,
			EnableQuickEditMode = 64,
			EnableExtendedFlags = 128,
		}

		[StructLayout (LayoutKind.Explicit, CharSet = CharSet.Unicode)]
		public struct KeyEventRecord {
			[FieldOffset (0), MarshalAs (UnmanagedType.Bool)]
			public bool bKeyDown;
			[FieldOffset (4), MarshalAs (UnmanagedType.U2)]
			public ushort wRepeatCount;
			[FieldOffset (6), MarshalAs (UnmanagedType.U2)]
			public ushort wVirtualKeyCode;
			[FieldOffset (8), MarshalAs (UnmanagedType.U2)]
			public ushort wVirtualScanCode;
			[FieldOffset (10)]
			public char UnicodeChar;
			[FieldOffset (12), MarshalAs (UnmanagedType.U4)]
			public ControlKeyState dwControlKeyState;
		}

		[Flags]
		public enum ButtonState {
			Button1Pressed = 1,
			Button2Pressed = 4,
			Button3Pressed = 8,
			Button4Pressed = 16,
			RightmostButtonPressed = 2,
			WheeledUp = unchecked((int)0x780000),
			WheeledDown = unchecked((int)0xFF880000),
		}

		[Flags]
		public enum ControlKeyState {
			RightAltPressed = 1,
			LeftAltPressed = 2,
			RightControlPressed = 4,
			LeftControlPressed = 8,
			ShiftPressed = 16,
			NumlockOn = 32,
			ScrolllockOn = 64,
			CapslockOn = 128,
			EnhancedKey = 256
		}

		[Flags]
		public enum EventFlags {
			MouseMoved = 1,
			DoubleClick = 2,
			MouseWheeled = 4,
			MouseHorizontalWheeled = 8
		}

		[StructLayout (LayoutKind.Explicit)]
		public struct MouseEventRecord {
			[FieldOffset (0)]
			public Coordinate MousePosition;
			[FieldOffset (4)]
			public ButtonState ButtonState;
			[FieldOffset (8)]
			public ControlKeyState ControlKeyState;
			[FieldOffset (12)]
			public EventFlags EventFlags;

			public override string ToString ()
			{
				return $"[Mouse({MousePosition},{ButtonState},{ControlKeyState},{EventFlags}";
			}
		}

		[StructLayout (LayoutKind.Sequential)]
		public struct Coordinate {
			public short X;
			public short Y;

			public Coordinate (short X, short Y)
			{
				this.X = X;
				this.Y = Y;
			}

			public override string ToString () => $"({X},{Y})";
		};

		public struct WindowBufferSizeRecord {
			public Coordinate size;

			public WindowBufferSizeRecord (short x, short y)
			{
				this.size = new Coordinate (x, y);
			}

			public override string ToString () => $"[WindowBufferSize{size}";
		}

		[StructLayout (LayoutKind.Sequential)]
		public struct MenuEventRecord {
			public uint dwCommandId;
		}

		[StructLayout (LayoutKind.Sequential)]
		public struct FocusEventRecord {
			public uint bSetFocus;
		}

		public enum EventType : ushort {
			Focus = 0x10,
			Key = 0x1,
			Menu = 0x8,
			Mouse = 2,
			WindowBufferSize = 4
		}

		[StructLayout (LayoutKind.Explicit)]
		public struct InputRecord {
			[FieldOffset (0)]
			public EventType EventType;
			[FieldOffset (4)]
			public KeyEventRecord KeyEvent;
			[FieldOffset (4)]
			public MouseEventRecord MouseEvent;
			[FieldOffset (4)]
			public WindowBufferSizeRecord WindowBufferSizeEvent;
			[FieldOffset (4)]
			public MenuEventRecord MenuEvent;
			[FieldOffset (4)]
			public FocusEventRecord FocusEvent;

			public override string ToString ()
			{
				switch (EventType) {
				case EventType.Focus:
					return FocusEvent.ToString ();
				case EventType.Key:
					return KeyEvent.ToString ();
				case EventType.Menu:
					return MenuEvent.ToString ();
				case EventType.Mouse:
					return MouseEvent.ToString ();
				case EventType.WindowBufferSize:
					return WindowBufferSizeEvent.ToString ();
				default:
					return "Unknown event type: " + EventType;
				}
			}
		};

		[Flags]
		enum ShareMode : uint {
			FileShareRead = 1,
			FileShareWrite = 2,
		}

		[Flags]
		enum DesiredAccess : uint {
			GenericRead = 2147483648,
			GenericWrite = 1073741824,
		}

		[StructLayout (LayoutKind.Sequential)]
		public struct ConsoleScreenBufferInfo {
			public Coord dwSize;
			public Coord dwCursorPosition;
			public ushort wAttributes;
			public SmallRect srWindow;
			public Coord dwMaximumWindowSize;
		}

		[StructLayout (LayoutKind.Sequential)]
		public struct Coord {
			public short X;
			public short Y;

			public Coord (short X, short Y)
			{
				this.X = X;
				this.Y = Y;
			}
			public override string ToString () => $"({X},{Y})";
		};

		[StructLayout (LayoutKind.Explicit, CharSet = CharSet.Unicode)]
		public struct CharUnion {
			[FieldOffset (0)] public char UnicodeChar;
			[FieldOffset (0)] public byte AsciiChar;
		}

		[StructLayout (LayoutKind.Explicit, CharSet = CharSet.Unicode)]
		public struct CharInfo {
			[FieldOffset (0)] public CharUnion Char;
			[FieldOffset (2)] public ushort Attributes;
		}

		[StructLayout (LayoutKind.Sequential)]
		public struct SmallRect {
			public short Left;
			public short Top;
			public short Right;
			public short Bottom;

			public static void MakeEmpty (ref SmallRect rect)
			{
				rect.Left = -1;
			}

			public static void Update (ref SmallRect rect, short col, short row)
			{
				if (rect.Left == -1) {
					//System.Diagnostics.Debugger.Log (0, "debug", $"damager From Empty {col},{row}\n");
					rect.Left = rect.Right = col;
					rect.Bottom = rect.Top = row;
					return;
				}
				if (col >= rect.Left && col <= rect.Right && row >= rect.Top && row <= rect.Bottom)
					return;
				if (col < rect.Left)
					rect.Left = col;
				if (col > rect.Right)
					rect.Right = col;
				if (row < rect.Top)
					rect.Top = row;
				if (row > rect.Bottom)
					rect.Bottom = row;
				//System.Diagnostics.Debugger.Log (0, "debug", $"Expanding {rect.ToString ()}\n");
			}

			public override string ToString ()
			{
				return $"Left={Left},Top={Top},Right={Right},Bottom={Bottom}";
			}
		}

		[StructLayout (LayoutKind.Sequential)]
		public struct ConsoleKeyInfoEx {
			public ConsoleKeyInfo consoleKeyInfo;
			public bool CapsLock;
			public bool NumLock;

			public ConsoleKeyInfoEx (ConsoleKeyInfo consoleKeyInfo, bool capslock, bool numlock)
			{
				this.consoleKeyInfo = consoleKeyInfo;
				CapsLock = capslock;
				NumLock = numlock;
			}
		}

		[DllImport ("kernel32.dll", SetLastError = true)]
		static extern IntPtr GetStdHandle (int nStdHandle);

		[DllImport ("kernel32.dll", SetLastError = true)]
		static extern bool CloseHandle (IntPtr handle);

		[DllImport ("kernel32.dll", EntryPoint = "ReadConsoleInputW", CharSet = CharSet.Unicode)]
		public static extern bool ReadConsoleInput (
			IntPtr hConsoleInput,
			IntPtr lpBuffer,
			uint nLength,
			out uint lpNumberOfEventsRead);

		[DllImport ("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		static extern bool ReadConsoleOutput (
			IntPtr hConsoleOutput,
			[Out] CharInfo [] lpBuffer,
			Coord dwBufferSize,
			Coord dwBufferCoord,
			ref SmallRect lpReadRegion
		);

		[DllImport ("kernel32.dll", EntryPoint = "WriteConsoleOutput", SetLastError = true, CharSet = CharSet.Unicode)]
		static extern bool WriteConsoleOutput (
			IntPtr hConsoleOutput,
			CharInfo [] lpBuffer,
			Coord dwBufferSize,
			Coord dwBufferCoord,
			ref SmallRect lpWriteRegion
		);

		[DllImport ("kernel32.dll")]
		static extern bool SetConsoleCursorPosition (IntPtr hConsoleOutput, Coord dwCursorPosition);

		[StructLayout (LayoutKind.Sequential)]
		public struct ConsoleCursorInfo {
			public uint dwSize;
			public bool bVisible;
		}

		[DllImport ("kernel32.dll", SetLastError = true)]
		static extern bool SetConsoleCursorInfo (IntPtr hConsoleOutput, [In] ref ConsoleCursorInfo lpConsoleCursorInfo);

		[DllImport ("kernel32.dll", SetLastError = true)]
		static extern bool GetConsoleCursorInfo (IntPtr hConsoleOutput, out ConsoleCursorInfo lpConsoleCursorInfo);

		[DllImport ("kernel32.dll")]
		static extern bool GetConsoleMode (IntPtr hConsoleHandle, out uint lpMode);


		[DllImport ("kernel32.dll")]
		static extern bool SetConsoleMode (IntPtr hConsoleHandle, uint dwMode);

		[DllImport ("kernel32.dll", SetLastError = true)]
		static extern IntPtr CreateConsoleScreenBuffer (
			DesiredAccess dwDesiredAccess,
			ShareMode dwShareMode,
			IntPtr secutiryAttributes,
			UInt32 flags,
			IntPtr screenBufferData
		);

		internal static IntPtr INVALID_HANDLE_VALUE = new IntPtr (-1);


		[DllImport ("kernel32.dll", SetLastError = true)]
		static extern bool SetConsoleActiveScreenBuffer (IntPtr Handle);

		[DllImport ("kernel32.dll", SetLastError = true)]
		static extern bool GetNumberOfConsoleInputEvents (IntPtr handle, out uint lpcNumberOfEvents);
		public uint InputEventCount {
			get {
				uint v;
				GetNumberOfConsoleInputEvents (InputHandle, out v);
				return v;
			}
		}

		public InputRecord [] ReadConsoleInput ()
		{
			const int bufferSize = 1;
			var pRecord = Marshal.AllocHGlobal (Marshal.SizeOf<InputRecord> () * bufferSize);
			try {
				ReadConsoleInput (InputHandle, pRecord, bufferSize,
					out var numberEventsRead);

				return numberEventsRead == 0
					? null
					: new [] { Marshal.PtrToStructure<InputRecord> (pRecord) };
			} catch (Exception) {
				return null;
			} finally {
				Marshal.FreeHGlobal (pRecord);
			}
		}

#if false      // Not needed on the constructor. Perhaps could be used on resizing. To study.
		[DllImport ("kernel32.dll", ExactSpelling = true)]
		static extern IntPtr GetConsoleWindow ();

		[DllImport ("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		static extern bool ShowWindow (IntPtr hWnd, int nCmdShow);

		public const int HIDE = 0;
		public const int MAXIMIZE = 3;
		public const int MINIMIZE = 6;
		public const int RESTORE = 9;

		internal void ShowWindow (int state)
		{
			IntPtr thisConsole = GetConsoleWindow ();
			ShowWindow (thisConsole, state);
		}
#endif
#if false // See: https://github.com/migueldeicaza/gui.cs/issues/357
		[StructLayout (LayoutKind.Sequential)]
		public struct SMALL_RECT {
			public short Left;
			public short Top;
			public short Right;
			public short Bottom;

			public SMALL_RECT (short Left, short Top, short Right, short Bottom)
			{
				this.Left = Left;
				this.Top = Top;
				this.Right = Right;
				this.Bottom = Bottom;
			}
		}

		[StructLayout (LayoutKind.Sequential)]
		public struct CONSOLE_SCREEN_BUFFER_INFO {
			public int dwSize;
			public int dwCursorPosition;
			public short wAttributes;
			public SMALL_RECT srWindow;
			public int dwMaximumWindowSize;
		}

		[DllImport ("kernel32.dll", SetLastError = true)]
		static extern bool GetConsoleScreenBufferInfo (IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO ConsoleScreenBufferInfo);

		// Theoretically GetConsoleScreenBuffer height should give the console Windoww size
		// It does not work, however, and always returns the size the window was initially created at
		internal Size GetWindowSize ()
		{
			var consoleScreenBufferInfo = new CONSOLE_SCREEN_BUFFER_INFO ();
			//consoleScreenBufferInfo.dwSize = Marshal.SizeOf (typeof (CONSOLE_SCREEN_BUFFER_INFO));
			GetConsoleScreenBufferInfo (OutputHandle, out consoleScreenBufferInfo);
			return new Size (consoleScreenBufferInfo.srWindow.Right - consoleScreenBufferInfo.srWindow.Left,
				consoleScreenBufferInfo.srWindow.Bottom - consoleScreenBufferInfo.srWindow.Top);
		}
#endif
	}

	internal class WindowsDriver : ConsoleDriver {
		static bool sync = false;
		WindowsConsole.CharInfo [] OutputBuffer;
		int cols, rows, top;
		WindowsConsole winConsole;
		WindowsConsole.SmallRect damageRegion;

		public override int Cols => cols;
		public override int Rows => rows;
		public override int Top => top;
		public override bool HeightAsBuffer { get; set; }

		public WindowsConsole WinConsole {
			get => winConsole;
			private set => winConsole = value;
		}

		Action<KeyEvent> keyHandler;
		Action<KeyEvent> keyDownHandler;
		Action<KeyEvent> keyUpHandler;
		Action<MouseEvent> mouseHandler;

		public WindowsDriver ()
		{
			winConsole = new WindowsConsole ();
		}

		bool winChanging;

		public override void PrepareToRun (MainLoop mainLoop, Action<KeyEvent> keyHandler, Action<KeyEvent> keyDownHandler, Action<KeyEvent> keyUpHandler, Action<MouseEvent> mouseHandler)
		{
			this.keyHandler = keyHandler;
			this.keyDownHandler = keyDownHandler;
			this.keyUpHandler = keyUpHandler;
			this.mouseHandler = mouseHandler;

			var mLoop = mainLoop.Driver as WindowsMainLoop;

			mLoop.ProcessInput = (e) => ProcessInput (e);

			mLoop.WinChanged = (e) => {
				ChangeWin (e);
			};
		}

		private void ChangeWin (Size e)
		{
			winChanging = true;
			if (!HeightAsBuffer) {
				top = 0;
				cols = e.Width;
				rows = e.Height;
				ResizeScreen ();
				UpdateOffScreen ();
				var bufferCoords = new WindowsConsole.Coord () {
					X = (short)Clip.Width,
					Y = (short)Clip.Height
				};
				winConsole.ReadFromConsoleOutput (e, bufferCoords, ref damageRegion);
				if (!winChanging) {
					TerminalResized.Invoke ();
				}
			}
		}

		void ProcessInput (WindowsConsole.InputRecord inputEvent)
		{
			switch (inputEvent.EventType) {
			case WindowsConsole.EventType.Key:
				var map = MapKey (ToConsoleKeyInfoEx (inputEvent.KeyEvent));
				if (map == (Key)0xffffffff) {
					KeyEvent key = new KeyEvent ();

					// Shift = VK_SHIFT = 0x10
					// Ctrl = VK_CONTROL = 0x11
					// Alt = VK_MENU = 0x12

					if (inputEvent.KeyEvent.dwControlKeyState.HasFlag (WindowsConsole.ControlKeyState.CapslockOn)) {
						inputEvent.KeyEvent.dwControlKeyState &= ~WindowsConsole.ControlKeyState.CapslockOn;
					}

					if (inputEvent.KeyEvent.dwControlKeyState.HasFlag (WindowsConsole.ControlKeyState.ScrolllockOn)) {
						inputEvent.KeyEvent.dwControlKeyState &= ~WindowsConsole.ControlKeyState.ScrolllockOn;
					}

					if (inputEvent.KeyEvent.dwControlKeyState.HasFlag (WindowsConsole.ControlKeyState.NumlockOn)) {
						inputEvent.KeyEvent.dwControlKeyState &= ~WindowsConsole.ControlKeyState.NumlockOn;
					}

					switch (inputEvent.KeyEvent.dwControlKeyState) {
					case WindowsConsole.ControlKeyState.RightAltPressed:
					case WindowsConsole.ControlKeyState.RightAltPressed |
						WindowsConsole.ControlKeyState.LeftControlPressed |
						WindowsConsole.ControlKeyState.EnhancedKey:
					case WindowsConsole.ControlKeyState.EnhancedKey:
						key = new KeyEvent (Key.CtrlMask | Key.AltMask, keyModifiers);
						break;
					case WindowsConsole.ControlKeyState.LeftAltPressed:
						key = new KeyEvent (Key.AltMask, keyModifiers);
						break;
					case WindowsConsole.ControlKeyState.RightControlPressed:
					case WindowsConsole.ControlKeyState.LeftControlPressed:
						key = new KeyEvent (Key.CtrlMask, keyModifiers);
						break;
					case WindowsConsole.ControlKeyState.ShiftPressed:
						key = new KeyEvent (Key.ShiftMask, keyModifiers);
						break;
					case WindowsConsole.ControlKeyState.NumlockOn:
						break;
					case WindowsConsole.ControlKeyState.ScrolllockOn:
						break;
					case WindowsConsole.ControlKeyState.CapslockOn:
						break;
					default:
						switch (inputEvent.KeyEvent.wVirtualKeyCode) {
						case 0x10:
							key = new KeyEvent (Key.ShiftMask, keyModifiers);
							break;
						case 0x11:
							key = new KeyEvent (Key.CtrlMask, keyModifiers);
							break;
						case 0x12:
							key = new KeyEvent (Key.AltMask, keyModifiers);
							break;
						default:
							key = new KeyEvent (Key.Unknown, keyModifiers);
							break;
						}
						break;
					}

					if (inputEvent.KeyEvent.bKeyDown)
						keyDownHandler (key);
					else
						keyUpHandler (key);
				} else {
					if (inputEvent.KeyEvent.bKeyDown) {
						// Key Down - Fire KeyDown Event and KeyStroke (ProcessKey) Event
						keyDownHandler (new KeyEvent (map, keyModifiers));
						keyHandler (new KeyEvent (map, keyModifiers));
					} else {
						keyUpHandler (new KeyEvent (map, keyModifiers));
					}
				}
				if (!inputEvent.KeyEvent.bKeyDown) {
					keyModifiers = null;
				}
				break;

			case WindowsConsole.EventType.Mouse:
				mouseHandler (ToDriverMouse (inputEvent.MouseEvent));
				if (IsButtonReleased)
					mouseHandler (ToDriverMouse (inputEvent.MouseEvent));
				break;

			case WindowsConsole.EventType.WindowBufferSize:
				cols = inputEvent.WindowBufferSizeEvent.size.X;
				rows = inputEvent.WindowBufferSizeEvent.size.Y;
				ResizeScreen ();
				UpdateOffScreen ();
				TerminalResized?.Invoke ();
				break;

			case WindowsConsole.EventType.Focus:
				break;
			}
		}

		WindowsConsole.ButtonState? LastMouseButtonPressed = null;
		bool IsButtonPressed = false;
		bool IsButtonReleased = false;
		bool IsButtonDoubleClicked = false;
		Point point;

		MouseEvent ToDriverMouse (WindowsConsole.MouseEventRecord mouseEvent)
		{
			MouseFlags mouseFlag = MouseFlags.AllEvents;

			if (IsButtonDoubleClicked) {
				Application.MainLoop.AddIdle (() => {
					ProcessButtonDoubleClickedAsync ().ConfigureAwait (false);
					return false;
				});
			}

			// The ButtonState member of the MouseEvent structure has bit corresponding to each mouse button.
			// This will tell when a mouse button is pressed. When the button is released this event will
			// be fired with it's bit set to 0. So when the button is up ButtonState will be 0.
			// To map to the correct driver events we save the last pressed mouse button so we can
			// map to the correct clicked event.
			if ((LastMouseButtonPressed != null || IsButtonReleased) && mouseEvent.ButtonState != 0) {
				LastMouseButtonPressed = null;
				IsButtonPressed = false;
				IsButtonReleased = false;
			}

			var p = new Point () {
				X = mouseEvent.MousePosition.X,
				Y = mouseEvent.MousePosition.Y
			};

			if ((mouseEvent.ButtonState != 0 && mouseEvent.EventFlags == 0 && LastMouseButtonPressed == null && !IsButtonDoubleClicked) ||
				(mouseEvent.EventFlags == WindowsConsole.EventFlags.MouseMoved &&
				mouseEvent.ButtonState != 0 && !IsButtonReleased && !IsButtonDoubleClicked)) {
				switch (mouseEvent.ButtonState) {
				case WindowsConsole.ButtonState.Button1Pressed:
					mouseFlag = MouseFlags.Button1Pressed;
					break;

				case WindowsConsole.ButtonState.Button2Pressed:
					mouseFlag = MouseFlags.Button2Pressed;
					break;

				case WindowsConsole.ButtonState.RightmostButtonPressed:
					mouseFlag = MouseFlags.Button3Pressed;
					break;
				}

				if (mouseEvent.EventFlags == WindowsConsole.EventFlags.MouseMoved) {
					mouseFlag |= MouseFlags.ReportMousePosition;
					point = new Point ();
					IsButtonReleased = false;
				} else {
					point = new Point () {
						X = mouseEvent.MousePosition.X,
						Y = mouseEvent.MousePosition.Y
					};
				}
				LastMouseButtonPressed = mouseEvent.ButtonState;
				IsButtonPressed = true;

				if ((mouseFlag & MouseFlags.ReportMousePosition) == 0) {
					Application.MainLoop.AddIdle (() => {
						ProcessContinuousButtonPressedAsync (mouseEvent, mouseFlag).ConfigureAwait (false);
						return false;
					});
				}

			} else if ((mouseEvent.EventFlags == 0 || mouseEvent.EventFlags == WindowsConsole.EventFlags.MouseMoved) &&
				LastMouseButtonPressed != null && !IsButtonReleased && !IsButtonDoubleClicked) {
				switch (LastMouseButtonPressed) {
				case WindowsConsole.ButtonState.Button1Pressed:
					mouseFlag = MouseFlags.Button1Released;
					break;

				case WindowsConsole.ButtonState.Button2Pressed:
					mouseFlag = MouseFlags.Button2Released;
					break;

				case WindowsConsole.ButtonState.RightmostButtonPressed:
					mouseFlag = MouseFlags.Button3Released;
					break;
				}
				IsButtonPressed = false;
				IsButtonReleased = true;
			} else if ((mouseEvent.EventFlags == 0 || mouseEvent.EventFlags == WindowsConsole.EventFlags.MouseMoved) &&
				  IsButtonReleased && p == point) {
				switch (LastMouseButtonPressed) {
				case WindowsConsole.ButtonState.Button1Pressed:
					mouseFlag = MouseFlags.Button1Clicked;
					break;

				case WindowsConsole.ButtonState.Button2Pressed:
					mouseFlag = MouseFlags.Button2Clicked;
					break;

				case WindowsConsole.ButtonState.RightmostButtonPressed:
					mouseFlag = MouseFlags.Button3Clicked;
					break;
				}
				point = new Point () {
					X = mouseEvent.MousePosition.X,
					Y = mouseEvent.MousePosition.Y
				};
				LastMouseButtonPressed = null;
				IsButtonReleased = false;
			} else if (mouseEvent.EventFlags.HasFlag (WindowsConsole.EventFlags.DoubleClick)) {
				switch (mouseEvent.ButtonState) {
				case WindowsConsole.ButtonState.Button1Pressed:
					mouseFlag = MouseFlags.Button1DoubleClicked;
					break;

				case WindowsConsole.ButtonState.Button2Pressed:
					mouseFlag = MouseFlags.Button2DoubleClicked;
					break;

				case WindowsConsole.ButtonState.RightmostButtonPressed:
					mouseFlag = MouseFlags.Button3DoubleClicked;
					break;
				}
				IsButtonDoubleClicked = true;
			} else if (mouseEvent.EventFlags == 0 && mouseEvent.ButtonState != 0 && IsButtonDoubleClicked) {
				switch (mouseEvent.ButtonState) {
				case WindowsConsole.ButtonState.Button1Pressed:
					mouseFlag = MouseFlags.Button1TripleClicked;
					break;

				case WindowsConsole.ButtonState.Button2Pressed:
					mouseFlag = MouseFlags.Button2TripleClicked;
					break;

				case WindowsConsole.ButtonState.RightmostButtonPressed:
					mouseFlag = MouseFlags.Button3TripleClicked;
					break;
				}
				IsButtonDoubleClicked = false;
			} else if (mouseEvent.EventFlags == WindowsConsole.EventFlags.MouseWheeled) {
				switch (mouseEvent.ButtonState) {
				case WindowsConsole.ButtonState.WheeledUp:
					mouseFlag = MouseFlags.WheeledUp;
					break;

				case WindowsConsole.ButtonState.WheeledDown:
					mouseFlag = MouseFlags.WheeledDown;
					break;
				}

			} else if (mouseEvent.EventFlags == WindowsConsole.EventFlags.MouseWheeled &&
				mouseEvent.ControlKeyState == WindowsConsole.ControlKeyState.ShiftPressed) {
				switch (mouseEvent.ButtonState) {
				case WindowsConsole.ButtonState.WheeledUp:
					mouseFlag = MouseFlags.WheeledLeft;
					break;

				case WindowsConsole.ButtonState.WheeledDown:
					mouseFlag = MouseFlags.WheeledRight;
					break;
				}

			} else if (mouseEvent.EventFlags == WindowsConsole.EventFlags.MouseMoved) {
				if (mouseEvent.MousePosition.X != point.X || mouseEvent.MousePosition.Y != point.Y) {
					mouseFlag = MouseFlags.ReportMousePosition;
					point = new Point ();
				} else {
					mouseFlag = 0;
				}
			} else if (mouseEvent.ButtonState == 0 && mouseEvent.EventFlags == 0) {
				mouseFlag = 0;
			}

			mouseFlag = SetControlKeyStates (mouseEvent, mouseFlag);

			return new MouseEvent () {
				X = mouseEvent.MousePosition.X,
				Y = mouseEvent.MousePosition.Y,
				Flags = mouseFlag
			};
		}

		async Task ProcessButtonDoubleClickedAsync ()
		{
			await Task.Delay (200);
			IsButtonDoubleClicked = false;
		}

		async Task ProcessContinuousButtonPressedAsync (WindowsConsole.MouseEventRecord mouseEvent, MouseFlags mouseFlag)
		{
			while (IsButtonPressed) {
				await Task.Delay (100);
				var me = new MouseEvent () {
					X = mouseEvent.MousePosition.X,
					Y = mouseEvent.MousePosition.Y,
					Flags = mouseFlag
				};

				var view = Application.wantContinuousButtonPressedView;
				if (view == null) {
					break;
				}
				if (IsButtonPressed && (mouseFlag & MouseFlags.ReportMousePosition) == 0) {
					mouseHandler (me);
				}
			}
		}

		static MouseFlags SetControlKeyStates (WindowsConsole.MouseEventRecord mouseEvent, MouseFlags mouseFlag)
		{
			if (mouseEvent.ControlKeyState.HasFlag (WindowsConsole.ControlKeyState.RightControlPressed) ||
				mouseEvent.ControlKeyState.HasFlag (WindowsConsole.ControlKeyState.LeftControlPressed))
				mouseFlag |= MouseFlags.ButtonCtrl;

			if (mouseEvent.ControlKeyState.HasFlag (WindowsConsole.ControlKeyState.ShiftPressed))
				mouseFlag |= MouseFlags.ButtonShift;

			if (mouseEvent.ControlKeyState.HasFlag (WindowsConsole.ControlKeyState.RightAltPressed) ||
				 mouseEvent.ControlKeyState.HasFlag (WindowsConsole.ControlKeyState.LeftAltPressed))
				mouseFlag |= MouseFlags.ButtonAlt;
			return mouseFlag;
		}

		KeyModifiers keyModifiers;

		public WindowsConsole.ConsoleKeyInfoEx ToConsoleKeyInfoEx (WindowsConsole.KeyEventRecord keyEvent)
		{
			var state = keyEvent.dwControlKeyState;

			bool shift = (state & WindowsConsole.ControlKeyState.ShiftPressed) != 0;
			bool alt = (state & (WindowsConsole.ControlKeyState.LeftAltPressed | WindowsConsole.ControlKeyState.RightAltPressed)) != 0;
			bool control = (state & (WindowsConsole.ControlKeyState.LeftControlPressed | WindowsConsole.ControlKeyState.RightControlPressed)) != 0;
			bool capslock = (state & (WindowsConsole.ControlKeyState.CapslockOn)) != 0;
			bool numlock = (state & (WindowsConsole.ControlKeyState.NumlockOn)) != 0;
			bool scrolllock = (state & (WindowsConsole.ControlKeyState.ScrolllockOn)) != 0;

			if (keyModifiers == null)
				keyModifiers = new KeyModifiers ();
			if (shift)
				keyModifiers.Shift = shift;
			if (alt)
				keyModifiers.Alt = alt;
			if (control)
				keyModifiers.Ctrl = control;
			if (capslock)
				keyModifiers.Capslock = capslock;
			if (numlock)
				keyModifiers.Numlock = numlock;
			if (scrolllock)
				keyModifiers.Scrolllock = scrolllock;

			var ConsoleKeyInfo = new ConsoleKeyInfo (keyEvent.UnicodeChar, (ConsoleKey)keyEvent.wVirtualKeyCode, shift, alt, control);
			return new WindowsConsole.ConsoleKeyInfoEx (ConsoleKeyInfo, capslock, numlock);
		}

		public Key MapKey (WindowsConsole.ConsoleKeyInfoEx keyInfoEx)
		{
			var keyInfo = keyInfoEx.consoleKeyInfo;
			switch (keyInfo.Key) {
			case ConsoleKey.Escape:
				return MapKeyModifiers (keyInfo, Key.Esc);
			case ConsoleKey.Tab:
				return keyInfo.Modifiers == ConsoleModifiers.Shift ? Key.BackTab : Key.Tab;
			case ConsoleKey.Home:
				return MapKeyModifiers (keyInfo, Key.Home);
			case ConsoleKey.End:
				return MapKeyModifiers (keyInfo, Key.End);
			case ConsoleKey.LeftArrow:
				return MapKeyModifiers (keyInfo, Key.CursorLeft);
			case ConsoleKey.RightArrow:
				return MapKeyModifiers (keyInfo, Key.CursorRight);
			case ConsoleKey.UpArrow:
				return MapKeyModifiers (keyInfo, Key.CursorUp);
			case ConsoleKey.DownArrow:
				return MapKeyModifiers (keyInfo, Key.CursorDown);
			case ConsoleKey.PageUp:
				return MapKeyModifiers (keyInfo, Key.PageUp);
			case ConsoleKey.PageDown:
				return MapKeyModifiers (keyInfo, Key.PageDown);
			case ConsoleKey.Enter:
				return MapKeyModifiers (keyInfo, Key.Enter);
			case ConsoleKey.Spacebar:
				return MapKeyModifiers (keyInfo, keyInfo.KeyChar == 0 ? Key.Space : (Key)keyInfo.KeyChar);
			case ConsoleKey.Backspace:
				return MapKeyModifiers (keyInfo, Key.Backspace);
			case ConsoleKey.Delete:
				return MapKeyModifiers (keyInfo, Key.DeleteChar);
			case ConsoleKey.Insert:
				return MapKeyModifiers (keyInfo, Key.InsertChar);

			case ConsoleKey.NumPad0:
				return keyInfoEx.NumLock ? Key.D0 : Key.InsertChar;
			case ConsoleKey.NumPad1:
				return keyInfoEx.NumLock ? Key.D1 : Key.End;
			case ConsoleKey.NumPad2:
				return keyInfoEx.NumLock ? Key.D2 : Key.CursorDown;
			case ConsoleKey.NumPad3:
				return keyInfoEx.NumLock ? Key.D3 : Key.PageDown;
			case ConsoleKey.NumPad4:
				return keyInfoEx.NumLock ? Key.D4 : Key.CursorLeft;
			case ConsoleKey.NumPad5:
				return keyInfoEx.NumLock ? Key.D5 : (Key)((uint)keyInfo.KeyChar);
			case ConsoleKey.NumPad6:
				return keyInfoEx.NumLock ? Key.D6 : Key.CursorRight;
			case ConsoleKey.NumPad7:
				return keyInfoEx.NumLock ? Key.D7 : Key.Home;
			case ConsoleKey.NumPad8:
				return keyInfoEx.NumLock ? Key.D8 : Key.CursorUp;
			case ConsoleKey.NumPad9:
				return keyInfoEx.NumLock ? Key.D9 : Key.PageUp;

			case ConsoleKey.Oem1:
			case ConsoleKey.Oem2:
			case ConsoleKey.Oem3:
			case ConsoleKey.Oem4:
			case ConsoleKey.Oem5:
			case ConsoleKey.Oem6:
			case ConsoleKey.Oem7:
			case ConsoleKey.Oem8:
			case ConsoleKey.Oem102:
			case ConsoleKey.OemPeriod:
			case ConsoleKey.OemComma:
			case ConsoleKey.OemPlus:
			case ConsoleKey.OemMinus:
				if (keyInfo.KeyChar == 0)
					return Key.Unknown;

				return (Key)((uint)keyInfo.KeyChar);
			}

			var key = keyInfo.Key;
			//var alphaBase = ((keyInfo.Modifiers == ConsoleModifiers.Shift) ^ (keyInfoEx.CapsLock)) ? 'A' : 'a';

			if (key >= ConsoleKey.A && key <= ConsoleKey.Z) {
				var delta = key - ConsoleKey.A;
				if (keyInfo.Modifiers == ConsoleModifiers.Control) {
					return (Key)(((uint)Key.CtrlMask) | ((uint)Key.A + delta));
				}
				if (keyInfo.Modifiers == ConsoleModifiers.Alt) {
					return (Key)(((uint)Key.AltMask) | ((uint)Key.A + delta));
				}
				if ((keyInfo.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0) {
					if (keyInfo.KeyChar == 0 || (keyInfo.KeyChar != 0 && keyInfo.KeyChar >= 1 && keyInfo.KeyChar <= 26)) {
						return MapKeyModifiers (keyInfo, (Key)((uint)Key.A + delta));
					}
				}
				//return (Key)((uint)alphaBase + delta);
				return (Key)((uint)keyInfo.KeyChar);
			}
			if (key >= ConsoleKey.D0 && key <= ConsoleKey.D9) {
				var delta = key - ConsoleKey.D0;
				if (keyInfo.Modifiers == ConsoleModifiers.Alt) {
					return (Key)(((uint)Key.AltMask) | ((uint)Key.D0 + delta));
				}
				if (keyInfo.Modifiers == ConsoleModifiers.Control) {
					return (Key)(((uint)Key.CtrlMask) | ((uint)Key.D0 + delta));
				}
				if ((keyInfo.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0) {
					if (keyInfo.KeyChar == 0 || keyInfo.KeyChar == 30) {
						return MapKeyModifiers (keyInfo, (Key)((uint)Key.D0 + delta));
					}
				}
				return (Key)((uint)keyInfo.KeyChar);
			}
			if (key >= ConsoleKey.F1 && key <= ConsoleKey.F12) {
				var delta = key - ConsoleKey.F1;
				if ((keyInfo.Modifiers & (ConsoleModifiers.Shift | ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0) {
					return MapKeyModifiers (keyInfo, (Key)((uint)Key.F1 + delta));
				}

				return (Key)((uint)Key.F1 + delta);
			}
			if (keyInfo.KeyChar != 0) {
				return MapKeyModifiers (keyInfo, (Key)((uint)keyInfo.KeyChar));
			}

			return (Key)(0xffffffff);
		}

		Key MapKeyModifiers (ConsoleKeyInfo keyInfo, Key key)
		{
			Key keyMod = new Key ();
			if ((keyInfo.Modifiers & ConsoleModifiers.Shift) != 0)
				keyMod = Key.ShiftMask;
			if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0)
				keyMod |= Key.CtrlMask;
			if ((keyInfo.Modifiers & ConsoleModifiers.Alt) != 0)
				keyMod |= Key.AltMask;

			return keyMod != Key.Null ? keyMod | key : key;
		}

		public override void Init (Action terminalResized)
		{
			TerminalResized = terminalResized;

			cols = Console.WindowWidth;
			rows = Console.WindowHeight;
#if false
			winConsole.ShowWindow (WindowsConsole.RESTORE);
#endif
			WindowsConsole.SmallRect.MakeEmpty (ref damageRegion);

			ResizeScreen ();
			UpdateOffScreen ();

			Colors.TopLevel = new ColorScheme ();
			Colors.Base = new ColorScheme ();
			Colors.Dialog = new ColorScheme ();
			Colors.Menu = new ColorScheme ();
			Colors.Error = new ColorScheme ();

			Colors.TopLevel.Normal = MakeColor (ConsoleColor.Green, ConsoleColor.Black);
			Colors.TopLevel.Focus = MakeColor (ConsoleColor.White, ConsoleColor.DarkCyan);
			Colors.TopLevel.HotNormal = MakeColor (ConsoleColor.DarkYellow, ConsoleColor.Black);
			Colors.TopLevel.HotFocus = MakeColor (ConsoleColor.DarkBlue, ConsoleColor.DarkCyan);

			Colors.Base.Normal = MakeColor (ConsoleColor.White, ConsoleColor.DarkBlue);
			Colors.Base.Focus = MakeColor (ConsoleColor.Black, ConsoleColor.Gray);
			Colors.Base.HotNormal = MakeColor (ConsoleColor.DarkCyan, ConsoleColor.DarkBlue);
			Colors.Base.HotFocus = MakeColor (ConsoleColor.Blue, ConsoleColor.Gray);

			Colors.Menu.Normal = MakeColor (ConsoleColor.White, ConsoleColor.DarkGray);
			Colors.Menu.Focus = MakeColor (ConsoleColor.White, ConsoleColor.Black);
			Colors.Menu.HotNormal = MakeColor (ConsoleColor.Yellow, ConsoleColor.DarkGray);
			Colors.Menu.HotFocus = MakeColor (ConsoleColor.Yellow, ConsoleColor.Black);
			Colors.Menu.Disabled = MakeColor (ConsoleColor.Gray, ConsoleColor.DarkGray);

			Colors.Dialog.Normal = MakeColor (ConsoleColor.Black, ConsoleColor.Gray);
			Colors.Dialog.Focus = MakeColor (ConsoleColor.White, ConsoleColor.DarkGray);
			Colors.Dialog.HotNormal = MakeColor (ConsoleColor.DarkBlue, ConsoleColor.Gray);
			Colors.Dialog.HotFocus = MakeColor (ConsoleColor.DarkBlue, ConsoleColor.DarkGray);

			Colors.Error.Normal = MakeColor (ConsoleColor.DarkRed, ConsoleColor.White);
			Colors.Error.Focus = MakeColor (ConsoleColor.White, ConsoleColor.DarkRed);
			Colors.Error.HotNormal = MakeColor (ConsoleColor.Black, ConsoleColor.White);
			Colors.Error.HotFocus = MakeColor (ConsoleColor.Black, ConsoleColor.DarkRed);
		}

		void ResizeScreen ()
		{
			OutputBuffer = new WindowsConsole.CharInfo [Rows * Cols];
			Clip = new Rect (0, 0, Cols, Rows);
			damageRegion = new WindowsConsole.SmallRect () {
				Top = 0,
				Left = 0,
				Bottom = (short)Rows,
				Right = (short)Cols
			};
			winConsole.ForceRefreshCursorVisibility ();
		}

		void UpdateOffScreen ()
		{
			for (int row = 0; row < rows; row++) {
				for (int col = 0; col < cols; col++) {
					int position = row * cols + col;
					OutputBuffer [position].Attributes = (ushort)Colors.TopLevel.Normal;
					OutputBuffer [position].Char.UnicodeChar = ' ';
				}
			}

			winChanging = false;
		}

		int ccol, crow;
		public override void Move (int col, int row)
		{
			ccol = col;
			crow = row;
		}

		public override void AddRune (Rune rune)
		{
			rune = MakePrintable (rune);
			var position = crow * Cols + ccol;

			if (Clip.Contains (ccol, crow)) {
				OutputBuffer [position].Attributes = (ushort)currentAttribute;
				OutputBuffer [position].Attributes |= (ushort)(currentUnderlineAttribute ? WindowsConsole.COMMON_LVB_UNDERSCORE : 0);
				OutputBuffer [position].Char.UnicodeChar = (char)rune;
				WindowsConsole.SmallRect.Update (ref damageRegion, (short)ccol, (short)crow);
			}

			ccol++;
			var runeWidth = Rune.ColumnWidth (rune);
			if (runeWidth > 1) {
				for (int i = 1; i < runeWidth; i++) {
					AddStr (" ");
				}
			}
			//if (ccol == Cols) {
			//	ccol = 0;
			//	if (crow + 1 < Rows)
			//		crow++;
			//}
			if (sync)
				UpdateScreen ();
		}

		public override void AddStr (ustring str)
		{
			foreach (var rune in str)
				AddRune (rune);
		}

		int currentAttribute;
		bool currentUnderlineAttribute;

		public override void SetAttribute (Attribute c)
		{
			currentAttribute = c.Value;
			currentUnderlineAttribute = c.UnderLine;
		}

		Attribute MakeColor (ConsoleColor f, ConsoleColor b)
		{
			// Encode the colors into the int value.
			return new Attribute (
				value: ((int)f | (int)b << 4),
				foreground: (Color)f,
				background: (Color)b
			);
		}

		public override Attribute MakeAttribute (Color fore, Color back, bool underline = false)
		{
			var attr = MakeColor ((ConsoleColor)fore, (ConsoleColor)back);
			attr.UnderLine = underline;
			return attr;
		}

		public override void Refresh ()
		{
			UpdateScreen ();

			winConsole.SetInitialCursorVisibility ();
#if false
			var bufferCoords = new WindowsConsole.Coord (){
				X = (short)Clip.Width,
				Y = (short)Clip.Height
			};

			var window = new WindowsConsole.SmallRect (){
				Top = 0,
				Left = 0,
				Right = (short)Clip.Right,
				Bottom = (short)Clip.Bottom
			};

			UpdateCursor();
			winConsole.WriteToConsole (OutputBuffer, bufferCoords, window);
#endif
		}

		public override void UpdateScreen ()
		{
			if (damageRegion.Left == -1)
				return;

			var bufferCoords = new WindowsConsole.Coord () {
				X = (short)Clip.Width,
				Y = (short)Clip.Height
			};

			//var window = new WindowsConsole.SmallRect () {
			//	Top = 0,
			//	Left = 0,
			//	Right = (short)Clip.Right,
			//	Bottom = (short)Clip.Bottom
			//};

			UpdateCursor ();
			winConsole.WriteToConsole (OutputBuffer, bufferCoords, damageRegion);
			//			System.Diagnostics.Debugger.Log(0, "debug", $"Region={damageRegion.Right - damageRegion.Left},{damageRegion.Bottom - damageRegion.Top}\n");
			WindowsConsole.SmallRect.MakeEmpty (ref damageRegion);
		}

		public override void UpdateCursor ()
		{
			var position = new WindowsConsole.Coord () {
				X = (short)ccol,
				Y = (short)crow
			};
			winConsole.SetCursorPosition (position);
		}

		public override void End ()
		{
			winConsole.Cleanup ();
		}

		public override Attribute GetAttribute ()
		{
			return currentAttribute;
		}

		/// <inheritdoc/>
		public override bool GetCursorVisibility (out CursorVisibility visibility)
		{
			return winConsole.GetCursorVisibility (out visibility);
		}

		/// <inheritdoc/>
		public override bool SetCursorVisibility (CursorVisibility visibility)
		{
			return winConsole.SetCursorVisibility (visibility);
		}

		/// <inheritdoc/>
		public override bool EnsureCursorVisibility ()
		{
			return winConsole.EnsureCursorVisibility ();
		}

		#region Unused
		public override void SetColors (ConsoleColor foreground, ConsoleColor background)
		{
		}

		public override void SetColors (short foregroundColorId, short backgroundColorId)
		{
		}

		public override void Suspend ()
		{
		}

		public override void StartReportingMouseMoves ()
		{
		}

		public override void StopReportingMouseMoves ()
		{
		}

		public override void UncookMouse ()
		{
		}

		public override void CookMouse ()
		{
		}

		public override void SendKeys (char keyChar, ConsoleKey key, bool shift, bool alt, bool control)
		{
			WindowsConsole.InputRecord input = new WindowsConsole.InputRecord ();
			input.EventType = WindowsConsole.EventType.Key;

			WindowsConsole.KeyEventRecord keyEvent = new WindowsConsole.KeyEventRecord ();
			keyEvent.bKeyDown = true;
			WindowsConsole.ControlKeyState controlKey = new WindowsConsole.ControlKeyState ();
			if (shift) {
				controlKey |= WindowsConsole.ControlKeyState.ShiftPressed;
				keyEvent.UnicodeChar = '\0';
				keyEvent.wVirtualKeyCode = 16;
			}
			if (alt) {
				controlKey |= WindowsConsole.ControlKeyState.LeftAltPressed;
				controlKey |= WindowsConsole.ControlKeyState.RightAltPressed;
				keyEvent.UnicodeChar = '\0';
				keyEvent.wVirtualKeyCode = 18;
			}
			if (control) {
				controlKey |= WindowsConsole.ControlKeyState.LeftControlPressed;
				controlKey |= WindowsConsole.ControlKeyState.RightControlPressed;
				keyEvent.UnicodeChar = '\0';
				keyEvent.wVirtualKeyCode = 17;
			}
			keyEvent.dwControlKeyState = controlKey;

			input.KeyEvent = keyEvent;

			if (shift || alt || control) {
				ProcessInput (input);
			}

			keyEvent.UnicodeChar = keyChar;
			if ((shift || alt || control)
				&& (key >= ConsoleKey.A && key <= ConsoleKey.Z
				|| key >= ConsoleKey.D0 && key <= ConsoleKey.D9)) {
				keyEvent.wVirtualKeyCode = (ushort)key;
			} else {
				keyEvent.wVirtualKeyCode = '\0';
			}

			input.KeyEvent = keyEvent;

			try {
				ProcessInput (input);
			} catch (OverflowException) { } finally {
				keyEvent.bKeyDown = false;
				input.KeyEvent = keyEvent;
				ProcessInput (input);
			}
		}
		#endregion
	}

	/// <summary>
	/// Mainloop intended to be used with the <see cref="WindowsDriver"/>, and can
	/// only be used on Windows.
	/// </summary>
	/// <remarks>
	/// This implementation is used for WindowsDriver.
	/// </remarks>
	internal class WindowsMainLoop : IMainLoopDriver {
		ManualResetEventSlim eventReady = new ManualResetEventSlim (false);
		ManualResetEventSlim waitForProbe = new ManualResetEventSlim (false);
		//ManualResetEventSlim winChange = new ManualResetEventSlim (false);
		MainLoop mainLoop;
		ConsoleDriver consoleDriver;
		WindowsConsole winConsole;
		//bool winChanged;
		//Size windowSize;
		CancellationTokenSource tokenSource = new CancellationTokenSource ();

		// The records that we keep fetching
		WindowsConsole.InputRecord [] result = new WindowsConsole.InputRecord [1];

		/// <summary>
		/// Invoked when a Key is pressed or released.
		/// </summary>
		public Action<WindowsConsole.InputRecord> ProcessInput;

		/// <summary>
		/// Invoked when the window is changed.
		/// </summary>
		public Action<Size> WinChanged;

		public WindowsMainLoop (ConsoleDriver consoleDriver = null)
		{
			if (consoleDriver == null) {
				throw new ArgumentNullException ("Console driver instance must be provided.");
			}
			this.consoleDriver = consoleDriver;
			winConsole = ((WindowsDriver)consoleDriver).WinConsole;
		}

		void IMainLoopDriver.Setup (MainLoop mainLoop)
		{
			this.mainLoop = mainLoop;
			Task.Run (WindowsInputHandler);
			// Nor working yet.
			//Task.Run (CheckWinChange);
		}

		void WindowsInputHandler ()
		{
			while (true) {
				waitForProbe.Wait ();
				waitForProbe.Reset ();

				result = winConsole.ReadConsoleInput ();

				eventReady.Set ();
			}
		}

		//void CheckWinChange ()
		//{
		//	while (true) {
		//		winChange.Wait ();
		//		winChange.Reset ();
		//		WaitWinChange ();
		//		winChanged = true;
		//		eventReady.Set ();
		//	}
		//}

		//void WaitWinChange ()
		//{
		//	while (true) {
		//		if (!consoleDriver.HeightAsBuffer) {
		//			windowSize = new Size (Console.WindowWidth, Console.WindowHeight);
		//			if (windowSize.Height < consoleDriver.Rows) {
		//				// I still haven't been able to find a way to capture the shrinking in height.
		//				//return;
		//			}
		//		}
		//	}
		//}

		void IMainLoopDriver.Wakeup ()
		{
			//tokenSource.Cancel ();
			eventReady.Set ();
		}

		bool IMainLoopDriver.EventsPending (bool wait)
		{
			if (CheckTimers (wait, out var waitTimeout)) {
				return true;
			}

			result = null;
			waitForProbe.Set ();
			// Nor working yet.
			//winChange.Set ();

			try {
				if (!tokenSource.IsCancellationRequested) {
					eventReady.Wait (waitTimeout, tokenSource.Token);
				}
			} catch (OperationCanceledException) {
				return true;
			} finally {
				eventReady.Reset ();
			}

			if (!tokenSource.IsCancellationRequested) {
				//return result != null || CheckTimers (wait, out _) || winChanged;
				return result != null || CheckTimers (wait, out _);
			}

			tokenSource.Dispose ();
			tokenSource = new CancellationTokenSource ();
			return true;
		}

		bool CheckTimers (bool wait, out int waitTimeout)
		{
			long now = DateTime.UtcNow.Ticks;

			if (mainLoop.timeouts.Count > 0) {
				waitTimeout = (int)((mainLoop.timeouts.Keys [0] - now) / TimeSpan.TicksPerMillisecond);
				if (waitTimeout < 0)
					return true;
			} else {
				waitTimeout = -1;
			}

			if (!wait)
				waitTimeout = 0;

			int ic;
			lock (mainLoop.idleHandlers) {
				ic = mainLoop.idleHandlers.Count;
			}

			return ic > 0;
		}

		void IMainLoopDriver.MainIteration ()
		{
			if (result != null) {
				var inputEvent = result [0];
				result = null;
				ProcessInput?.Invoke (inputEvent);
			}
			//if (winChanged) {
			//	winChanged = false;
			//	WinChanged?.Invoke (windowSize);
			//}
		}
	}
}
