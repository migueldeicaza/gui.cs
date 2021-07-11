//
// DateField.cs: text entry for date
//
// Author: Barry Nolte
//
// Licensed under the MIT license
//
using NStack;
using System;
using System.Globalization;
using System.Linq;

namespace Terminal.Gui {
	/// <summary>
	///   Simple Date editing <see cref="View"/>
	/// </summary>
	/// <remarks>
	///   The <see cref="DateField"/> <see cref="View"/> provides date editing functionality with mouse support.
	/// </remarks>
	public class DateField : TextField {
		DateTime date;
		bool isShort;
		int longFieldLen = 10;
		int shortFieldLen = 8;
		string sepChar;
		string longFormat;
		string shortFormat;
		bool isJalaali;

		int FieldLen { get { return isShort ? shortFieldLen : longFieldLen; } }
		string Format { get { return isShort ? shortFormat : longFormat; } }

		/// <summary>
		///   DateChanged event, raised when the <see cref="Date"/> property has changed.
		/// </summary>
		/// <remarks>
		///   This event is raised when the <see cref="Date"/> property changes.
		/// </remarks>
		/// <remarks>
		///   The passed event arguments containing the old value, new value, and format string.
		/// </remarks>
		public event Action<DateTimeEventArgs<DateTime>> DateChanged;

		/// <summary>
		///    Initializes a new instance of <see cref="DateField"/> using <see cref="LayoutStyle.Absolute"/> layout.
		/// </summary>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		/// <param name="date">Initial date contents.</param>
		/// <param name="isShort">If true, shows only two digits for the year.</param>
		/// <param name="isJalaali">If true, parse will convert jalaali input fo georgian date</param>
		public DateField (int x, int y, DateTime date, bool isShort = false, bool isJalaali = false) : base (x, y, isShort ? 10 : 12, "")
		{
			this.isShort = isShort;
			this.isJalaali = isJalaali;
			Initialize (date);
		}

		/// <summary>
		///  Initializes a new instance of <see cref="DateField"/> using <see cref="LayoutStyle.Computed"/> layout.
		/// </summary>
		public DateField () : this (DateTime.MinValue) { }

		/// <summary>
		///  Initializes a new instance of <see cref="DateField"/> using <see cref="LayoutStyle.Computed"/> layout.
		/// </summary>
		/// <param name="date"></param>
		/// <param name="isJalaali"></param>
		public DateField (DateTime date, bool isJalaali = false) : base ("")
		{

			this.isJalaali = isJalaali;
			if (!isJalaali)
				this.isShort = true;
			Width = FieldLen + 2;
			Initialize (date);
		}

		void Initialize (DateTime date)
		{
			CultureInfo cultureInfo = CultureInfo.CurrentCulture;
			sepChar = cultureInfo.DateTimeFormat.DateSeparator;
			longFormat = isJalaali ? $" yyyy{sepChar}MM{sepChar}dd" : GetLongFormat (cultureInfo.DateTimeFormat.ShortDatePattern);
			shortFormat = isJalaali ? $" yy{sepChar}MM{sepChar}dd" : GetShortFormat (longFormat);
			CursorPosition = 1;
			Date = date;
			TextChanged += DateField_Changed;
		}

		void DateField_Changed (ustring e)
		{
			try {

				if (!DateTime.TryParseExact (GetDate (Text).ToString (), GetInvarianteFormat (), CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime result))
					Text = e;

			} catch (Exception) {
				Text = e;
			}
		}

		string GetInvarianteFormat ()
		{
			return $"MM{sepChar}dd{sepChar}yyyy";
		}

		string GetLongFormat (string lf)
		{
			ustring [] frm = ustring.Make (lf).Split (ustring.Make (sepChar));
			for (int i = 0; i < frm.Length; i++) {
				if (frm [i].Contains ("M") && frm [i].RuneCount < 2)
					lf = lf.Replace ("M", "MM");
				if (frm [i].Contains ("d") && frm [i].RuneCount < 2)
					lf = lf.Replace ("d", "dd");
				if (frm [i].Contains ("y") && frm [i].RuneCount < 4)
					lf = lf.Replace ("yy", "yyyy");
			}
			return $" {lf}";
		}

		string GetShortFormat (string lf)
		{
			return lf.Replace ("yyyy", "yy");
		}

		/// <summary>
		///   Gets or sets the date of the <see cref="DateField"/>.
		/// </summary>
		/// <remarks>
		/// </remarks>
		public DateTime Date {
			get {
				return date;
			}
			set {
				if (ReadOnly)
					return;

				var oldData = date;
				date = value;
				if (isJalaali) {

					this.Text = ToJalaaliString (Format, date);
				} else {
					this.Text = value.ToString (Format);
				}
				var args = new DateTimeEventArgs<DateTime> (oldData, value, Format);
				if (oldData != value) {
					OnDateChanged (args);
				}
			}
		}



		/// <summary>
		/// Get or set the date format for the widget.
		/// </summary>
		public bool IsShortFormat {
			get => isShort;
			set {
				isShort = value;
				if (isShort)
					Width = 10;
				else
					Width = 12;
				var ro = ReadOnly;
				if (ro)
					ReadOnly = false;
				SetText (Text);
				ReadOnly = ro;
				SetNeedsDisplay ();
			}
		}

		bool SetText (Rune key)
		{
			var text = TextModel.ToRunes (Text);
			var newText = text.GetRange (0, CursorPosition);
			newText.Add (key);
			if (CursorPosition < FieldLen)
				newText = newText.Concat (text.GetRange (CursorPosition + 1, text.Count - (CursorPosition + 1))).ToList ();
			return SetText (ustring.Make (newText));
		}

		bool SetText (ustring text)
		{
			if (text.IsEmpty) {
				return false;
			}

			ustring [] vals = text.Split (ustring.Make (sepChar));
			ustring [] frm = ustring.Make (Format).Split (ustring.Make (sepChar));
			bool isValidDate = true;
			int idx = GetFormatIndex (frm, "y");
			int year = Int32.Parse (vals [idx].ToString ());
			int month;
			int day;
			idx = GetFormatIndex (frm, "M");
			if (Int32.Parse (vals [idx].ToString ()) < 1) {
				isValidDate = false;
				month = 1;
				vals [idx] = "1";
			} else if (Int32.Parse (vals [idx].ToString ()) > 12) {
				isValidDate = false;
				month = 12;
				vals [idx] = "12";
			} else
				month = Int32.Parse (vals [idx].ToString ());
			idx = GetFormatIndex (frm, "d");
			if (Int32.Parse (vals [idx].ToString ()) < 1) {
				isValidDate = false;
				day = 1;
				vals [idx] = "1";
			} else if (Int32.Parse (vals [idx].ToString ()) > 31) {
				isValidDate = false;
				day = DateTime.DaysInMonth (year, month);
				vals [idx] = day.ToString ();
			} else
				day = Int32.Parse (vals [idx].ToString ());
			DateTime result;
			if (isJalaali) {
				try {
					PersianCalendar pc = new PersianCalendar ();
					if (year < 10) {
						year += 1400;
					}
					result = pc.ToDateTime (year, month, day, 12, 0, 0, 0);
					year = result.Year;
					month = result.Month;
					day = result.Day;
				} catch {
					return false;
				}
			} else {
				string d = GetDate (month, day, year, frm);

				if (!DateTime.TryParseExact (d, Format, CultureInfo.CurrentCulture, DateTimeStyles.None, out result) ||
					!isValidDate)
					return false;
			}

			Date = result;
			return true;
		}

		string GetDate (int month, int day, int year, ustring [] fm)
		{
			string date = " ";
			for (int i = 0; i < fm.Length; i++) {
				if (fm [i].Contains ("M")) {
					date += $"{month,2:00}";
				} else if (fm [i].Contains ("d")) {
					date += $"{day,2:00}";
				} else {
					if (!isShort && year.ToString ().Length == 2) {
						var y = DateTime.Now.Year.ToString ();
						date += y.Substring (0, 2) + year.ToString ();
					} else if (isShort && year.ToString ().Length == 4) {
						date += $"{year.ToString ().Substring (2, 2)}";
					} else {
						date += $"{year,2:00}";
					}
				}
				if (i < 2)
					date += $"{sepChar}";
			}
			return date;
		}

		ustring GetDate (ustring text)
		{
			ustring [] vals = text.Split (ustring.Make (sepChar));
			ustring [] frm = ustring.Make (Format).Split (ustring.Make (sepChar));
			ustring [] date = { null, null, null };

			for (int i = 0; i < frm.Length; i++) {
				if (frm [i].Contains ("M")) {
					date [0] = vals [i].TrimSpace ();
				} else if (frm [i].Contains ("d")) {
					date [1] = vals [i].TrimSpace ();
				} else {
					var year = vals [i].TrimSpace ();
					if (year.RuneCount == 2) {
						var y = DateTime.Now.Year.ToString ();
						date [2] = y.Substring (0, 2) + year.ToString ();
					} else {
						date [2] = vals [i].TrimSpace ();
					}
				}
			}
			return date [0] + ustring.Make (sepChar) + date [1] + ustring.Make (sepChar) + date [2];

		}

		int GetFormatIndex (ustring [] fm, string t)
		{
			int idx = -1;
			for (int i = 0; i < fm.Length; i++) {
				if (fm [i].Contains (t)) {
					idx = i;
					break;
				}
			}
			return idx;
		}

		void IncCursorPosition ()
		{
			if (CursorPosition == FieldLen)
				return;
			if (Text [++CursorPosition] == sepChar.ToCharArray () [0])
				CursorPosition++;
		}

		void DecCursorPosition ()
		{
			if (CursorPosition == 1)
				return;
			if (Text [--CursorPosition] == sepChar.ToCharArray () [0])
				CursorPosition--;
		}

		void AdjCursorPosition ()
		{
			if (Text [CursorPosition] == sepChar.ToCharArray () [0])
				CursorPosition++;
		}

		/// <inheritdoc/>
		public override bool ProcessKey (KeyEvent kb)
		{
			switch (kb.Key) {
			case Key.DeleteChar:
			case Key.D | Key.CtrlMask:
				if (ReadOnly)
					return true;

				SetText ('0');
				break;

			case Key.Delete:
			case Key.Backspace:
				if (ReadOnly)
					return true;

				SetText ('0');
				DecCursorPosition ();
				break;

			// Home, C-A
			case Key.Home:
			case Key.A | Key.CtrlMask:
				CursorPosition = 1;
				break;

			case Key.CursorLeft:
			case Key.B | Key.CtrlMask:
				DecCursorPosition ();
				break;

			case Key.End:
			case Key.E | Key.CtrlMask: // End
				CursorPosition = FieldLen;
				break;

			case Key.CursorRight:
			case Key.F | Key.CtrlMask:
				IncCursorPosition ();
				break;

			default:
				// Ignore non-numeric characters.
				if (kb.Key < (Key)((int)'0') || kb.Key > (Key)((int)'9'))
					return false;

				if (ReadOnly)
					return true;

				if (SetText (TextModel.ToRunes (ustring.Make ((uint)kb.Key)).First ()))
					IncCursorPosition ();
				return true;
			}
			return true;
		}

		/// <inheritdoc/>
		public override bool MouseEvent (MouseEvent ev)
		{
			if (!ev.Flags.HasFlag (MouseFlags.Button1Clicked))
				return false;
			if (!HasFocus)
				SetFocus ();

			var point = ev.X;
			if (point > FieldLen)
				point = FieldLen;
			if (point < 1)
				point = 1;
			CursorPosition = point;
			AdjCursorPosition ();
			return true;
		}

		/// <summary>
		/// Event firing method for the <see cref="DateChanged"/> event.
		/// </summary>
		/// <param name="args">Event arguments</param>
		public virtual void OnDateChanged (DateTimeEventArgs<DateTime> args)
		{
			DateChanged?.Invoke (args);
		}

		string ToJalaaliString (string format, DateTime date)
		{
			if (string.IsNullOrEmpty (format)) format = " yyyy/MM/dd";
			PersianCalendar pc = new PersianCalendar ();


			var year = pc.GetYear (date);
			var month = pc.GetMonth (date);
			var day = pc.GetDayOfMonth (date);


			format = format.Replace ("yyyy", year.ToString (CultureInfo.InvariantCulture));
			format = format.Replace ("yy", (year % 100).ToString ("00", CultureInfo.InvariantCulture));
			format = format.Replace ("MM", month.ToString ("00", CultureInfo.InvariantCulture));
			format = format.Replace ("M", month.ToString (CultureInfo.InvariantCulture));
			format = format.Replace ("dd", day.ToString ("00", CultureInfo.InvariantCulture));
			format = format.Replace ("d", day.ToString (CultureInfo.InvariantCulture));

			return format;
		}

	}

	/// <summary>
	/// Defines the event arguments for <see cref="DateField.DateChanged"/> and <see cref="TimeField.TimeChanged"/> events.
	/// </summary>
	public class DateTimeEventArgs<T> : EventArgs {
		/// <summary>
		/// The old <see cref="DateField"/> or <see cref="TimeField"/> value.
		/// </summary>
		public T OldValue { get; }

		/// <summary>
		/// The new <see cref="DateField"/> or <see cref="TimeField"/> value.
		/// </summary>
		public T NewValue { get; }

		/// <summary>
		/// The <see cref="DateField"/> or <see cref="TimeField"/> format.
		/// </summary>
		public string Format { get; }

		/// <summary>
		/// Initializes a new instance of <see cref="DateTimeEventArgs{T}"/>
		/// </summary>
		/// <param name="oldValue">The old <see cref="DateField"/> or <see cref="TimeField"/> value.</param>
		/// <param name="newValue">The new <see cref="DateField"/> or <see cref="TimeField"/> value.</param>
		/// <param name="format">The <see cref="DateField"/> or <see cref="TimeField"/> format string.</param>
		public DateTimeEventArgs (T oldValue, T newValue, string format)
		{
			OldValue = oldValue;
			NewValue = newValue;
			Format = format;
		}
	}
}