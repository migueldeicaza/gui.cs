using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Terminal.Gui;
using Xunit;
using System.Globalization;
using Xunit.Abstractions;

namespace Terminal.Gui.Views {

	public class TableViewTests {
		readonly ITestOutputHelper output;

		public TableViewTests (ITestOutputHelper output)
		{
			this.output = output;
		}
		[Fact]
		public void EnsureValidScrollOffsets_WithNoCells ()
		{
			var tableView = new TableView ();

			Assert.Equal (0, tableView.RowOffset);
			Assert.Equal (0, tableView.ColumnOffset);

			// Set empty table
			tableView.Table = new DataTable ();

			// Since table has no rows or columns scroll offset should default to 0
			tableView.EnsureValidScrollOffsets ();
			Assert.Equal (0, tableView.RowOffset);
			Assert.Equal (0, tableView.ColumnOffset);
		}



		[Fact]
		public void EnsureValidScrollOffsets_LoadSmallerTable ()
		{
			var tableView = new TableView ();
			tableView.Bounds = new Rect (0, 0, 25, 10);

			Assert.Equal (0, tableView.RowOffset);
			Assert.Equal (0, tableView.ColumnOffset);

			// Set big table
			tableView.Table = BuildTable (25, 50);

			// Scroll down and along
			tableView.RowOffset = 20;
			tableView.ColumnOffset = 10;

			tableView.EnsureValidScrollOffsets ();

			// The scroll should be valid at the moment
			Assert.Equal (20, tableView.RowOffset);
			Assert.Equal (10, tableView.ColumnOffset);

			// Set small table
			tableView.Table = BuildTable (2, 2);

			// Setting a small table should automatically trigger fixing the scroll offsets to ensure valid cells
			Assert.Equal (0, tableView.RowOffset);
			Assert.Equal (0, tableView.ColumnOffset);


			// Trying to set invalid indexes should not be possible
			tableView.RowOffset = 20;
			tableView.ColumnOffset = 10;

			Assert.Equal (1, tableView.RowOffset);
			Assert.Equal (1, tableView.ColumnOffset);
		}

		[Fact]
		public void SelectedCellChanged_NotFiredForSameValue ()
		{
			var tableView = new TableView () {
				Table = BuildTable (25, 50)
			};

			bool called = false;
			tableView.SelectedCellChanged += (e) => { called = true; };

			Assert.Equal (0, tableView.SelectedColumn);
			Assert.False (called);

			// Changing value to same as it already was should not raise an event
			tableView.SelectedColumn = 0;

			Assert.False (called);

			tableView.SelectedColumn = 10;
			Assert.True (called);
		}



		[Fact]
		public void SelectedCellChanged_SelectedColumnIndexesCorrect ()
		{
			var tableView = new TableView () {
				Table = BuildTable (25, 50)
			};

			bool called = false;
			tableView.SelectedCellChanged += (e) => {
				called = true;
				Assert.Equal (0, e.OldCol);
				Assert.Equal (10, e.NewCol);
			};

			tableView.SelectedColumn = 10;
			Assert.True (called);
		}

		[Fact]
		public void SelectedCellChanged_SelectedRowIndexesCorrect ()
		{
			var tableView = new TableView () {
				Table = BuildTable (25, 50)
			};

			bool called = false;
			tableView.SelectedCellChanged += (e) => {
				called = true;
				Assert.Equal (0, e.OldRow);
				Assert.Equal (10, e.NewRow);
			};

			tableView.SelectedRow = 10;
			Assert.True (called);
		}

		[Fact]
		public void Test_SumColumnWidth_UnicodeLength ()
		{
			Assert.Equal (11, "hello there".Sum (c => Rune.ColumnWidth (c)));

			// Creates a string with the peculiar (french?) r symbol
			String surrogate = "Les Mise" + Char.ConvertFromUtf32 (Int32.Parse ("0301", NumberStyles.HexNumber)) + "rables";

			// The unicode width of this string is shorter than the string length! 
			Assert.Equal (14, surrogate.Sum (c => Rune.ColumnWidth (c)));
			Assert.Equal (15, surrogate.Length);
		}

		[Fact]
		public void IsSelected_MultiSelectionOn_Vertical ()
		{
			var tableView = new TableView () {
				Table = BuildTable (25, 50),
				MultiSelect = true
			};

			// 3 cell vertical selection
			tableView.SetSelection (1, 1, false);
			tableView.SetSelection (1, 3, true);

			Assert.False (tableView.IsSelected (0, 0));
			Assert.False (tableView.IsSelected (1, 0));
			Assert.False (tableView.IsSelected (2, 0));

			Assert.False (tableView.IsSelected (0, 1));
			Assert.True (tableView.IsSelected (1, 1));
			Assert.False (tableView.IsSelected (2, 1));

			Assert.False (tableView.IsSelected (0, 2));
			Assert.True (tableView.IsSelected (1, 2));
			Assert.False (tableView.IsSelected (2, 2));

			Assert.False (tableView.IsSelected (0, 3));
			Assert.True (tableView.IsSelected (1, 3));
			Assert.False (tableView.IsSelected (2, 3));

			Assert.False (tableView.IsSelected (0, 4));
			Assert.False (tableView.IsSelected (1, 4));
			Assert.False (tableView.IsSelected (2, 4));
		}


		[Fact]
		public void IsSelected_MultiSelectionOn_Horizontal ()
		{
			var tableView = new TableView () {
				Table = BuildTable (25, 50),
				MultiSelect = true
			};

			// 2 cell horizontal selection
			tableView.SetSelection (1, 0, false);
			tableView.SetSelection (2, 0, true);

			Assert.False (tableView.IsSelected (0, 0));
			Assert.True (tableView.IsSelected (1, 0));
			Assert.True (tableView.IsSelected (2, 0));
			Assert.False (tableView.IsSelected (3, 0));

			Assert.False (tableView.IsSelected (0, 1));
			Assert.False (tableView.IsSelected (1, 1));
			Assert.False (tableView.IsSelected (2, 1));
			Assert.False (tableView.IsSelected (3, 1));
		}



		[Fact]
		public void IsSelected_MultiSelectionOn_BoxSelection ()
		{
			var tableView = new TableView () {
				Table = BuildTable (25, 50),
				MultiSelect = true
			};

			// 4 cell horizontal in box 2x2
			tableView.SetSelection (0, 0, false);
			tableView.SetSelection (1, 1, true);

			Assert.True (tableView.IsSelected (0, 0));
			Assert.True (tableView.IsSelected (1, 0));
			Assert.False (tableView.IsSelected (2, 0));

			Assert.True (tableView.IsSelected (0, 1));
			Assert.True (tableView.IsSelected (1, 1));
			Assert.False (tableView.IsSelected (2, 1));

			Assert.False (tableView.IsSelected (0, 2));
			Assert.False (tableView.IsSelected (1, 2));
			Assert.False (tableView.IsSelected (2, 2));
		}

		[Fact]
		public void PageDown_ExcludesHeaders ()
		{

			var driver = new FakeDriver ();
			Application.Init (driver, new FakeMainLoop (() => FakeConsole.ReadKey (true)));
			driver.Init (() => { });


			var tableView = new TableView () {
				Table = BuildTable (25, 50),
				MultiSelect = true,
				Bounds = new Rect (0, 0, 10, 5)
			};

			// Header should take up 2 lines
			tableView.Style.ShowHorizontalHeaderOverline = false;
			tableView.Style.ShowHorizontalHeaderUnderline = true;
			tableView.Style.AlwaysShowHeaders = false;

			Assert.Equal (0, tableView.RowOffset);

			tableView.ProcessKey (new KeyEvent (Key.PageDown, new KeyModifiers ()));

			// window height is 5 rows 2 are header so page down should give 3 new rows
			Assert.Equal (3, tableView.RowOffset);

			// header is no longer visible so page down should give 5 new rows
			tableView.ProcessKey (new KeyEvent (Key.PageDown, new KeyModifiers ()));

			Assert.Equal (8, tableView.RowOffset);

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}

		[Fact]
		public void DeleteRow_SelectAll_AdjustsSelectionToPreventOverrun ()
		{
			// create a 4 by 4 table
			var tableView = new TableView () {
				Table = BuildTable (4, 4),
				MultiSelect = true,
				Bounds = new Rect (0, 0, 10, 5)
			};

			tableView.SelectAll ();
			Assert.Equal (16, tableView.GetAllSelectedCells ().Count ());

			// delete one of the columns
			tableView.Table.Columns.RemoveAt (2);

			// table should now be 3x4
			Assert.Equal (12, tableView.GetAllSelectedCells ().Count ());

			// remove a row
			tableView.Table.Rows.RemoveAt (1);

			// table should now be 3x3
			Assert.Equal (9, tableView.GetAllSelectedCells ().Count ());
		}


		[Fact]
		public void DeleteRow_SelectLastRow_AdjustsSelectionToPreventOverrun ()
		{
			// create a 4 by 4 table
			var tableView = new TableView () {
				Table = BuildTable (4, 4),
				MultiSelect = true,
				Bounds = new Rect (0, 0, 10, 5)
			};

			// select the last row
			tableView.MultiSelectedRegions.Clear ();
			tableView.MultiSelectedRegions.Push (new TableView.TableSelection (new Point (0, 3), new Rect (0, 3, 4, 1)));

			Assert.Equal (4, tableView.GetAllSelectedCells ().Count ());

			// remove a row
			tableView.Table.Rows.RemoveAt (0);

			tableView.EnsureValidSelection ();

			// since the selection no longer exists it should be removed
			Assert.Empty (tableView.MultiSelectedRegions);
		}

		[Theory]
		[InlineData (true)]
		[InlineData (false)]
		public void GetAllSelectedCells_SingleCellSelected_ReturnsOne (bool multiSelect)
		{
			var tableView = new TableView () {
				Table = BuildTable (3, 3),
				MultiSelect = multiSelect,
				Bounds = new Rect (0, 0, 10, 5)
			};

			tableView.SetSelection (1, 1, false);

			Assert.Single (tableView.GetAllSelectedCells ());
			Assert.Equal (new Point (1, 1), tableView.GetAllSelectedCells ().Single ());
		}


		[Fact]
		public void GetAllSelectedCells_SquareSelection_ReturnsFour ()
		{
			var tableView = new TableView () {
				Table = BuildTable (3, 3),
				MultiSelect = true,
				Bounds = new Rect (0, 0, 10, 5)
			};

			// move cursor to 1,1
			tableView.SetSelection (1, 1, false);
			// spread selection across to 2,2 (e.g. shift+right then shift+down)
			tableView.SetSelection (2, 2, true);

			var selected = tableView.GetAllSelectedCells ().ToArray ();

			Assert.Equal (4, selected.Length);
			Assert.Equal (new Point (1, 1), selected [0]);
			Assert.Equal (new Point (2, 1), selected [1]);
			Assert.Equal (new Point (1, 2), selected [2]);
			Assert.Equal (new Point (2, 2), selected [3]);
		}


		[Fact]
		public void GetAllSelectedCells_SquareSelection_FullRowSelect ()
		{
			var tableView = new TableView () {
				Table = BuildTable (3, 3),
				MultiSelect = true,
				FullRowSelect = true,
				Bounds = new Rect (0, 0, 10, 5)
			};

			// move cursor to 1,1
			tableView.SetSelection (1, 1, false);
			// spread selection across to 2,2 (e.g. shift+right then shift+down)
			tableView.SetSelection (2, 2, true);

			var selected = tableView.GetAllSelectedCells ().ToArray ();

			Assert.Equal (6, selected.Length);
			Assert.Equal (new Point (0, 1), selected [0]);
			Assert.Equal (new Point (1, 1), selected [1]);
			Assert.Equal (new Point (2, 1), selected [2]);
			Assert.Equal (new Point (0, 2), selected [3]);
			Assert.Equal (new Point (1, 2), selected [4]);
			Assert.Equal (new Point (2, 2), selected [5]);
		}


		[Fact]
		public void GetAllSelectedCells_TwoIsolatedSelections_ReturnsSix ()
		{
			var tableView = new TableView () {
				Table = BuildTable (20, 20),
				MultiSelect = true,
				Bounds = new Rect (0, 0, 10, 5)
			};

			/*  
				Sets up disconnected selections like:

				00000000000
				01100000000
				01100000000
				00000001100
				00000000000
			*/

			tableView.MultiSelectedRegions.Clear ();
			tableView.MultiSelectedRegions.Push (new TableView.TableSelection (new Point (1, 1), new Rect (1, 1, 2, 2)));
			tableView.MultiSelectedRegions.Push (new TableView.TableSelection (new Point (7, 3), new Rect (7, 3, 2, 1)));

			tableView.SelectedColumn = 8;
			tableView.SelectedRow = 3;

			var selected = tableView.GetAllSelectedCells ().ToArray ();

			Assert.Equal (6, selected.Length);

			Assert.Equal (new Point (1, 1), selected [0]);
			Assert.Equal (new Point (2, 1), selected [1]);
			Assert.Equal (new Point (1, 2), selected [2]);
			Assert.Equal (new Point (2, 2), selected [3]);
			Assert.Equal (new Point (7, 3), selected [4]);
			Assert.Equal (new Point (8, 3), selected [5]);
		}

		[Fact]
		public void TableView_ExpandLastColumn_True ()
		{
			var tv = SetUpMiniTable ();

			// the thing we are testing
			tv.Style.ExpandLastColumn = true;

			tv.Redraw (tv.Bounds);

			string expected = @"
┌─┬──────┐
│A│B     │
├─┼──────┤
│1│2     │
";
			GraphViewTests.AssertDriverContentsAre (expected, output);

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}


		[Fact]
		public void TableView_ExpandLastColumn_False ()
		{
			var tv = SetUpMiniTable ();

			// the thing we are testing
			tv.Style.ExpandLastColumn = false;

			tv.Redraw (tv.Bounds);

			string expected = @"
┌─┬─┬────┐
│A│B│    │
├─┼─┼────┤
│1│2│    │
";
			GraphViewTests.AssertDriverContentsAre (expected, output);

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}

		[Fact]
		public void TableView_ExpandLastColumn_False_ExactBounds ()
		{
			var tv = SetUpMiniTable ();

			// the thing we are testing
			tv.Style.ExpandLastColumn = false;
			// width exactly matches the max col widths
			tv.Bounds = new Rect (0, 0, 5, 4);

			tv.Redraw (tv.Bounds);

			string expected = @"
┌─┬─┐
│A│B│
├─┼─┤
│1│2│
";
			GraphViewTests.AssertDriverContentsAre (expected, output);

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}

		[Fact]
		public void TableView_ColorsTest_ColorGetter ()
		{
			var tv = SetUpMiniTable ();

			tv.Style.ExpandLastColumn = false;
			tv.Style.InvertSelectedCellFirstCharacter = true;

			// width exactly matches the max col widths
			tv.Bounds = new Rect (0, 0, 5, 4);
			
			// Create a style for column B
			var bStyle = tv.Style.GetOrCreateColumnStyle (tv.Table.Columns ["B"]);

			// when B is 2 use the custom highlight colour
			ColorScheme cellHighlight = new ColorScheme () { Normal = Attribute.Make (Color.BrightCyan, Color.DarkGray) };
			bStyle.ColorGetter = (a) => Convert.ToInt32(a.CellValue) == 2 ? cellHighlight : null;

			tv.Redraw (tv.Bounds);

			string expected = @"
┌─┬─┐
│A│B│
├─┼─┤
│1│2│
";
			GraphViewTests.AssertDriverContentsAre (expected, output);

			string expectedColors = @"
00000
00000
00000
01020
";
			var invertedNormalColor = Application.Driver.MakeAttribute (tv.ColorScheme.Normal.Background, tv.ColorScheme.Normal.Foreground);

			GraphViewTests.AssertDriverColorsAre (expectedColors, new Attribute [] {
				// 0
				tv.ColorScheme.Normal,				
				// 1
				invertedNormalColor,				
				// 2
				cellHighlight.Normal});

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}

		private TableView SetUpMiniTable ()
		{

			var tv = new TableView ();
			tv.Bounds = new Rect (0, 0, 10, 4);

			var dt = new DataTable ();
			var colA = dt.Columns.Add ("A");
			var colB = dt.Columns.Add ("B");
			dt.Rows.Add (1, 2);

			tv.Table = dt;
			tv.Style.GetOrCreateColumnStyle (colA).MinWidth = 1;
			tv.Style.GetOrCreateColumnStyle (colA).MinWidth = 1;
			tv.Style.GetOrCreateColumnStyle (colB).MaxWidth = 1;
			tv.Style.GetOrCreateColumnStyle (colB).MaxWidth = 1;

			GraphViewTests.InitFakeDriver ();
			tv.ColorScheme = new ColorScheme () {
				Normal = Application.Driver.MakeAttribute (Color.White, Color.Black),
				HotFocus = Application.Driver.MakeAttribute (Color.White, Color.Black)
			};
			return tv;
		}

		/// <summary>
		/// Builds a simple table of string columns with the requested number of columns and rows
		/// </summary>
		/// <param name="cols"></param>
		/// <param name="rows"></param>
		/// <returns></returns>
		public static DataTable BuildTable (int cols, int rows)
		{
			var dt = new DataTable ();

			for (int c = 0; c < cols; c++) {
				dt.Columns.Add ("Col" + c);
			}

			for (int r = 0; r < rows; r++) {
				var newRow = dt.NewRow ();

				for (int c = 0; c < cols; c++) {
					newRow [c] = $"R{r}C{c}";
				}

				dt.Rows.Add (newRow);
			}

			return dt;
		}
	}
}