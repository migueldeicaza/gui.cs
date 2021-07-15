//
// Button.cs: Button control
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//

using System;
using NStack;

namespace Terminal.Gui {
	/// <summary>
	///   Button is a <see cref="View"/> that provides an item that invokes an <see cref="Action"/> when activated by the user.
	/// </summary>
	/// <remarks>
	/// <para>
	///   Provides a button showing text invokes an <see cref="Action"/> when clicked on with a mouse
	///   or when the user presses SPACE, ENTER, or hotkey. The hotkey is the first letter or digit following the first underscore ('_') 
	///   in the button text. 
	/// </para>
	/// <para>
	///   Use <see cref="View.HotKeySpecifier"/> to change the hotkey specifier from the default of ('_'). 
	/// </para>
	/// <para>
	///   If no hotkey specifier is found, the first uppercase letter encountered will be used as the hotkey.
	/// </para>
	/// <para>
	///   When the button is configured as the default (<see cref="IsDefault"/>) and the user presses
	///   the ENTER key, if no other <see cref="View"/> processes the <see cref="KeyEvent"/>, the <see cref="Button"/>'s
	///   <see cref="Action"/> will be invoked.
	/// </para>
	/// </remarks>
	public class Button : View {
		ustring text;
		bool is_default;

		/// <summary>
		///   Initializes a new instance of <see cref="Button"/> using <see cref="LayoutStyle.Computed"/> layout.
		/// </summary>
		/// <remarks>
		///   The width of the <see cref="Button"/> is computed based on the
		///   text length. The height will always be 1.
		/// </remarks>
		public Button () : this (text: string.Empty, is_default: false) { }

		/// <summary>
		///   Initializes a new instance of <see cref="Button"/> using <see cref="LayoutStyle.Computed"/> layout.
		/// </summary>
		/// <remarks>
		///   The width of the <see cref="Button"/> is computed based on the
		///   text length. The height will always be 1.
		/// </remarks>
		/// <param name="text">The button's text</param>
		/// <param name="is_default">
		///   If <c>true</c>, a special decoration is used, and the user pressing the enter key 
		///   in a <see cref="Dialog"/> will implicitly activate this button.
		/// </param>
		public Button (ustring text, bool is_default = false) : base (text)
		{
			Init (text, is_default);
		}

		/// <summary>
		///   Initializes a new instance of <see cref="Button"/> using <see cref="LayoutStyle.Absolute"/> layout, based on the given text
		/// </summary>
		/// <remarks>
		///   The width of the <see cref="Button"/> is computed based on the
		///   text length. The height will always be 1.
		/// </remarks>
		/// <param name="x">X position where the button will be shown.</param>
		/// <param name="y">Y position where the button will be shown.</param>
		/// <param name="text">The button's text</param>
		public Button (int x, int y, ustring text) : this (x, y, text, false) { }

		/// <summary>
		///   Initializes a new instance of <see cref="Button"/> using <see cref="LayoutStyle.Absolute"/> layout, based on the given text.
		/// </summary>
		/// <remarks>
		///   The width of the <see cref="Button"/> is computed based on the
		///   text length. The height will always be 1.
		/// </remarks>
		/// <param name="x">X position where the button will be shown.</param>
		/// <param name="y">Y position where the button will be shown.</param>
		/// <param name="text">The button's text</param>
		/// <param name="is_default">
		///   If <c>true</c>, a special decoration is used, and the user pressing the enter key 
		///   in a <see cref="Dialog"/> will implicitly activate this button.
		/// </param>
		public Button (int x, int y, ustring text, bool is_default)
		    : base (new Rect (x, y, text.RuneCount + 4 + (is_default ? 2 : 0), 1), text)
		{
			Init (text, is_default);
		}

		Rune _leftBracket;
		Rune _rightBracket;
		Rune _leftDefault;
		Rune _rightDefault;

		void Init (ustring text, bool is_default)
		{
			TextAlignment = TextAlignment.Centered;

			HotKeySpecifier = new Rune ('_');

			_leftBracket = new Rune (Driver != null ? Driver.LeftBracket : '[');
			_rightBracket = new Rune (Driver != null ? Driver.RightBracket : ']');
			_leftDefault = new Rune (Driver != null ? Driver.LeftDefaultIndicator : '<');
			_rightDefault = new Rune (Driver != null ? Driver.RightDefaultIndicator : '>');

			CanFocus = true;
			this.is_default = is_default;
			this.text = text ?? string.Empty;
			Update ();
		}

		/// <summary>
		///   The text displayed by this <see cref="Button"/>.
		/// </summary>
		public new ustring Text {
			get {
				return text;
			}

			set {
				text = value;
				Update ();
			}
		}

		/// <summary>
		/// Gets or sets whether the <see cref="Button"/> is the default action to activate in a dialog.
		/// </summary>
		/// <value><c>true</c> if is default; otherwise, <c>false</c>.</value>
		public bool IsDefault {
			get => is_default;
			set {
				is_default = value;
				Update ();
			}
		}

		internal void Update ()
		{
			if (IsDefault)
				base.Text = ustring.Make (_leftBracket) + ustring.Make (_leftDefault) + " " + text + " " + ustring.Make (_rightDefault) + ustring.Make (_rightBracket);
			else
				base.Text = ustring.Make (_leftBracket) + " " + text + " " + ustring.Make (_rightBracket);

			int w = base.Text.RuneCount - (base.Text.Contains (HotKeySpecifier) ? 1 : 0);
			GetCurrentWidth (out int cWidth);
			var canSetWidth = SetWidth (w, out int rWidth);
			if (canSetWidth && (cWidth < rWidth || AutoSize)) {
				Width = rWidth;
				w = rWidth;
			} else if (!canSetWidth || !AutoSize) {
				w = cWidth;
			}
			var layout = LayoutStyle;
			bool layoutChanged = false;
			if (!(Height is Dim.DimAbsolute)) {
				// The height is always equal to 1 and must be Dim.DimAbsolute.
				layoutChanged = true;
				LayoutStyle = LayoutStyle.Absolute;
			}
			Height = 1;
			if (layoutChanged) {
				LayoutStyle = layout;
			}
			Frame = new Rect (Frame.Location, new Size (w, 1));
			SetNeedsDisplay ();
		}

		bool CheckKey (KeyEvent key)
		{
			if (key.Key == (Key.AltMask | HotKey)) {
				SetFocus ();
				Clicked?.Invoke ();
				return true;
			}
			return false;
		}

		///<inheritdoc/>
		public override bool ProcessHotKey (KeyEvent kb)
		{
			if (kb.IsAlt)
				return CheckKey (kb);

			return false;
		}

		///<inheritdoc/>
		public override bool ProcessColdKey (KeyEvent kb)
		{
			if (IsDefault && kb.KeyValue == '\n') {
				Clicked?.Invoke ();
				return true;
			}
			return CheckKey (kb);
		}

		///<inheritdoc/>
		public override bool ProcessKey (KeyEvent kb)
		{
			var c = kb.KeyValue;
			if (c == '\n' || c == ' ' || kb.Key == HotKey) {
				Clicked?.Invoke ();
				return true;
			}
			return base.ProcessKey (kb);
		}


		/// <summary>
		///   Clicked <see cref="Action"/>, raised when the user clicks the primary mouse button within the Bounds of this <see cref="View"/>
		///   or if the user presses the action key while this view is focused. (TODO: IsDefault)
		/// </summary>
		/// <remarks>
		///   Client code can hook up to this event, it is
		///   raised when the button is activated either with
		///   the mouse or the keyboard.
		/// </remarks>
		public event Action Clicked;

		///<inheritdoc/>
		public override bool MouseEvent (MouseEvent me)
		{
			if (me.Flags == MouseFlags.Button1Clicked || me.Flags == MouseFlags.Button1DoubleClicked ||
				me.Flags == MouseFlags.Button1TripleClicked) {
				if (CanFocus) {
					if (!HasFocus) {
						SetFocus ();
						SetNeedsDisplay ();
					}
					Clicked?.Invoke ();
				}

				return true;
			}
			return false;
		}

		///<inheritdoc/>
		public override void PositionCursor ()
		{
			if (HotKey == Key.Unknown) {
				for (int i = 0; i < base.Text.RuneCount; i++) {
					if (base.Text [i] == text [0]) {
						Move (i, 0);
						return;
					}
				}
			}
			base.PositionCursor ();
		}

		///<inheritdoc/>
		public override bool OnEnter (View view)
		{
			Application.Driver.SetCursorVisibility (CursorVisibility.Invisible);

			return base.OnEnter (view);
		}
	}
}
