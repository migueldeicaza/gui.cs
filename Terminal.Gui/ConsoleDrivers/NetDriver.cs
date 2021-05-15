//#define PROCESS_REQUEST
//
// NetDriver.cs: The System.Console-based .NET driver, works on Windows and Unix, but is not particularly efficient.
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NStack;

namespace Terminal.Gui {
	internal class NetWinVTConsole {
		IntPtr InputHandle, OutputHandle, ErrorHandle;
		uint originalInputConsoleMode, originalOutputConsoleMode, originalErrorConsoleMode;

		public NetWinVTConsole ()
		{
			InputHandle = GetStdHandle (STD_INPUT_HANDLE);
			if (!GetConsoleMode (InputHandle, out uint mode)) {
				throw new ApplicationException ($"Failed to get input console mode, error code: {GetLastError ()}.");
			}
			originalInputConsoleMode = mode;
			if ((mode & ENABLE_VIRTUAL_TERMINAL_INPUT) < ENABLE_VIRTUAL_TERMINAL_INPUT) {
				mode |= ENABLE_VIRTUAL_TERMINAL_INPUT;
				if (!SetConsoleMode (InputHandle, mode)) {
					throw new ApplicationException ($"Failed to set input console mode, error code: {GetLastError ()}.");
				}
			}

			OutputHandle = GetStdHandle (STD_OUTPUT_HANDLE);
			if (!GetConsoleMode (OutputHandle, out mode)) {
				throw new ApplicationException ($"Failed to get output console mode, error code: {GetLastError ()}.");
			}
			originalOutputConsoleMode = mode;
			if ((mode & (ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN)) < DISABLE_NEWLINE_AUTO_RETURN) {
				mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
				if (!SetConsoleMode (OutputHandle, mode)) {
					throw new ApplicationException ($"Failed to set output console mode, error code: {GetLastError ()}.");
				}
			}

			ErrorHandle = GetStdHandle (STD_ERROR_HANDLE);
			if (!GetConsoleMode (ErrorHandle, out mode)) {
				throw new ApplicationException ($"Failed to get error console mode, error code: {GetLastError ()}.");
			}
			originalErrorConsoleMode = mode;
			if ((mode & (DISABLE_NEWLINE_AUTO_RETURN)) < DISABLE_NEWLINE_AUTO_RETURN) {
				mode |= DISABLE_NEWLINE_AUTO_RETURN;
				if (!SetConsoleMode (ErrorHandle, mode)) {
					throw new ApplicationException ($"Failed to set error console mode, error code: {GetLastError ()}.");
				}
			}
		}

		public void Cleanup ()
		{
			if (!SetConsoleMode (InputHandle, originalInputConsoleMode)) {
				throw new ApplicationException ($"Failed to restore input console mode, error code: {GetLastError ()}.");
			}
			if (!SetConsoleMode (OutputHandle, originalOutputConsoleMode)) {
				throw new ApplicationException ($"Failed to restore output console mode, error code: {GetLastError ()}.");
			}
			if (!SetConsoleMode (ErrorHandle, originalErrorConsoleMode)) {
				throw new ApplicationException ($"Failed to restore error console mode, error code: {GetLastError ()}.");
			}
		}

		const int STD_INPUT_HANDLE = -10;
		internal const int STD_OUTPUT_HANDLE = -11;
		const int STD_ERROR_HANDLE = -12;

		// Input modes.
		const uint ENABLE_PROCESSED_INPUT = 1;
		const uint ENABLE_LINE_INPUT = 2;
		const uint ENABLE_ECHO_INPUT = 4;
		const uint ENABLE_WINDOW_INPUT = 8;
		const uint ENABLE_MOUSE_INPUT = 16;
		const uint ENABLE_INSERT_MODE = 32;
		const uint ENABLE_QUICK_EDIT_MODE = 64;
		const uint ENABLE_EXTENDED_FLAGS = 128;
		const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 512;

		// Output modes.
		const uint ENABLE_PROCESSED_OUTPUT = 1;
		const uint ENABLE_WRAP_AT_EOL_OUTPUT = 2;
		const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 4;
		const uint DISABLE_NEWLINE_AUTO_RETURN = 8;
		const uint ENABLE_LVB_GRID_WORLDWIDE = 10;

		[DllImport ("kernel32.dll", SetLastError = true)]
		internal static extern IntPtr GetStdHandle (int nStdHandle);

		[DllImport ("kernel32.dll")]
		static extern bool GetConsoleMode (IntPtr hConsoleHandle, out uint lpMode);

		[DllImport ("kernel32.dll")]
		static extern bool SetConsoleMode (IntPtr hConsoleHandle, uint dwMode);

		[DllImport ("kernel32.dll")]
		static extern uint GetLastError ();

		[DllImport ("kernel32.dll")]
		internal static extern bool SetConsoleTextAttribute (IntPtr hConsoleOutput, ushort attributes);

		[DllImport ("kernel32.dll")]
		internal static extern bool GetConsoleScreenBufferInfo (IntPtr hConsoleOutput, out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);
	}

	struct COORD {
		internal short X;
		internal short Y;
	}

	struct SMALL_RECT {
		internal short Left;
		internal short Top;
		internal short Right;
		internal short Bottom;
	}

	[StructLayoutAttribute (LayoutKind.Sequential)]
	struct CONSOLE_SCREEN_BUFFER_INFO {
		internal COORD dwSize;
		internal COORD dwCursorPosition;
		internal ushort wAttributes;
		internal SMALL_RECT srWindow;
		internal COORD dwMaximumWindowSize;
	}

	internal class NetEvents {
		ManualResetEventSlim inputReady = new ManualResetEventSlim (false);
		ManualResetEventSlim waitForStart = new ManualResetEventSlim (false);
		ManualResetEventSlim winChange = new ManualResetEventSlim (false);
		Queue<InputResult?> inputResultQueue = new Queue<InputResult?> ();
		ConsoleDriver consoleDriver;
		int lastWindowHeight;
		int largestWindowHeight;
#if PROCESS_REQUEST
		bool neededProcessRequest;
#endif
		public int NumberOfCSI { get; }

		public NetEvents (ConsoleDriver consoleDriver, int numberOfCSI = 1)
		{
			if (consoleDriver == null) {
				throw new ArgumentNullException ("Console driver instance must be provided.");
			}
			this.consoleDriver = consoleDriver;
			NumberOfCSI = numberOfCSI;
			Task.Run (ProcessInputResultQueue);
			Task.Run (CheckWinChange);
		}

		public InputResult? ReadConsoleInput ()
		{
			while (true) {
				waitForStart.Set ();
				winChange.Set ();

				if (inputResultQueue.Count == 0) {
					inputReady.Wait ();
					inputReady.Reset ();
				}
#if PROCESS_REQUEST
				neededProcessRequest = false;
#endif
				if (inputResultQueue.Count > 0) {
					return inputResultQueue.Dequeue ();
				}
			}
		}

		void ProcessInputResultQueue ()
		{
			while (true) {
				waitForStart.Wait ();
				waitForStart.Reset ();

				if (inputResultQueue.Count == 0) {
					GetConsoleInputType (Console.ReadKey (true));
				}

				inputReady.Set ();
			}
		}

		void CheckWinChange ()
		{
			while (true) {
				winChange.Wait ();
				winChange.Reset ();
				WaitWinChange ();
				inputReady.Set ();
			}
		}

		void WaitWinChange ()
		{
			while (true) {
				if (!consoleDriver.HeightAsBuffer) {
					if (Console.WindowWidth != consoleDriver.Cols || Console.WindowHeight != consoleDriver.Rows) {
						var w = Math.Max (Console.WindowWidth, 0);
						var h = Math.Max (Console.WindowHeight, 0);
						GetWindowSizeEvent (new Size (w, h));
						return;
					}
				} else {
					largestWindowHeight = Math.Max (Console.BufferHeight, largestWindowHeight);
					if (Console.BufferWidth != consoleDriver.Cols || largestWindowHeight != consoleDriver.Rows
						|| Console.WindowHeight != lastWindowHeight) {
						lastWindowHeight = Console.WindowHeight;
						GetWindowSizeEvent (new Size (Console.BufferWidth, lastWindowHeight));
						return;
					}
					if (Console.WindowTop != consoleDriver.Top) {
						// Top only working on Windows.
						var winPositionEv = new WindowPositionEvent () {
							Top = Console.WindowTop
						};
						inputResultQueue.Enqueue (new InputResult () {
							EventType = EventType.WindowPosition,
							WindowPositionEvent = winPositionEv
						});
						return;
					}
#if PROCESS_REQUEST
					if (!neededProcessRequest) {
						Console.Out.Write ("\x1b[6n");
						neededProcessRequest = true;
					}
#endif
				}
			}
		}

		void GetWindowSizeEvent (Size size)
		{
			WindowSizeEvent windowSizeEvent = new WindowSizeEvent () {
				Size = size
			};

			inputResultQueue.Enqueue (new InputResult () {
				EventType = EventType.WindowSize,
				WindowSizeEvent = windowSizeEvent
			});
		}

		void GetConsoleInputType (ConsoleKeyInfo consoleKeyInfo)
		{
			InputResult inputResult = new InputResult {
				EventType = EventType.Key
			};
			ConsoleKeyInfo newConsoleKeyInfo = consoleKeyInfo;
			ConsoleKey key = 0;
			MouseEvent mouseEvent = new MouseEvent ();
			var keyChar = consoleKeyInfo.KeyChar;
			switch ((uint)keyChar) {
			case 0:
				if (consoleKeyInfo.Key == (ConsoleKey)64) {    // Ctrl+Space in Windows.
					newConsoleKeyInfo = new ConsoleKeyInfo (' ', ConsoleKey.Spacebar,
						(consoleKeyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
						(consoleKeyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
						(consoleKeyInfo.Modifiers & ConsoleModifiers.Control) != 0);
				}
				break;
			case uint n when (n >= '\u0001' && n <= '\u001a'):
				if (consoleKeyInfo.Key == 0) {
					key = (ConsoleKey)(char)(consoleKeyInfo.KeyChar + (uint)ConsoleKey.A - 1);
					newConsoleKeyInfo = new ConsoleKeyInfo ((char)key,
						key,
						(consoleKeyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
						(consoleKeyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
						true);
				}
				break;
			case 27:
			case 91:
				ConsoleKeyInfo [] cki = new ConsoleKeyInfo [] { consoleKeyInfo };
				ConsoleModifiers mod = consoleKeyInfo.Modifiers;
				int delay = 0;
				while (delay < 100) {
					if (Console.KeyAvailable) {
						do {
							var result = Console.ReadKey (true);
							Array.Resize (ref cki, cki == null ? 1 : cki.Length + 1);
							cki [cki.Length - 1] = result;
						} while (Console.KeyAvailable);
						break;
					}
					Thread.Sleep (50);
					delay += 50;
				}
				SplitCSI (cki, ref inputResult, ref newConsoleKeyInfo, ref key, ref mouseEvent, ref mod);
				return;
			case 127:
				newConsoleKeyInfo = new ConsoleKeyInfo (consoleKeyInfo.KeyChar, ConsoleKey.Backspace,
					(consoleKeyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
					(consoleKeyInfo.Modifiers & ConsoleModifiers.Alt) != 0,
					(consoleKeyInfo.Modifiers & ConsoleModifiers.Control) != 0);
				break;
			default:
				newConsoleKeyInfo = consoleKeyInfo;
				break;
			}
			if (inputResult.EventType == EventType.Key) {
				inputResult.ConsoleKeyInfo = newConsoleKeyInfo;
			} else {
				inputResult.MouseEvent = mouseEvent;
			}

			inputResultQueue.Enqueue (inputResult);
		}

		void SplitCSI (ConsoleKeyInfo [] cki, ref InputResult inputResult, ref ConsoleKeyInfo newConsoleKeyInfo, ref ConsoleKey key, ref MouseEvent mouseEvent, ref ConsoleModifiers mod)
		{
			ConsoleKeyInfo [] splitedCki = new ConsoleKeyInfo [] { };
			int length = 0;
			var kChar = GetKeyCharArray (cki);
			var nCSI = GetNumberOfCSI (kChar);
			int curCSI = 0;
			char previousKChar = '\0';
			if (nCSI > 1) {
				for (int i = 0; i < cki.Length; i++) {
					var ck = cki [i];
					if (NumberOfCSI > 0 && nCSI - curCSI > NumberOfCSI) {
						if (cki [i + 1].KeyChar == '\x1b' && previousKChar != '\0') {
							curCSI++;
							previousKChar = '\0';
						} else {
							previousKChar = ck.KeyChar;
						}
						continue;
					}
					if (ck.KeyChar == '\x1b') {
						if (ck.KeyChar == 'R') {
							ResizeArray (ck);
						}
						if (splitedCki.Length > 1) {
							DecodeCSI (ref inputResult, ref newConsoleKeyInfo, ref key, ref mouseEvent, splitedCki, ref mod);
						}
						splitedCki = new ConsoleKeyInfo [] { };
						length = 0;
					}
					ResizeArray (ck);
					if (i == cki.Length - 1 && splitedCki.Length > 0) {
						DecodeCSI (ref inputResult, ref newConsoleKeyInfo, ref key, ref mouseEvent, splitedCki, ref mod);
					}
				}
			} else {
				DecodeCSI (ref inputResult, ref newConsoleKeyInfo, ref key, ref mouseEvent, cki, ref mod);
			}

			void ResizeArray (ConsoleKeyInfo ck)
			{
				length++;
				Array.Resize (ref splitedCki, length);
				splitedCki [length - 1] = ck;
			}
		}

		char [] GetKeyCharArray (ConsoleKeyInfo [] cki)
		{
			char [] kChar = new char [] { };
			var length = 0;
			foreach (var kc in cki) {
				length++;
				Array.Resize (ref kChar, length);
				kChar [length - 1] = kc.KeyChar;
			}

			return kChar;
		}

		int GetNumberOfCSI (char [] csi)
		{
			int nCSI = 0;
			for (int i = 0; i < csi.Length; i++) {
				if (csi [i] == '\x1b' || (csi [i] == '[' && (i == 0 || (i > 0 && csi [i - 1] != '\x1b')))) {
					nCSI++;
				}
			}

			return nCSI;
		}

		void DecodeCSI (ref InputResult inputResult, ref ConsoleKeyInfo newConsoleKeyInfo, ref ConsoleKey key, ref MouseEvent mouseEvent, ConsoleKeyInfo [] cki, ref ConsoleModifiers mod)
		{
			switch (cki.Length) {
			case 2:
				if ((uint)cki [1].KeyChar >= 1 && (uint)cki [1].KeyChar <= 26) {
					key = (ConsoleKey)(char)(cki [1].KeyChar + (uint)ConsoleKey.A - 1);
					newConsoleKeyInfo = new ConsoleKeyInfo (cki [1].KeyChar,
						key,
						false,
						true,
						true);
				} else {
					if (cki [1].KeyChar >= 97 && cki [1].KeyChar <= 122) {
						key = (ConsoleKey)cki [1].KeyChar.ToString ().ToUpper () [0];
					} else {
						key = (ConsoleKey)cki [1].KeyChar;
					}
					newConsoleKeyInfo = new ConsoleKeyInfo ((char)key,
						(ConsoleKey)Math.Min ((uint)key, 255),
						false,
						true,
						false);
				}
				break;
			case 3:
				if (cki [1].KeyChar == '[' || cki [1].KeyChar == 79) {
					key = GetConsoleKey (cki [2].KeyChar, ref mod, cki.Length);
				}
				newConsoleKeyInfo = new ConsoleKeyInfo ('\0',
					key,
					(mod & ConsoleModifiers.Shift) != 0,
					(mod & ConsoleModifiers.Alt) != 0,
					(mod & ConsoleModifiers.Control) != 0);
				break;
			case 4:
				if (cki [1].KeyChar == '[' && cki [3].KeyChar == 126) {
					key = GetConsoleKey (cki [2].KeyChar, ref mod, cki.Length);
					newConsoleKeyInfo = new ConsoleKeyInfo ('\0',
						key,
						(mod & ConsoleModifiers.Shift) != 0,
						(mod & ConsoleModifiers.Alt) != 0,
						(mod & ConsoleModifiers.Control) != 0);
				}
				break;
			case 5:
				if (cki [1].KeyChar == '[' && (cki [2].KeyChar == 49 || cki [2].KeyChar == 50)
					&& cki [4].KeyChar == 126) {
					key = GetConsoleKey (cki [3].KeyChar, ref mod, cki.Length);
				} else if (cki [1].KeyChar == 49 && cki [2].KeyChar == ';') { // For WSL
					mod |= GetConsoleModifiers (cki [3].KeyChar);
					key = ConsoleKey.End;
				}
				newConsoleKeyInfo = new ConsoleKeyInfo ('\0',
					key,
					(mod & ConsoleModifiers.Shift) != 0,
					(mod & ConsoleModifiers.Alt) != 0,
					(mod & ConsoleModifiers.Control) != 0);
				break;
			case 6:
				if (cki [1].KeyChar == '[' && cki [2].KeyChar == 49 && cki [3].KeyChar == ';') {
					mod |= GetConsoleModifiers (cki [4].KeyChar);
					key = GetConsoleKey (cki [5].KeyChar, ref mod, cki.Length);
				} else if (cki [1].KeyChar == '[' && cki [3].KeyChar == ';') {
					mod |= GetConsoleModifiers (cki [4].KeyChar);
					key = GetConsoleKey (cki [2].KeyChar, ref mod, cki.Length);
				}
				newConsoleKeyInfo = new ConsoleKeyInfo ('\0',
					key,
					(mod & ConsoleModifiers.Shift) != 0,
					(mod & ConsoleModifiers.Alt) != 0,
					(mod & ConsoleModifiers.Control) != 0);
				break;
			case 7:
				GetRequestEvent (GetKeyCharArray (cki));
				return;
			case int n when n >= 8:
				GetMouseEvent (cki);
				return;
			}
			if (inputResult.EventType == EventType.Key) {
				inputResult.ConsoleKeyInfo = newConsoleKeyInfo;
			} else {
				inputResult.MouseEvent = mouseEvent;
			}

			inputResultQueue.Enqueue (inputResult);
		}

		Point lastCursorPosition;

		void GetRequestEvent (char [] kChar)
		{
			EventType eventType = new EventType ();
			Point point = new Point ();
			int foundPoint = 0;
			string value = "";
			for (int i = 0; i < kChar.Length; i++) {
				var c = kChar [i];
				if (c == '[') {
					foundPoint++;
				} else if (foundPoint == 1 && c != ';') {
					value += c.ToString ();
				} else if (c == ';') {
					if (foundPoint == 1) {
						point.Y = int.Parse (value) - 1;
					}
					value = "";
					foundPoint++;
				} else if (foundPoint > 0 && i < kChar.Length - 1) {
					value += c.ToString ();
				} else if (i == kChar.Length - 1) {
					point.X = int.Parse (value) + Console.WindowTop - 1;

					switch (c) {
					case 'R':
						if (lastCursorPosition.Y != point.Y) {
							lastCursorPosition = point;
							eventType = EventType.WindowPosition;
							var winPositionEv = new WindowPositionEvent () {
								CursorPosition = point
							};
							inputResultQueue.Enqueue (new InputResult () {
								EventType = eventType,
								WindowPositionEvent = winPositionEv
							});
						} else {
							return;
						}
						break;
					default:
						throw new NotImplementedException ();
					}
				}
			}

			inputReady.Set ();
		}

		MouseEvent lastMouseEvent;
		bool isButtonPressed;
		bool isButtonClicked;
		bool isButtonDoubleClicked;
		bool isButtonTripleClicked;
		bool isProcContBtnPressedRuning;
		bool isButtonReleased;

		void GetMouseEvent (ConsoleKeyInfo [] cki)
		{
			MouseEvent mouseEvent = new MouseEvent ();
			MouseButtonState buttonState = 0;
			Point point = new Point ();
			int buttonCode = 0;
			bool foundButtonCode = false;
			int foundPoint = 0;
			string value = "";
			var kChar = GetKeyCharArray (cki);
			for (int i = 0; i < kChar.Length; i++) {
				var c = kChar [i];
				if (c == '<') {
					foundButtonCode = true;
				} else if (foundButtonCode && c != ';') {
					value += c.ToString ();
				} else if (c == ';') {
					if (foundButtonCode) {
						foundButtonCode = false;
						buttonCode = int.Parse (value);
					}
					if (foundPoint == 1) {
						point.X = int.Parse (value) - 1;
					}
					value = "";
					foundPoint++;
				} else if (foundPoint > 0 && c != 'm' && c != 'M') {
					value += c.ToString ();
				} else if (c == 'm' || c == 'M') {
					point.Y = int.Parse (value) + Console.WindowTop - 1;

					//if (c == 'M') {
					//	isButtonPressed = true;
					//} else if (c == 'm') {
					//	isButtonPressed = false;
					//}

					switch (buttonCode) {
					case 0:
					case 8:
					case 16:
					case 24:
					case 32:
					case 36:
					case 40:
					case 48:
					case 56:
						buttonState = c == 'M' ? MouseButtonState.Button1Pressed
							: MouseButtonState.Button1Released;
						break;
					case 1:
					case 9:
					case 17:
					case 25:
					case 33:
					case 37:
					case 41:
					case 45:
					case 49:
					case 53:
					case 57:
					case 61:
						buttonState = c == 'M' ? MouseButtonState.Button2Pressed
							: MouseButtonState.Button2Released;
						break;
					case 2:
					case 10:
					case 14:
					case 18:
					case 22:
					case 26:
					case 30:
					case 34:
					case 42:
					case 46:
					case 50:
					case 54:
					case 58:
					case 62:
						buttonState = c == 'M' ? MouseButtonState.Button3Pressed
							: MouseButtonState.Button3Released;
						break;
					case 35:
					case 39:
					case 43:
					case 47:
					case 55:
					case 59:
					case 63:
						buttonState = MouseButtonState.ReportMousePosition;
						break;
					case 64:
						buttonState = MouseButtonState.ButtonWheeledUp;
						break;
					case 65:
						buttonState = MouseButtonState.ButtonWheeledDown;
						break;
					case 68:
					case 72:
					case 80:
						buttonState = MouseButtonState.ButtonWheeledLeft;       // Shift/Ctrl+ButtonWheeledUp
						break;
					case 69:
					case 73:
					case 81:
						buttonState = MouseButtonState.ButtonWheeledRight;      // Shift/Ctrl+ButtonWheeledDown
						break;
					}
					// Modifiers.
					switch (buttonCode) {
					case 8:
					case 9:
					case 10:
					case 43:
						buttonState |= MouseButtonState.ButtonAlt;
						break;
					case 14:
					case 47:
						buttonState |= MouseButtonState.ButtonAlt | MouseButtonState.ButtonShift;
						break;
					case 16:
					case 17:
					case 18:
					case 51:
						buttonState |= MouseButtonState.ButtonCtrl;
						break;
					case 22:
					case 55:
						buttonState |= MouseButtonState.ButtonCtrl | MouseButtonState.ButtonShift;
						break;
					case 24:
					case 25:
					case 26:
					case 59:
						buttonState |= MouseButtonState.ButtonAlt | MouseButtonState.ButtonCtrl;
						break;
					case 30:
					case 63:
						buttonState |= MouseButtonState.ButtonCtrl | MouseButtonState.ButtonShift | MouseButtonState.ButtonAlt;
						break;
					case 32:
					case 33:
					case 34:
						buttonState |= MouseButtonState.ReportMousePosition;
						break;
					case 36:
					case 37:
						buttonState |= MouseButtonState.ReportMousePosition | MouseButtonState.ButtonShift;
						break;
					case 39:
					case 68:
					case 69:
						buttonState |= MouseButtonState.ButtonShift;
						break;
					case 40:
					case 41:
					case 42:
						buttonState |= MouseButtonState.ReportMousePosition | MouseButtonState.ButtonAlt;
						break;
					case 45:
					case 46:
						buttonState |= MouseButtonState.ReportMousePosition | MouseButtonState.ButtonAlt | MouseButtonState.ButtonShift;
						break;
					case 48:
					case 49:
					case 50:
						buttonState |= MouseButtonState.ReportMousePosition | MouseButtonState.ButtonCtrl;
						break;
					case 53:
					case 54:
						buttonState |= MouseButtonState.ReportMousePosition | MouseButtonState.ButtonCtrl | MouseButtonState.ButtonShift;
						break;
					case 56:
					case 57:
					case 58:
						buttonState |= MouseButtonState.ReportMousePosition | MouseButtonState.ButtonCtrl | MouseButtonState.ButtonAlt;
						break;
					case 61:
					case 62:
						buttonState |= MouseButtonState.ReportMousePosition | MouseButtonState.ButtonCtrl | MouseButtonState.ButtonShift | MouseButtonState.ButtonAlt;
						break;
					}
				}
			}
			mouseEvent.Position.X = point.X;
			mouseEvent.Position.Y = point.Y;
			mouseEvent.ButtonState = buttonState;

			if ((buttonState & MouseButtonState.Button1Pressed) != 0
				|| (buttonState & MouseButtonState.Button2Pressed) != 0
				|| (buttonState & MouseButtonState.Button3Pressed) != 0) {
				isButtonPressed = true;
			} else {
				isButtonPressed = false;
			}

			if ((isButtonClicked || isButtonDoubleClicked || isButtonTripleClicked)
				&& ((buttonState & MouseButtonState.Button1Released) != 0
				|| (buttonState & MouseButtonState.Button2Released) != 0
				|| (buttonState & MouseButtonState.Button3Released) != 0)) {
				isButtonTripleClicked = false;
				return;
			}

			if (isButtonClicked && !isButtonDoubleClicked && lastMouseEvent.Position != default && lastMouseEvent.Position == point
				&& ((buttonState & MouseButtonState.Button1Pressed) != 0
				|| (buttonState & MouseButtonState.Button2Pressed) != 0
				|| (buttonState & MouseButtonState.Button3Pressed) != 0)) {
				isButtonDoubleClicked = true;
				ProcessButtonDoubleClicked (mouseEvent);
				Application.MainLoop.AddIdle (() => {
					Task.Run (async () => {
						await Task.Delay (300);
						isButtonDoubleClicked = false;
					});
					return false;
				});
				inputReady.Set ();
				return;
			}
			if (isButtonDoubleClicked && lastMouseEvent.Position != default && lastMouseEvent.Position == point
				&& ((buttonState & MouseButtonState.Button1Pressed) != 0
				|| (buttonState & MouseButtonState.Button2Pressed) != 0
				|| (buttonState & MouseButtonState.Button3Pressed) != 0)) {
				isButtonTripleClicked = true;
				ProcessButtonTripleClicked (mouseEvent);
				inputReady.Set ();
				return;
			}

			if (!isButtonPressed && !isButtonClicked && !isButtonDoubleClicked && !isButtonTripleClicked
				&& !isButtonReleased
				&& ((buttonState & MouseButtonState.Button1Released) == 0
				&& (buttonState & MouseButtonState.Button2Released) == 0
				&& (buttonState & MouseButtonState.Button3Released) == 0)) {
				ProcessButtonReleased (lastMouseEvent);
				inputReady.Set ();
				return;
			}

			inputResultQueue.Enqueue (new InputResult () {
				EventType = EventType.Mouse,
				MouseEvent = mouseEvent
			});

			if (!isButtonClicked && !lastMouseEvent.ButtonState.HasFlag (MouseButtonState.ReportMousePosition)
				&& lastMouseEvent.Position != default && lastMouseEvent.Position == point
				&& ((buttonState & MouseButtonState.Button1Released) != 0
				|| (buttonState & MouseButtonState.Button2Released) != 0
				|| (buttonState & MouseButtonState.Button3Released) != 0)) {
				isButtonClicked = true;
				ProcessButtonClicked (mouseEvent);
				Application.MainLoop.AddIdle (() => {
					Task.Run (async () => {
						await Task.Delay (300);
						isButtonClicked = false;
					});
					return false;
				});
				inputReady.Set ();
				return;
			}

			lastMouseEvent = mouseEvent;
			if (isButtonPressed && !isButtonClicked && !isButtonDoubleClicked && !isButtonTripleClicked && !isProcContBtnPressedRuning) {
				isButtonReleased = false;
				Application.MainLoop.AddIdle (() => {
					ProcessContinuousButtonPressedAsync ().ConfigureAwait (false);
					return false;
				});
			}

			inputReady.Set ();
		}

		void ProcessButtonClicked (MouseEvent mouseEvent)
		{
			var me = new MouseEvent () {
				Position = mouseEvent.Position,
				ButtonState = mouseEvent.ButtonState
			};
			if ((mouseEvent.ButtonState & MouseButtonState.Button1Released) != 0) {
				me.ButtonState &= ~MouseButtonState.Button1Released;
				me.ButtonState |= MouseButtonState.Button1Clicked;
			} else if ((mouseEvent.ButtonState & MouseButtonState.Button2Released) != 0) {
				me.ButtonState &= ~MouseButtonState.Button2Released;
				me.ButtonState |= MouseButtonState.Button2Clicked;
			} else if ((mouseEvent.ButtonState & MouseButtonState.Button3Released) != 0) {
				me.ButtonState &= ~MouseButtonState.Button3Released;
				me.ButtonState |= MouseButtonState.Button3Clicked;
			}
			isButtonReleased = true;

			inputResultQueue.Enqueue (new InputResult () {
				EventType = EventType.Mouse,
				MouseEvent = me
			});
		}

		void ProcessButtonDoubleClicked (MouseEvent mouseEvent)
		{
			var me = new MouseEvent () {
				Position = mouseEvent.Position,
				ButtonState = mouseEvent.ButtonState
			};
			if ((mouseEvent.ButtonState & MouseButtonState.Button1Pressed) != 0) {
				me.ButtonState &= ~MouseButtonState.Button1Pressed;
				me.ButtonState |= MouseButtonState.Button1DoubleClicked;
			} else if ((mouseEvent.ButtonState & MouseButtonState.Button2Pressed) != 0) {
				me.ButtonState &= ~MouseButtonState.Button2Pressed;
				me.ButtonState |= MouseButtonState.Button2DoubleClicked;
			} else if ((mouseEvent.ButtonState & MouseButtonState.Button3Pressed) != 0) {
				me.ButtonState &= ~MouseButtonState.Button3Pressed;
				me.ButtonState |= MouseButtonState.Button3DoubleClicked;
			}
			isButtonReleased = true;

			inputResultQueue.Enqueue (new InputResult () {
				EventType = EventType.Mouse,
				MouseEvent = me
			});
		}

		void ProcessButtonTripleClicked (MouseEvent mouseEvent)
		{
			var me = new MouseEvent () {
				Position = mouseEvent.Position,
				ButtonState = mouseEvent.ButtonState
			};
			if ((mouseEvent.ButtonState & MouseButtonState.Button1Pressed) != 0) {
				me.ButtonState &= ~MouseButtonState.Button1Pressed;
				me.ButtonState |= MouseButtonState.Button1TripleClicked;
			} else if ((mouseEvent.ButtonState & MouseButtonState.Button2Pressed) != 0) {
				me.ButtonState &= ~MouseButtonState.Button2Pressed;
				me.ButtonState |= MouseButtonState.Button2TrippleClicked;
			} else if ((mouseEvent.ButtonState & MouseButtonState.Button3Pressed) != 0) {
				me.ButtonState &= ~MouseButtonState.Button3Pressed;
				me.ButtonState |= MouseButtonState.Button3TripleClicked;
			}
			isButtonReleased = true;

			inputResultQueue.Enqueue (new InputResult () {
				EventType = EventType.Mouse,
				MouseEvent = me
			});
		}

		async Task ProcessContinuousButtonPressedAsync ()
		{
			isProcContBtnPressedRuning = true;
			Point point = new Point ();
			while (isButtonPressed) {
				await Task.Delay (200);
				var view = Application.wantContinuousButtonPressedView;
				if (isButtonPressed && !Console.KeyAvailable
					&& !isButtonClicked && !isButtonDoubleClicked && !isButtonTripleClicked
					&& (view != null || view == null && lastMouseEvent.Position != point)) {
					point = lastMouseEvent.Position;
					inputResultQueue.Enqueue (new InputResult () {
						EventType = EventType.Mouse,
						MouseEvent = lastMouseEvent
					});
					inputReady.Set ();
				} else {
					break;
				}
			}
			isProcContBtnPressedRuning = false;
			//isButtonPressed = false;
		}

		void ProcessButtonReleased (MouseEvent mouseEvent)
		{
			var me = new MouseEvent () {
				Position = mouseEvent.Position,
				ButtonState = mouseEvent.ButtonState
			};
			if ((mouseEvent.ButtonState & MouseButtonState.Button1Pressed) != 0) {
				me.ButtonState &= ~(MouseButtonState.Button1Pressed | MouseButtonState.ReportMousePosition);
				me.ButtonState |= MouseButtonState.Button1Released;
			} else if ((mouseEvent.ButtonState & MouseButtonState.Button2Pressed) != 0) {
				me.ButtonState &= ~(MouseButtonState.Button2Pressed | MouseButtonState.ReportMousePosition);
				me.ButtonState |= MouseButtonState.Button2Released;
			} else if ((mouseEvent.ButtonState & MouseButtonState.Button3Pressed) != 0) {
				me.ButtonState &= ~(MouseButtonState.Button3Pressed | MouseButtonState.ReportMousePosition);
				me.ButtonState |= MouseButtonState.Button3Released;
			}
			isButtonReleased = true;
			lastMouseEvent = me;

			inputResultQueue.Enqueue (new InputResult () {
				EventType = EventType.Mouse,
				MouseEvent = me
			});
		}

		ConsoleModifiers GetConsoleModifiers (uint keyChar)
		{
			switch (keyChar) {
			case 50:
				return ConsoleModifiers.Shift;
			case 51:
				return ConsoleModifiers.Alt;
			case 52:
				return ConsoleModifiers.Shift | ConsoleModifiers.Alt;
			case 53:
				return ConsoleModifiers.Control;
			case 54:
				return ConsoleModifiers.Shift | ConsoleModifiers.Control;
			case 55:
				return ConsoleModifiers.Alt | ConsoleModifiers.Control;
			case 56:
				return ConsoleModifiers.Shift | ConsoleModifiers.Alt | ConsoleModifiers.Control;
			default:
				return 0;
			}
		}

		ConsoleKey GetConsoleKey (char keyChar, ref ConsoleModifiers mod, int length)
		{
			ConsoleKey key;
			switch (keyChar) {
			case 'A':
				key = ConsoleKey.UpArrow;
				break;
			case 'B':
				key = ConsoleKey.DownArrow;
				break;
			case 'C':
				key = ConsoleKey.RightArrow;
				break;
			case 'D':
				key = ConsoleKey.LeftArrow;
				break;
			case 'F':
				key = ConsoleKey.End;
				break;
			case 'H':
				key = ConsoleKey.Home;
				break;
			case 'P':
				key = ConsoleKey.F1;
				break;
			case 'Q':
				key = ConsoleKey.F2;
				break;
			case 'R':
				key = ConsoleKey.F3;
				break;
			case 'S':
				key = ConsoleKey.F4;
				break;
			case 'Z':
				key = ConsoleKey.Tab;
				mod |= ConsoleModifiers.Shift;
				break;
			case '0':
				key = ConsoleKey.F9;
				break;
			case '1':
				key = ConsoleKey.F10;
				break;
			case '2':
				key = ConsoleKey.Insert;
				break;
			case '3':
				if (length == 5) {
					key = ConsoleKey.F11;
				} else {
					key = ConsoleKey.Delete;
				}
				break;
			case '4':
				key = ConsoleKey.F12;
				break;
			case '5':
				if (length == 5) {
					key = ConsoleKey.F5;
				} else {
					key = ConsoleKey.PageUp;
				}
				break;
			case '6':
				key = ConsoleKey.PageDown;
				break;
			case '7':
				key = ConsoleKey.F6;
				break;
			case '8':
				key = ConsoleKey.F7;
				break;
			case '9':
				key = ConsoleKey.F8;
				break;
			default:
				key = 0;
				break;
			}

			return key;
		}

		public enum EventType {
			Key = 1,
			Mouse = 2,
			WindowSize = 3,
			WindowPosition = 4
		}

		[Flags]
		public enum MouseButtonState {
			Button1Pressed = 0x1,
			Button1Released = 0x2,
			Button1Clicked = 0x4,
			Button1DoubleClicked = 0x8,
			Button1TripleClicked = 0x10,
			Button2Pressed = 0x20,
			Button2Released = 0x40,
			Button2Clicked = 0x80,
			Button2DoubleClicked = 0x100,
			Button2TrippleClicked = 0x200,
			Button3Pressed = 0x400,
			Button3Released = 0x800,
			Button3Clicked = 0x1000,
			Button3DoubleClicked = 0x2000,
			Button3TripleClicked = 0x4000,
			ButtonWheeledUp = 0x8000,
			ButtonWheeledDown = 0x10000,
			ButtonWheeledLeft = 0x20000,
			ButtonWheeledRight = 0x40000,
			Button4Pressed = 0x80000,
			Button4Released = 0x100000,
			Button4Clicked = 0x200000,
			Button4DoubleClicked = 0x400000,
			Button4TripleClicked = 0x800000,
			ButtonShift = 0x1000000,
			ButtonCtrl = 0x2000000,
			ButtonAlt = 0x4000000,
			ReportMousePosition = 0x8000000,
			AllEvents = Button1Pressed | Button1Released | Button1Clicked | Button1DoubleClicked | Button1TripleClicked | Button2Pressed | Button2Released | Button2Clicked | Button2DoubleClicked | Button2TrippleClicked | Button3Pressed | Button3Released | Button3Clicked | Button3DoubleClicked | Button3TripleClicked | ButtonWheeledUp | ButtonWheeledDown | ButtonWheeledLeft | ButtonWheeledRight | Button4Pressed | Button4Released | Button4Clicked | Button4DoubleClicked | Button4TripleClicked | ReportMousePosition
		}

		public struct MouseEvent {
			public Point Position;
			public MouseButtonState ButtonState;
		}

		public struct WindowSizeEvent {
			public Size Size;
		}

		public struct WindowPositionEvent {
			public int Top;
			public Point CursorPosition;
		}

		public struct InputResult {
			public EventType EventType;
			public ConsoleKeyInfo ConsoleKeyInfo;
			public MouseEvent MouseEvent;
			public WindowSizeEvent WindowSizeEvent;
			public WindowPositionEvent WindowPositionEvent;
		}
	}

	internal class NetDriver : ConsoleDriver {
		int cols, rows, top;
		public override int Cols => cols;
		public override int Rows => rows;
		public override int Top => top;
		public override bool HeightAsBuffer { get; set; }

		public NetWinVTConsole NetWinConsole { get; }
		public bool IsWinPlatform { get; }
		public bool AlwaysSetPosition { get; set; }

		int largestWindowHeight;

		public NetDriver ()
		{
			var p = Environment.OSVersion.Platform;
			if (p == PlatformID.Win32NT || p == PlatformID.Win32S || p == PlatformID.Win32Windows) {
				IsWinPlatform = true;
				NetWinConsole = new NetWinVTConsole ();
			}
			largestWindowHeight = Math.Max (Console.BufferHeight, largestWindowHeight);
		}

		// The format is rows, columns and 3 values on the last column: Rune, Attribute and Dirty Flag
		int [,,] contents;
		bool [] dirtyLine;

		static bool sync = false;

		// Current row, and current col, tracked by Move/AddCh only
		int ccol, crow;
		public override void Move (int col, int row)
		{
			ccol = col;
			crow = row;
		}

		public override void AddRune (Rune rune)
		{
			if (contents.Length != Rows * Cols * 4) {
				return;
			}
			rune = MakePrintable (rune);
			var runeWidth = Rune.ColumnWidth (rune);
			if (Clip.Contains (ccol, crow) && ccol + Math.Max (runeWidth, 1) <= Cols) {
				contents [crow, ccol, 0] = (int)(uint)rune;
				contents [crow, ccol, 1] = currentAttribute;
				contents [crow, ccol, 2] = 1;
				contents [crow, ccol, 3] = currentUnderlineAttribute;
				dirtyLine [crow] = true;

				ccol++;
				if (runeWidth > 1) {
					for (int i = 1; i < runeWidth; i++) {
						if (ccol < cols) {
							contents [crow, ccol, 2] = 0;
						} else {
							break;
						}
						ccol++;
					}
				}
			} else if (ccol > -1 && crow > -1 && ccol < cols && crow < rows) {
				contents [crow, ccol, 2] = 1;
				dirtyLine [crow] = true;
			}

			//if (ccol == Cols) {
			//	ccol = 0;
			//	if (crow + 1 < Rows)
			//		crow++;
			//}
			if (sync) {
				UpdateScreen ();
			}
		}

		public override void AddStr (ustring str)
		{
			foreach (var rune in str)
				AddRune (rune);
		}

		public override void End ()
		{
			if (IsWinPlatform) {
				NetWinConsole.Cleanup ();
			}

			StopReportingMouseMoves ();
			Console.ResetColor ();
			Clear ();
		}

		void Clear ()
		{
			if (Rows > 0) {
				Console.Clear ();
				Console.Out.Write ("\x1b[3J");
				//Console.Out.Write ("\x1b[?25l");
			}
		}

		static Attribute MakeColor (ConsoleColor f, ConsoleColor b)
		{
			// Encode the colors into the int value.
			return new Attribute (
				value: ((((int)f) & 0xffff) << 16) | (((int)b) & 0xffff),
				foreground: (Color)f,
				background: (Color)b
				);
		}

		public override void Init (Action terminalResized)
		{
			TerminalResized = terminalResized;

			Console.TreatControlCAsInput = true;

			cols = Console.WindowWidth;
			rows = Console.WindowHeight;

			Clear ();
			ResizeScreen ();
			UpdateOffScreen ();

			StartReportingMouseMoves ();

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

			// Focused,
			//    Selected, Hot: Yellow on Black
			//    Selected, text: white on black
			//    Unselected, hot: yellow on cyan
			//    unselected, text: same as unfocused
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
			if (!HeightAsBuffer) {
				if (Console.WindowHeight > 0) {
					// Can raise an exception while is still resizing.
					try {
						// Not supported on Unix.
						if (IsWinPlatform) {
#pragma warning disable CA1416
							Console.CursorTop = 0;
							Console.CursorLeft = 0;
							Console.WindowTop = 0;
							Console.WindowLeft = 0;
							Console.SetBufferSize (Cols, Rows);
#pragma warning restore CA1416
						} else {
							//Console.Out.Write ($"\x1b[8;{Console.WindowHeight};{Console.WindowWidth}t");
							Console.Out.Write ($"\x1b[0;0" +
								$";{Rows};{Cols}w");
						}
					} catch (System.IO.IOException) {
						return;
					} catch (ArgumentOutOfRangeException) {
						return;
					}
				}
			} else {
				if (IsWinPlatform && Console.WindowHeight > 0) {
					// Can raise an exception while is still resizing.
					try {
#pragma warning disable CA1416
						Console.WindowTop = Math.Max (Math.Min (top, Rows - Console.WindowHeight), 0);
#pragma warning restore CA1416
					} catch (Exception) {
						return;
					}
				} else {
					Console.Out.Write ($"\x1b[{top};{Console.WindowLeft}" +
						$";{Rows};{Cols}w");
				}
			}

			Clip = new Rect (0, 0, Cols, Rows);

			contents = new int [Rows, Cols, 4];
			dirtyLine = new bool [Rows];
		}

		void UpdateOffScreen ()
		{
			// Can raise an exception while is still resizing.
			try {
				for (int row = 0; row < rows; row++) {
					for (int c = 0; c < cols; c++) {
						contents [row, c, 0] = ' ';
						contents [row, c, 1] = (ushort)Colors.TopLevel.Normal;
						contents [row, c, 2] = 0;
						contents [row, c, 3] = 0; // underline
						dirtyLine [row] = true;
					}
				}
			} catch (IndexOutOfRangeException) { }

			winChanging = false;
		}

		public override Attribute MakeAttribute (Color fore, Color back, bool underline = false)
		{
			var attr = MakeColor ((ConsoleColor)fore, (ConsoleColor)back);
			attr.UnderLine = underline;
			return attr;
		}

		int redrawColor = -1;
		void SetColor (int color)
		{
			redrawColor = color;
			IEnumerable<int> values = Enum.GetValues (typeof (ConsoleColor))
			      .OfType<ConsoleColor> ()
			      .Select (s => (int)s);
			if (values.Contains (color & 0xffff)) {
				Console.BackgroundColor = (ConsoleColor)(color & 0xffff);

			}
			if (values.Contains ((color >> 16) & 0xffff)) {
				Console.ForegroundColor = (ConsoleColor)((color >> 16) & 0xffff);
			}
		}

		bool redrawUnderline = false;
		void SetUnderline (bool state)
		{
			redrawUnderline = state;
			if (IsWinPlatform == true) {
				var OutputHandle = NetWinVTConsole.GetStdHandle (NetWinVTConsole.STD_OUTPUT_HANDLE);
				var success = NetWinVTConsole.GetConsoleScreenBufferInfo (OutputHandle, out var csbi);
				if (success) {
					if (state == true) {
						csbi.wAttributes |= 0x8000;
					} else {
						csbi.wAttributes &= 0x7FFF;
					}
					NetWinVTConsole.SetConsoleTextAttribute (OutputHandle, csbi.wAttributes);
				}
			}
		}

		public override void Refresh ()
		{
			UpdateScreen ();
		}

		public override void UpdateScreen ()
		{
			if (winChanging || Console.WindowHeight == 0 || contents.Length != Rows * Cols * 4
				|| (!HeightAsBuffer && Rows != Console.WindowHeight)
				|| (HeightAsBuffer && Rows != largestWindowHeight)) {
				return;
			}

			int top = Top;
			int rows = Math.Min (Console.WindowHeight + top, Rows);
			int cols = Cols;

			Console.CursorVisible = false;
			for (int row = top; row < rows; row++) {
				if (!dirtyLine [row]) {
					continue;
				}
				dirtyLine [row] = false;
				for (int col = 0; col < cols; col++) {
					if (contents [row, col, 2] != 1) {
						continue;
					}
					if (Console.WindowHeight > 0 && !SetCursorPosition (col, row)) {
						return;
					}
					for (; col < cols && contents [row, col, 2] == 1; col++) {
						var color = contents [row, col, 1];
						if (color != redrawColor) {
							SetColor (color);
						}
						var underline = contents [row, col, 3] == 1 ? true : false;
						if (underline != redrawUnderline) {
							SetUnderline (underline);
						}
						if (AlwaysSetPosition && !SetCursorPosition (col, row)) {
							return;
						}
						Console.Write ((char)contents [row, col, 0]);
						contents [row, col, 2] = 0;
					}
				}
			}
			Console.CursorVisible = true;
			UpdateCursor ();
		}

		bool SetCursorPosition (int col, int row)
		{
			// Could happens that the windows is still resizing and the col is bigger than Console.WindowWidth.
			try {
				Console.SetCursorPosition (col, row);
				return true;
			} catch (Exception) {
				return false;
			}
		}

		public override void UpdateCursor ()
		{
			// Prevents the exception of size changing during resizing.
			try {
				if (ccol >= 0 && ccol <= cols && crow >= 0 && crow <= rows) {
					Console.SetCursorPosition (ccol, crow);
				}
			} catch (System.IO.IOException) {
			} catch (ArgumentOutOfRangeException) {
			}
		}

		public override void StartReportingMouseMoves ()
		{
			Console.Out.Write ("\x1b[?1003h\x1b[?1015h\x1b[?1006h");
			Console.Out.Flush ();
		}

		public override void StopReportingMouseMoves ()
		{
			Console.Out.Write ("\x1b[?1003l\x1b[?1015l\x1b[?1006l");
			Console.Out.Flush ();
		}

		public override void Suspend ()
		{
		}

		int currentAttribute;
		int currentUnderlineAttribute;

		public override void SetAttribute (Attribute c)
		{
			currentAttribute = c.Value;
			currentUnderlineAttribute = c.UnderLine ? 1 : 0;
		}

		Key MapKey (ConsoleKeyInfo keyInfo)
		{
			MapKeyModifiers (keyInfo, (Key)keyInfo.Key);
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
				return (Key)((uint)keyInfo.KeyChar);
			}

			var key = keyInfo.Key;
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

		KeyModifiers keyModifiers;

		Key MapKeyModifiers (ConsoleKeyInfo keyInfo, Key key)
		{
			if (keyModifiers == null) {
				keyModifiers = new KeyModifiers ();
			}
			Key keyMod = new Key ();
			if ((keyInfo.Modifiers & ConsoleModifiers.Shift) != 0) {
				keyMod = Key.ShiftMask;
				keyModifiers.Shift = true;
			}
			if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0) {
				keyMod |= Key.CtrlMask;
				keyModifiers.Ctrl = true;
			}
			if ((keyInfo.Modifiers & ConsoleModifiers.Alt) != 0) {
				keyMod |= Key.AltMask;
				keyModifiers.Alt = true;
			}

			return keyMod != Key.Null ? keyMod | key : key;
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

			var mLoop = mainLoop.Driver as NetMainLoop;

			// Note: Net doesn't support keydown/up events and thus any passed keyDown/UpHandlers will be simulated to be called.
			mLoop.ProcessInput = (e) => ProcessInput (e);
		}

		void ProcessInput (NetEvents.InputResult inputEvent)
		{
			switch (inputEvent.EventType) {
			case NetEvents.EventType.Key:
				var map = MapKey (inputEvent.ConsoleKeyInfo);
				if (map == (Key)0xffffffff) {
					return;
				}
				keyDownHandler (new KeyEvent (map, keyModifiers));
				keyHandler (new KeyEvent (map, keyModifiers));
				keyUpHandler (new KeyEvent (map, keyModifiers));
				keyModifiers = new KeyModifiers ();
				break;
			case NetEvents.EventType.Mouse:
				mouseHandler (ToDriverMouse (inputEvent.MouseEvent));
				break;
			case NetEvents.EventType.WindowSize:
				ChangeWin ();
				break;
			case NetEvents.EventType.WindowPosition:
				var newTop = inputEvent.WindowPositionEvent.Top;
				if (HeightAsBuffer && top != newTop) {
					top = newTop;
					Refresh ();
				}
				break;
			}
		}

		bool winChanging;

		void ChangeWin ()
		{
			winChanging = true;
			const int Min_WindowWidth = 14;
			Size size = new Size ();
			if (!HeightAsBuffer) {
				size = new Size (Math.Max (Min_WindowWidth, Console.WindowWidth),
					Console.WindowHeight);
				top = 0;
			} else {
				largestWindowHeight = Math.Max (Console.BufferHeight, largestWindowHeight);
				size = new Size (Console.BufferWidth, largestWindowHeight);
			}
			cols = size.Width;
			rows = size.Height;
			ResizeScreen ();
			UpdateOffScreen ();
			if (!winChanging) {
				TerminalResized.Invoke ();
			}
		}

		MouseEvent ToDriverMouse (NetEvents.MouseEvent me)
		{
			MouseFlags mouseFlag = 0;

			if ((me.ButtonState & NetEvents.MouseButtonState.Button1Pressed) != 0) {
				mouseFlag |= MouseFlags.Button1Pressed;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button1Released) != 0) {
				mouseFlag |= MouseFlags.Button1Released;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button1Clicked) != 0) {
				mouseFlag |= MouseFlags.Button1Clicked;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button1DoubleClicked) != 0) {
				mouseFlag |= MouseFlags.Button1DoubleClicked;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button1TripleClicked) != 0) {
				mouseFlag |= MouseFlags.Button1TripleClicked;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button2Pressed) != 0) {
				mouseFlag |= MouseFlags.Button2Pressed;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button2Released) != 0) {
				mouseFlag |= MouseFlags.Button2Released;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button2Clicked) != 0) {
				mouseFlag |= MouseFlags.Button2Clicked;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button2DoubleClicked) != 0) {
				mouseFlag |= MouseFlags.Button2DoubleClicked;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button2TrippleClicked) != 0) {
				mouseFlag |= MouseFlags.Button2TripleClicked;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button3Pressed) != 0) {
				mouseFlag |= MouseFlags.Button3Pressed;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button3Released) != 0) {
				mouseFlag |= MouseFlags.Button3Released;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button3Clicked) != 0) {
				mouseFlag |= MouseFlags.Button3Clicked;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button3DoubleClicked) != 0) {
				mouseFlag |= MouseFlags.Button3DoubleClicked;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button3TripleClicked) != 0) {
				mouseFlag |= MouseFlags.Button3TripleClicked;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.ButtonWheeledUp) != 0) {
				mouseFlag |= MouseFlags.WheeledUp;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.ButtonWheeledDown) != 0) {
				mouseFlag |= MouseFlags.WheeledDown;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.ButtonWheeledLeft) != 0) {
				mouseFlag |= MouseFlags.WheeledLeft;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.ButtonWheeledRight) != 0) {
				mouseFlag |= MouseFlags.WheeledRight;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button4Pressed) != 0) {
				mouseFlag |= MouseFlags.Button4Pressed;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button4Released) != 0) {
				mouseFlag |= MouseFlags.Button4Released;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button4Clicked) != 0) {
				mouseFlag |= MouseFlags.Button4Clicked;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button4DoubleClicked) != 0) {
				mouseFlag |= MouseFlags.Button4DoubleClicked;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.Button4TripleClicked) != 0) {
				mouseFlag |= MouseFlags.Button4TripleClicked;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.ReportMousePosition) != 0) {
				mouseFlag |= MouseFlags.ReportMousePosition;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.ButtonShift) != 0) {
				mouseFlag |= MouseFlags.ButtonShift;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.ButtonCtrl) != 0) {
				mouseFlag |= MouseFlags.ButtonCtrl;
			}
			if ((me.ButtonState & NetEvents.MouseButtonState.ButtonAlt) != 0) {
				mouseFlag |= MouseFlags.ButtonAlt;
			}

			return new MouseEvent () {
				X = me.Position.X,
				Y = me.Position.Y,
				Flags = mouseFlag
			};
		}

		public override Attribute GetAttribute ()
		{
			Attribute attr = currentAttribute;
			attr.UnderLine = currentUnderlineAttribute == 1 ? true : false;
			return attr;
		}

		#region Unused
		public override void SetColors (ConsoleColor foreground, ConsoleColor background)
		{
		}

		public override void SetColors (short foregroundColorId, short backgroundColorId)
		{
		}

		public override void CookMouse ()
		{
		}

		public override void UncookMouse ()
		{
		}

		/// <inheritdoc/>
		public override bool GetCursorVisibility (out CursorVisibility visibility)
		{
			visibility = CursorVisibility.Default;

			return false;
		}

		/// <inheritdoc/>
		public override bool SetCursorVisibility (CursorVisibility visibility)
		{
			return false;
		}

		/// <inheritdoc/>
		public override bool EnsureCursorVisibility ()
		{
			return false;
		}

		public override void SendKeys (char keyChar, ConsoleKey key, bool shift, bool alt, bool control)
		{
			NetEvents.InputResult input = new NetEvents.InputResult ();
			ConsoleKey ck;
			if (char.IsLetter (keyChar)) {
				ck = key;
			} else {
				ck = (ConsoleKey)'\0';
			}
			input.EventType = NetEvents.EventType.Key;
			input.ConsoleKeyInfo = new ConsoleKeyInfo (keyChar, ck, shift, alt, control);

			try {
				ProcessInput (input);
			} catch (OverflowException) { }
		}
		#endregion

		//
		// These are for the .NET driver, but running natively on Windows, wont run
		// on the Mono emulation
		//

	}

	/// <summary>
	/// Mainloop intended to be used with the .NET System.Console API, and can
	/// be used on Windows and Unix, it is cross platform but lacks things like
	/// file descriptor monitoring.
	/// </summary>
	/// <remarks>
	/// This implementation is used for NetDriver.
	/// </remarks>
	internal class NetMainLoop : IMainLoopDriver {
		ManualResetEventSlim keyReady = new ManualResetEventSlim (false);
		ManualResetEventSlim waitForProbe = new ManualResetEventSlim (false);
		Queue<NetEvents.InputResult?> inputResult = new Queue<NetEvents.InputResult?> ();
		MainLoop mainLoop;
		CancellationTokenSource tokenSource = new CancellationTokenSource ();
		NetEvents netEvents;

		/// <summary>
		/// Invoked when a Key is pressed.
		/// </summary>
		public Action<NetEvents.InputResult> ProcessInput;

		/// <summary>
		/// Initializes the class with the console driver.
		/// </summary>
		/// <remarks>
		///   Passing a consoleDriver is provided to capture windows resizing.
		/// </remarks>
		/// <param name="consoleDriver">The console driver used by this Net main loop.</param>
		public NetMainLoop (ConsoleDriver consoleDriver = null)
		{
			if (consoleDriver == null) {
				throw new ArgumentNullException ("Console driver instance must be provided.");
			}
			netEvents = new NetEvents (consoleDriver);
		}

		void NetInputHandler ()
		{
			while (true) {
				waitForProbe.Wait ();
				waitForProbe.Reset ();
				if (inputResult.Count == 0) {
					inputResult.Enqueue (netEvents.ReadConsoleInput ());
				}
				try {
					while (inputResult.Peek () == null) {
						inputResult.Dequeue ();
					}
					if (inputResult.Count > 0) {
						keyReady.Set ();
					}
				} catch (InvalidOperationException) { }
			}
		}

		void IMainLoopDriver.Setup (MainLoop mainLoop)
		{
			this.mainLoop = mainLoop;
			Task.Run (NetInputHandler);
		}

		void IMainLoopDriver.Wakeup ()
		{
			keyReady.Set ();
		}

		bool IMainLoopDriver.EventsPending (bool wait)
		{
			waitForProbe.Set ();

			if (CheckTimers (wait, out var waitTimeout)) {
				return true;
			}

			try {
				if (!tokenSource.IsCancellationRequested) {
					keyReady.Wait (waitTimeout, tokenSource.Token);
				}
			} catch (OperationCanceledException) {
				return true;
			} finally {
				keyReady.Reset ();
			}

			if (!tokenSource.IsCancellationRequested) {
				return inputResult.Count > 0 || CheckTimers (wait, out _);
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
			if (inputResult.Count > 0) {
				ProcessInput?.Invoke (inputResult.Dequeue ().Value);
			}
		}
	}
}