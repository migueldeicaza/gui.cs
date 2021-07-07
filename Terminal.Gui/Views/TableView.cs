using NStack;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Terminal.Gui {



	/// <summary>
	/// View for tabular data based on a <see cref="DataTable"/>.
	/// 
	/// <a href="https://migueldeicaza.github.io/gui.cs/articles/tableview.html">See TableView Deep Dive for more information</a>.
	/// </summary>
	public class TableView : View {

		/// <summary>
		///  Defines the event arguments for <see cref="TableView.CellActivated"/> event
		/// </summary>
		public class CellActivatedEventArgs : EventArgs {
			/// <summary>
			/// The current table to which the new indexes refer.  May be null e.g. if selection change is the result of clearing the table from the view
			/// </summary>
			/// <value></value>
			public DataTable Table { get; }


			/// <summary>
			/// The column index of the <see cref="Table"/> cell that is being activated
			/// </summary>
			/// <value></value>
			public int Col { get; }

			/// <summary>
			/// The row index of the <see cref="Table"/> cell that is being activated
			/// </summary>
			/// <value></value>
			public int Row { get; }

			/// <summary>
			/// Creates a new instance of arguments describing a cell being activated in <see cref="TableView"/>
			/// </summary>
			/// <param name="t"></param>
			/// <param name="col"></param>
			/// <param name="row"></param>
			public CellActivatedEventArgs (DataTable t, int col, int row)
			{
				Table = t;
				Col = col;
				Row = row;
			}
		}

		private int columnOffset;
		private int rowOffset;
		private int selectedRow;
		private int selectedColumn;
		private DataTable table;
		private TableStyle style = new TableStyle ();

		/// <summary>
		/// The default maximum cell width for <see cref="TableView.MaxCellWidth"/> and <see cref="ColumnStyle.MaxWidth"/>
		/// </summary>
		public const int DefaultMaxCellWidth = 100;

		/// <summary>
		/// The data table to render in the view.  Setting this property automatically updates and redraws the control.
		/// </summary>
		public DataTable Table { get => table; set { table = value; Update (); } }

		/// <summary>
		/// Contains options for changing how the table is rendered
		/// </summary>
		public TableStyle Style { get => style; set { style = value; Update (); } }

		/// <summary>
		/// True to select the entire row at once.  False to select individual cells.  Defaults to false
		/// </summary>
		public bool FullRowSelect { get; set; }

		/// <summary>
		/// True to allow regions to be selected 
		/// </summary>
		/// <value></value>
		public bool MultiSelect { get; set; } = true;

		/// <summary>
		/// When <see cref="MultiSelect"/> is enabled this property contain all rectangles of selected cells.  Rectangles describe column/rows selected in <see cref="Table"/> (not screen coordinates)
		/// </summary>
		/// <returns></returns>
		public Stack<TableSelection> MultiSelectedRegions { get; } = new Stack<TableSelection> ();

		/// <summary>
		/// Horizontal scroll offset.  The index of the first column in <see cref="Table"/> to display when when rendering the view.
		/// </summary>
		/// <remarks>This property allows very wide tables to be rendered with horizontal scrolling</remarks>
		public int ColumnOffset {
			get => columnOffset;

			//try to prevent this being set to an out of bounds column
			set => columnOffset = Table == null ? 0 : Math.Max (0, Math.Min (Table.Columns.Count - 1, value));
		}

		/// <summary>
		/// Vertical scroll offset.  The index of the first row in <see cref="Table"/> to display in the first non header line of the control when rendering the view.
		/// </summary>
		public int RowOffset {
			get => rowOffset;
			set => rowOffset = Table == null ? 0 : Math.Max (0, Math.Min (Table.Rows.Count - 1, value));
		}

		/// <summary>
		/// The index of <see cref="DataTable.Columns"/> in <see cref="Table"/> that the user has currently selected
		/// </summary>
		public int SelectedColumn {
			get => selectedColumn;

			set {
				var oldValue = selectedColumn;

				//try to prevent this being set to an out of bounds column
				selectedColumn = Table == null ? 0 : Math.Min (Table.Columns.Count - 1, Math.Max (0, value));

				if (oldValue != selectedColumn)
					OnSelectedCellChanged (new SelectedCellChangedEventArgs (Table, oldValue, SelectedColumn, SelectedRow, SelectedRow));
			}
		}

		/// <summary>
		/// The index of <see cref="DataTable.Rows"/> in <see cref="Table"/> that the user has currently selected
		/// </summary>
		public int SelectedRow {
			get => selectedRow;
			set {

				var oldValue = selectedRow;

				selectedRow = Table == null ? 0 : Math.Min (Table.Rows.Count - 1, Math.Max (0, value));

				if (oldValue != selectedRow)
					OnSelectedCellChanged (new SelectedCellChangedEventArgs (Table, SelectedColumn, SelectedColumn, oldValue, selectedRow));
			}
		}

		/// <summary>
		/// The maximum number of characters to render in any given column.  This prevents one long column from pushing out all the others
		/// </summary>
		public int MaxCellWidth { get; set; } = DefaultMaxCellWidth;

		/// <summary>
		/// The text representation that should be rendered for cells with the value <see cref="DBNull.Value"/>
		/// </summary>
		public string NullSymbol { get; set; } = "-";

		/// <summary>
		/// The symbol to add after each cell value and header value to visually seperate values (if not using vertical gridlines)
		/// </summary>
		public char SeparatorSymbol { get; set; } = ' ';

		/// <summary>
		/// This event is raised when the selected cell in the table changes.
		/// </summary>
		public event Action<SelectedCellChangedEventArgs> SelectedCellChanged;

		/// <summary>
		/// This event is raised when a cell is activated e.g. by double clicking or pressing <see cref="CellActivationKey"/>
		/// </summary>
		public event Action<CellActivatedEventArgs> CellActivated;

		/// <summary>
		/// The key which when pressed should trigger <see cref="CellActivated"/> event.  Defaults to Enter.
		/// </summary>
		public Key CellActivationKey { get; set; } = Key.Enter;

		/// <summary>
		/// Initialzies a <see cref="TableView"/> class using <see cref="LayoutStyle.Computed"/> layout. 
		/// </summary>
		/// <param name="table">The table to display in the control</param>
		public TableView (DataTable table) : this ()
		{
			this.Table = table;
		}

		/// <summary>
		/// Initialzies a <see cref="TableView"/> class using <see cref="LayoutStyle.Computed"/> layout. Set the <see cref="Table"/> property to begin editing
		/// </summary>
		public TableView () : base ()
		{
			CanFocus = true;
		}

		///<inheritdoc/>
		public override void Redraw (Rect bounds)
		{
			Move (0, 0);
			var frame = Frame;

			// What columns to render at what X offset in viewport
			var columnsToRender = CalculateViewport (bounds).ToArray ();

			Driver.SetAttribute (ColorScheme.Normal);

			//invalidate current row (prevents scrolling around leaving old characters in the frame
			Driver.AddStr (new string (' ', bounds.Width));

			int line = 0;

			if (ShouldRenderHeaders ()) {
				// Render something like:
				/*
					┌────────────────────┬──────────┬───────────┬──────────────┬─────────┐
					│ArithmeticComparator│chi       │Healthboard│Interpretation│Labnumber│
					└────────────────────┴──────────┴───────────┴──────────────┴─────────┘
				*/
				if (Style.ShowHorizontalHeaderOverline) {
					RenderHeaderOverline (line, bounds.Width, columnsToRender);
					line++;
				}

				RenderHeaderMidline (line, columnsToRender);
				line++;

				if (Style.ShowHorizontalHeaderUnderline) {
					RenderHeaderUnderline (line, bounds.Width, columnsToRender);
					line++;
				}
			}

			int headerLinesConsumed = line;

			//render the cells
			for (; line < frame.Height; line++) {

				ClearLine (line, bounds.Width);

				//work out what Row to render
				var rowToRender = RowOffset + (line - headerLinesConsumed);

				//if we have run off the end of the table
				if (Table == null || rowToRender >= Table.Rows.Count || rowToRender < 0)
					continue;

				RenderRow (line, rowToRender, columnsToRender);
			}
		}

		/// <summary>
		/// Clears a line of the console by filling it with spaces
		/// </summary>
		/// <param name="row"></param>
		/// <param name="width"></param>
		private void ClearLine (int row, int width)
		{
			Move (0, row);
			Driver.SetAttribute (ColorScheme.Normal);
			Driver.AddStr (new string (' ', width));
		}

		/// <summary>
		/// Returns the amount of vertical space currently occupied by the header or 0 if it is not visible.
		/// </summary>
		/// <returns></returns>
		private int GetHeaderHeightIfAny ()
		{
			return ShouldRenderHeaders () ? GetHeaderHeight () : 0;
		}

		/// <summary>
		/// Returns the amount of vertical space required to display the header
		/// </summary>
		/// <returns></returns>
		private int GetHeaderHeight ()
		{
			int heightRequired = 1;

			if (Style.ShowHorizontalHeaderOverline)
				heightRequired++;

			if (Style.ShowHorizontalHeaderUnderline)
				heightRequired++;

			return heightRequired;
		}

		private void RenderHeaderOverline (int row, int availableWidth, ColumnToRender [] columnsToRender)
		{
			// Renders a line above table headers (when visible) like:
			// ┌────────────────────┬──────────┬───────────┬──────────────┬─────────┐

			for (int c = 0; c < availableWidth; c++) {

				var rune = Driver.HLine;

				if (Style.ShowVerticalHeaderLines) {

					if (c == 0) {
						rune = Driver.ULCorner;
					}
					// if the next column is the start of a header
					else if (columnsToRender.Any (r => r.X == c + 1)) {
						rune = Driver.TopTee;
					} else if (c == availableWidth - 1) {
						rune = Driver.URCorner;
					}
					  // if the next console column is the lastcolumns end
					  else if (Style.ExpandLastColumn == false &&
						   columnsToRender.Any (r => r.IsVeryLast && r.X + r.Width - 1 == c)) {
						rune = Driver.TopTee;
					}
				}

				AddRuneAt (Driver, c, row, rune);
			}
		}

		private void RenderHeaderMidline (int row, ColumnToRender [] columnsToRender)
		{
			// Renders something like:
			// │ArithmeticComparator│chi       │Healthboard│Interpretation│Labnumber│

			ClearLine (row, Bounds.Width);

			//render start of line
			if (style.ShowVerticalHeaderLines)
				AddRune (0, row, Driver.VLine);

			for (int i = 0; i < columnsToRender.Length; i++) {

				var current = columnsToRender [i];

				var colStyle = Style.GetColumnStyleIfAny (current.Column);
				var colName = current.Column.ColumnName;

				RenderSeparator (current.X - 1, row, true);

				Move (current.X, row);

				Driver.AddStr (TruncateOrPad (colName, colName, current.Width, colStyle));

				if (Style.ExpandLastColumn == false && current.IsVeryLast) {
					RenderSeparator (current.X + current.Width - 1, row, true);
				}
			}

			//render end of line
			if (style.ShowVerticalHeaderLines)
				AddRune (Bounds.Width - 1, row, Driver.VLine);
		}

		private void RenderHeaderUnderline (int row, int availableWidth, ColumnToRender [] columnsToRender)
		{
			// Renders a line below the table headers (when visible) like:
			// ├──────────┼───────────┼───────────────────┼──────────┼────────┼─────────────┤

			for (int c = 0; c < availableWidth; c++) {

				var rune = Driver.HLine;

				if (Style.ShowVerticalHeaderLines) {
					if (c == 0) {
						rune = Style.ShowVerticalCellLines ? Driver.LeftTee : Driver.LLCorner;
					}
					// if the next column is the start of a header
					else if (columnsToRender.Any (r => r.X == c + 1)) {

						/*TODO: is ┼ symbol in Driver?*/
						rune = Style.ShowVerticalCellLines ? '┼' : Driver.BottomTee;
					} else if (c == availableWidth - 1) {
						rune = Style.ShowVerticalCellLines ? Driver.RightTee : Driver.LRCorner;
					}
					  // if the next console column is the lastcolumns end
					  else if (Style.ExpandLastColumn == false &&
							  columnsToRender.Any (r => r.IsVeryLast && r.X + r.Width - 1 == c)) {
						rune = Style.ShowVerticalCellLines ? '┼' : Driver.BottomTee;
					}
				}

				AddRuneAt (Driver, c, row, rune);
			}

		}
		private void RenderRow (int row, int rowToRender, ColumnToRender [] columnsToRender)
		{
			var rowScheme = (Style.RowColorGetter?.Invoke (
				new RowColorGetterArgs(Table,rowToRender))) ?? ColorScheme;

			//render start of line
			if (style.ShowVerticalCellLines)
				AddRune (0, row, Driver.VLine);

			//start by clearing the entire line
			Move (0, row);
			Driver.SetAttribute (FullRowSelect && IsSelected (0, rowToRender) ? rowScheme.HotFocus : rowScheme.Normal);
			Driver.AddStr (new string (' ', Bounds.Width));

			// Render cells for each visible header for the current row
			for (int i = 0; i < columnsToRender.Length; i++) {

				var current = columnsToRender [i];

				var colStyle = Style.GetColumnStyleIfAny (current.Column);

				// move to start of cell (in line with header positions)
				Move (current.X, row);

				// Set color scheme based on whether the current cell is the selected one
				bool isSelectedCell = IsSelected (current.Column.Ordinal, rowToRender);

				var val = Table.Rows [rowToRender] [current.Column];

				// Render the (possibly truncated) cell value
				var representation = GetRepresentation (val, colStyle);

				// to get the colour scheme
				var colorSchemeGetter = colStyle?.ColorGetter;

				ColorScheme scheme;
				if(colorSchemeGetter != null) {
					// user has a delegate for defining row color per cell, call it
					scheme = colorSchemeGetter(
						new CellColorGetterArgs (Table, rowToRender, current.Column.Ordinal, val, representation,rowScheme));

					// if users custom color getter returned null, use the row scheme
					if(scheme == null) {
						scheme = rowScheme;
					}
				}
				else {
					// There is no custom cell coloring delegate so use the scheme for the row
					scheme = rowScheme;
				}

				var cellColor = isSelectedCell ? scheme.HotFocus : scheme.Normal;

				var render = TruncateOrPad (val, representation, current.Width, colStyle);

				// While many cells can be selected (see MultiSelectedRegions) only one cell is the primary (drives navigation etc)
				bool isPrimaryCell = current.Column.Ordinal == selectedColumn && rowToRender == selectedRow;
				
				RenderCell (cellColor,render,isPrimaryCell);
								
				// Reset color scheme to normal for drawing separators if we drew text with custom scheme
				if (scheme != rowScheme) {
					Driver.SetAttribute (isSelectedCell ? rowScheme.HotFocus : rowScheme.Normal);
				}

				// If not in full row select mode always, reset color scheme to normal and render the vertical line (or space) at the end of the cell
				if (!FullRowSelect)
					Driver.SetAttribute (rowScheme.Normal);

				RenderSeparator (current.X - 1, row, false);

				if (Style.ExpandLastColumn == false && current.IsVeryLast) {
					RenderSeparator (current.X + current.Width - 1, row, false);
				}
			}

			//render end of line
			if (style.ShowVerticalCellLines)
				AddRune (Bounds.Width - 1, row, Driver.VLine);
		}

		/// <summary>
		/// Override to provide custom multi colouring to cells.  Use <see cref="View.Driver"/> to
		/// with <see cref="ConsoleDriver.AddStr(ustring)"/>.  The driver will already be
		/// in the correct place when rendering and you must render the full <paramref name="render"/>
		/// or the view will not look right.  For simpler provision of color use <see cref="ColumnStyle.ColorGetter"/>
		/// For changing the content that is rendered use <see cref="ColumnStyle.RepresentationGetter"/>
		/// </summary>
		/// <param name="cellColor"></param>
		/// <param name="render"></param>
		/// <param name="isPrimaryCell"></param>
		protected virtual void RenderCell (Attribute cellColor, string render,bool isPrimaryCell)
		{
			// If the cell is the selected col/row then draw the first rune in inverted colors
			// this allows the user to track which cell is the active one during a multi cell
			// selection or in full row select mode
			if (Style.InvertSelectedCellFirstCharacter && isPrimaryCell) {

				if (render.Length > 0) {
					// invert the color of the current cell for the first character
					Driver.SetAttribute (Driver.MakeAttribute (cellColor.Background, cellColor.Foreground));
					Driver.AddRune (render [0]);

					if (render.Length > 1) {
						Driver.SetAttribute (cellColor);
						Driver.AddStr (render.Substring (1));
					}
				}
			} else {
				Driver.SetAttribute (cellColor);
				Driver.AddStr (render);
			}
		}

		private void RenderSeparator (int col, int row, bool isHeader)
		{
			if (col < 0)
				return;

			var renderLines = isHeader ? style.ShowVerticalHeaderLines : style.ShowVerticalCellLines;

			Rune symbol = renderLines ? Driver.VLine : SeparatorSymbol;
			AddRune (col, row, symbol);
		}

		void AddRuneAt (ConsoleDriver d, int col, int row, Rune ch)
		{
			Move (col, row);
			d.AddRune (ch);
		}

		/// <summary>
		/// Truncates or pads <paramref name="representation"/> so that it occupies a exactly <paramref name="availableHorizontalSpace"/> using the alignment specified in <paramref name="colStyle"/> (or left if no style is defined)
		/// </summary>
		/// <param name="originalCellValue">The object in this cell of the <see cref="Table"/></param>
		/// <param name="representation">The string representation of <paramref name="originalCellValue"/></param>
		/// <param name="availableHorizontalSpace"></param>
		/// <param name="colStyle">Optional style indicating custom alignment for the cell</param>
		/// <returns></returns>
		private string TruncateOrPad (object originalCellValue, string representation, int availableHorizontalSpace, ColumnStyle colStyle)
		{
			if (string.IsNullOrEmpty (representation))
				return representation;

			// if value is not wide enough
			if (representation.Sum (c => Rune.ColumnWidth (c)) < availableHorizontalSpace) {

				// pad it out with spaces to the given alignment
				int toPad = availableHorizontalSpace - (representation.Sum (c => Rune.ColumnWidth (c)) + 1 /*leave 1 space for cell boundary*/);

				switch (colStyle?.GetAlignment (originalCellValue) ?? TextAlignment.Left) {

				case TextAlignment.Left:
					return representation + new string (' ', toPad);
				case TextAlignment.Right:
					return new string (' ', toPad) + representation;

				// TODO: With single line cells, centered and justified are the same right?
				case TextAlignment.Centered:
				case TextAlignment.Justified:
					return
						new string (' ', (int)Math.Floor (toPad / 2.0)) + // round down
						representation +
						 new string (' ', (int)Math.Ceiling (toPad / 2.0)); // round up
				}
			}

			// value is too wide
			return new string (representation.TakeWhile (c => (availableHorizontalSpace -= Rune.ColumnWidth (c)) > 0).ToArray ());
		}

		/// <inheritdoc/>
		public override bool ProcessKey (KeyEvent keyEvent)
		{
			if (Table == null) {
				PositionCursor ();
				return false;
			}

			if (keyEvent.Key == CellActivationKey && Table != null) {
				OnCellActivated (new CellActivatedEventArgs (Table, SelectedColumn, SelectedRow));
				return true;
			}

			switch (keyEvent.Key) {
			case Key.CursorLeft:
			case Key.CursorLeft | Key.ShiftMask:
				ChangeSelectionByOffset (-1, 0, keyEvent.Key.HasFlag (Key.ShiftMask));
				Update ();
				break;
			case Key.CursorRight:
			case Key.CursorRight | Key.ShiftMask:
				ChangeSelectionByOffset (1, 0, keyEvent.Key.HasFlag (Key.ShiftMask));
				Update ();
				break;
			case Key.CursorDown:
			case Key.CursorDown | Key.ShiftMask:
				ChangeSelectionByOffset (0, 1, keyEvent.Key.HasFlag (Key.ShiftMask));
				Update ();
				break;
			case Key.CursorUp:
			case Key.CursorUp | Key.ShiftMask:
				ChangeSelectionByOffset (0, -1, keyEvent.Key.HasFlag (Key.ShiftMask));
				Update ();
				break;
			case Key.PageUp:
			case Key.PageUp | Key.ShiftMask:
				ChangeSelectionByOffset (0, -(Bounds.Height - GetHeaderHeightIfAny ()), keyEvent.Key.HasFlag (Key.ShiftMask));
				Update ();
				break;
			case Key.PageDown:
			case Key.PageDown | Key.ShiftMask:
				ChangeSelectionByOffset (0, Bounds.Height - GetHeaderHeightIfAny (), keyEvent.Key.HasFlag (Key.ShiftMask));
				Update ();
				break;
			case Key.Home | Key.CtrlMask:
			case Key.Home | Key.CtrlMask | Key.ShiftMask:
				// jump to table origin
				SetSelection (0, 0, keyEvent.Key.HasFlag (Key.ShiftMask));
				Update ();
				break;
			case Key.Home:
			case Key.Home | Key.ShiftMask:
				// jump to start of line
				SetSelection (0, SelectedRow, keyEvent.Key.HasFlag (Key.ShiftMask));
				Update ();
				break;
			case Key.End | Key.CtrlMask:
			case Key.End | Key.CtrlMask | Key.ShiftMask:
				// jump to end of table
				SetSelection (Table.Columns.Count - 1, Table.Rows.Count - 1, keyEvent.Key.HasFlag (Key.ShiftMask));
				Update ();
				break;
			case Key.A | Key.CtrlMask:
				SelectAll ();
				Update ();
				break;
			case Key.End:
			case Key.End | Key.ShiftMask:
				//jump to end of row
				SetSelection (Table.Columns.Count - 1, SelectedRow, keyEvent.Key.HasFlag (Key.ShiftMask));
				Update ();
				break;
			default:
				// Not a keystroke we care about
				return false;
			}
			PositionCursor ();
			return true;
		}

		/// <summary>
		/// Moves the <see cref="SelectedRow"/> and <see cref="SelectedColumn"/> to the given col/row in <see cref="Table"/>. Optionally starting a box selection (see <see cref="MultiSelect"/>)
		/// </summary>
		/// <param name="col"></param>
		/// <param name="row"></param>
		/// <param name="extendExistingSelection">True to create a multi cell selection or adjust an existing one</param>
		public void SetSelection (int col, int row, bool extendExistingSelection)
		{
			if (!MultiSelect || !extendExistingSelection)
				MultiSelectedRegions.Clear ();

			if (extendExistingSelection) {
				// If we are extending current selection but there isn't one
				if (MultiSelectedRegions.Count == 0) {
					// Create a new region between the old active cell and the new cell
					var rect = CreateTableSelection (SelectedColumn, SelectedRow, col, row);
					MultiSelectedRegions.Push (rect);
				} else {
					// Extend the current head selection to include the new cell
					var head = MultiSelectedRegions.Pop ();
					var newRect = CreateTableSelection (head.Origin.X, head.Origin.Y, col, row);
					MultiSelectedRegions.Push (newRect);
				}
			}

			SelectedColumn = col;
			SelectedRow = row;
		}

		/// <summary>
		/// Moves the <see cref="SelectedRow"/> and <see cref="SelectedColumn"/> by the provided offsets. Optionally starting a box selection (see <see cref="MultiSelect"/>)
		/// </summary>
		/// <param name="offsetX">Offset in number of columns</param>
		/// <param name="offsetY">Offset in number of rows</param>
		/// <param name="extendExistingSelection">True to create a multi cell selection or adjust an existing one</param>
		public void ChangeSelectionByOffset (int offsetX, int offsetY, bool extendExistingSelection)
		{
			SetSelection (SelectedColumn + offsetX, SelectedRow + offsetY, extendExistingSelection);
		}

		/// <summary>
		/// When <see cref="MultiSelect"/> is on, creates selection over all cells in the table (replacing any old selection regions)
		/// </summary>
		public void SelectAll ()
		{
			if (Table == null || !MultiSelect || Table.Rows.Count == 0)
				return;

			MultiSelectedRegions.Clear ();

			// Create a single region over entire table, set the origin of the selection to the active cell so that a followup spread selection e.g. shift-right behaves properly
			MultiSelectedRegions.Push (new TableSelection (new Point (SelectedColumn, SelectedRow), new Rect (0, 0, Table.Columns.Count, table.Rows.Count)));
			Update ();
		}

		/// <summary>
		/// Returns all cells in any <see cref="MultiSelectedRegions"/> (if <see cref="MultiSelect"/> is enabled) and the selected cell
		/// </summary>
		/// <returns></returns>
		public IEnumerable<Point> GetAllSelectedCells ()
		{
			if (Table == null || Table.Rows.Count == 0)
				yield break;

			EnsureValidSelection ();

			// If there are one or more rectangular selections
			if (MultiSelect && MultiSelectedRegions.Any ()) {

				// Quiz any cells for whether they are selected.  For performance we only need to check those between the top left and lower right vertex of selection regions
				var yMin = MultiSelectedRegions.Min (r => r.Rect.Top);
				var yMax = MultiSelectedRegions.Max (r => r.Rect.Bottom);

				var xMin = FullRowSelect ? 0 : MultiSelectedRegions.Min (r => r.Rect.Left);
				var xMax = FullRowSelect ? Table.Columns.Count : MultiSelectedRegions.Max (r => r.Rect.Right);

				for (int y = yMin; y < yMax; y++) {
					for (int x = xMin; x < xMax; x++) {
						if (IsSelected (x, y)) {
							yield return new Point (x, y);
						}
					}
				}
			} else {

				// if there are no region selections then it is just the active cell

				// if we are selecting the full row
				if (FullRowSelect) {
					// all cells in active row are selected
					for (int x = 0; x < Table.Columns.Count; x++) {
						yield return new Point (x, SelectedRow);
					}
				} else {
					// Not full row select and no multi selections
					yield return new Point (SelectedColumn, SelectedRow);
				}
			}
		}

		/// <summary>
		/// Returns a new rectangle between the two points with positive width/height regardless of relative positioning of the points.  pt1 is always considered the <see cref="TableSelection.Origin"/> point
		/// </summary>
		/// <param name="pt1X">Origin point for the selection in X</param>
		/// <param name="pt1Y">Origin point for the selection in Y</param>
		/// <param name="pt2X">End point for the selection in X</param>
		/// <param name="pt2Y">End point for the selection in Y</param>
		/// <returns></returns>
		private TableSelection CreateTableSelection (int pt1X, int pt1Y, int pt2X, int pt2Y)
		{
			var top = Math.Min (pt1Y, pt2Y);
			var bot = Math.Max (pt1Y, pt2Y);

			var left = Math.Min (pt1X, pt2X);
			var right = Math.Max (pt1X, pt2X);

			// Rect class is inclusive of Top Left but exclusive of Bottom Right so extend by 1
			return new TableSelection (new Point (pt1X, pt1Y), new Rect (left, top, right - left + 1, bot - top + 1));
		}

		/// <summary>
		/// Returns true if the given cell is selected either because it is the active cell or part of a multi cell selection (e.g. <see cref="FullRowSelect"/>)
		/// </summary>
		/// <param name="col"></param>
		/// <param name="row"></param>
		/// <returns></returns>
		public bool IsSelected (int col, int row)
		{
			// Cell is also selected if in any multi selection region
			if (MultiSelect && MultiSelectedRegions.Any (r => r.Rect.Contains (col, row)))
				return true;

			// Cell is also selected if Y axis appears in any region (when FullRowSelect is enabled)
			if (FullRowSelect && MultiSelect && MultiSelectedRegions.Any (r => r.Rect.Bottom > row && r.Rect.Top <= row))
				return true;

			return row == SelectedRow &&
					(col == SelectedColumn || FullRowSelect);
		}

		/// <summary>
		/// Positions the cursor in the area of the screen in which the start of the active cell is rendered.  Calls base implementation if active cell is not visible due to scrolling or table is loaded etc
		/// </summary>
		public override void PositionCursor ()
		{
			if (Table == null) {
				base.PositionCursor ();
				return;
			}

			var screenPoint = CellToScreen (SelectedColumn, SelectedRow);

			if (screenPoint != null)
				Move (screenPoint.Value.X, screenPoint.Value.Y);
		}

		///<inheritdoc/>
		public override bool MouseEvent (MouseEvent me)
		{
			if (!me.Flags.HasFlag (MouseFlags.Button1Clicked) && !me.Flags.HasFlag (MouseFlags.Button1DoubleClicked) &&
				me.Flags != MouseFlags.WheeledDown && me.Flags != MouseFlags.WheeledUp &&
				me.Flags != MouseFlags.WheeledLeft && me.Flags != MouseFlags.WheeledRight)
				return false;

			if (!HasFocus && CanFocus) {
				SetFocus ();
			}

			if (Table == null) {
				return false;
			}

			// Scroll wheel flags
			switch (me.Flags) {
			case MouseFlags.WheeledDown:
				RowOffset++;
				EnsureValidScrollOffsets ();
				SetNeedsDisplay ();
				return true;

			case MouseFlags.WheeledUp:
				RowOffset--;
				EnsureValidScrollOffsets ();
				SetNeedsDisplay ();
				return true;

			case MouseFlags.WheeledRight:
				ColumnOffset++;
				EnsureValidScrollOffsets ();
				SetNeedsDisplay ();
				return true;

			case MouseFlags.WheeledLeft:
				ColumnOffset--;
				EnsureValidScrollOffsets ();
				SetNeedsDisplay ();
				return true;
			}

			if (me.Flags.HasFlag (MouseFlags.Button1Clicked)) {

				var hit = ScreenToCell (me.X, me.Y);
				if (hit != null) {

					SetSelection (hit.Value.X, hit.Value.Y, me.Flags.HasFlag (MouseFlags.ButtonShift));
					Update ();
				}
			}

			// Double clicking a cell activates
			if (me.Flags == MouseFlags.Button1DoubleClicked) {
				var hit = ScreenToCell (me.X, me.Y);
				if (hit != null) {
					OnCellActivated (new CellActivatedEventArgs (Table, hit.Value.X, hit.Value.Y));
				}
			}

			return false;
		}

		/// <summary>
		/// Returns the column and row of <see cref="Table"/> that corresponds to a given point on the screen (relative to the control client area).  Returns null if the point is in the header, no table is loaded or outside the control bounds
		/// </summary>
		/// <param name="clientX">X offset from the top left of the control</param>
		/// <param name="clientY">Y offset from the top left of the control</param>
		/// <returns></returns>
		public Point? ScreenToCell (int clientX, int clientY)
		{
			if (Table == null)
				return null;

			var viewPort = CalculateViewport (Bounds);

			var headerHeight = GetHeaderHeightIfAny ();

			var col = viewPort.LastOrDefault (c => c.X <= clientX);

			// Click is on the header section of rendered UI
			if (clientY < headerHeight)
				return null;

			var rowIdx = RowOffset - headerHeight + clientY;

			if (col != null && rowIdx >= 0) {

				return new Point (col.Column.Ordinal, rowIdx);
			}

			return null;
		}

		/// <summary>
		/// Returns the screen position (relative to the control client area) that the given cell is rendered or null if it is outside the current scroll area or no table is loaded
		/// </summary>
		/// <param name="tableColumn">The index of the <see cref="Table"/> column you are looking for, use <see cref="DataColumn.Ordinal"/></param>
		/// <param name="tableRow">The index of the row in <see cref="Table"/> that you are looking for</param>
		/// <returns></returns>
		public Point? CellToScreen (int tableColumn, int tableRow)
		{
			if (Table == null)
				return null;

			var viewPort = CalculateViewport (Bounds);

			var headerHeight = GetHeaderHeightIfAny ();

			var colHit = viewPort.FirstOrDefault (c => c.Column.Ordinal == tableColumn);

			// current column is outside the scroll area
			if (colHit == null)
				return null;

			// the cell is too far up above the current scroll area
			if (RowOffset > tableRow)
				return null;

			// the cell is way down below the scroll area and off the screen
			if (tableRow > RowOffset + (Bounds.Height - headerHeight))
				return null;

			return new Point (colHit.X, tableRow + headerHeight - RowOffset);
		}
		/// <summary>
		/// Updates the view to reflect changes to <see cref="Table"/> and to (<see cref="ColumnOffset"/> / <see cref="RowOffset"/>) etc
		/// </summary>
		/// <remarks>This always calls <see cref="View.SetNeedsDisplay()"/></remarks>
		public void Update ()
		{
			if (Table == null) {
				SetNeedsDisplay ();
				return;
			}

			EnsureValidScrollOffsets ();
			EnsureValidSelection ();

			EnsureSelectedCellIsVisible ();

			SetNeedsDisplay ();
		}

		/// <summary>
		/// Updates <see cref="ColumnOffset"/> and <see cref="RowOffset"/> where they are outside the bounds of the table (by adjusting them to the nearest existing cell).  Has no effect if <see cref="Table"/> has not been set.
		/// </summary>
		/// <remarks>Changes will not be immediately visible in the display until you call <see cref="View.SetNeedsDisplay()"/></remarks>
		public void EnsureValidScrollOffsets ()
		{
			if (Table == null) {
				return;
			}

			ColumnOffset = Math.Max (Math.Min (ColumnOffset, Table.Columns.Count - 1), 0);
			RowOffset = Math.Max (Math.Min (RowOffset, Table.Rows.Count - 1), 0);
		}


		/// <summary>
		/// Updates <see cref="SelectedColumn"/>, <see cref="SelectedRow"/> and <see cref="MultiSelectedRegions"/> where they are outside the bounds of the table (by adjusting them to the nearest existing cell).  Has no effect if <see cref="Table"/> has not been set.
		/// </summary>
		/// <remarks>Changes will not be immediately visible in the display until you call <see cref="View.SetNeedsDisplay()"/></remarks>
		public void EnsureValidSelection ()
		{
			if (Table == null) {

				// Table doesn't exist, we should probably clear those selections
				MultiSelectedRegions.Clear ();
				return;
			}

			SelectedColumn = Math.Max (Math.Min (SelectedColumn, Table.Columns.Count - 1), 0);
			SelectedRow = Math.Max (Math.Min (SelectedRow, Table.Rows.Count - 1), 0);

			var oldRegions = MultiSelectedRegions.ToArray ().Reverse ();

			MultiSelectedRegions.Clear ();

			// evaluate 
			foreach (var region in oldRegions) {
				// ignore regions entirely below current table state
				if (region.Rect.Top >= Table.Rows.Count)
					continue;

				// ignore regions entirely too far right of table columns
				if (region.Rect.Left >= Table.Columns.Count)
					continue;

				// ensure region's origin exists
				region.Origin = new Point (
					Math.Max (Math.Min (region.Origin.X, Table.Columns.Count - 1), 0),
					Math.Max (Math.Min (region.Origin.Y, Table.Rows.Count - 1), 0));

				// ensure regions do not go over edge of table bounds
				region.Rect = Rect.FromLTRB (region.Rect.Left,
					region.Rect.Top,
					Math.Max (Math.Min (region.Rect.Right, Table.Columns.Count), 0),
					Math.Max (Math.Min (region.Rect.Bottom, Table.Rows.Count), 0)
					);

				MultiSelectedRegions.Push (region);
			}

		}

		/// <summary>
		/// Updates scroll offsets to ensure that the selected cell is visible.  Has no effect if <see cref="Table"/> has not been set.
		/// </summary>
		/// <remarks>Changes will not be immediately visible in the display until you call <see cref="View.SetNeedsDisplay()"/></remarks>
		public void EnsureSelectedCellIsVisible ()
		{
			if (Table == null || Table.Columns.Count <= 0) {
				return;
			}

			var columnsToRender = CalculateViewport (Bounds).ToArray ();
			var headerHeight = GetHeaderHeightIfAny ();

			//if we have scrolled too far to the left 
			if (SelectedColumn < columnsToRender.Min (r => r.Column.Ordinal)) {
				ColumnOffset = SelectedColumn;
			}

			//if we have scrolled too far to the right
			if (SelectedColumn > columnsToRender.Max (r => r.Column.Ordinal)) {
				ColumnOffset = SelectedColumn;
			}

			//if we have scrolled too far down
			if (SelectedRow >= RowOffset + (Bounds.Height - headerHeight)) {
				RowOffset = SelectedRow;
			}
			//if we have scrolled too far up
			if (SelectedRow < RowOffset) {
				RowOffset = SelectedRow;
			}
		}

		/// <summary>
		/// Invokes the <see cref="SelectedCellChanged"/> event
		/// </summary>
		protected virtual void OnSelectedCellChanged (SelectedCellChangedEventArgs args)
		{
			SelectedCellChanged?.Invoke (args);
		}

		/// <summary>
		/// Invokes the <see cref="CellActivated"/> event
		/// </summary>
		/// <param name="args"></param>
		protected virtual void OnCellActivated (CellActivatedEventArgs args)
		{
			CellActivated?.Invoke (args);
		}

		/// <summary>
		/// Calculates which columns should be rendered given the <paramref name="bounds"/> in which to display and the <see cref="ColumnOffset"/>
		/// </summary>
		/// <param name="bounds"></param>
		/// <param name="padding"></param>
		/// <returns></returns>
		private IEnumerable<ColumnToRender> CalculateViewport (Rect bounds, int padding = 1)
		{
			if (Table == null)
				yield break;

			int usedSpace = 0;

			//if horizontal space is required at the start of the line (before the first header)
			if (Style.ShowVerticalHeaderLines || Style.ShowVerticalCellLines)
				usedSpace += 1;

			int availableHorizontalSpace = bounds.Width;
			int rowsToRender = bounds.Height;

			// reserved for the headers row
			if (ShouldRenderHeaders ())
				rowsToRender -= GetHeaderHeight ();

			bool first = true;
			var lastColumn = Table.Columns.Cast<DataColumn> ().Last ();

			foreach (var col in Table.Columns.Cast<DataColumn> ().Skip (ColumnOffset)) {

				int startingIdxForCurrentHeader = usedSpace;
				var colStyle = Style.GetColumnStyleIfAny (col);
				int colWidth;

				// is there enough space for this column (and it's data)?
				usedSpace += colWidth = CalculateMaxCellWidth (col, rowsToRender, colStyle) + padding;

				// no (don't render it) unless its the only column we are render (that must be one massively wide column!)
				if (!first && usedSpace > availableHorizontalSpace)
					yield break;

				// there is space
				yield return new ColumnToRender (col, startingIdxForCurrentHeader,
					// required for if we end up here because first == true i.e. we have a single massive width (overspilling bounds) column to present
					Math.Min (availableHorizontalSpace, colWidth),
					lastColumn == col);
				first = false;
			}
		}

		private bool ShouldRenderHeaders ()
		{
			if (Table == null || Table.Columns.Count == 0)
				return false;

			return Style.AlwaysShowHeaders || rowOffset == 0;
		}

		/// <summary>
		/// Returns the maximum of the <paramref name="col"/> name and the maximum length of data that will be rendered starting at <see cref="RowOffset"/> and rendering <paramref name="rowsToRender"/>
		/// </summary>
		/// <param name="col"></param>
		/// <param name="rowsToRender"></param>
		/// <param name="colStyle"></param>
		/// <returns></returns>
		private int CalculateMaxCellWidth (DataColumn col, int rowsToRender, ColumnStyle colStyle)
		{
			int spaceRequired = col.ColumnName.Sum (c => Rune.ColumnWidth (c));

			// if table has no rows
			if (RowOffset < 0)
				return spaceRequired;


			for (int i = RowOffset; i < RowOffset + rowsToRender && i < Table.Rows.Count; i++) {

				//expand required space if cell is bigger than the last biggest cell or header
				spaceRequired = Math.Max (spaceRequired, GetRepresentation (Table.Rows [i] [col], colStyle).Sum (c => Rune.ColumnWidth (c)));
			}

			// Don't require more space than the style allows
			if (colStyle != null) {

				// enforce maximum cell width based on style
				if (spaceRequired > colStyle.MaxWidth) {
					spaceRequired = colStyle.MaxWidth;
				}

				// enforce minimum cell width based on style
				if (spaceRequired < colStyle.MinWidth) {
					spaceRequired = colStyle.MinWidth;
				}
			}

			// enforce maximum cell width based on global table style
			if (spaceRequired > MaxCellWidth)
				spaceRequired = MaxCellWidth;


			return spaceRequired;
		}

		/// <summary>
		/// Returns the value that should be rendered to best represent a strongly typed <paramref name="value"/> read from <see cref="Table"/>
		/// </summary>
		/// <param name="value"></param>
		/// <param name="colStyle">Optional style defining how to represent cell values</param>
		/// <returns></returns>
		private string GetRepresentation (object value, ColumnStyle colStyle)
		{
			if (value == null || value == DBNull.Value) {
				return NullSymbol;
			}

			return colStyle != null ? colStyle.GetRepresentation (value) : value.ToString ();
		}

		/// <summary>
		/// Delegate for providing color to <see cref="TableView"/> cells based on the value being rendered
		/// </summary>
		/// <param name="args">Contains information about the cell for which color is needed</param>
		/// <returns></returns>
		public delegate ColorScheme CellColorGetterDelegate (CellColorGetterArgs args);

		/// <summary>
		/// Delegate for providing color for a whole row of a <see cref="TableView"/>
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public delegate ColorScheme RowColorGetterDelegate (RowColorGetterArgs args);

		#region Nested Types
		/// <summary>
		/// Describes how to render a given column in  a <see cref="TableView"/> including <see cref="Alignment"/> 
		/// and textual representation of cells (e.g. date formats)
		/// 
		/// <a href="https://migueldeicaza.github.io/gui.cs/articles/tableview.html">See TableView Deep Dive for more information</a>.
		/// </summary>
		public class ColumnStyle {

			/// <summary>
			/// Defines the default alignment for all values rendered in this column.  For custom alignment based on cell contents use <see cref="AlignmentGetter"/>.
			/// </summary>
			public TextAlignment Alignment { get; set; }

			/// <summary>
			/// Defines a delegate for returning custom alignment per cell based on cell values.  When specified this will override <see cref="Alignment"/>
			/// </summary>
			public Func<object, TextAlignment> AlignmentGetter;

			/// <summary>
			/// Defines a delegate for returning custom representations of cell values.  If not set then <see cref="object.ToString()"/> is used.  Return values from your delegate may be truncated e.g. based on <see cref="MaxWidth"/>
			/// </summary>
			public Func<object, string> RepresentationGetter;

			/// <summary>
			/// Defines a delegate for returning a custom color scheme per cell based on cell values.
			/// Return null for the default
			/// </summary>
			public CellColorGetterDelegate ColorGetter;

			/// <summary>
			/// Defines the format for values e.g. "yyyy-MM-dd" for dates
			/// </summary>
			public string Format { get; set; }

			/// <summary>
			/// Set the maximum width of the column in characters.  This value will be ignored if more than the tables <see cref="TableView.MaxCellWidth"/>.  Defaults to <see cref="TableView.DefaultMaxCellWidth"/>
			/// </summary>
			public int MaxWidth { get; set; } = TableView.DefaultMaxCellWidth;

			/// <summary>
			/// Set the minimum width of the column in characters.  This value will be ignored if more than the tables <see cref="TableView.MaxCellWidth"/> or the <see cref="MaxWidth"/>
			/// </summary>
			public int MinWidth { get; set; }

			/// <summary>
			/// Returns the alignment for the cell based on <paramref name="cellValue"/> and <see cref="AlignmentGetter"/>/<see cref="Alignment"/>
			/// </summary>
			/// <param name="cellValue"></param>
			/// <returns></returns>
			public TextAlignment GetAlignment (object cellValue)
			{
				if (AlignmentGetter != null)
					return AlignmentGetter (cellValue);

				return Alignment;
			}

			/// <summary>
			/// Returns the full string to render (which may be truncated if too long) that the current style says best represents the given <paramref name="value"/>
			/// </summary>
			/// <param name="value"></param>
			/// <returns></returns>
			public string GetRepresentation (object value)
			{
				if (!string.IsNullOrWhiteSpace (Format)) {

					if (value is IFormattable f)
						return f.ToString (Format, null);
				}


				if (RepresentationGetter != null)
					return RepresentationGetter (value);

				return value?.ToString ();
			}
		}
		/// <summary>
		/// Defines rendering options that affect how the table is displayed.
		/// 
		/// <a href="https://migueldeicaza.github.io/gui.cs/articles/tableview.html">See TableView Deep Dive for more information</a>.
		/// </summary>
		public class TableStyle {

			/// <summary>
			/// When scrolling down always lock the column headers in place as the first row of the table
			/// </summary>
			public bool AlwaysShowHeaders { get; set; } = false;

			/// <summary>
			/// True to render a solid line above the headers
			/// </summary>
			public bool ShowHorizontalHeaderOverline { get; set; } = true;

			/// <summary>
			/// True to render a solid line under the headers
			/// </summary>
			public bool ShowHorizontalHeaderUnderline { get; set; } = true;

			/// <summary>
			/// True to render a solid line vertical line between cells
			/// </summary>
			public bool ShowVerticalCellLines { get; set; } = true;

			/// <summary>
			/// True to render a solid line vertical line between headers
			/// </summary>
			public bool ShowVerticalHeaderLines { get; set; } = true;

			/// <summary>
			/// True to invert the colors of the first symbol of the selected cell in the <see cref="TableView"/>.
			/// This gives the appearance of a cursor for when the <see cref="ConsoleDriver"/> doesn't otherwise show
			/// this
			/// </summary>
			public bool InvertSelectedCellFirstCharacter { get; set; } = false;

			/// <summary>
			/// Collection of columns for which you want special rendering (e.g. custom column lengths, text alignment etc)
			/// </summary>
			public Dictionary<DataColumn, ColumnStyle> ColumnStyles { get; set; } = new Dictionary<DataColumn, ColumnStyle> ();

			/// <summary>
			/// Delegate for coloring specific rows in a different color.  For cell color <see cref="ColumnStyle.ColorGetter"/>
			/// </summary>
			/// <value></value>
			public RowColorGetterDelegate RowColorGetter {get;set;}

			/// <summary>
			/// Determines rendering when the last column in the table is visible but it's
			/// content or <see cref="ColumnStyle.MaxWidth"/> is less than the remaining 
			/// space in the control.  True (the default) will expand the column to fill
			/// the remaining bounds of the control.  False will draw a column ending line
			/// and leave a blank column that cannot be selected in the remaining space.  
			/// </summary>
			/// <value></value>
			public bool ExpandLastColumn {get;set;} = true;
			
			/// <summary>
			/// Returns the entry from <see cref="ColumnStyles"/> for the given <paramref name="col"/> or null if no custom styling is defined for it
			/// </summary>
			/// <param name="col"></param>
			/// <returns></returns>
			public ColumnStyle GetColumnStyleIfAny (DataColumn col)
			{
				return ColumnStyles.TryGetValue (col, out ColumnStyle result) ? result : null;
			}

			/// <summary>
			/// Returns an existing <see cref="ColumnStyle"/> for the given <paramref name="col"/> or creates a new one with default options
			/// </summary>
			/// <param name="col"></param>
			/// <returns></returns>
			public ColumnStyle GetOrCreateColumnStyle (DataColumn col)
			{
				if (!ColumnStyles.ContainsKey (col))
					ColumnStyles.Add (col, new ColumnStyle ());

				return ColumnStyles [col];
			}
		}

		/// <summary>
		/// Describes a desire to render a column at a given horizontal position in the UI
		/// </summary>
		internal class ColumnToRender {

			/// <summary>
			/// The column to render
			/// </summary>
			public DataColumn Column { get; set; }

			/// <summary>
			/// The horizontal position to begin rendering the column at
			/// </summary>
			public int X { get; set; }

			/// <summary>
			/// The width that the column should occupy as calculated by <see cref="CalculateViewport(Rect, int)"/>.  Note that this includes
			/// space for padding i.e. the separator between columns.
			/// </summary>
			public int Width { get; }

			/// <summary>
			/// True if this column is the very last column in the <see cref="Table"/> (not just the last visible column)
			/// </summary>
			public bool IsVeryLast { get; }

			public ColumnToRender (DataColumn col, int x, int width, bool isVeryLast)
			{
				Column = col;
				X = x;
				Width = width;
				IsVeryLast = isVeryLast;
			}

		}

		/// <summary>
		/// Arguments for a <see cref="CellColorGetterDelegate"/>.  Describes a cell for which a rendering
		/// <see cref="ColorScheme"/> is being sought
		/// </summary>
		public class CellColorGetterArgs {

			/// <summary>
			/// The data table hosted by the <see cref="TableView"/> control.
			/// </summary>
			public DataTable Table { get; }

			/// <summary>
			/// The index of the row in <see cref="Table"/> for which color is needed
			/// </summary>
			public int RowIndex { get; }

			/// <summary>
			/// The index of column in <see cref="Table"/> for which color is needed
			/// </summary>
			public int ColIdex { get; }

			/// <summary>
			/// The hard typed value being rendered in the cell for which color is needed
			/// </summary>
			public object CellValue { get; }

			/// <summary>
			/// The textual representation of <see cref="CellValue"/> (what will actually be drawn to the screen)
			/// </summary>
			public string Representation { get; }

			/// <summary>
			/// the color scheme that is going to be used to render the cell if no cell specific color scheme is returned
			/// </summary>
			public ColorScheme RowScheme { get; }

			internal CellColorGetterArgs (DataTable table, int rowIdx, int colIdx, object cellValue, string representation, ColorScheme rowScheme)
			{
				Table = table;
				RowIndex = rowIdx;
				ColIdex = colIdx;
				CellValue = cellValue;
				Representation = representation;
				RowScheme = rowScheme;
			}

		}

		/// <summary>
		/// Arguments for <see cref="RowColorGetterDelegate"/>. Describes a row of data in a <see cref="DataTable"/>
		/// for which <see cref="ColorScheme"/> is sought.
		/// </summary>
		public class RowColorGetterArgs {

			/// <summary>
			/// The data table hosted by the <see cref="TableView"/> control.
			/// </summary>
			public DataTable Table { get; }

			/// <summary>
			/// The index of the row in <see cref="Table"/> for which color is needed
			/// </summary>
			public int RowIndex { get; }

			internal RowColorGetterArgs (DataTable table, int rowIdx)
			{
				Table = table;
				RowIndex = rowIdx;
			}
		}

		/// <summary>
		/// Defines the event arguments for <see cref="TableView.SelectedCellChanged"/> 
		/// </summary>
		public class SelectedCellChangedEventArgs : EventArgs {
			/// <summary>
			/// The current table to which the new indexes refer.  May be null e.g. if selection change is the result of clearing the table from the view
			/// </summary>
			/// <value></value>
			public DataTable Table { get; }


			/// <summary>
			/// The previous selected column index.  May be invalid e.g. when the selection has been changed as a result of replacing the existing Table with a smaller one
			/// </summary>
			/// <value></value>
			public int OldCol { get; }


			/// <summary>
			/// The newly selected column index.
			/// </summary>
			/// <value></value>
			public int NewCol { get; }


			/// <summary>
			/// The previous selected row index.  May be invalid e.g. when the selection has been changed as a result of deleting rows from the table
			/// </summary>
			/// <value></value>
			public int OldRow { get; }


			/// <summary>
			/// The newly selected row index.
			/// </summary>
			/// <value></value>
			public int NewRow { get; }

			/// <summary>
			/// Creates a new instance of arguments describing a change in selected cell in a <see cref="TableView"/>
			/// </summary>
			/// <param name="t"></param>
			/// <param name="oldCol"></param>
			/// <param name="newCol"></param>
			/// <param name="oldRow"></param>
			/// <param name="newRow"></param>
			public SelectedCellChangedEventArgs (DataTable t, int oldCol, int newCol, int oldRow, int newRow)
			{
				Table = t;
				OldCol = oldCol;
				NewCol = newCol;
				OldRow = oldRow;
				NewRow = newRow;
			}
		}

		/// <summary>
		/// Describes a selected region of the table
		/// </summary>
		public class TableSelection {

			/// <summary>
			/// Corner of the <see cref="Rect"/> where selection began
			/// </summary>
			/// <value></value>
			public Point Origin { get; set; }

			/// <summary>
			/// Area selected
			/// </summary>
			/// <value></value>
			public Rect Rect { get; set; }

			/// <summary>
			/// Creates a new selected area starting at the origin corner and covering the provided rectangular area
			/// </summary>
			/// <param name="origin"></param>
			/// <param name="rect"></param>
			public TableSelection (Point origin, Rect rect)
			{
				Origin = origin;
				Rect = rect;
			}
		}
		#endregion
	}
}
