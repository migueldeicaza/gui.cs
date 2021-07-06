//
// Driver.cs: Curses-based Driver
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NStack;
using Unix.Terminal;

namespace Terminal.Gui {

	/// <summary>
	/// This is the Curses driver for the gui.cs/Terminal framework.
	/// </summary>
	internal class CursesDriver : ConsoleDriver {
		public override int Cols => Curses.Cols;
		public override int Rows => Curses.Lines;
		public override int Left => 0;
		public override int Top => 0;
		public override bool HeightAsBuffer { get; set; }
		public override IClipboard Clipboard { get => clipboard; }

		CursorVisibility? initialCursorVisibility = null;
		CursorVisibility? currentCursorVisibility = null;
		IClipboard clipboard;

		// Current row, and current col, tracked by Move/AddRune only
		int ccol, crow;
		bool needMove;
		public override void Move (int col, int row)
		{
			ccol = col;
			crow = row;

			if (Clip.Contains (col, row)) {
				Curses.move (row, col);
				needMove = false;
			} else {
				Curses.move (Clip.Y, Clip.X);
				needMove = true;
			}
		}

		static bool sync = false;
		public override void AddRune (Rune rune)
		{
			if (Clip.Contains (ccol, crow)) {
				if (needMove) {
					Curses.move (crow, ccol);
					needMove = false;
				}
				Curses.addch ((int)(uint)MakePrintable (rune));
			} else
				needMove = true;
			if (sync)
				Application.Driver.Refresh ();
			ccol++;
			var runeWidth = Rune.ColumnWidth (rune);
			if (runeWidth > 1) {
				for (int i = 1; i < runeWidth; i++) {
					ccol++;
				}
			}
		}

		public override void AddStr (ustring str)
		{
			// TODO; optimize this to determine if the str fits in the clip region, and if so, use Curses.addstr directly
			foreach (var rune in str)
				AddRune (rune);
		}

		public override void Refresh ()
		{
			Curses.refresh ();
			if (Curses.CheckWinChange ()) {
				Clip = new Rect (0, 0, Cols, Rows);
				TerminalResized?.Invoke ();
			}
		}

		public override void UpdateCursor () => Refresh ();

		public override void End ()
		{
			if (reportableMouseEvents.HasFlag (Curses.Event.ReportMousePosition)) {
				StopReportingMouseMoves ();
			}

			SetCursorVisibility (CursorVisibility.Default);

			Curses.endwin ();

			// I'm commenting this because was used in a trying to fix the Linux hanging and forgot to exclude it.
			// Clear and reset entire screen.
			//Console.Out.Write ("\x1b[2J");
			//Console.Out.Flush ();

			// Set top and bottom lines of a window.
			//Console.Out.Write ("\x1b[1;25r");
			//Console.Out.Flush ();

			//Set cursor key to cursor.
			//Console.Out.Write ("\x1b[?1l");
			//Console.Out.Flush ();
		}

		public override void UpdateScreen () => window.redrawwin ();

		int currentAttribute;

		public override void SetAttribute (Attribute c)
		{
			currentAttribute = c.Value;
			Curses.attrset (currentAttribute);
		}

		public Curses.Window window;

		//static short last_color_pair = 16;

		/// <summary>
		/// Creates a curses color from the provided foreground and background colors
		/// </summary>
		/// <param name="foreground">Contains the curses attributes for the foreground (color, plus any attributes)</param>
		/// <param name="background">Contains the curses attributes for the background (color, plus any attributes)</param>
		/// <returns></returns>
		public static Attribute MakeColor (short foreground, short background)
		{
			var v = (short)((int)foreground | background << 4);
			//Curses.InitColorPair (++last_color_pair, foreground, background);
			Curses.InitColorPair (v, foreground, background);
			return new Attribute (
				//value: Curses.ColorPair (last_color_pair),
				value: Curses.ColorPair (v),
				//foreground: (Color)foreground,
				foreground: MapCursesColor (foreground),
				//background: (Color)background);
				background: MapCursesColor (background));
		}

		int [,] colorPairs = new int [16, 16];

		public override void SetColors (ConsoleColor foreground, ConsoleColor background)
		{
			int f = (short)foreground;
			int b = (short)background;
			var v = colorPairs [f, b];
			if ((v & 0x10000) == 0) {
				b &= 0x7;
				bool bold = (f & 0x8) != 0;
				f &= 0x7;

				v = MakeColor ((short)f, (short)b) | (bold ? Curses.A_BOLD : 0);
				colorPairs [(int)foreground, (int)background] = v | 0x1000;
			}
			SetAttribute (v & 0xffff);
		}

		Dictionary<int, int> rawPairs = new Dictionary<int, int> ();
		public override void SetColors (short foreColorId, short backgroundColorId)
		{
			int key = ((ushort)foreColorId << 16) | (ushort)backgroundColorId;
			if (!rawPairs.TryGetValue (key, out var v)) {
				v = MakeColor (foreColorId, backgroundColorId);
				rawPairs [key] = v;
			}
			SetAttribute (v);
		}

		static Key MapCursesKey (int cursesKey)
		{
			switch (cursesKey) {
			case Curses.KeyF1: return Key.F1;
			case Curses.KeyF2: return Key.F2;
			case Curses.KeyF3: return Key.F3;
			case Curses.KeyF4: return Key.F4;
			case Curses.KeyF5: return Key.F5;
			case Curses.KeyF6: return Key.F6;
			case Curses.KeyF7: return Key.F7;
			case Curses.KeyF8: return Key.F8;
			case Curses.KeyF9: return Key.F9;
			case Curses.KeyF10: return Key.F10;
			case Curses.KeyF11: return Key.F11;
			case Curses.KeyF12: return Key.F12;
			case Curses.KeyUp: return Key.CursorUp;
			case Curses.KeyDown: return Key.CursorDown;
			case Curses.KeyLeft: return Key.CursorLeft;
			case Curses.KeyRight: return Key.CursorRight;
			case Curses.KeyHome: return Key.Home;
			case Curses.KeyEnd: return Key.End;
			case Curses.KeyNPage: return Key.PageDown;
			case Curses.KeyPPage: return Key.PageUp;
			case Curses.KeyDeleteChar: return Key.DeleteChar;
			case Curses.KeyInsertChar: return Key.InsertChar;
			case Curses.KeyTab: return Key.Tab;
			case Curses.KeyBackTab: return Key.BackTab;
			case Curses.KeyBackspace: return Key.Backspace;
			case Curses.ShiftKeyUp: return Key.CursorUp | Key.ShiftMask;
			case Curses.ShiftKeyDown: return Key.CursorDown | Key.ShiftMask;
			case Curses.ShiftKeyLeft: return Key.CursorLeft | Key.ShiftMask;
			case Curses.ShiftKeyRight: return Key.CursorRight | Key.ShiftMask;
			case Curses.ShiftKeyHome: return Key.Home | Key.ShiftMask;
			case Curses.ShiftKeyEnd: return Key.End | Key.ShiftMask;
			case Curses.ShiftKeyNPage: return Key.PageDown | Key.ShiftMask;
			case Curses.ShiftKeyPPage: return Key.PageUp | Key.ShiftMask;
			case Curses.AltKeyUp: return Key.CursorUp | Key.AltMask;
			case Curses.AltKeyDown: return Key.CursorDown | Key.AltMask;
			case Curses.AltKeyLeft: return Key.CursorLeft | Key.AltMask;
			case Curses.AltKeyRight: return Key.CursorRight | Key.AltMask;
			case Curses.AltKeyHome: return Key.Home | Key.AltMask;
			case Curses.AltKeyEnd: return Key.End | Key.AltMask;
			case Curses.AltKeyNPage: return Key.PageDown | Key.AltMask;
			case Curses.AltKeyPPage: return Key.PageUp | Key.AltMask;
			case Curses.CtrlKeyUp: return Key.CursorUp | Key.CtrlMask;
			case Curses.CtrlKeyDown: return Key.CursorDown | Key.CtrlMask;
			case Curses.CtrlKeyLeft: return Key.CursorLeft | Key.CtrlMask;
			case Curses.CtrlKeyRight: return Key.CursorRight | Key.CtrlMask;
			case Curses.CtrlKeyHome: return Key.Home | Key.CtrlMask;
			case Curses.CtrlKeyEnd: return Key.End | Key.CtrlMask;
			case Curses.CtrlKeyNPage: return Key.PageDown | Key.CtrlMask;
			case Curses.CtrlKeyPPage: return Key.PageUp | Key.CtrlMask;
			case Curses.ShiftCtrlKeyUp: return Key.CursorUp | Key.ShiftMask | Key.CtrlMask;
			case Curses.ShiftCtrlKeyDown: return Key.CursorDown | Key.ShiftMask | Key.CtrlMask;
			case Curses.ShiftCtrlKeyLeft: return Key.CursorLeft | Key.ShiftMask | Key.CtrlMask;
			case Curses.ShiftCtrlKeyRight: return Key.CursorRight | Key.ShiftMask | Key.CtrlMask;
			case Curses.ShiftCtrlKeyHome: return Key.Home | Key.ShiftMask | Key.CtrlMask;
			case Curses.ShiftCtrlKeyEnd: return Key.End | Key.ShiftMask | Key.CtrlMask;
			case Curses.ShiftCtrlKeyNPage: return Key.PageDown | Key.ShiftMask | Key.CtrlMask;
			case Curses.ShiftCtrlKeyPPage: return Key.PageUp | Key.ShiftMask | Key.CtrlMask;
			case Curses.ShiftAltKeyUp: return Key.CursorUp | Key.ShiftMask | Key.AltMask;
			case Curses.ShiftAltKeyDown: return Key.CursorDown | Key.ShiftMask | Key.AltMask;
			case Curses.ShiftAltKeyLeft: return Key.CursorLeft | Key.ShiftMask | Key.AltMask;
			case Curses.ShiftAltKeyRight: return Key.CursorRight | Key.ShiftMask | Key.AltMask;
			case Curses.ShiftAltKeyNPage: return Key.PageDown | Key.ShiftMask | Key.AltMask;
			case Curses.ShiftAltKeyPPage: return Key.PageUp | Key.ShiftMask | Key.AltMask;
			case Curses.ShiftAltKeyHome: return Key.Home | Key.ShiftMask | Key.AltMask;
			case Curses.ShiftAltKeyEnd: return Key.End | Key.ShiftMask | Key.AltMask;
			case Curses.AltCtrlKeyNPage: return Key.PageDown | Key.AltMask | Key.CtrlMask;
			case Curses.AltCtrlKeyPPage: return Key.PageUp | Key.AltMask | Key.CtrlMask;
			case Curses.AltCtrlKeyHome: return Key.Home | Key.AltMask | Key.CtrlMask;
			case Curses.AltCtrlKeyEnd: return Key.End | Key.AltMask | Key.CtrlMask;
			default: return Key.Unknown;
			}
		}

		Curses.Event? LastMouseButtonPressed;
		bool IsButtonPressed;
		bool cancelButtonClicked;
		bool isReportMousePosition;
		Point point;

		MouseEvent ToDriverMouse (Curses.MouseEvent cev)
		{
			MouseFlags mouseFlag = MouseFlags.AllEvents;

			if (LastMouseButtonPressed != null && cev.ButtonState != Curses.Event.ReportMousePosition) {
				LastMouseButtonPressed = null;
				IsButtonPressed = false;
			}


			if ((cev.ButtonState == Curses.Event.Button1Clicked || cev.ButtonState == Curses.Event.Button2Clicked ||
				cev.ButtonState == Curses.Event.Button3Clicked) &&
				LastMouseButtonPressed == null) {

				IsButtonPressed = false;
				mouseFlag = ProcessButtonClickedEvent (cev);

			} else if (((cev.ButtonState == Curses.Event.Button1Pressed || cev.ButtonState == Curses.Event.Button2Pressed ||
				cev.ButtonState == Curses.Event.Button3Pressed) && LastMouseButtonPressed == null) ||
				IsButtonPressed && cev.ButtonState == Curses.Event.ReportMousePosition) {

				mouseFlag = MapCursesButton (cev.ButtonState);
				if (cev.ButtonState != Curses.Event.ReportMousePosition)
					LastMouseButtonPressed = cev.ButtonState;
				IsButtonPressed = true;
				isReportMousePosition = false;

				if (cev.ButtonState == Curses.Event.ReportMousePosition) {
					mouseFlag = MapCursesButton ((Curses.Event)LastMouseButtonPressed) | MouseFlags.ReportMousePosition;
					point = new Point ();
					cancelButtonClicked = true;
				} else {
					point = new Point () {
						X = cev.X,
						Y = cev.Y
					};
				}

				if ((mouseFlag & MouseFlags.ReportMousePosition) == 0) {
					ProcessContinuousButtonPressedAsync (cev, mouseFlag).ConfigureAwait (false);
				}


			} else if ((cev.ButtonState == Curses.Event.Button1Released || cev.ButtonState == Curses.Event.Button2Released ||
				cev.ButtonState == Curses.Event.Button3Released)) {

				mouseFlag = ProcessButtonReleasedEvent (cev);
				IsButtonPressed = false;

			} else if (cev.ButtonState == Curses.Event.ButtonWheeledUp) {

				mouseFlag = MouseFlags.WheeledUp;

			} else if (cev.ButtonState == Curses.Event.ButtonWheeledDown) {

				mouseFlag = MouseFlags.WheeledDown;

			} else if ((cev.ButtonState & (Curses.Event.ButtonWheeledUp & Curses.Event.ButtonShift)) != 0) {

				mouseFlag = MouseFlags.WheeledLeft;

			} else if ((cev.ButtonState & (Curses.Event.ButtonWheeledDown & Curses.Event.ButtonShift)) != 0) {

				mouseFlag = MouseFlags.WheeledRight;

			} else if (cev.ButtonState == Curses.Event.ReportMousePosition) {

				mouseFlag = MouseFlags.ReportMousePosition;
				isReportMousePosition = true;

			} else {
				mouseFlag = 0;
				var eFlags = cev.ButtonState;
				foreach (Enum value in Enum.GetValues (eFlags.GetType ())) {
					if (eFlags.HasFlag (value)) {
						mouseFlag |= MapCursesButton ((Curses.Event)value);
					}
				}
			}

			mouseFlag = SetControlKeyStates (cev, mouseFlag);

			point = new Point () {
				X = cev.X,
				Y = cev.Y
			};

			return new MouseEvent () {
				X = cev.X,
				Y = cev.Y,
				//Flags = MapCursesButton (cev.ButtonState)
				Flags = mouseFlag
			};
		}

		MouseFlags ProcessButtonClickedEvent (Curses.MouseEvent cev)
		{
			LastMouseButtonPressed = cev.ButtonState;
			var mf = GetButtonState (cev, true);
			mouseHandler (ProcessButtonState (cev, mf));
			if (LastMouseButtonPressed != null && LastMouseButtonPressed == cev.ButtonState) {
				mf = GetButtonState (cev, false);
				mouseHandler (ProcessButtonState (cev, mf));
				if (LastMouseButtonPressed != null && LastMouseButtonPressed == cev.ButtonState) {
					mf = MapCursesButton (cev.ButtonState);
				}
			}
			LastMouseButtonPressed = null;
			return mf;
		}

		MouseFlags ProcessButtonReleasedEvent (Curses.MouseEvent cev)
		{
			var mf = MapCursesButton (cev.ButtonState);
			if (!cancelButtonClicked && LastMouseButtonPressed == null && !isReportMousePosition) {
				mouseHandler (ProcessButtonState (cev, mf));
				mf = GetButtonState (cev);
			} else if (isReportMousePosition) {
				mf = MouseFlags.ReportMousePosition;
			}
			cancelButtonClicked = false;
			return mf;
		}

		async Task ProcessContinuousButtonPressedAsync (Curses.MouseEvent cev, MouseFlags mouseFlag)
		{
			while (IsButtonPressed && LastMouseButtonPressed != null) {
				await Task.Delay (100);
				var me = new MouseEvent () {
					X = cev.X,
					Y = cev.Y,
					Flags = mouseFlag
				};

				var view = Application.wantContinuousButtonPressedView;
				if (view == null)
					break;
				if (IsButtonPressed && LastMouseButtonPressed != null && (mouseFlag & MouseFlags.ReportMousePosition) == 0) {
					mouseHandler (me);
					//mainLoop.Driver.Wakeup ();
				}
			}
		}

		MouseFlags GetButtonState (Curses.MouseEvent cev, bool pressed = false)
		{
			MouseFlags mf = default;
			switch (cev.ButtonState) {
			case Curses.Event.Button1Clicked:
				if (pressed)
					mf = MouseFlags.Button1Pressed;
				else
					mf = MouseFlags.Button1Released;
				break;

			case Curses.Event.Button2Clicked:
				if (pressed)
					mf = MouseFlags.Button2Pressed;
				else
					mf = MouseFlags.Button2Released;
				break;

			case Curses.Event.Button3Clicked:
				if (pressed)
					mf = MouseFlags.Button3Pressed;
				else
					mf = MouseFlags.Button3Released;
				break;

			case Curses.Event.Button1Released:
				mf = MouseFlags.Button1Clicked;
				break;

			case Curses.Event.Button2Released:
				mf = MouseFlags.Button2Clicked;
				break;

			case Curses.Event.Button3Released:
				mf = MouseFlags.Button3Clicked;
				break;


			}
			return mf;
		}

		MouseEvent ProcessButtonState (Curses.MouseEvent cev, MouseFlags mf)
		{
			return new MouseEvent () {
				X = cev.X,
				Y = cev.Y,
				Flags = mf
			};
		}

		MouseFlags MapCursesButton (Curses.Event cursesButton)
		{
			switch (cursesButton) {
			case Curses.Event.Button1Pressed: return MouseFlags.Button1Pressed;
			case Curses.Event.Button1Released: return MouseFlags.Button1Released;
			case Curses.Event.Button1Clicked: return MouseFlags.Button1Clicked;
			case Curses.Event.Button1DoubleClicked: return MouseFlags.Button1DoubleClicked;
			case Curses.Event.Button1TripleClicked: return MouseFlags.Button1TripleClicked;
			case Curses.Event.Button2Pressed: return MouseFlags.Button2Pressed;
			case Curses.Event.Button2Released: return MouseFlags.Button2Released;
			case Curses.Event.Button2Clicked: return MouseFlags.Button2Clicked;
			case Curses.Event.Button2DoubleClicked: return MouseFlags.Button2DoubleClicked;
			case Curses.Event.Button2TrippleClicked: return MouseFlags.Button2TripleClicked;
			case Curses.Event.Button3Pressed: return MouseFlags.Button3Pressed;
			case Curses.Event.Button3Released: return MouseFlags.Button3Released;
			case Curses.Event.Button3Clicked: return MouseFlags.Button3Clicked;
			case Curses.Event.Button3DoubleClicked: return MouseFlags.Button3DoubleClicked;
			case Curses.Event.Button3TripleClicked: return MouseFlags.Button3TripleClicked;
			case Curses.Event.ButtonWheeledUp: return MouseFlags.WheeledUp;
			case Curses.Event.ButtonWheeledDown: return MouseFlags.WheeledDown;
			case Curses.Event.Button4Pressed: return MouseFlags.Button4Pressed;
			case Curses.Event.Button4Released: return MouseFlags.Button4Released;
			case Curses.Event.Button4Clicked: return MouseFlags.Button4Clicked;
			case Curses.Event.Button4DoubleClicked: return MouseFlags.Button4DoubleClicked;
			case Curses.Event.Button4TripleClicked: return MouseFlags.Button4TripleClicked;
			case Curses.Event.ButtonShift: return MouseFlags.ButtonShift;
			case Curses.Event.ButtonCtrl: return MouseFlags.ButtonCtrl;
			case Curses.Event.ButtonAlt: return MouseFlags.ButtonAlt;
			case Curses.Event.ReportMousePosition: return MouseFlags.ReportMousePosition;
			case Curses.Event.AllEvents: return MouseFlags.AllEvents;
			default: return 0;
			}
		}

		static MouseFlags SetControlKeyStates (Curses.MouseEvent cev, MouseFlags mouseFlag)
		{
			if ((cev.ButtonState & Curses.Event.ButtonCtrl) != 0 && (mouseFlag & MouseFlags.ButtonCtrl) == 0)
				mouseFlag |= MouseFlags.ButtonCtrl;

			if ((cev.ButtonState & Curses.Event.ButtonShift) != 0 && (mouseFlag & MouseFlags.ButtonShift) == 0)
				mouseFlag |= MouseFlags.ButtonShift;

			if ((cev.ButtonState & Curses.Event.ButtonAlt) != 0 && (mouseFlag & MouseFlags.ButtonAlt) == 0)
				mouseFlag |= MouseFlags.ButtonAlt;
			return mouseFlag;
		}


		KeyModifiers keyModifiers;

		KeyModifiers MapKeyModifiers (Key key)
		{
			if (keyModifiers == null)
				keyModifiers = new KeyModifiers ();

			if (!keyModifiers.Shift && (key & Key.ShiftMask) != 0)
				keyModifiers.Shift = true;
			if (!keyModifiers.Alt && (key & Key.AltMask) != 0)
				keyModifiers.Alt = true;
			if (!keyModifiers.Ctrl && (key & Key.CtrlMask) != 0)
				keyModifiers.Ctrl = true;

			return keyModifiers;
		}

		void ProcessInput (Action<KeyEvent> keyHandler, Action<KeyEvent> keyDownHandler, Action<KeyEvent> keyUpHandler, Action<MouseEvent> mouseHandler)
		{
			int wch;
			var code = Curses.get_wch (out wch);
			if (code == Curses.ERR)
				return;

			keyModifiers = new KeyModifiers ();
			Key k = Key.Null;

			if (code == Curses.KEY_CODE_YES) {
				if (wch == Curses.KeyResize) {
					if (Curses.CheckWinChange ()) {
						TerminalResized?.Invoke ();
						return;
					}
				}
				if (wch == Curses.KeyMouse) {
					Curses.getmouse (out Curses.MouseEvent ev);
					//System.Diagnostics.Debug.WriteLine ($"ButtonState: {ev.ButtonState}; ID: {ev.ID}; X: {ev.X}; Y: {ev.Y}; Z: {ev.Z}");
					mouseHandler (ToDriverMouse (ev));
					return;
				}
				k = MapCursesKey (wch);
				if (wch >= 277 && wch <= 288) { // Shift+(F1 - F12)
					wch -= 12;
					k = Key.ShiftMask | MapCursesKey (wch);
				} else if (wch >= 289 && wch <= 300) { // Ctrl+(F1 - F12)
					wch -= 24;
					k = Key.CtrlMask | MapCursesKey (wch);
				} else if (wch >= 301 && wch <= 312) { // Ctrl+Shift+(F1 - F12)
					wch -= 36;
					k = Key.CtrlMask | Key.ShiftMask | MapCursesKey (wch);
				} else if (wch >= 313 && wch <= 324) { // Alt+(F1 - F12)
					wch -= 48;
					k = Key.AltMask | MapCursesKey (wch);
				} else if (wch >= 325 && wch <= 327) { // Shift+Alt+(F1 - F3)
					wch -= 60;
					k = Key.ShiftMask | Key.AltMask | MapCursesKey (wch);
				}
				keyDownHandler (new KeyEvent (k, MapKeyModifiers (k)));
				keyHandler (new KeyEvent (k, MapKeyModifiers (k)));
				keyUpHandler (new KeyEvent (k, MapKeyModifiers (k)));
				return;
			}

			// Special handling for ESC, we want to try to catch ESC+letter to simulate alt-letter as well as Alt-Fkey
			if (wch == 27) {
				Curses.timeout (200);

				code = Curses.get_wch (out int wch2);

				if (code == Curses.KEY_CODE_YES) {
					k = Key.AltMask | MapCursesKey (wch);
				}
				if (code == 0) {
					KeyEvent key;

					// The ESC-number handling, debatable.
					// Simulates the AltMask itself by pressing Alt + Space.
					if (wch2 == (int)Key.Space) {
						k = Key.AltMask;
					} else if (wch2 - (int)Key.Space >= (uint)Key.A && wch2 - (int)Key.Space <= (uint)Key.Z) {
						k = (Key)((uint)Key.AltMask + (wch2 - (int)Key.Space));
					} else if (wch2 >= (uint)Key.A - 64 && wch2 <= (uint)Key.Z - 64) {
						k = (Key)((uint)(Key.AltMask | Key.CtrlMask) + (wch2 + 64));
					} else if (wch2 >= (uint)Key.D0 && wch2 <= (uint)Key.D9) {
						k = (Key)((uint)Key.AltMask + (uint)Key.D0 + (wch2 - (uint)Key.D0));
					} else if (wch2 == 27) {
						k = (Key)wch2;
					} else if (wch2 == Curses.KEY_CODE_SEQ) {
						int [] c = null;
						while (code == 0) {
							code = Curses.get_wch (out wch2);
							if (wch2 > 0) {
								Array.Resize (ref c, c == null ? 1 : c.Length + 1);
								c [c.Length - 1] = wch2;
							}
						}
						if (c [0] == 49 && c [1] == 59 && c [2] == 55 && c [3] >= 80 && c [3] <= 83) { // Ctrl+Alt+(F1 - F4)
							wch2 = c [3] + 185;
							k = Key.CtrlMask | Key.AltMask | MapCursesKey (wch2);
						} else if (c [0] == 49 && c [2] == 59 && c [3] == 55 && c [4] == 126 && c [1] >= 53 && c [1] <= 57) { // Ctrl+Alt+(F5 - F8)
							wch2 = c [1] == 53 ? c [1] + 216 : c [1] + 215;
							k = Key.CtrlMask | Key.AltMask | MapCursesKey (wch2);
						} else if (c [0] == 50 && c [2] == 59 && c [3] == 55 && c [4] == 126 && c [1] >= 48 && c [1] <= 52) { // Ctrl+Alt+(F9 - F12)
							wch2 = c [1] < 51 ? c [1] + 225 : c [1] + 224;
							k = Key.CtrlMask | Key.AltMask | MapCursesKey (wch2);
						} else if (c [0] == 49 && c [1] == 59 && c [2] == 56 && c [3] >= 80 && c [3] <= 83) { // Ctrl+Shift+Alt+(F1 - F4)
							wch2 = c [3] + 185;
							k = Key.CtrlMask | Key.ShiftMask | Key.AltMask | MapCursesKey (wch2);
						} else if (c [0] == 49 && c [2] == 59 && c [3] == 56 && c [4] == 126 && c [1] >= 53 && c [1] <= 57) { // Ctrl+Shift+Alt+(F5 - F8)
							wch2 = c [1] == 53 ? c [1] + 216 : c [1] + 215;
							k = Key.CtrlMask | Key.ShiftMask | Key.AltMask | MapCursesKey (wch2);
						} else if (c [0] == 50 && c [2] == 59 && c [3] == 56 && c [4] == 126 && c [1] >= 48 && c [1] <= 52) {  // Ctrl+Shift+Alt+(F9 - F12)
							wch2 = c [1] < 51 ? c [1] + 225 : c [1] + 224;
							k = Key.CtrlMask | Key.ShiftMask | Key.AltMask | MapCursesKey (wch2);
						} else if (c [0] == 49 && c [1] == 59 && c [2] == 52 && c [3] == 83) {  // Shift+Alt+(F4)
							wch2 = 268;
							k = Key.ShiftMask | Key.AltMask | MapCursesKey (wch2);
						} else if (c [0] == 49 && c [2] == 59 && c [3] == 52 && c [4] == 126 && c [1] >= 53 && c [1] <= 57) {  // Shift+Alt+(F5 - F8)
							wch2 = c [1] < 55 ? c [1] + 216 : c [1] + 215;
							k = Key.ShiftMask | Key.AltMask | MapCursesKey (wch2);
						} else if (c [0] == 50 && c [2] == 59 && c [3] == 52 && c [4] == 126 && c [1] >= 48 && c [1] <= 52) {  // Shift+Alt+(F9 - F12)
							wch2 = c [1] < 51 ? c [1] + 225 : c [1] + 224;
							k = Key.ShiftMask | Key.AltMask | MapCursesKey (wch2);
						} else if (c [0] == 54 && c [1] == 59 && c [2] == 56 && c [3] == 126) {  // Shift+Ctrl+Alt+KeyNPage
							k = Key.ShiftMask | Key.CtrlMask | Key.AltMask | Key.PageDown;
						} else if (c [0] == 53 && c [1] == 59 && c [2] == 56 && c [3] == 126) {  // Shift+Ctrl+Alt+KeyPPage
							k = Key.ShiftMask | Key.CtrlMask | Key.AltMask | Key.PageUp;
						} else if (c [0] == 49 && c [1] == 59 && c [2] == 56 && c [3] == 72) {  // Shift+Ctrl+Alt+KeyHome
							k = Key.ShiftMask | Key.CtrlMask | Key.AltMask | Key.Home;
						} else if (c [0] == 49 && c [1] == 59 && c [2] == 56 && c [3] == 70) {  // Shift+Ctrl+Alt+KeyEnd
							k = Key.ShiftMask | Key.CtrlMask | Key.AltMask | Key.End;
						} else {
							k = MapCursesKey (wch2);
						}
					} else {
						// Unfortunately there are no way to differentiate Ctrl+Alt+alfa and Ctrl+Shift+Alt+alfa.
						if (((Key)wch2 & Key.CtrlMask) != 0) {
							keyModifiers.Ctrl = true;
						}
						if (wch2 == 0) {
							k = Key.CtrlMask | Key.AltMask | Key.Space;
						} else if (wch >= (uint)Key.A && wch <= (uint)Key.Z) {
							keyModifiers.Shift = true;
							keyModifiers.Alt = true;
						} else if (wch2 < 256) {
							k = (Key)wch2;
							keyModifiers.Alt = true;
						} else {
							k = (Key)((uint)(Key.AltMask | Key.CtrlMask) + wch2);
						}
					}
					key = new KeyEvent (k, MapKeyModifiers (k));
					keyDownHandler (key);
					keyHandler (key);
				} else {
					k = Key.Esc;
					keyHandler (new KeyEvent (k, MapKeyModifiers (k)));
				}
			} else if (wch == Curses.KeyTab) {
				k = MapCursesKey (wch);
				keyDownHandler (new KeyEvent (k, MapKeyModifiers (k)));
				keyHandler (new KeyEvent (k, MapKeyModifiers (k)));
			} else {
				// Unfortunately there are no way to differentiate Ctrl+alfa and Ctrl+Shift+alfa.
				k = (Key)wch;
				if (wch == 0) {
					k = Key.CtrlMask | Key.Space;
				} else if (wch >= (uint)Key.A - 64 && wch <= (uint)Key.Z - 64) {
					if ((Key)(wch + 64) != Key.J) {
						k = Key.CtrlMask | (Key)(wch + 64);
					}
				} else if (wch >= (uint)Key.A && wch <= (uint)Key.Z) {
					keyModifiers.Shift = true;
				}
				keyDownHandler (new KeyEvent (k, MapKeyModifiers (k)));
				keyHandler (new KeyEvent (k, MapKeyModifiers (k)));
				keyUpHandler (new KeyEvent (k, MapKeyModifiers (k)));
			}
			// Cause OnKeyUp and OnKeyPressed. Note that the special handling for ESC above 
			// will not impact KeyUp.
			// This is causing ESC firing even if another keystroke was handled.
			//if (wch == Curses.KeyTab) {
			//	keyUpHandler (new KeyEvent (MapCursesKey (wch), keyModifiers));
			//} else {
			//	keyUpHandler (new KeyEvent ((Key)wch, keyModifiers));
			//}
		}

		Action<KeyEvent> keyHandler;
		Action<MouseEvent> mouseHandler;

		public override void PrepareToRun (MainLoop mainLoop, Action<KeyEvent> keyHandler, Action<KeyEvent> keyDownHandler, Action<KeyEvent> keyUpHandler, Action<MouseEvent> mouseHandler)
		{
			// Note: Curses doesn't support keydown/up events and thus any passed keyDown/UpHandlers will never be called
			Curses.timeout (0);
			this.keyHandler = keyHandler;
			this.mouseHandler = mouseHandler;

			var mLoop = mainLoop.Driver as UnixMainLoop;

			mLoop.AddWatch (0, UnixMainLoop.Condition.PollIn, x => {
				ProcessInput (keyHandler, keyDownHandler, keyUpHandler, mouseHandler);
				return true;
			});

			mLoop.WinChanged += () => {
				if (Curses.CheckWinChange ()) {
					Clip = new Rect (0, 0, Cols, Rows);
					TerminalResized?.Invoke ();
				}
			};
		}

		Curses.Event oldMouseEvents, reportableMouseEvents;
		public override void Init (Action terminalResized)
		{
			if (window != null)
				return;

			try {
				//Set cursor key to application.
				//Console.Out.Write ("\x1b[?1h");
				//Console.Out.Flush ();

				window = Curses.initscr ();
			} catch (Exception e) {
				Console.WriteLine ("Curses failed to initialize, the exception is: " + e);
			}

			// 
			// We are setting Invisible as default so we could ignore XTerm DECSUSR setting
			//
			switch (Curses.curs_set (0)) {
			case 0:
				currentCursorVisibility = initialCursorVisibility = CursorVisibility.Invisible;
				break;

			case 1:
				currentCursorVisibility = initialCursorVisibility = CursorVisibility.Underline;
				Curses.curs_set (1);
				break;

			case 2:
				currentCursorVisibility = initialCursorVisibility = CursorVisibility.Box;
				Curses.curs_set (2);
				break;

			default:
				currentCursorVisibility = initialCursorVisibility = null;
				break;
			}

			if (RuntimeInformation.IsOSPlatform (OSPlatform.OSX)) {
				clipboard = new MacOSXClipboard ();
			} else {
				if (Is_WSL_Platform ()) {
					clipboard = new WSLClipboard ();
				} else {
					clipboard = new CursesClipboard ();
				}
			}

			Curses.raw ();
			Curses.noecho ();

			Curses.Window.Standard.keypad (true);
			reportableMouseEvents = Curses.mousemask (Curses.Event.AllEvents | Curses.Event.ReportMousePosition, out oldMouseEvents);
			TerminalResized = terminalResized;
			if (reportableMouseEvents.HasFlag (Curses.Event.ReportMousePosition))
				StartReportingMouseMoves ();

			//HLine = Curses.ACS_HLINE;
			//VLine = Curses.ACS_VLINE;
			//Stipple = Curses.ACS_CKBOARD;
			//Diamond = Curses.ACS_DIAMOND;
			//ULCorner = Curses.ACS_ULCORNER;
			//LLCorner = Curses.ACS_LLCORNER;
			//URCorner = Curses.ACS_URCORNER;
			//LRCorner = Curses.ACS_LRCORNER;
			//LeftTee = Curses.ACS_LTEE;
			//RightTee = Curses.ACS_RTEE;
			//TopTee = Curses.ACS_TTEE;
			//BottomTee = Curses.ACS_BTEE;
			//RightArrow = Curses.ACS_RARROW;
			//LeftArrow = Curses.ACS_LARROW;
			//UpArrow = Curses.ACS_UARROW;
			//DownArrow = Curses.ACS_DARROW;

			Colors.TopLevel = new ColorScheme ();
			Colors.Base = new ColorScheme ();
			Colors.Dialog = new ColorScheme ();
			Colors.Menu = new ColorScheme ();
			Colors.Error = new ColorScheme ();
			Clip = new Rect (0, 0, Cols, Rows);
			if (Curses.HasColors) {
				Curses.StartColor ();
				Curses.UseDefaultColors ();

				Colors.TopLevel.Normal = MakeColor (Curses.COLOR_GREEN, Curses.COLOR_BLACK);
				Colors.TopLevel.Focus = MakeColor (Curses.COLOR_WHITE, Curses.COLOR_CYAN);
				Colors.TopLevel.HotNormal = MakeColor (Curses.COLOR_YELLOW, Curses.COLOR_BLACK);
				Colors.TopLevel.HotFocus = MakeColor (Curses.COLOR_BLUE, Curses.COLOR_CYAN);

				Colors.Base.Normal = MakeColor (Curses.COLOR_WHITE, Curses.COLOR_BLUE);
				Colors.Base.Focus = MakeColor (Curses.COLOR_BLACK, Curses.COLOR_WHITE);
				Colors.Base.HotNormal = MakeColor (Curses.COLOR_CYAN, Curses.COLOR_BLUE);
				Colors.Base.HotFocus = Curses.A_BOLD | MakeColor (Curses.COLOR_BLUE, Curses.COLOR_GRAY);

				// Focused,
				//    Selected, Hot: Yellow on Black
				//    Selected, text: white on black
				//    Unselected, hot: yellow on cyan
				//    unselected, text: same as unfocused
				Colors.Menu.Normal = Curses.A_BOLD | MakeColor (Curses.COLOR_WHITE, Curses.COLOR_GRAY);
				Colors.Menu.Focus = Curses.A_BOLD | MakeColor (Curses.COLOR_WHITE, Curses.COLOR_BLACK);
				Colors.Menu.HotNormal = Curses.A_BOLD | MakeColor (Curses.COLOR_YELLOW, Curses.COLOR_GRAY);
				Colors.Menu.HotFocus = Curses.A_BOLD | MakeColor (Curses.COLOR_YELLOW, Curses.COLOR_BLACK);
				Colors.Menu.Disabled = MakeColor (Curses.COLOR_WHITE, Curses.COLOR_GRAY);

				Colors.Dialog.Normal = MakeColor (Curses.COLOR_BLACK, Curses.COLOR_WHITE);
				Colors.Dialog.Focus = MakeColor (Curses.COLOR_WHITE, Curses.COLOR_GRAY);
				Colors.Dialog.HotNormal = MakeColor (Curses.COLOR_BLUE, Curses.COLOR_WHITE);
				Colors.Dialog.HotFocus = MakeColor (Curses.COLOR_BLUE, Curses.COLOR_GRAY);

				Colors.Error.Normal = MakeColor (Curses.COLOR_RED, Curses.COLOR_WHITE);
				Colors.Error.Focus = Curses.A_BOLD | MakeColor (Curses.COLOR_WHITE, Curses.COLOR_RED);
				Colors.Error.HotNormal = MakeColor (Curses.COLOR_BLACK, Curses.COLOR_WHITE);
				Colors.Error.HotFocus = Curses.A_BOLD | MakeColor (Curses.COLOR_BLACK, Curses.COLOR_RED);
			} else {
				Colors.TopLevel.Normal = Curses.COLOR_GREEN;
				Colors.TopLevel.Focus = Curses.COLOR_WHITE;
				Colors.TopLevel.HotNormal = Curses.COLOR_YELLOW;
				Colors.TopLevel.HotFocus = Curses.COLOR_YELLOW;
				Colors.Base.Normal = Curses.A_NORMAL;
				Colors.Base.Focus = Curses.A_REVERSE;
				Colors.Base.HotNormal = Curses.A_BOLD;
				Colors.Base.HotFocus = Curses.A_BOLD | Curses.A_REVERSE;
				Colors.Menu.Normal = Curses.A_REVERSE;
				Colors.Menu.Focus = Curses.A_NORMAL;
				Colors.Menu.HotNormal = Curses.A_BOLD;
				Colors.Menu.HotFocus = Curses.A_NORMAL;
				Colors.Dialog.Normal = Curses.A_REVERSE;
				Colors.Dialog.Focus = Curses.A_NORMAL;
				Colors.Dialog.HotNormal = Curses.A_BOLD;
				Colors.Dialog.HotFocus = Curses.A_NORMAL;
				Colors.Error.Normal = Curses.A_BOLD;
				Colors.Error.Focus = Curses.A_BOLD | Curses.A_REVERSE;
				Colors.Error.HotNormal = Curses.A_BOLD | Curses.A_REVERSE;
				Colors.Error.HotFocus = Curses.A_REVERSE;
			}
		}

		public static bool Is_WSL_Platform ()
		{
			var result = BashRunner.Run ("uname -a");
			if (result.Contains ("microsoft") && result.Contains ("WSL")) {
				return true;
			}
			return false;
		}

		static int MapColor (Color color)
		{
			switch (color) {
			case Color.Black:
				return Curses.COLOR_BLACK;
			case Color.Blue:
				return Curses.COLOR_BLUE;
			case Color.Green:
				return Curses.COLOR_GREEN;
			case Color.Cyan:
				return Curses.COLOR_CYAN;
			case Color.Red:
				return Curses.COLOR_RED;
			case Color.Magenta:
				return Curses.COLOR_MAGENTA;
			case Color.Brown:
				return Curses.COLOR_YELLOW;
			case Color.Gray:
				return Curses.COLOR_WHITE;
			case Color.DarkGray:
				//return Curses.COLOR_BLACK | Curses.A_BOLD;
				return Curses.COLOR_GRAY;
			case Color.BrightBlue:
				return Curses.COLOR_BLUE | Curses.A_BOLD | Curses.COLOR_GRAY;
			case Color.BrightGreen:
				return Curses.COLOR_GREEN | Curses.A_BOLD | Curses.COLOR_GRAY;
			case Color.BrightCyan:
				return Curses.COLOR_CYAN | Curses.A_BOLD | Curses.COLOR_GRAY;
			case Color.BrightRed:
				return Curses.COLOR_RED | Curses.A_BOLD | Curses.COLOR_GRAY;
			case Color.BrightMagenta:
				return Curses.COLOR_MAGENTA | Curses.A_BOLD | Curses.COLOR_GRAY;
			case Color.BrightYellow:
				return Curses.COLOR_YELLOW | Curses.A_BOLD | Curses.COLOR_GRAY;
			case Color.White:
				return Curses.COLOR_WHITE | Curses.A_BOLD | Curses.COLOR_GRAY;
			}
			throw new ArgumentException ("Invalid color code");
		}

		static Color MapCursesColor (int color)
		{
			switch (color) {
			case Curses.COLOR_BLACK:
				return Color.Black;
			case Curses.COLOR_BLUE:
				return Color.Blue;
			case Curses.COLOR_GREEN:
				return Color.Green;
			case Curses.COLOR_CYAN:
				return Color.Cyan;
			case Curses.COLOR_RED:
				return Color.Red;
			case Curses.COLOR_MAGENTA:
				return Color.Magenta;
			case Curses.COLOR_YELLOW:
				return Color.Brown;
			case Curses.COLOR_WHITE:
				return Color.Gray;
			case Curses.COLOR_GRAY:
				return Color.DarkGray;
			case Curses.COLOR_BLUE | Curses.COLOR_GRAY:
				return Color.BrightBlue;
			case Curses.COLOR_GREEN | Curses.COLOR_GRAY:
				return Color.BrightGreen;
			case Curses.COLOR_CYAN | Curses.COLOR_GRAY:
				return Color.BrightCyan;
			case Curses.COLOR_RED | Curses.COLOR_GRAY:
				return Color.BrightRed;
			case Curses.COLOR_MAGENTA | Curses.COLOR_GRAY:
				return Color.BrightMagenta;
			case Curses.COLOR_YELLOW | Curses.COLOR_GRAY:
				return Color.BrightYellow;
			case Curses.COLOR_WHITE | Curses.COLOR_GRAY:
				return Color.White;
			}
			throw new ArgumentException ("Invalid curses color code");
		}

		public override Attribute MakeAttribute (Color fore, Color back)
		{
			var f = MapColor (fore);
			//return MakeColor ((short)(f & 0xffff), (short)MapColor (back)) | ((f & Curses.A_BOLD) != 0 ? Curses.A_BOLD : 0);
			return MakeColor ((short)(f & 0xffff), (short)MapColor (back));
		}

		public override void Suspend ()
		{
			if (reportableMouseEvents.HasFlag (Curses.Event.ReportMousePosition))
				StopReportingMouseMoves ();
			Platform.Suspend ();
			Curses.Window.Standard.redrawwin ();
			Curses.refresh ();
			if (reportableMouseEvents.HasFlag (Curses.Event.ReportMousePosition))
				StartReportingMouseMoves ();
		}

		public override void StartReportingMouseMoves ()
		{
			Console.Out.Write ("\x1b[?1003h");
			Console.Out.Flush ();
		}

		public override void StopReportingMouseMoves ()
		{
			Console.Out.Write ("\x1b[?1003l");
			Console.Out.Flush ();
		}

		//int lastMouseInterval;
		//bool mouseGrabbed;

		public override void UncookMouse ()
		{
			//if (mouseGrabbed)
			//	return;
			//lastMouseInterval = Curses.mouseinterval (0);
			//mouseGrabbed = true;
		}

		public override void CookMouse ()
		{
			//mouseGrabbed = false;
			//Curses.mouseinterval (lastMouseInterval);
		}

		public override Attribute GetAttribute ()
		{
			return currentAttribute;
		}

		/// <inheritdoc/>
		public override bool GetCursorVisibility (out CursorVisibility visibility)
		{
			visibility = CursorVisibility.Invisible;

			if (!currentCursorVisibility.HasValue)
				return false;

			visibility = currentCursorVisibility.Value;

			return true;
		}

		/// <inheritdoc/>
		public override bool SetCursorVisibility (CursorVisibility visibility)
		{
			if (initialCursorVisibility.HasValue == false)
				return false;

			Curses.curs_set (((int)visibility >> 16) & 0x000000FF);

			if (visibility != CursorVisibility.Invisible) {
				Console.Out.Write ("\x1b[{0} q", ((int)visibility >> 24) & 0xFF);
				Console.Out.Flush ();
			}

			currentCursorVisibility = visibility;

			return true;
		}

		/// <inheritdoc/>
		public override bool EnsureCursorVisibility ()
		{
			return false;
		}

		public override void SendKeys (char keyChar, ConsoleKey key, bool shift, bool alt, bool control)
		{
			Key k;

			if ((shift || alt || control)
				&& keyChar - (int)Key.Space >= (uint)Key.A && keyChar - (int)Key.Space <= (uint)Key.Z) {
				k = (Key)(keyChar - (uint)Key.Space);
			} else {
				k = (Key)keyChar;
			}
			if (shift) {
				k |= Key.ShiftMask;
			}
			if (alt) {
				k |= Key.AltMask;
			}
			if (control) {
				k |= Key.CtrlMask;
			}
			keyHandler (new KeyEvent (k, MapKeyModifiers (k)));
		}
	}

	internal static class Platform {
		[DllImport ("libc")]
		static extern int uname (IntPtr buf);

		[DllImport ("libc")]
		static extern int killpg (int pgrp, int pid);

		static int suspendSignal;

		static int GetSuspendSignal ()
		{
			if (suspendSignal != 0)
				return suspendSignal;

			IntPtr buf = Marshal.AllocHGlobal (8192);
			if (uname (buf) != 0) {
				Marshal.FreeHGlobal (buf);
				suspendSignal = -1;
				return suspendSignal;
			}
			try {
				switch (Marshal.PtrToStringAnsi (buf)) {
				case "Darwin":
				case "DragonFly":
				case "FreeBSD":
				case "NetBSD":
				case "OpenBSD":
					suspendSignal = 18;
					break;
				case "Linux":
					// TODO: should fetch the machine name and
					// if it is MIPS return 24
					suspendSignal = 20;
					break;
				case "Solaris":
					suspendSignal = 24;
					break;
				default:
					suspendSignal = -1;
					break;
				}
				return suspendSignal;
			} finally {
				Marshal.FreeHGlobal (buf);
			}
		}

		/// <summary>
		/// Suspends the process by sending SIGTSTP to itself
		/// </summary>
		/// <returns>The suspend.</returns>
		static public bool Suspend ()
		{
			int signal = GetSuspendSignal ();
			if (signal == -1)
				return false;
			killpg (0, signal);
			return true;
		}
	}

	class CursesClipboard : ClipboardBase {
		public override bool IsSupported => CheckSupport ();

		bool CheckSupport ()
		{
			try {
				var result = BashRunner.Run ("which xclip");
				return BashRunner.FileExists (result);
			} catch (Exception) {
				// Permissions issue.
				return false;
			}
		}

		protected override string GetClipboardDataImpl ()
		{
			var tempFileName = System.IO.Path.GetTempFileName ();
			try {
				// BashRunner.Run ($"xsel -o --clipboard > {tempFileName}");
				BashRunner.Run ($"xclip -selection clipboard -o > {tempFileName}");
				return System.IO.File.ReadAllText (tempFileName);
			} finally {
				System.IO.File.Delete (tempFileName);
			}
		}

		protected override void SetClipboardDataImpl (string text)
		{
			// var tempFileName = System.IO.Path.GetTempFileName ();
			// System.IO.File.WriteAllText (tempFileName, text);
			// try {
			// 	// BashRunner.Run ($"cat {tempFileName} | xsel -i --clipboard");
			// 	BashRunner.Run ($"cat {tempFileName} | xclip -selection clipboard");
			// } finally {
			// 	System.IO.File.Delete (tempFileName);
			// }

			BashRunner.Run ("xclip -selection clipboard -i", false, text);
		}
	}

	static class BashRunner {
		public static string Run (string commandLine, bool output = true, string inputText = "")
		{
			var arguments = $"-c \"{commandLine}\"";

			if (output) {
				var errorBuilder = new System.Text.StringBuilder ();
				var outputBuilder = new System.Text.StringBuilder ();

				using (var process = new System.Diagnostics.Process {
					StartInfo = new System.Diagnostics.ProcessStartInfo {
						FileName = "bash",
						Arguments = arguments,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						UseShellExecute = false,
						CreateNoWindow = false,
					}
				}) {
					process.Start ();
					process.OutputDataReceived += (sender, args) => { outputBuilder.AppendLine (args.Data); };
					process.BeginOutputReadLine ();
					process.ErrorDataReceived += (sender, args) => { errorBuilder.AppendLine (args.Data); };
					process.BeginErrorReadLine ();
					if (!process.DoubleWaitForExit ()) {
						var timeoutError = $@"Process timed out. Command line: bash {arguments}.
							Output: {outputBuilder}
							Error: {errorBuilder}";
						throw new Exception (timeoutError);
					}
					if (process.ExitCode == 0) {
						return outputBuilder.ToString ();
					}

					var error = $@"Could not execute process. Command line: bash {arguments}.
						Output: {outputBuilder}
						Error: {errorBuilder}";
					throw new Exception (error);
				}
			} else {
				using (var process = new System.Diagnostics.Process {
					StartInfo = new System.Diagnostics.ProcessStartInfo {
						FileName = "bash",
						Arguments = arguments,
						RedirectStandardInput = true,
						UseShellExecute = false,
						CreateNoWindow = false
					}
				}) {
					process.Start ();
					process.StandardInput.Write (inputText);
					process.StandardInput.Close ();
					process.WaitForExit ();
					return inputText;
				}
			}
		}

		static bool DoubleWaitForExit (this System.Diagnostics.Process process)
		{
			var result = process.WaitForExit (500);
			if (result) {
				process.WaitForExit ();
			}
			return result;
		}

		public static bool FileExists (string value)
		{
			return !string.IsNullOrEmpty (value) && !value.Contains ("not found");
		}
	}

	class MacOSXClipboard : ClipboardBase {
		IntPtr nsString = objc_getClass ("NSString");
		IntPtr nsPasteboard = objc_getClass ("NSPasteboard");
		IntPtr utfTextType;
		IntPtr generalPasteboard;
		IntPtr initWithUtf8Register = sel_registerName ("initWithUTF8String:");
		IntPtr allocRegister = sel_registerName ("alloc");
		IntPtr setStringRegister = sel_registerName ("setString:forType:");
		IntPtr stringForTypeRegister = sel_registerName ("stringForType:");
		IntPtr utf8Register = sel_registerName ("UTF8String");
		IntPtr nsStringPboardType;
		IntPtr generalPasteboardRegister = sel_registerName ("generalPasteboard");
		IntPtr clearContentsRegister = sel_registerName ("clearContents");

		public override bool IsSupported => CheckSupport ();

		bool CheckSupport ()
		{
			var result = BashRunner.Run ("which pbcopy");
			if (!BashRunner.FileExists (result)) {
				return false;
			}
			result = BashRunner.Run ("which pbpaste");
			return BashRunner.FileExists (result);
		}

		public MacOSXClipboard ()
		{
			utfTextType = objc_msgSend (objc_msgSend (nsString, allocRegister), initWithUtf8Register, "public.utf8-plain-text");
			nsStringPboardType = objc_msgSend (objc_msgSend (nsString, allocRegister), initWithUtf8Register, "NSStringPboardType");
			generalPasteboard = objc_msgSend (nsPasteboard, generalPasteboardRegister);
		}

		protected override string GetClipboardDataImpl ()
		{
			var ptr = objc_msgSend (generalPasteboard, stringForTypeRegister, nsStringPboardType);
			var charArray = objc_msgSend (ptr, utf8Register);
			return Marshal.PtrToStringAnsi (charArray);
		}

		protected override void SetClipboardDataImpl (string text)
		{
			IntPtr str = default;
			try {
				str = objc_msgSend (objc_msgSend (nsString, allocRegister), initWithUtf8Register, text);
				objc_msgSend (generalPasteboard, clearContentsRegister);
				objc_msgSend (generalPasteboard, setStringRegister, str, utfTextType);
			} finally {
				if (str != default) {
					objc_msgSend (str, sel_registerName ("release"));
				}
			}
		}

		[DllImport ("/System/Library/Frameworks/AppKit.framework/AppKit")]
		static extern IntPtr objc_getClass (string className);

		[DllImport ("/System/Library/Frameworks/AppKit.framework/AppKit")]
		static extern IntPtr objc_msgSend (IntPtr receiver, IntPtr selector);

		[DllImport ("/System/Library/Frameworks/AppKit.framework/AppKit")]
		static extern IntPtr objc_msgSend (IntPtr receiver, IntPtr selector, string arg1);

		[DllImport ("/System/Library/Frameworks/AppKit.framework/AppKit")]
		static extern IntPtr objc_msgSend (IntPtr receiver, IntPtr selector, IntPtr arg1);

		[DllImport ("/System/Library/Frameworks/AppKit.framework/AppKit")]
		static extern IntPtr objc_msgSend (IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

		[DllImport ("/System/Library/Frameworks/AppKit.framework/AppKit")]
		static extern IntPtr sel_registerName (string selectorName);
	}

	class WSLClipboard : ClipboardBase {
		public override bool IsSupported => CheckSupport ();

		bool CheckSupport ()
		{
			var result = BashRunner.Run ("which powershell.exe");
			return BashRunner.FileExists (result);

			//var result = BashRunner.Run ("which powershell.exe");
			//if (!BashRunner.FileExists (result)) {
			//	return false;
			//}
			//result = BashRunner.Run ("which clip.exe");
			//return BashRunner.FileExists (result);
		}

		protected override string GetClipboardDataImpl ()
		{
			using (var powershell = new System.Diagnostics.Process {
				StartInfo = new System.Diagnostics.ProcessStartInfo {
					RedirectStandardOutput = true,
					FileName = "powershell.exe",
					Arguments = "-noprofile -command \"Get-Clipboard\""
				}
			}) {
				powershell.Start ();
				var result = powershell.StandardOutput.ReadToEnd ();
				powershell.StandardOutput.Close ();
				powershell.WaitForExit ();
				return result.TrimEnd ();
			}
		}

		protected override void SetClipboardDataImpl (string text)
		{
			using (var powershell = new System.Diagnostics.Process {
				StartInfo = new System.Diagnostics.ProcessStartInfo {
					FileName = "powershell.exe",
					Arguments = $"-noprofile -command \"Set-Clipboard -Value \\\"{text}\\\"\""
				}
			}) {
				powershell.Start ();
				powershell.WaitForExit ();
			}

			//using (var clipExe = new System.Diagnostics.Process {
			//	StartInfo = new System.Diagnostics.ProcessStartInfo {
			//		FileName = "clip.exe",
			//		RedirectStandardInput = true
			//	}
			//}) {
			//	clipExe.Start ();
			//	clipExe.StandardInput.Write (text);
			//	clipExe.StandardInput.Close ();
			//	clipExe.WaitForExit ();
			//}
		}
	}
}
