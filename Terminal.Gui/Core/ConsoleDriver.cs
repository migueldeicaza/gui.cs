//
// ConsoleDriver.cs: Definition for the Console Driver API
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// Define this to enable diagnostics drawing for Window Frames
using NStack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Terminal.Gui {
	/// <summary>
	/// Basic colors that can be used to set the foreground and background colors in console applications.
	/// </summary>
	public enum Color {
		/// <summary>
		/// The black color.
		/// </summary>
		Black,
		/// <summary>
		/// The blue color.
		/// </summary>
		Blue,
		/// <summary>
		/// The green color.
		/// </summary>
		Green,
		/// <summary>
		/// The cyan color.
		/// </summary>
		Cyan,
		/// <summary>
		/// The red color.
		/// </summary>
		Red,
		/// <summary>
		/// The magenta color.
		/// </summary>
		Magenta,
		/// <summary>
		/// The brown color.
		/// </summary>
		Brown,
		/// <summary>
		/// The gray color.
		/// </summary>
		Gray,
		/// <summary>
		/// The dark gray color.
		/// </summary>
		DarkGray,
		/// <summary>
		/// The bright bBlue color.
		/// </summary>
		BrightBlue,
		/// <summary>
		/// The bright green color.
		/// </summary>
		BrightGreen,
		/// <summary>
		/// The bright cyan color.
		/// </summary>
		BrightCyan,
		/// <summary>
		/// The bright red color.
		/// </summary>
		BrightRed,
		/// <summary>
		/// The bright magenta color.
		/// </summary>
		BrightMagenta,
		/// <summary>
		/// The bright yellow color.
		/// </summary>
		BrightYellow,
		/// <summary>
		/// The White color.
		/// </summary>
		White
	}

	/// <summary>
	/// Attributes are used as elements that contain both a foreground and a background or platform specific features
	/// </summary>
	/// <remarks>
	///   <see cref="Attribute"/>s are needed to map colors to terminal capabilities that might lack colors, on color
	///   scenarios, they encode both the foreground and the background color and are used in the <see cref="ColorScheme"/>
	///   class to define color schemes that can be used in your application.
	/// </remarks>
	public struct Attribute {
		/// <summary>
		/// The color attribute value.
		/// </summary>
		public int Value { get; }
		/// <summary>
		/// The foreground color.
		/// </summary>
		public Color Foreground { get; }
		/// <summary>
		/// The background color.
		/// </summary>
		public Color Background { get; }
		/// <summary>
		/// The background color.
		/// </summary>
		public bool UnderLine { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="Attribute"/> struct.
		/// </summary>
		/// <param name="value">Value.</param>
		/// <param name="foreground">Foreground</param>
		/// <param name="background">Background</param>
		/// <param name="underline">Underline</param>
		public Attribute (int value, Color foreground = new Color (), Color background = new Color (), bool underline = false)
		{
			Value = value;
			UnderLine = underline;
			Foreground = foreground;
			Background = background;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Attribute"/> struct.
		/// </summary>
		/// <param name="foreground">Foreground</param>
		/// <param name="background">Background</param>
		/// <param name="underline">Underline</param>
		public Attribute (Color foreground = new Color (), Color background = new Color (), bool underline = false)
		{
			Value = Make (foreground, background).Value;
			UnderLine = underline;
			Foreground = foreground;
			Background = background;
		}

		/// <summary>
		/// Implicit conversion from an <see cref="Attribute"/> to the underlying Int32 representation
		/// </summary>
		/// <returns>The integer value stored in the attribute.</returns>
		/// <param name="c">The attribute to convert</param>
		public static implicit operator int (Attribute c) => c.Value;

		/// <summary>
		/// Implicitly convert an integer value into an <see cref="Attribute"/>
		/// </summary>
		/// <returns>An attribute with the specified integer value.</returns>
		/// <param name="v">value</param>
		public static implicit operator Attribute (int v) => new Attribute (v);

		/// <summary>
		/// Creates an <see cref="Attribute"/> from the specified foreground and background.
		/// </summary>
		/// <returns>The make.</returns>
		/// <param name="foreground">Foreground color to use.</param>
		/// <param name="background">Background color to use.</param>
		public static Attribute Make (Color foreground, Color background)
		{
			if (Application.Driver == null)
				throw new InvalidOperationException ("The Application has not been initialized");
			return Application.Driver.MakeAttribute (foreground, background);
		}

		/// <summary>
		/// Gets the current <see cref="Attribute"/> from the driver.
		/// </summary>
		/// <returns>The current attribute.</returns>
		public static Attribute Get ()
		{
			if (Application.Driver == null)
				throw new InvalidOperationException ("The Application has not been initialized");
			return Application.Driver.GetAttribute ();
		}
	}

	/// <summary>
	/// Color scheme definitions, they cover some common scenarios and are used
	/// typically in containers such as <see cref="Window"/> and <see cref="FrameView"/> to set the scheme that is used by all the
	/// views contained inside.
	/// </summary>
	public class ColorScheme : IEquatable<ColorScheme> {
		Attribute _normal;
		Attribute _focus;
		Attribute _hotNormal;
		Attribute _hotFocus;
		Attribute _disabled;
		internal string caller = "";

		/// <summary>
		/// The default color for text, when the view is not focused.
		/// </summary>
		public Attribute Normal { get { return _normal; } set { _normal = SetAttribute (value); } }

		/// <summary>
		/// The color for text when the view has the focus.
		/// </summary>
		public Attribute Focus { get { return _focus; } set { _focus = SetAttribute (value); } }

		/// <summary>
		/// The color for the hotkey when a view is not focused
		/// </summary>
		public Attribute HotNormal { get { return _hotNormal; } set { _hotNormal = SetAttribute (value); } }

		/// <summary>
		/// The color for the hotkey when the view is focused.
		/// </summary>
		public Attribute HotFocus { get { return _hotFocus; } set { _hotFocus = SetAttribute (value); } }

		/// <summary>
		/// The default color for text, when the view is disabled.
		/// </summary>
		public Attribute Disabled { get { return _disabled; } set { _disabled = SetAttribute (value); } }

		bool preparingScheme = false;

		Attribute SetAttribute (Attribute attribute, [CallerMemberName] string callerMemberName = null)
		{
			if (!Application._initialized && !preparingScheme)
				return attribute;

			if (preparingScheme)
				return attribute;

			preparingScheme = true;
			switch (caller) {
			case "TopLevel":
				switch (callerMemberName) {
				case "Normal":
					HotNormal = Application.Driver.MakeAttribute (HotNormal.Foreground, attribute.Background);
					break;
				case "Focus":
					HotFocus = Application.Driver.MakeAttribute (HotFocus.Foreground, attribute.Background);
					break;
				case "HotNormal":
					HotFocus = Application.Driver.MakeAttribute (attribute.Foreground, HotFocus.Background);
					break;
				case "HotFocus":
					HotNormal = Application.Driver.MakeAttribute (attribute.Foreground, HotNormal.Background);
					if (Focus.Foreground != attribute.Background)
						Focus = Application.Driver.MakeAttribute (Focus.Foreground, attribute.Background);
					break;
				}
				break;

			case "Base":
				switch (callerMemberName) {
				case "Normal":
					HotNormal = Application.Driver.MakeAttribute (HotNormal.Foreground, attribute.Background);
					break;
				case "Focus":
					HotFocus = Application.Driver.MakeAttribute (HotFocus.Foreground, attribute.Background);
					break;
				case "HotNormal":
					HotFocus = Application.Driver.MakeAttribute (attribute.Foreground, HotFocus.Background);
					Normal = Application.Driver.MakeAttribute (Normal.Foreground, attribute.Background);
					break;
				case "HotFocus":
					HotNormal = Application.Driver.MakeAttribute (attribute.Foreground, HotNormal.Background);
					if (Focus.Foreground != attribute.Background)
						Focus = Application.Driver.MakeAttribute (Focus.Foreground, attribute.Background);
					break;
				}
				break;

			case "Menu":
				switch (callerMemberName) {
				case "Normal":
					if (Focus.Background != attribute.Background)
						Focus = Application.Driver.MakeAttribute (attribute.Foreground, Focus.Background);
					HotNormal = Application.Driver.MakeAttribute (HotNormal.Foreground, attribute.Background);
					Disabled = Application.Driver.MakeAttribute (Disabled.Foreground, attribute.Background);
					break;
				case "Focus":
					Normal = Application.Driver.MakeAttribute (attribute.Foreground, Normal.Background);
					HotFocus = Application.Driver.MakeAttribute (HotFocus.Foreground, attribute.Background);
					break;
				case "HotNormal":
					if (Focus.Background != attribute.Background)
						HotFocus = Application.Driver.MakeAttribute (attribute.Foreground, HotFocus.Background);
					Normal = Application.Driver.MakeAttribute (Normal.Foreground, attribute.Background);
					Disabled = Application.Driver.MakeAttribute (Disabled.Foreground, attribute.Background);
					break;
				case "HotFocus":
					HotNormal = Application.Driver.MakeAttribute (attribute.Foreground, HotNormal.Background);
					if (Focus.Foreground != attribute.Background)
						Focus = Application.Driver.MakeAttribute (Focus.Foreground, attribute.Background);
					break;
				case "Disabled":
					if (Focus.Background != attribute.Background)
						HotFocus = Application.Driver.MakeAttribute (attribute.Foreground, HotFocus.Background);
					Normal = Application.Driver.MakeAttribute (Normal.Foreground, attribute.Background);
					HotNormal = Application.Driver.MakeAttribute (HotNormal.Foreground, attribute.Background);
					break;
				}
				break;

			case "Dialog":
				switch (callerMemberName) {
				case "Normal":
					if (Focus.Background != attribute.Background)
						Focus = Application.Driver.MakeAttribute (attribute.Foreground, Focus.Background);
					HotNormal = Application.Driver.MakeAttribute (HotNormal.Foreground, attribute.Background);
					break;
				case "Focus":
					Normal = Application.Driver.MakeAttribute (attribute.Foreground, Normal.Background);
					HotFocus = Application.Driver.MakeAttribute (HotFocus.Foreground, attribute.Background);
					break;
				case "HotNormal":
					if (Focus.Background != attribute.Background)
						HotFocus = Application.Driver.MakeAttribute (attribute.Foreground, HotFocus.Background);
					if (Normal.Foreground != attribute.Background)
						Normal = Application.Driver.MakeAttribute (Normal.Foreground, attribute.Background);
					break;
				case "HotFocus":
					HotNormal = Application.Driver.MakeAttribute (attribute.Foreground, HotNormal.Background);
					if (Focus.Foreground != attribute.Background)
						Focus = Application.Driver.MakeAttribute (Focus.Foreground, attribute.Background);
					break;
				}
				break;

			case "Error":
				switch (callerMemberName) {
				case "Normal":
					HotNormal = Application.Driver.MakeAttribute (HotNormal.Foreground, attribute.Background);
					HotFocus = Application.Driver.MakeAttribute (HotFocus.Foreground, attribute.Background);
					break;
				case "HotNormal":
				case "HotFocus":
					HotFocus = Application.Driver.MakeAttribute (attribute.Foreground, attribute.Background);
					Normal = Application.Driver.MakeAttribute (Normal.Foreground, attribute.Background);
					break;
				}
				break;
			}
			preparingScheme = false;
			return attribute;
		}

		/// <summary>
		/// Compares two <see cref="ColorScheme"/> objects for equality.
		/// </summary>
		/// <param name="obj"></param>
		/// <returns>true if the two objects are equal</returns>
		public override bool Equals (object obj)
		{
			return Equals (obj as ColorScheme);
		}

		/// <summary>
		/// Compares two <see cref="ColorScheme"/> objects for equality.
		/// </summary>
		/// <param name="other"></param>
		/// <returns>true if the two objects are equal</returns>
		public bool Equals (ColorScheme other)
		{
			return other != null &&
			       EqualityComparer<Attribute>.Default.Equals (_normal, other._normal) &&
			       EqualityComparer<Attribute>.Default.Equals (_focus, other._focus) &&
			       EqualityComparer<Attribute>.Default.Equals (_hotNormal, other._hotNormal) &&
			       EqualityComparer<Attribute>.Default.Equals (_hotFocus, other._hotFocus) &&
			       EqualityComparer<Attribute>.Default.Equals (_disabled, other._disabled);
		}

		/// <summary>
		/// Returns a hashcode for this instance.
		/// </summary>
		/// <returns>hashcode for this instance</returns>
		public override int GetHashCode ()
		{
			int hashCode = -1242460230;
			hashCode = hashCode * -1521134295 + _normal.GetHashCode ();
			hashCode = hashCode * -1521134295 + _focus.GetHashCode ();
			hashCode = hashCode * -1521134295 + _hotNormal.GetHashCode ();
			hashCode = hashCode * -1521134295 + _hotFocus.GetHashCode ();
			hashCode = hashCode * -1521134295 + _disabled.GetHashCode ();
			return hashCode;
		}

		/// <summary>
		/// Compares two <see cref="ColorScheme"/> objects for equality.
		/// </summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns><c>true</c> if the two objects are equivalent</returns>
		public static bool operator == (ColorScheme left, ColorScheme right)
		{
			return EqualityComparer<ColorScheme>.Default.Equals (left, right);
		}

		/// <summary>
		/// Compares two <see cref="ColorScheme"/> objects for inequality.
		/// </summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns><c>true</c> if the two objects are not equivalent</returns>
		public static bool operator != (ColorScheme left, ColorScheme right)
		{
			return !(left == right);
		}
	}

	/// <summary>
	/// The default <see cref="ColorScheme"/>s for the application.
	/// </summary>
	public static class Colors {
		static Colors ()
		{
			// Use reflection to dynamically create the default set of ColorSchemes from the list defined 
			// by the class. 
			ColorSchemes = typeof (Colors).GetProperties ()
				.Where (p => p.PropertyType == typeof (ColorScheme))
				.Select (p => new KeyValuePair<string, ColorScheme> (p.Name, new ColorScheme ())) // (ColorScheme)p.GetValue (p)))
				.ToDictionary (t => t.Key, t => t.Value);
		}

		/// <summary>
		/// The application toplevel color scheme, for the default toplevel views.
		/// </summary>
		/// <remarks>
		/// <para>
		///	This API will be deprecated in the future. Use <see cref="Colors.ColorSchemes"/> instead (e.g. <c>edit.ColorScheme = Colors.ColorSchemes["TopLevel"];</c>
		/// </para>
		/// </remarks>
		public static ColorScheme TopLevel { get => GetColorScheme (); set => SetColorScheme (value); }

		/// <summary>
		/// The base color scheme, for the default toplevel views.
		/// </summary>
		/// <remarks>
		/// <para>
		///	This API will be deprecated in the future. Use <see cref="Colors.ColorSchemes"/> instead (e.g. <c>edit.ColorScheme = Colors.ColorSchemes["Base"];</c>
		/// </para>
		/// </remarks>
		public static ColorScheme Base { get => GetColorScheme (); set => SetColorScheme (value); }

		/// <summary>
		/// The dialog color scheme, for standard popup dialog boxes
		/// </summary>
		/// <remarks>
		/// <para>
		///	This API will be deprecated in the future. Use <see cref="Colors.ColorSchemes"/> instead (e.g. <c>edit.ColorScheme = Colors.ColorSchemes["Dialog"];</c>
		/// </para>
		/// </remarks>
		public static ColorScheme Dialog { get => GetColorScheme (); set => SetColorScheme (value); }

		/// <summary>
		/// The menu bar color
		/// </summary>
		/// <remarks>
		/// <para>
		///	This API will be deprecated in the future. Use <see cref="Colors.ColorSchemes"/> instead (e.g. <c>edit.ColorScheme = Colors.ColorSchemes["Menu"];</c>
		/// </para>
		/// </remarks>
		public static ColorScheme Menu { get => GetColorScheme (); set => SetColorScheme (value); }

		/// <summary>
		/// The color scheme for showing errors.
		/// </summary>
		/// <remarks>
		/// <para>
		///	This API will be deprecated in the future. Use <see cref="Colors.ColorSchemes"/> instead (e.g. <c>edit.ColorScheme = Colors.ColorSchemes["Error"];</c>
		/// </para>
		/// </remarks>
		public static ColorScheme Error { get => GetColorScheme (); set => SetColorScheme (value); }

		static ColorScheme GetColorScheme ([CallerMemberName] string callerMemberName = null)
		{
			return ColorSchemes [callerMemberName];
		}

		static void SetColorScheme (ColorScheme colorScheme, [CallerMemberName] string callerMemberName = null)
		{
			ColorSchemes [callerMemberName] = colorScheme;
			colorScheme.caller = callerMemberName;
		}

		/// <summary>
		/// Provides the defined <see cref="ColorScheme"/>s.
		/// </summary>
		public static Dictionary<string, ColorScheme> ColorSchemes { get; }
	}

	/// <summary>
	/// Cursors Visibility that are displayed
	/// </summary>
	// 
	// Hexa value are set as 0xAABBCCDD where :
	//
	//     AA stand for the TERMINFO DECSUSR parameter value to be used under Linux & MacOS
	//     BB stand for the NCurses curs_set parameter value to be used under Linux & MacOS
	//     CC stand for the CONSOLE_CURSOR_INFO.bVisible parameter value to be used under Windows
	//     DD stand for the CONSOLE_CURSOR_INFO.dwSize parameter value to be used under Windows
	//
	public enum CursorVisibility {
		/// <summary>
		///	Cursor caret has default
		/// </summary>
		/// <remarks>Works under Xterm-like terminal otherwise this is equivalent to <see ref="Underscore"/>. This default directly depends of the XTerm user configuration settings so it could be Block, I-Beam, Underline with possible blinking.</remarks>
		Default = 0x00010119,

		/// <summary>
		///	Cursor caret is hidden
		/// </summary>
		Invisible = 0x03000019,

		/// <summary>
		///	Cursor caret is normally shown as a blinking underline bar _
		/// </summary>
		Underline = 0x03010119,

		/// <summary>
		///	Cursor caret is normally shown as a underline bar _
		/// </summary>
		/// <remarks>Under Windows, this is equivalent to <see ref="UnderscoreBlinking"/></remarks>
		UnderlineFix = 0x04010119,

		/// <summary>
		///	Cursor caret is displayed a blinking vertical bar |
		/// </summary>
		/// <remarks>Works under Xterm-like terminal otherwise this is equivalent to <see ref="Underscore"/></remarks>
		Vertical = 0x05010119,

		/// <summary>
		///	Cursor caret is displayed a blinking vertical bar |
		/// </summary>
		/// <remarks>Works under Xterm-like terminal otherwise this is equivalent to <see ref="Underscore"/></remarks>
		VerticalFix = 0x06010119,

		/// <summary>
		///	Cursor caret is displayed as a blinking block ▉
		/// </summary>
		Box = 0x01020164,

		/// <summary>
		///	Cursor caret is displayed a block ▉
		/// </summary>
		/// <remarks>Works under Xterm-like terminal otherwise this is equivalent to <see ref="Block"/></remarks>
		BoxFix = 0x02020164,
	}

	///// <summary>
	///// Special characters that can be drawn with 
	///// </summary>
	//public enum SpecialChar {
	//	/// <summary>
	//	/// Horizontal line character.
	//	/// </summary>
	//	HLine,

	//	/// <summary>
	//	/// Vertical line character.
	//	/// </summary>
	//	VLine,

	//	/// <summary>
	//	/// Stipple pattern
	//	/// </summary>
	//	Stipple,

	//	/// <summary>
	//	/// Diamond character
	//	/// </summary>
	//	Diamond,

	//	/// <summary>
	//	/// Upper left corner
	//	/// </summary>
	//	ULCorner,

	//	/// <summary>
	//	/// Lower left corner
	//	/// </summary>
	//	LLCorner,

	//	/// <summary>
	//	/// Upper right corner
	//	/// </summary>
	//	URCorner,

	//	/// <summary>
	//	/// Lower right corner
	//	/// </summary>
	//	LRCorner,

	//	/// <summary>
	//	/// Left tee
	//	/// </summary>
	//	LeftTee,

	//	/// <summary>
	//	/// Right tee
	//	/// </summary>
	//	RightTee,

	//	/// <summary>
	//	/// Top tee
	//	/// </summary>
	//	TopTee,

	//	/// <summary>
	//	/// The bottom tee.
	//	/// </summary>
	//	BottomTee,
	//}

	/// <summary>
	/// ConsoleDriver is an abstract class that defines the requirements for a console driver.  
	/// There are currently three implementations: <see cref="CursesDriver"/> (for Unix and Mac), <see cref="WindowsDriver"/>, and <see cref="NetDriver"/> that uses the .NET Console API.
	/// </summary>
	public abstract class ConsoleDriver {
		/// <summary>
		/// The handler fired when the terminal is resized.
		/// </summary>
		protected Action TerminalResized;

		/// <summary>
		/// The current number of columns in the terminal.
		/// </summary>
		public abstract int Cols { get; }
		/// <summary>
		/// The current number of rows in the terminal.
		/// </summary>
		public abstract int Rows { get; }
		/// <summary>
		/// The current top in the terminal.
		/// </summary>
		public abstract int Top { get; }

		/// <summary>
		/// If false height is measured by the window height and thus no scrolling.
		/// If true then height is measured by the buffer height, enabling scrolling.
		/// </summary>
		public abstract bool HeightAsBuffer { get; set; }

		/// <summary>
		/// Initializes the driver
		/// </summary>
		/// <param name="terminalResized">Method to invoke when the terminal is resized.</param>
		public abstract void Init (Action terminalResized);
		/// <summary>
		/// Moves the cursor to the specified column and row.
		/// </summary>
		/// <param name="col">Column to move the cursor to.</param>
		/// <param name="row">Row to move the cursor to.</param>
		public abstract void Move (int col, int row);
		/// <summary>
		/// Adds the specified rune to the display at the current cursor position
		/// </summary>
		/// <param name="rune">Rune to add.</param>
		public abstract void AddRune (Rune rune);
		/// <summary>
		/// Ensures a Rune is not a control character and can be displayed by translating characters below 0x20
		/// to equivalent, printable, Unicode chars.
		/// </summary>
		/// <param name="c">Rune to translate</param>
		/// <returns></returns>
		public static Rune MakePrintable (Rune c)
		{
			if (c <= 0x1F || (c >= 0x80 && c <= 0x9F)) {
				// ASCII (C0) control characters.
				// C1 control characters (https://www.aivosto.com/articles/control-characters.html#c1)
				return new Rune (c + 0x2400);
			} else {
				return c;
			}
		}

		/// <summary>
		/// Adds the specified
		/// </summary>
		/// <param name="str">String.</param>
		public abstract void AddStr (ustring str);
		/// <summary>
		/// Prepare the driver and set the key and mouse events handlers.
		/// </summary>
		/// <param name="mainLoop">The main loop.</param>
		/// <param name="keyHandler">The handler for ProcessKey</param>
		/// <param name="keyDownHandler">The handler for key down events</param>
		/// <param name="keyUpHandler">The handler for key up events</param>
		/// <param name="mouseHandler">The handler for mouse events</param>
		public abstract void PrepareToRun (MainLoop mainLoop, Action<KeyEvent> keyHandler, Action<KeyEvent> keyDownHandler, Action<KeyEvent> keyUpHandler, Action<MouseEvent> mouseHandler);

		/// <summary>
		/// Updates the screen to reflect all the changes that have been done to the display buffer
		/// </summary>
		public abstract void Refresh ();

		/// <summary>
		/// Updates the location of the cursor position
		/// </summary>
		public abstract void UpdateCursor ();

		/// <summary>
		/// Retreive the cursor caret visibility
		/// </summary>
		/// <param name="visibility">The current <see cref="CursorVisibility"/></param>
		/// <returns>true upon success</returns>
		public abstract bool GetCursorVisibility (out CursorVisibility visibility);

		/// <summary>
		/// Change the cursor caret visibility
		/// </summary>
		/// <param name="visibility">The wished <see cref="CursorVisibility"/></param>
		/// <returns>true upon success</returns>
		public abstract bool SetCursorVisibility (CursorVisibility visibility);

		/// <summary>
		/// Ensure the cursor visibility
		/// </summary>
		/// <returns>true upon success</returns>
		public abstract bool EnsureCursorVisibility ();

		/// <summary>
		/// Ends the execution of the console driver.
		/// </summary>
		public abstract void End ();

		/// <summary>
		/// Redraws the physical screen with the contents that have been queued up via any of the printing commands.
		/// </summary>
		public abstract void UpdateScreen ();

		/// <summary>
		/// Selects the specified attribute as the attribute to use for future calls to AddRune, AddString.
		/// </summary>
		/// <param name="c">C.</param>
		public abstract void SetAttribute (Attribute c);

		/// <summary>
		/// Set Colors from limit sets of colors.
		/// </summary>
		/// <param name="foreground">Foreground.</param>
		/// <param name="background">Background.</param>
		public abstract void SetColors (ConsoleColor foreground, ConsoleColor background);

		// Advanced uses - set colors to any pre-set pairs, you would need to init_color
		// that independently with the R, G, B values.
		/// <summary>
		/// Advanced uses - set colors to any pre-set pairs, you would need to init_color
		/// that independently with the R, G, B values.
		/// </summary>
		/// <param name="foregroundColorId">Foreground color identifier.</param>
		/// <param name="backgroundColorId">Background color identifier.</param>
		public abstract void SetColors (short foregroundColorId, short backgroundColorId);

		/// <summary>
		/// Allows sending keys without typing on a keyboard.
		/// </summary>
		/// <param name="keyChar">The character key.</param>
		/// <param name="key">The key.</param>
		/// <param name="shift">If shift key is sending.</param>
		/// <param name="alt">If alt key is sending.</param>
		/// <param name="control">If control key is sending.</param>
		public abstract void SendKeys (char keyChar, ConsoleKey key, bool shift, bool alt, bool control);

		/// <summary>
		/// Set the handler when the terminal is resized.
		/// </summary>
		/// <param name="terminalResized"></param>
		public void SetTerminalResized (Action terminalResized)
		{
			TerminalResized = terminalResized;
		}

		/// <summary>
		/// Draws the title for a Window-style view incorporating padding. 
		/// </summary>
		/// <param name="region">Screen relative region where the frame will be drawn.</param>
		/// <param name="title">The title for the window. The title will only be drawn if <c>title</c> is not null or empty and paddingTop is greater than 0.</param>
		/// <param name="paddingLeft">Number of columns to pad on the left (if 0 the border will not appear on the left).</param>
		/// <param name="paddingTop">Number of rows to pad on the top (if 0 the border and title will not appear on the top).</param>
		/// <param name="paddingRight">Number of columns to pad on the right (if 0 the border will not appear on the right).</param>
		/// <param name="paddingBottom">Number of rows to pad on the bottom (if 0 the border will not appear on the bottom).</param>
		/// <param name="textAlignment">Not yet implemented.</param>
		/// <remarks></remarks>
		public virtual void DrawWindowTitle (Rect region, ustring title, int paddingLeft, int paddingTop, int paddingRight, int paddingBottom, TextAlignment textAlignment = TextAlignment.Left)
		{
			var width = region.Width - (paddingLeft + 2) * 2;
			if (!ustring.IsNullOrEmpty (title) && width > 4 && region.Y + paddingTop <= region.Y + paddingBottom) {
				Move (region.X + 1 + paddingLeft, region.Y + paddingTop);
				AddRune (' ');
				var str = title.RuneCount >= width ? title [0, width - 2] : title;
				AddStr (str);
				AddRune (' ');
			}
		}

		/// <summary>
		/// Enables diagnostic functions
		/// </summary>
		[Flags]
		public enum DiagnosticFlags : uint {
			/// <summary>
			/// All diagnostics off
			/// </summary>
			Off = 0b_0000_0000,
			/// <summary>
			/// When enabled, <see cref="DrawWindowFrame(Rect, int, int, int, int, bool, bool)"/> will draw a 
			/// ruler in the frame for any side with a padding value greater than 0.
			/// </summary>
			FrameRuler = 0b_0000_0001,
			/// <summary>
			/// When Enabled, <see cref="DrawWindowFrame(Rect, int, int, int, int, bool, bool)"/> will use
			/// 'L', 'R', 'T', and 'B' for padding instead of ' '.
			/// </summary>
			FramePadding = 0b_0000_0010,
		}

		/// <summary>
		/// Set flags to enable/disable <see cref="ConsoleDriver"/> diagnostics.
		/// </summary>
		public static DiagnosticFlags Diagnostics { get; set; }

		/// <summary>
		/// Draws a frame for a window with padding and an optional visible border inside the padding. 
		/// </summary>
		/// <param name="region">Screen relative region where the frame will be drawn.</param>
		/// <param name="paddingLeft">Number of columns to pad on the left (if 0 the border will not appear on the left).</param>
		/// <param name="paddingTop">Number of rows to pad on the top (if 0 the border and title will not appear on the top).</param>
		/// <param name="paddingRight">Number of columns to pad on the right (if 0 the border will not appear on the right).</param>
		/// <param name="paddingBottom">Number of rows to pad on the bottom (if 0 the border will not appear on the bottom).</param>
		/// <param name="border">If set to <c>true</c> and any padding dimension is > 0 the border will be drawn.</param>
		/// <param name="fill">If set to <c>true</c> it will clear the content area (the area inside the padding) with the current color, otherwise the content area will be left untouched.</param>
		public virtual void DrawWindowFrame (Rect region, int paddingLeft = 0, int paddingTop = 0, int paddingRight = 0, int paddingBottom = 0, bool border = true, bool fill = false)
		{
			char clearChar = ' ';
			char leftChar = clearChar;
			char rightChar = clearChar;
			char topChar = clearChar;
			char bottomChar = clearChar;

			if ((Diagnostics & DiagnosticFlags.FramePadding) == DiagnosticFlags.FramePadding) {
				leftChar = 'L';
				rightChar = 'R';
				topChar = 'T';
				bottomChar = 'B';
				clearChar = 'C';
			}

			void AddRuneAt (int col, int row, Rune ch)
			{
				Move (col, row);
				AddRune (ch);
			}

			// fwidth is count of hLine chars
			int fwidth = (int)(region.Width - (paddingRight + paddingLeft));

			// fheight is count of vLine chars
			int fheight = (int)(region.Height - (paddingBottom + paddingTop));

			// fleft is location of left frame line
			int fleft = region.X + paddingLeft - 1;

			// fright is location of right frame line
			int fright = fleft + fwidth + 1;

			// ftop is location of top frame line
			int ftop = region.Y + paddingTop - 1;

			// fbottom is locaiton of bottom frame line
			int fbottom = ftop + fheight + 1;

			Rune hLine = border ? HLine : clearChar;
			Rune vLine = border ? VLine : clearChar;
			Rune uRCorner = border ? URCorner : clearChar;
			Rune uLCorner = border ? ULCorner : clearChar;
			Rune lLCorner = border ? LLCorner : clearChar;
			Rune lRCorner = border ? LRCorner : clearChar;

			// Outside top
			if (paddingTop > 1) {
				for (int r = region.Y; r < ftop; r++) {
					for (int c = region.X; c < region.X + region.Width; c++) {
						AddRuneAt (c, r, topChar);
					}
				}
			}

			// Outside top-left
			for (int c = region.X; c < fleft; c++) {
				AddRuneAt (c, ftop, leftChar);
			}

			// Frame top-left corner
			AddRuneAt (fleft, ftop, paddingTop >= 0 ? (paddingLeft >= 0 ? uLCorner : hLine) : leftChar);

			// Frame top
			for (int c = fleft + 1; c < fleft + 1 + fwidth; c++) {
				AddRuneAt (c, ftop, paddingTop > 0 ? hLine : topChar);
			}

			// Frame top-right corner
			if (fright > fleft) {
				AddRuneAt (fright, ftop, paddingTop >= 0 ? (paddingRight >= 0 ? uRCorner : hLine) : rightChar);
			}

			// Outside top-right corner
			for (int c = fright + 1; c < fright + paddingRight; c++) {
				AddRuneAt (c, ftop, rightChar);
			}

			// Left, Fill, Right
			if (fbottom > ftop) {
				for (int r = ftop + 1; r < fbottom; r++) {
					// Outside left
					for (int c = region.X; c < fleft; c++) {
						AddRuneAt (c, r, leftChar);
					}

					// Frame left
					AddRuneAt (fleft, r, paddingLeft > 0 ? vLine : leftChar);

					// Fill
					if (fill) {
						for (int x = fleft + 1; x < fright; x++) {
							AddRuneAt (x, r, clearChar);
						}
					}

					// Frame right
					if (fright > fleft) {
						var v = vLine;
						if ((Diagnostics & DiagnosticFlags.FrameRuler) == DiagnosticFlags.FrameRuler) {
							v = (char)(((int)'0') + ((r - ftop) % 10)); // vLine;
						}
						AddRuneAt (fright, r, paddingRight > 0 ? v : rightChar);
					}

					// Outside right
					for (int c = fright + 1; c < fright + paddingRight; c++) {
						AddRuneAt (c, r, rightChar);
					}
				}

				// Outside Bottom
				for (int c = region.X; c < region.X + region.Width; c++) {
					AddRuneAt (c, fbottom, leftChar);
				}

				// Frame bottom-left
				AddRuneAt (fleft, fbottom, paddingLeft > 0 ? lLCorner : leftChar);

				if (fright > fleft) {
					// Frame bottom
					for (int c = fleft + 1; c < fright; c++) {
						var h = hLine;
						if ((Diagnostics & DiagnosticFlags.FrameRuler) == DiagnosticFlags.FrameRuler) {
							h = (char)(((int)'0') + ((c - fleft) % 10)); // hLine;
						}
						AddRuneAt (c, fbottom, paddingBottom > 0 ? h : bottomChar);
					}

					// Frame bottom-right
					AddRuneAt (fright, fbottom, paddingRight > 0 ? (paddingBottom > 0 ? lRCorner : hLine) : rightChar);
				}

				// Outside right
				for (int c = fright + 1; c < fright + paddingRight; c++) {
					AddRuneAt (c, fbottom, rightChar);
				}
			}

			// Out bottom - ensure top is always drawn if we overlap
			if (paddingBottom > 0) {
				for (int r = fbottom + 1; r < fbottom + paddingBottom; r++) {
					for (int c = region.X; c < region.X + region.Width; c++) {
						AddRuneAt (c, r, bottomChar);
					}
				}
			}
		}

		/// <summary>
		/// Draws a frame on the specified region with the specified padding around the frame.
		/// </summary>
		/// <param name="region">Screen relative region where the frame will be drawn.</param>
		/// <param name="padding">Padding to add on the sides.</param>
		/// <param name="fill">If set to <c>true</c> it will clear the contents with the current color, otherwise the contents will be left untouched.</param>
		/// <remarks>This API has been superseded by <see cref="DrawWindowFrame(Rect, int, int, int, int, bool, bool)"/>.</remarks>
		/// <remarks>This API is equivalent to calling <c>DrawWindowFrame(Rect, p - 1, p - 1, p - 1, p - 1)</c>. In other words,
		/// A padding value of 0 means there is actually a one cell border.
		/// </remarks>
		public virtual void DrawFrame (Rect region, int padding, bool fill)
		{
			// DrawFrame assumes the border is always at least one row/col thick
			// DrawWindowFrame assumes a padding of 0 means NO padding and no frame
			DrawWindowFrame (new Rect (region.X, region.Y, region.Width, region.Height),
				padding + 1, padding + 1, padding + 1, padding + 1, border: false, fill: fill);
		}


		/// <summary>
		/// Suspend the application, typically needs to save the state, suspend the app and upon return, reset the console driver.
		/// </summary>
		public abstract void Suspend ();

		Rect clip;

		/// <summary>
		/// Controls the current clipping region that AddRune/AddStr is subject to.
		/// </summary>
		/// <value>The clip.</value>
		public Rect Clip {
			get => clip;
			set => this.clip = value;
		}

		/// <summary>
		/// Start of mouse moves.
		/// </summary>
		public abstract void StartReportingMouseMoves ();

		/// <summary>
		/// Stop reporting mouses moves.
		/// </summary>
		public abstract void StopReportingMouseMoves ();

		/// <summary>
		/// Disables the cooked event processing from the mouse driver.  At startup, it is assumed mouse events are cooked.
		/// </summary>
		public abstract void UncookMouse ();

		/// <summary>
		/// Enables the cooked event processing from the mouse driver
		/// </summary>
		public abstract void CookMouse ();

		/// <summary>
		/// Horizontal line character.
		/// </summary>
		public Rune HLine = '\u2500';

		/// <summary>
		/// Vertical line character.
		/// </summary>
		public Rune VLine = '\u2502';

		/// <summary>
		/// Stipple pattern
		/// </summary>
		public Rune Stipple = '\u2591';

		/// <summary>
		/// Diamond character
		/// </summary>
		public Rune Diamond = '\u25ca';

		/// <summary>
		/// Upper left corner
		/// </summary>
		public Rune ULCorner = '\u250C';

		/// <summary>
		/// Lower left corner
		/// </summary>
		public Rune LLCorner = '\u2514';

		/// <summary>
		/// Upper right corner
		/// </summary>
		public Rune URCorner = '\u2510';

		/// <summary>
		/// Lower right corner
		/// </summary>
		public Rune LRCorner = '\u2518';

		/// <summary>
		/// Left tee
		/// </summary>
		public Rune LeftTee = '\u251c';

		/// <summary>
		/// Right tee
		/// </summary>
		public Rune RightTee = '\u2524';

		/// <summary>
		/// Top tee
		/// </summary>
		public Rune TopTee = '\u252c';

		/// <summary>
		/// The bottom tee.
		/// </summary>
		public Rune BottomTee = '\u2534';

		/// <summary>
		/// Checkmark.
		/// </summary>
		public Rune Checked = '\u221a';

		/// <summary>
		/// Un-checked checkmark.
		/// </summary>
		public Rune UnChecked = '\u2574';

		/// <summary>
		/// Selected mark.
		/// </summary>
		public Rune Selected = '\u25cf';

		/// <summary>
		/// Un-selected selected mark.
		/// </summary>
		public Rune UnSelected = '\u25cc';

		/// <summary>
		/// Right Arrow.
		/// </summary>
		public Rune RightArrow = '\u25ba';

		/// <summary>
		/// Left Arrow.
		/// </summary>
		public Rune LeftArrow = '\u25c4';

		/// <summary>
		/// Down Arrow.
		/// </summary>
		public Rune DownArrow = '\u25bc';

		/// <summary>
		/// Up Arrow.
		/// </summary>
		public Rune UpArrow = '\u25b2';

		/// <summary>
		/// Left indicator for default action (e.g. for <see cref="Button"/>).
		/// </summary>
		public Rune LeftDefaultIndicator = '\u25e6';

		/// <summary>
		/// Right indicator for default action (e.g. for <see cref="Button"/>).
		/// </summary>
		public Rune RightDefaultIndicator = '\u25e6';

		/// <summary>
		/// Left frame/bracket (e.g. '[' for <see cref="Button"/>).
		/// </summary>
		public Rune LeftBracket = '[';

		/// <summary>
		/// Right frame/bracket (e.g. ']' for <see cref="Button"/>).
		/// </summary>
		public Rune RightBracket = ']';

		/// <summary>
		/// On Segment indicator for meter views (e.g. <see cref="ProgressBar"/>.
		/// </summary>
		public Rune OnMeterSegment = '\u258c';

		/// <summary>
		/// Off Segment indicator for meter views (e.g. <see cref="ProgressBar"/>.
		/// </summary>
		public Rune OffMeterSegement = ' ';

		/// <summary>
		/// Make the attribute for the foreground and background colors.
		/// </summary>
		/// <param name="fore">Foreground.</param>
		/// <param name="back">Background.</param>
		/// <param name="underline">Underline.</param>
		/// <returns></returns>
		public abstract Attribute MakeAttribute (Color fore, Color back, bool underline = false);

		/// <summary>
		/// Gets the current <see cref="Attribute"/>.
		/// </summary>
		/// <returns>The current attribute.</returns>
		public abstract Attribute GetAttribute ();
	}
}
