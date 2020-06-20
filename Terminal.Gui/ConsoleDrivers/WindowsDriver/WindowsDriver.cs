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

		public WindowsConsole ()
		{
			InputHandle = Win32Bindings.GetStdHandle (STD_INPUT_HANDLE);
			OutputHandle = Win32Bindings.GetStdHandle (STD_OUTPUT_HANDLE);
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
				ScreenBuffer = Win32Bindings.CreateConsoleScreenBuffer (
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

				if (!Win32Bindings.SetConsoleActiveScreenBuffer (ScreenBuffer)) {
					var err = Marshal.GetLastWin32Error ();
					throw new System.ComponentModel.Win32Exception (err);
				}

				OriginalStdOutChars = new CharInfo [Console.WindowHeight * Console.WindowWidth];

				Win32Bindings.ReadConsoleOutput (OutputHandle, OriginalStdOutChars, coords,
					new Coord { X = 0, Y = 0 }, ref window);
			}

			return Win32Bindings.WriteConsoleOutput (ScreenBuffer, charInfoBuffer, coords, 
				new Coord { X = window.Left, Y = window.Top }, ref window);
		}

		public bool SetCursorPosition (Coord position)
		{
			return Win32Bindings.SetConsoleCursorPosition (ScreenBuffer, position);
		}

		public void Cleanup ()
		{
			ConsoleMode = originalConsoleMode;
			//ContinueListeningForConsoleEvents = false;
			if (!Win32Bindings.SetConsoleActiveScreenBuffer (OutputHandle)) {
				var err = Marshal.GetLastWin32Error ();
				Console.WriteLine ("Error: {0}", err);
			}

			if (ScreenBuffer != IntPtr.Zero) Win32Bindings.CloseHandle (ScreenBuffer);

			ScreenBuffer = IntPtr.Zero;
		}

		//bool ContinueListeningForConsoleEvents = true;

		public uint ConsoleMode {
			get {
				Win32Bindings.GetConsoleMode (InputHandle, out var v);
				return v;
			}

			set {
				Win32Bindings.SetConsoleMode (InputHandle, value);
			}
		}

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

		internal struct WindowBufferSizeRecord {
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
		public enum ShareMode : uint {
			FileShareRead = 1,
			FileShareWrite = 2,
		}

		[Flags]
		public enum DesiredAccess : uint {
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


		internal static IntPtr INVALID_HANDLE_VALUE = new IntPtr (-1);


		public uint InputEventCount {
			get {
				Win32Bindings.GetNumberOfConsoleInputEvents (InputHandle, out var v);
				return v;
			}
		}
		
		public InputRecord [] ReadConsoleInput ()
		{
			const int bufferSize = 1;
			var pRecord = Marshal.AllocHGlobal (Marshal.SizeOf<InputRecord> () * bufferSize);
			try {
				Win32Bindings.ReadConsoleInput (InputHandle, pRecord, bufferSize,
					out var numberEventsRead);

				return numberEventsRead == 0
					? null
					: new [] {Marshal.PtrToStructure<InputRecord> (pRecord)};
			} catch (Exception) {
				return null;
			} finally {
				Marshal.FreeHGlobal (pRecord);
			}
		}

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

	internal class WindowsDriver : ConsoleDriver, IMainLoopDriver {
		static bool sync = false;
		ManualResetEventSlim eventReady = new ManualResetEventSlim (false);
		ManualResetEventSlim waitForProbe = new ManualResetEventSlim (false);
		MainLoop mainLoop;
		WindowsConsole.CharInfo [] OutputBuffer;
		int cols, rows;
		WindowsConsole winConsole;
		WindowsConsole.SmallRect damageRegion;

		public override int Cols => cols;
		public override int Rows => rows;

		public WindowsDriver ()
		{
			winConsole = new WindowsConsole ();

			SetupColorsAndBorders ();

			cols = Console.WindowWidth;
			rows = Console.WindowHeight;
			WindowsConsole.SmallRect.MakeEmpty (ref damageRegion);

			ResizeScreen ();
			UpdateOffScreen ();

			Task.Run ((Action)WindowsInputHandler);
		}

		private void SetupColorsAndBorders ()
		{
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
			Colors.Dialog.Focus = MakeColor (ConsoleColor.Black, ConsoleColor.DarkGray);
			Colors.Dialog.HotNormal = MakeColor (ConsoleColor.DarkBlue, ConsoleColor.Gray);
			Colors.Dialog.HotFocus = MakeColor (ConsoleColor.DarkBlue, ConsoleColor.DarkGray);

			Colors.Error.Normal = MakeColor (ConsoleColor.DarkRed, ConsoleColor.White);
			Colors.Error.Focus = MakeColor (ConsoleColor.White, ConsoleColor.DarkRed);
			Colors.Error.HotNormal = MakeColor (ConsoleColor.Black, ConsoleColor.White);
			Colors.Error.HotFocus = MakeColor (ConsoleColor.Black, ConsoleColor.DarkRed);

			HLine = '\u2500';
			VLine = '\u2502';
			Stipple = '\u2591';
			Diamond = '\u25ca';
			ULCorner = '\u250C';
			LLCorner = '\u2514';
			URCorner = '\u2510';
			LRCorner = '\u2518';
			LeftTee = '\u251c';
			RightTee = '\u2524';
			TopTee = '\u252c';
			BottomTee = '\u2534';
			Checked = '\u221a';
			UnChecked = ' ';
			Selected = '\u25cf';
			UnSelected = '\u25cc';
			RightArrow = '\u25ba';
			LeftArrow = '\u25c4';
			UpArrow = '\u25b2';
			DownArrow = '\u25bc';
			LeftDefaultIndicator = '\u25e6';
			RightDefaultIndicator = '\u25e6';
			LeftBracket = '[';
			RightBracket = ']';
			OnMeterSegment = '\u258c';
			OffMeterSegement = ' ';
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

		// The records that we keep fetching
		WindowsConsole.InputRecord [] result, records = new WindowsConsole.InputRecord [1];

		void WindowsInputHandler ()
		{
			while (true) {
				waitForProbe.Wait ();
				waitForProbe.Reset ();

				result = winConsole.ReadConsoleInput();

				eventReady.Set ();
			}
		}

		void IMainLoopDriver.Setup (MainLoop mainLoop)
		{
			this.mainLoop = mainLoop;
		}

		void IMainLoopDriver.Wakeup ()
		{
			tokenSource.Cancel ();
			//eventReady.Reset ();
			//eventReady.Set ();
		}

		bool IMainLoopDriver.EventsPending (bool wait)
		{
			int waitTimeout = 0;

			if (CkeckTimeout (wait, ref waitTimeout))
				return true;

			result = null;
			waitForProbe.Set ();

			try {
				while (result == null) {
					if (!tokenSource.IsCancellationRequested)
						eventReady.Wait (0, tokenSource.Token);
					if (result != null) {
						break;
					}
					if (mainLoop.idleHandlers.Count > 0 || CkeckTimeout (wait, ref waitTimeout)) {
						return true;
					}
				}
			} catch (OperationCanceledException) {
				return true;
			} finally {
				eventReady.Reset ();
			}

			if (!tokenSource.IsCancellationRequested)
				return result != null;

			tokenSource.Dispose ();
			tokenSource = new CancellationTokenSource ();
			return true;
		}

		bool CkeckTimeout (bool wait, ref int waitTimeout)
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

			return false;
		}

		Action<KeyEvent> keyHandler;
		Action<KeyEvent> keyDownHandler;
		Action<KeyEvent> keyUpHandler;
		Action<MouseEvent> mouseHandler;

		public override void PrepareToRun (MainLoop mainLoop, Action<KeyEvent> keyHandler, Action<KeyEvent> keyDownHandler, Action<KeyEvent> keyUpHandler, Action<MouseEvent> mouseHandler)
		{
			this.keyHandler = keyHandler;
			this.keyDownHandler = keyDownHandler;
			this.keyUpHandler = keyUpHandler;
			this.mouseHandler = mouseHandler;
		}

		void IMainLoopDriver.MainIteration ()
		{
			if (result == null)
				return;

			var inputEvent = result [0];
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

			default:
				break;
			}
			result = null;
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
				  IsButtonReleased) {
				var p = new Point () {
					X = mouseEvent.MousePosition.X,
					Y = mouseEvent.MousePosition.Y
				};
				//if (p == point) {
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
				//} else {
				//	mouseFlag = 0;
				//}
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
				await Task.Delay (200);
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

		public ConsoleKeyInfoEx ToConsoleKeyInfoEx (WindowsConsole.KeyEventRecord keyEvent)
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
			return new ConsoleKeyInfoEx (ConsoleKeyInfo, capslock, numlock);
		}

		public Key MapKey (ConsoleKeyInfoEx keyInfoEx)
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
				return MapKeyModifiers (keyInfo, Key.Space);
			case ConsoleKey.Backspace:
				return MapKeyModifiers (keyInfo, Key.Backspace);
			case ConsoleKey.Delete:
				return MapKeyModifiers (keyInfo, Key.DeleteChar);
			case ConsoleKey.Insert:
				return MapKeyModifiers (keyInfo, Key.InsertChar);

			case ConsoleKey.NumPad0:
				return keyInfoEx.NumLock ? (Key)(uint)'0' : Key.InsertChar;
			case ConsoleKey.NumPad1:
				return keyInfoEx.NumLock ? (Key)(uint)'1' : Key.End;
			case ConsoleKey.NumPad2:
				return keyInfoEx.NumLock ? (Key)(uint)'2' : Key.CursorDown;
			case ConsoleKey.NumPad3:
				return keyInfoEx.NumLock ? (Key)(uint)'3' : Key.PageDown;
			case ConsoleKey.NumPad4:
				return keyInfoEx.NumLock ? (Key)(uint)'4' : Key.CursorLeft;
			case ConsoleKey.NumPad5:
				return keyInfoEx.NumLock ? (Key)(uint)'5' : (Key)((uint)keyInfo.KeyChar);
			case ConsoleKey.NumPad6:
				return keyInfoEx.NumLock ? (Key)(uint)'6' : Key.CursorRight;
			case ConsoleKey.NumPad7:
				return keyInfoEx.NumLock ? (Key)(uint)'7' : Key.Home;
			case ConsoleKey.NumPad8:
				return keyInfoEx.NumLock ? (Key)(uint)'8' : Key.CursorUp;
			case ConsoleKey.NumPad9:
				return keyInfoEx.NumLock ? (Key)(uint)'9' : Key.PageUp;

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
				if (keyInfo.Modifiers == ConsoleModifiers.Control)
					return (Key)((uint)Key.ControlA + delta);
				if (keyInfo.Modifiers == ConsoleModifiers.Alt)
					return (Key)(((uint)Key.AltMask) | ((uint)'A' + delta));
				if ((keyInfo.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) != 0) {
					if (keyInfo.KeyChar == 0)
						return (Key)(((uint)Key.AltMask) + ((uint)Key.ControlA + delta));
					else
						return (Key)((uint)keyInfo.KeyChar);
				}
				//return (Key)((uint)alphaBase + delta);
				return (Key)((uint)keyInfo.KeyChar);
			}
			if (key >= ConsoleKey.D0 && key <= ConsoleKey.D9) {
				var delta = key - ConsoleKey.D0;
				if (keyInfo.Modifiers == ConsoleModifiers.Alt)
					return (Key)(((uint)Key.AltMask) | ((uint)'0' + delta));

				return (Key)((uint)keyInfo.KeyChar);
			}
			if (key >= ConsoleKey.F1 && key <= ConsoleKey.F12) {
				var delta = key - ConsoleKey.F1;

				return (Key)((int)Key.F1 + delta);
			}

			return (Key)(0xffffffff);
		}

		private Key MapKeyModifiers (ConsoleKeyInfo keyInfo, Key key)
		{
			Key keyMod = new Key ();
			if (keyInfo.Modifiers.HasFlag (ConsoleModifiers.Shift))
				keyMod = Key.ShiftMask;
			if (keyInfo.Modifiers.HasFlag (ConsoleModifiers.Control))
				keyMod |= Key.CtrlMask;
			if (keyInfo.Modifiers.HasFlag (ConsoleModifiers.Alt))
				keyMod |= Key.AltMask;

			return keyMod != Key.ControlSpace ? keyMod | key : key;
		}

		public override void Init (Action terminalResized)
		{
			TerminalResized = terminalResized;
			SetupColorsAndBorders ();
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
		}

		void UpdateOffScreen ()
		{
			for (int row = 0; row < rows; row++)
				for (int col = 0; col < cols; col++) {
					int position = row * cols + col;
					OutputBuffer [position].Attributes = (ushort)Colors.TopLevel.Normal;
					OutputBuffer [position].Char.UnicodeChar = ' ';
				}
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
		CancellationTokenSource tokenSource = new CancellationTokenSource ();

		public override void SetAttribute (Attribute c)
		{
			currentAttribute = c.value;
		}

		Attribute MakeColor (ConsoleColor f, ConsoleColor b)
		{
			// Encode the colors into the int value.
			return new Attribute () {
				value = ((int)f | (int)b << 4),
				foreground = (Color)f,
				background = (Color)b
			};
		}

		public override Attribute MakeAttribute (Color fore, Color back)
		{
			return MakeColor ((ConsoleColor)fore, (ConsoleColor)back);
		}

		public override void Refresh ()
		{
			UpdateScreen ();
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

			var window = new WindowsConsole.SmallRect () {
				Top = 0,
				Left = 0,
				Right = (short)Clip.Right,
				Bottom = (short)Clip.Bottom
			};

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
		#endregion

	}

}
