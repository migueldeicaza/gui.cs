//
// Slider.cs: Slider control
//
// Authors:
//   José Miguel Perricone (jmperricone@hotmail.com)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NStack;

namespace Terminal.Gui {

	/// <summary>
	/// Slider Style and Config.
	/// </summary>
	public class SliderStyle {

		/// <summary>
		/// Allow range start and end be in the same option, as a single option.
		/// </summary>
		public bool RangeAllowSingle { get; set; }

		/// <summary>
		/// Show <see cref="LeftBorder"/> character and <see cref="RightBorder"/> character at the beginning and at the end of the slider respectively.
		/// </summary>
		public bool ShowBorders { get; set; }

		/// <summary>
		/// Show/Hide the slider Header.
		/// </summary>
		public bool ShowHeader { get; set; }

		/// <summary>
		/// Show/Hide the options legends.
		/// </summary>
		public bool ShowLegends { get; set; }

		/// <summary>
		/// Left margin.
		/// </summary>
		public int LeftMargin { get; set; }
		/// <summary>
		/// Right margin.
		/// </summary>
		public int RightMargin { get; set; }

		/// <summary>
		/// Left space before the first option.
		/// </summary>
		public int LeftSpacing { get; set; }
		/// <summary>
		/// Right space after the last option.
		/// </summary>
		public int RightSpacing { get; set; }
		/// <summary>
		/// Space between options.
		/// </summary>
		public int InnerSpacing { get; set; }

		/// <summary>
		/// Left Border Character
		/// </summary>
		public Rune LeftBorder { get; set; }
		/// <summary>
		/// Right Border Character
		/// </summary>
		public Rune RightBorder { get; set; }
		/// <summary>
		/// First Option Character when <see cref="ShowBorders"/> == false
		/// </summary>
		public Rune First { get; set; }
		/// <summary>
		/// Last Option Character when <see cref="ShowBorders"/> == false
		/// </summary>
		public Rune Last { get; set; }
		/// <summary>
		/// Character that is not an option.<br/>
		/// E.g: ──●────●────●── The line character.
		/// </summary>
		public Rune Space { get; set; }
		/// <summary>
		/// Character of each option.
		/// </summary>
		public Rune Option { get; set; }
		/// <summary>
		/// Character when the option is set.
		/// </summary>
		public Rune Set { get; set; }
		/// <summary>
		/// Character to fill a range.
		/// </summary>
		public Rune Range { get; set; }
		/// <summary>
		/// Start Character of the range.
		/// </summary>
		public Rune StartRange { get; set; }
		/// <summary>
		/// End Character of the range.
		/// </summary>
		public Rune EndRange { get; set; }

		/// <summary>
		/// Set the attribute for this character.
		/// </summary>
		public Attribute LeftBorderAttr { get; set; }
		/// <summary>
		/// Set the attribute for this character.
		/// </summary>
		public Attribute RightBorderAttr { get; set; }
		/// <summary>
		/// Set the attribute for this character.
		/// </summary>
		public Attribute FirstAttr { get; set; }
		/// <summary>
		/// Set the attribute for this character.
		/// </summary>
		public Attribute LastAttr { get; set; }
		/// <summary>
		/// Set the attribute for this character.
		/// </summary>
		public Attribute SpaceAttr { get; set; }
		/// <summary>
		/// Set the attribute for this character.
		/// </summary>
		public Attribute OptionAttr { get; set; }
		/// <summary>
		/// Set the attribute for this character.
		/// </summary>
		public Attribute SetAttr { get; set; }
		/// <summary>
		/// Set the attribute for this character.
		/// </summary>
		public Attribute RangeAttr { get; set; }
		/// <summary>
		/// Set the attribute for this character.
		/// </summary>
		public Attribute StartRangeAttr { get; set; }
		/// <summary>
		/// Set the attribute for this character.
		/// </summary>
		public Attribute EndRangeAttr { get; set; }
		/// <summary>
		/// Set the attribute for this character.
		/// </summary>
		public Attribute LegendAttr { get; set; }
		/// <summary>
		/// Set the attribute for this character.
		/// </summary>
		public Attribute LegendSetAttr { get; set; }

		/// <summary>
		/// Set all characters attributes.
		/// </summary>
		/// <param name="attribute"></param>
		public void SetAllAttributes (Attribute attribute)
		{
			LeftBorderAttr = attribute;
			RightBorderAttr = attribute;
			FirstAttr = attribute;
			LastAttr = attribute;
			SpaceAttr = attribute;
			OptionAttr = attribute;
			SetAttr = attribute;
			RangeAttr = attribute;
			StartRangeAttr = attribute;
			EndRangeAttr = attribute;
			LegendAttr = attribute;
			LegendSetAttr = attribute;
		}
	}

	/// <summary>
	/// Slider Types
	/// </summary>
	public enum SliderType {
		/// <summary>
		/// ├─┼─┼─┼─┼─█─┼─┼─┼─┼─┼─┼─┤
		/// </summary>
		Single,
		/// <summary>
		/// ├─┼─█─┼─┼─█─┼─┼─┼─┼─█─┼─┤
		/// </summary>
		Multiple,
		/// <summary>
		/// ├▒▒▒▒▒▒▒▒▒█─┼─┼─┼─┼─┼─┼─┤
		/// </summary>
		LeftRange,
		/// <summary>
		/// ├─┼─┼─┼─┼─█▒▒▒▒▒▒▒▒▒▒▒▒▒┤
		/// </summary>
		RightRange,
		/// <summary>
		/// ├─┼─┼─┼─┼─█▒▒▒▒▒▒▒█─┼─┼─┤
		/// </summary>
		Range
	}

	/// <summary>
	/// Represents an option in the slider.
	/// </summary>
	/// <typeparam name="T">Data of the option.</typeparam>
	public class SliderOption<T> {
		/// <summary>
		/// Display legend of the option.
		/// </summary>
		public string Legend { get; set; }

		/// <summary>
		/// Custom data of the option.
		/// </summary>
		public T Data { get; set; }

		/// <summary>
		/// To Raise the <see cref="Set"/> event from the Slider.
		/// </summary>
		internal void OnSet ()
		{
			Set?.Invoke (this);
		}

		/// <summary>
		/// To Raise the <see cref="UnSet"/> event from the Slider.
		/// </summary>
		internal void OnUnSet ()
		{
			UnSet?.Invoke (this);
		}

		/// <summary>
		/// To Raise the <see cref="Changed"/> event from the Slider.
		/// </summary>
		internal void OnChanged (bool isSet)
		{
			Changed?.Invoke (this, isSet);
		}

		/// <summary>
		/// Event Raised when this option is set.
		/// </summary>
		public event Action<SliderOption<T>> Set;
		/// <summary>
		/// Event Raised when this option is unset.
		/// </summary>
		public event Action<SliderOption<T>> UnSet;
		/// <summary>
		/// Event Raised when this option change with the current state.
		/// </summary>
		public event Action<SliderOption<T>, bool> Changed;
	}

	/// <summary>
	/// Slider control.
	/// </summary>
	public class Slider : Slider<object> {

		/// <summary>
		/// Initializes a new instance of the <see cref="Slider"/> class.
		/// </summary>
		public Slider ()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Slider"/> class.
		/// </summary>
		/// <param name="options">Initial slider options.</param>
		public Slider (List<object> options) : base (options)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Slider"/> class.
		/// </summary>
		/// <param name="header">Header text of the slider.</param>
		/// <param name="options">Initial slider options.</param>
		public Slider (ustring header, List<object> options) : base (header, options)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Slider"/> class.
		/// </summary>
		/// <param name="options">Initial slider options.</param>
		public Slider (List<SliderOption<object>> options) : base (options)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Slider"/> class.
		/// </summary>
		/// <param name="header">Header text of the slider.</param>
		/// <param name="options">Initial slider options.</param>
		public Slider (ustring header, List<SliderOption<object>> options) : base (header, options)
		{
		}
	}

	/// <summary>
	/// 
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class Slider<T> : View {

		SliderStyle style;
		SliderType type = SliderType.Single;
		List<int> currentOptions = new List<int> ();
		List<SliderOption<T>> options;
		int currentOption = 0;

		ustring header;

		#region Events

		/// <summary>
		/// Event raised when the slider option/s changed.
		/// The dictionary contains: key = option index, value = T
		/// </summary>
		public event Action<Dictionary<int, SliderOption<T>>> OptionsChanged;

		/// <summary>
		/// Event raised When the option is hovered with the keys or the mouse.
		/// </summary>
		public event Action<int, SliderOption<T>> OptionFocused;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="Slider"/> class.
		/// </summary>
		public Slider () : this (ustring.Empty, new List<T> ())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Slider"/> class.
		/// </summary>
		/// <param name="options">Initial slider options.</param>
		public Slider (List<T> options) : this (ustring.Empty, options.Select (e => new SliderOption<T> { Data = e, Legend = e.ToString () }).ToList ())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Slider"/> class.
		/// </summary>
		/// <param name="header">Header text of the slider.</param>
		/// <param name="options">Initial slider options.</param>
		public Slider (ustring header, List<T> options) : this (header, options.Select (e => new SliderOption<T> { Data = e, Legend = e.ToString () }).ToList ())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Slider"/> class.
		/// </summary>
		/// <param name="options">Initial slider options.</param>
		public Slider (List<SliderOption<T>> options) : this (ustring.Empty, options)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Slider"/> class.
		/// </summary>
		/// <param name="header">Header text of the slider.</param>
		/// <param name="options">Initial slider options.</param>
		public Slider (ustring header, List<SliderOption<T>> options)
		{
			SetDefaultHorizontalStyle ();

			CanFocus = true;

			if (header != ustring.Empty) {
				style.ShowHeader = true;
				this.header = header;
			}

			this.Options = options;

			this.Height = CalcHeight ();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Slider type. <see cref="SliderType"></see>
		/// </summary>
		public SliderType Type {
			get { return type; }
			set {
				type = value;
				SetNeedsDisplay ();
			}
		}

		/// <summary>
		/// Header Text Property.
		/// To show or hide the header use <see cref="SliderStyle.ShowHeader"/>.
		/// </summary>
		public ustring Header {
			get { return header; }
			set {
				header = value;
				Height = CalcHeight ();
				SetNeedsDisplay ();
			}
		}

		/// <summary>
		/// Slider type. <see cref="SliderType"></see>
		/// </summary>
		public SliderStyle Style {
			get { return style; }
			set {
				style = Style;
				Width = CalcWidth ();
			}
		}

		/// <summary>
		/// Set the slider options.
		/// </summary>
		public List<SliderOption<T>> Options {
			get { return options; }
			set {
				options = value;

				if (options == null || options.Count == 0)
					return;

				// Note(jmperricone): We need to set/find the LeftSpacing, InnerSpacing and RightSpacing.

				// Layout cases:
				// 1) Values set by the user.
				// 2) Find "Best" values.
				// 3) Using Computed Layout.

				// Case 1
				// TODO:

				// Case 2
				var max = options.Max (e => e.Legend.ToString ().Length);
				style.InnerSpacing = max + (max % 2 == 0 ? 1 : 0);
				style.LeftSpacing = max / 2;
				style.RightSpacing = max / 2;

				// Case 3
				// TODO:

				Width = CalcWidth ();
			}
		}

		#endregion

		#region Helpers

		void SetDefaultHorizontalStyle ()
		{
			style = new SliderStyle {
				ShowBorders = false,
				ShowLegends = true,

				LeftSpacing = 0,
				InnerSpacing = 3,
				RightSpacing = 0,

				LeftBorder = '▕',
				RightBorder = '▏',
				First = '├',
				Last = '┤',
				Space = '─',
				Option = '●', // '┼'
				Set = ' ',//'█',
				Range = '▒', // '▒',
				StartRange = '█',
				EndRange = '█'
			};

			style.SetAllAttributes (new Attribute (Color.White, Color.Black));
			style.LegendSetAttr = new Attribute (Color.BrightRed, Color.Black);

			style.SetAttr = new Attribute (Color.Red, Color.Red);
			style.RangeAttr = new Attribute (Color.BrightRed, Color.BrightRed);
		}

		void SetDefaultVerticalsStyle ()
		{
			// TODO(jmperricone): Implement Vertical Slider and styling.
		}

		int CalcWidth ()
		{
			var width = 0;
			width += style.ShowBorders ? 2 : 0;
			width += style.LeftSpacing + style.RightSpacing;
			width += options.Count;
			width += (options.Count - 1) * style.InnerSpacing;

			// If header is bigger than the slider, add margin to the slider and return header's width.
			if (style.ShowHeader && header != ustring.Empty && header.Length > width) {
				var diff = header.Length - width;
				style.LeftMargin = diff / 2;
				style.RightMargin = diff / 2 + diff % 2;
				return header.Length;
			}
			return width;
		}

		int CalcHeight ()
		{
			var height = 1; // slider
			height += style.ShowHeader ? 1 : 0;
			height += style.ShowLegends ? 1 : 0;
			return height;
		}

		(int x, int y) GetPositionByOption (int option)
		{
			var x = style.ShowBorders ? 1 : 0;
			x += style.LeftMargin;
			x += style.LeftSpacing;
			x += option * (style.InnerSpacing + 1);

			return (x, style.ShowHeader ? 1 : 0);
		}

		int GetOptionByPosition (int x, int y)
		{
			if (y != (style.ShowHeader ? 1 : 0))
				return -1;

			x -= style.ShowBorders ? 1 : 0;
			x -= style.LeftMargin;
			x -= style.LeftSpacing;

			var option = x / (style.InnerSpacing + 1);
			var valid = x % (style.InnerSpacing + 1) == 0;

			if (!valid || option < 0 || option > options.Count - 1) {
				return -1;
			}

			return option;
		}

		void SetOption ()
		{
			switch (type) {
			case SliderType.Single:
			case SliderType.LeftRange:
			case SliderType.RightRange:
				// Raise unset event.
				if (currentOptions.Count == 1) {
					options [currentOptions [0]].OnUnSet ();
				}

				// Set the option.
				currentOptions.Clear ();
				currentOptions.Add (currentOption);

				// Raise set event.
				options [currentOption].OnSet ();
				// Raise changed event.
				OptionsChanged?.Invoke (currentOptions.ToDictionary (e => e, e => options [e]));

				break;
			case SliderType.Multiple:
				if (currentOptions.Contains (currentOption)) {
					currentOptions.Remove (currentOption);
					options [currentOption].OnUnSet ();
				} else {
					currentOptions.Add (currentOption);
					options [currentOption].OnSet ();
				}
				OptionsChanged?.Invoke (currentOptions.ToDictionary (e => e, e => options [e]));
				break;
			case SliderType.Range:
				if (currentOptions.Count == 0) {
					// Always 2 options sets
					if (style.RangeAllowSingle) {
						currentOptions.Add (currentOption);
						currentOptions.Add (currentOption);
					} else {
						currentOptions.Add (currentOption);
						if (currentOption == options.Count - 1) {
							currentOptions.Add (currentOption - 1);
						} else {
							currentOptions.Add (currentOption + 1);
						}
					}

					// Raise per Option Set event.
					options [currentOptions [0]].OnSet ();
					options [currentOptions [1]].OnSet ();

					OptionsChanged?.Invoke (currentOptions.ToDictionary (e => e, e => options [e]));
				} else if (currentOptions.Count == 2) {

				}
				break;
			default:
				throw new ArgumentOutOfRangeException (type.ToString ());
			}
		}

		#endregion

		#region Cursor and Drawing

		/// <inheritdoc/>
		public override void PositionCursor ()
		{
			(int x, int y) = GetPositionByOption (currentOption);
			Move (x, y);
		}

		/// <inheritdoc/>
		public override void Redraw (Rect bounds)
		{
			Driver.SetCursorVisibility (CursorVisibility.Box);
			Driver.SetAttribute (style.RightBorderAttr);

			if (this.options == null && this.options.Count > 0) {
				return;
			}

			// Testing 
			//for (int yy = 0; yy < bounds.Height; yy++) {
			//	for (int xx = 0; xx < bounds.Width; xx++) {
			//		Move (xx, yy);
			//		Driver.AddRune ('X');
			//	}
			//}

			// if autosize best

			/*
			aaaaa aaaaa aaaaa aaaaa aaaaa aaaaa
			  *     *     *
			*/
			var y = 0;

			// Draw Header
			if (style.ShowHeader) {
				Move (0, y++);
				DrawHeader ();
			}

			// Draw Slider
			Move (style.LeftMargin, y++);
			DrawSlider ();

			// Draw Legends.
			if (style.ShowLegends) {
				Move (style.LeftMargin + (style.ShowBorders ? 1 : 0), y);
				DrawLegends ();
			}
		}

		void DrawHeader ()
		{
			Driver.AddStr (HAlignText (header, Bounds.Width, TextAlignment.Centered));
		}

		void DrawSlider ()
		{
			var isSet = currentOptions.Count > 0;

			// Left Border
			if (style.ShowBorders) {
				Driver.SetAttribute (style.LeftBorderAttr);
				Driver.AddRune (style.LeftBorder);
			}

			// Left Spacing
			if (style.LeftSpacing > 0) {
				Driver.SetAttribute (isSet && type == SliderType.LeftRange ? style.RangeAttr : style.SpaceAttr);
				var rune = isSet && type == SliderType.LeftRange ? style.Range : style.Space;
				for (int i = 0; i < this.style.LeftSpacing; i++) {
					Driver.AddRune (rune);
				}
			}

			// Slider
			if (options.Count > 0) {
				for (int i = 0; i < options.Count; i++) {

					var isRange = false;
					var drawRange = false;

					if (isSet && type == SliderType.LeftRange && i <= currentOptions [0]) {
						isRange = true;
						drawRange = i < currentOptions [0];
					} else if (isSet && type == SliderType.RightRange && i >= currentOptions [0]) {
						isRange = true;
						drawRange = i >= currentOptions [0];
					} else if (isSet && type == SliderType.Range && i >= currentOptions [0] && i <= currentOptions [1]) {
						isRange = true;
						drawRange = i >= currentOptions [0] && i < currentOptions [1];
					} else {
						// Is Not a Range.
					}

					// Draw Option
					Driver.SetAttribute (isSet && currentOptions.Contains (i) ? style.SetAttr : isRange ? style.RangeAttr : style.OptionAttr);
					Driver.AddRune (isSet && currentOptions.Contains (i) ? style.Set : isRange ? style.Range : style.Option);

					// Draw Spacing
					if (i < options.Count - 1) { // Skip if is the Last Spacing.
						Driver.SetAttribute (isRange && isSet && drawRange ? style.RangeAttr : style.SpaceAttr);
						for (int s = 0; s < style.InnerSpacing; s++) {
							Driver.AddRune (isRange && isSet && drawRange ? style.Range : style.Space);
						}
					}
				}
			}

			// Right Spacing
			if (style.RightSpacing > 0) {
				Driver.SetAttribute (isSet && type == SliderType.RightRange ? style.RangeAttr : style.SpaceAttr);
				var rune = isSet && type == SliderType.RightRange ? style.Range : style.Space;
				for (int i = 0; i < this.style.RightSpacing; i++) {
					Driver.AddRune (rune);
				}
			}

			// Right Border
			if (style.ShowBorders) {
				Driver.SetAttribute (style.RightBorderAttr);
				Driver.AddRune (style.RightBorder);
			}
		}

		ustring HAlignText (ustring text, int width, TextAlignment textAlignment)
		{
			var w = width - text.Length;
			var s1 = new string (' ', w / 2);
			var s2 = new string (' ', w % 2);

			switch (textAlignment) {
			case TextAlignment.Justified:
			case TextAlignment.Left:
				return text + s1 + s1 + s2;
			case TextAlignment.Centered:
				if (text.Length % 2 != 0) {
					//return s1 + text + s1;
					return s1 + s2 + text + s1;
				} else {
					return s1 + s2 + text + s1;
				}
			case TextAlignment.Right:
				return s1 + s1 + s2 + text;
			default:
				return text;
			}
		}

		void DrawLegends ()
		{
			var isSet = currentOptions.Count > 0;

			for (int i = 0; i < options.Count; i++) {

				bool isOptionSet = false;

				switch (type) {
				case SliderType.Single:
				case SliderType.Multiple:
					if (isSet && currentOptions.Contains (i))
						isOptionSet = true;
					break;
				case SliderType.LeftRange:
					if (isSet && i <= currentOptions [0])
						isOptionSet = true;
					break;
				case SliderType.RightRange:
					if (isSet && i >= currentOptions [0])
						isOptionSet = true;
					break;
				case SliderType.Range:
					if (isSet && i >= currentOptions [0] && i <= currentOptions [1])
						isOptionSet = true;
					break;
				}

				if (isOptionSet) {
					Driver.SetAttribute (style.LegendSetAttr);
				} else {
					Driver.SetAttribute (style.LegendAttr);
				}

				if (i == 0) {
					Driver.AddStr (HAlignText (options [i].Legend.ToString (), style.InnerSpacing, TextAlignment.Centered));
				} else if (i == options.Count - 1) {
					Driver.AddStr (HAlignText (options [i].Legend.ToString (), style.InnerSpacing, TextAlignment.Centered));
				} else {
					Driver.AddStr (HAlignText (options [i].Legend.ToString (), style.InnerSpacing, TextAlignment.Centered));
				}
				if (i != options.Count - 1)
					Driver.AddRune (' ');
			}
		}

		#endregion

		#region Key and Mouse

		/// <inheritdoc/>
		public override bool MouseEvent (MouseEvent mouseEvent)
		{
			// Note(jmperricone): Maybe we click to focus the cursor, and on next click we set the option.
			//                    That will makes OptionFocused Event more relevant.
			// TODO(jmperricone): Make Range type work with mouse.

			if (mouseEvent.Flags.HasFlag (MouseFlags.Button1Clicked)) {
				var option = GetOptionByPosition (mouseEvent.X, mouseEvent.Y);
				if (option != -1) {
					currentOption = option;
					OptionFocused?.Invoke (currentOption, options [currentOption]);
					SetOption ();
					SetNeedsDisplay ();
					return true;
				}
			}
			return false;
		}

		/// <inheritdoc/>
		public override bool ProcessKey (KeyEvent keyEvent)
		{
			switch (keyEvent.Key) {
			case Key.CursorLeft | Key.CtrlMask:
				if (currentOptions.Contains (currentOption)) {
					var prev = currentOption > 0 ? currentOption - 1 : currentOption;
					if (!currentOptions.Contains (prev) || (type == SliderType.Range && style.RangeAllowSingle)) {
						currentOptions.Remove (currentOption);
						currentOption = prev;
						// Note(jmperricone): We are setting the option here, do we send the OptionFocused Event too ?
						OptionFocused?.Invoke (currentOption, options [currentOption]);
						currentOptions.Add (currentOption);
						currentOptions.Sort (); // Range Type
						OptionsChanged?.Invoke (currentOptions.ToDictionary (e => e, e => options [e]));
					}
				}
				break;
			case Key.CursorRight | Key.CtrlMask:
				if (currentOptions.Contains (currentOption)) {
					var next = currentOption < this.options.Count - 1 ? currentOption + 1 : currentOption;
					if (!currentOptions.Contains (next) || (type == SliderType.Range && style.RangeAllowSingle)) {
						currentOptions.Remove (currentOption);
						currentOption = next;
						// Note(jmperricone): We are setting the option here, do we send the OptionFocused Event too ?
						OptionFocused?.Invoke (currentOption, options [currentOption]);
						currentOptions.Add (currentOption);
						currentOptions.Sort (); // Range Type
						OptionsChanged?.Invoke (currentOptions.ToDictionary (e => e, e => options [e]));
					}
				}
				break;
			case Key.CursorLeft:
				currentOption = currentOption > 0 ? currentOption - 1 : currentOption;
				OptionFocused?.Invoke (currentOption, options [currentOption]);
				break;
			case Key.CursorRight:
				currentOption = currentOption < this.options.Count - 1 ? currentOption + 1 : currentOption;
				OptionFocused?.Invoke (currentOption, options [currentOption]);
				break;
			case Key.Enter:
				SetOption ();
				break;
			default:
				return false;
			}

			SetNeedsDisplay ();
			return true;
		}

		#endregion
	}
}
