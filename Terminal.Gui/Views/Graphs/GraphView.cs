﻿using NStack;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace Terminal.Gui.Graphs {

	/// <summary>
	/// Control for rendering graphs (bar, scatter etc)
	/// </summary>
	public class GraphView : View {

		/// <summary>
		/// Horizontal axis
		/// </summary>
		/// <value></value>
		public Axis AxisX { get; }

		/// <summary>
		/// Vertical axis
		/// </summary>
		/// <value></value>
		public Axis AxisY { get; }

		/// <summary>
		/// Collection of data series that are rendered in the graph
		/// </summary>
		public List<ISeries> Series { get; } = new List<ISeries> ();


		/// <summary>
		/// Elements drawn into graph after series have been drawn e.g. Legends etc
		/// </summary>
		public List<IAnnotation> Annotations { get; } = new List<IAnnotation> ();

		/// <summary>
		/// Amount of space to leave on left of control.  Graph content (<see cref="Series"/>)
		/// will not be rendered in margins but axis labels may be
		/// </summary>
		public uint MarginLeft { get; set; }

		/// <summary>
		/// Amount of space to leave on bottom of control.  Graph content (<see cref="Series"/>)
		/// will not be rendered in margins but axis labels may be
		/// </summary>
		public uint MarginBottom { get; set; }

		/// <summary>
		/// The graph space position of the bottom left of the control.
		/// Changing this scrolls the viewport around in the graph
		/// </summary>
		/// <value></value>
		public PointF ScrollOffset { get; set; } = new PointF (0, 0);

		/// <summary>
		/// Translates console width/height into graph space. Defaults
		/// to 1 row/col of console space being 1 unit of graph space. 
		/// </summary>
		/// <returns></returns>
		public PointF CellSize { get; set; } = new PointF (1, 1);

		/// <summary>
		/// The color of the background of the graph and axis/labels
		/// </summary>
		public Attribute? GraphColor { get; set; }

		/// <summary>
		/// Creates a new graph with a 1 to 1 graph space with absolute layout
		/// </summary>
		public GraphView ()
		{
			CanFocus = true;

			AxisX = new HorizontalAxis ();
			AxisY = new VerticalAxis ();
		}

		/// <summary>
		/// Clears all settings configured on the graph and resets all properties
		/// to default values (<see cref="CellSize"/>, <see cref="ScrollOffset"/> etc) 
		/// </summary>
		public void Reset ()
		{
			ScrollOffset = new PointF (0, 0);
			CellSize = new PointF (1, 1);
			AxisX.Reset ();
			AxisY.Reset ();
			Series.Clear ();
			Annotations.Clear ();
			GraphColor = null;
			SetNeedsDisplay ();
		}

		///<inheritdoc/>
		public override void Redraw (Rect bounds)
		{
			if(CellSize.X == 0 || CellSize.Y == 0) {
				throw new Exception ($"{nameof(CellSize)} cannot be 0");
			}


			SetDriverColorToGraphColor (Driver); 

			Move (0, 0);

			// clear all old content
			for (int i = 0; i < Bounds.Height; i++) {
				Move (0, i);
				Driver.AddStr (new string (' ', Bounds.Width));
			}

			// If there is no data do not display a graph
			if (!Series.Any () && !Annotations.Any ()) {
				return;
			}

			// Draw 'before' annotations
			foreach (var a in Annotations.Where (a => a.BeforeSeries)) {
				a.Render (this, Driver, Bounds);
			}

			SetDriverColorToGraphColor (Driver);

			// The drawable area of the graph (anything that isn't in the margins)
			Rect drawBounds = new Rect((int)MarginLeft,0, Bounds.Width - ((int)MarginLeft), Bounds.Height - (int)MarginBottom);
			RectangleF graphSpace = ScreenToGraphSpace (drawBounds);

			foreach (var s in Series) {

				s.DrawSeries (this, Driver, drawBounds, graphSpace);

				// If a series changes the graph color reset it
				SetDriverColorToGraphColor (Driver);
			}

			SetDriverColorToGraphColor (Driver);

			AxisY.DrawAxisLine (Driver, this, Bounds);
			AxisX.DrawAxisLine (Driver, this, Bounds);

			AxisY.DrawAxisLabels (Driver, this, Bounds);
			AxisX.DrawAxisLabels (Driver, this, Bounds);

			SetDriverColorToGraphColor (Driver);

			// Draw origin with plus
			var origin = GraphSpaceToScreen (new PointF (0, 0));


			if (origin.X >= MarginLeft && origin.X < Bounds.Width) {
				if (origin.Y >= 0 && origin.Y <= Bounds.Height - MarginBottom) {

					if (AxisX.Visible && AxisY.Visible) {
						Move (origin.X, origin.Y);
						AddRune (origin.X, origin.Y, '\u253C');
					}
				}
			}

			// Draw 'after' annotations
			foreach (var a in Annotations.Where (a => !a.BeforeSeries)) {
				a.Render (this, Driver, Bounds);
			}

		}

		/// <summary>
		/// Sets the color attribute of <paramref name="driver"/> to the <see cref="GraphColor"/>
		/// (if defined) or <see cref="ColorScheme"/> otherwise.
		/// </summary>
		public void SetDriverColorToGraphColor (ConsoleDriver driver)
		{
			driver.SetAttribute (GraphColor ?? ColorScheme.Normal);
		}

		/// <summary>
		/// Returns the section of the graph that is represented by the given
		/// screen position
		/// </summary>
		/// <param name="col"></param>
		/// <param name="row"></param>
		/// <returns></returns>
		public RectangleF ScreenToGraphSpace (int col, int row)
		{
			return new RectangleF (
				ScrollOffset.X + ((col - MarginLeft) * CellSize.X),
				ScrollOffset.Y + ((Bounds.Height - (row + MarginBottom + 1)) * CellSize.Y),
				CellSize.X, CellSize.Y);
		}


		/// <summary>
		/// Returns the section of the graph that is represented by the screen area
		/// </summary>
		/// <param name="screenArea"></param>
		/// <returns></returns>
		public RectangleF ScreenToGraphSpace (Rect screenArea)
		{
			// get position of the bottom left
			var pos = ScreenToGraphSpace (screenArea.Left, screenArea.Bottom-1);

			return new RectangleF (pos.X, pos.Y, screenArea.Width * CellSize.X, screenArea.Height * CellSize.Y);
		}
		/// <summary>
		/// Calculates the screen location for a given point in graph space.
		/// Bear in mind these be off screen
		/// </summary>
		/// <param name="location">Point in graph space that may or may not be represented in the
		/// visible area of graph currently presented.  E.g. 0,0 for origin</param>
		/// <returns>Screen position (Column/Row) which would be used to render the graph <paramref name="location"/>.
		/// Note that this can be outside the current client area of the control</returns>
		public Point GraphSpaceToScreen (PointF location)
		{
			return new Point (

				(int)((location.X - ScrollOffset.X) / CellSize.X) + (int)MarginLeft,
				 // screen coordinates are top down while graph coordinates are bottom up
				 (Bounds.Height - 1) - (int)MarginBottom - (int)((location.Y - ScrollOffset.Y) / CellSize.Y)
				);
		}



		/// <inheritdoc/>
		public override bool ProcessKey (KeyEvent keyEvent)
		{
			//&& Focused == tabsBar

			if (HasFocus && CanFocus) {
				switch (keyEvent.Key) {

				case Key.CursorLeft:
					Scroll (-CellSize.X, 0);
					return true;
				case Key.CursorLeft | Key.CtrlMask:
					Scroll (-CellSize.X * 5, 0);
					return true;
				case Key.CursorRight:
					Scroll (CellSize.X, 0);
					return true;
				case Key.CursorRight | Key.CtrlMask:
					Scroll (CellSize.X * 5, 0);
					return true;
				case Key.CursorDown:
					Scroll (0, -CellSize.Y);
					return true;
				case Key.CursorDown | Key.CtrlMask:
					Scroll (0, -CellSize.Y * 5);
					return true;
				case Key.CursorUp:
					Scroll (0, CellSize.Y);
					return true;
				case Key.CursorUp | Key.CtrlMask:
					Scroll (0, CellSize.Y * 5);
					return true;
				}
			}

			return base.ProcessKey (keyEvent);
		}

		/// <summary>
		/// Scrolls the view by a given number of units in graph space.
		/// See <see cref="CellSize"/> to translate this into rows/cols
		/// </summary>
		/// <param name="offsetX"></param>
		/// <param name="offsetY"></param>
		private void Scroll (float offsetX, float offsetY)
		{
			ScrollOffset = new PointF (
				ScrollOffset.X + offsetX,
				ScrollOffset.Y + offsetY);

			SetNeedsDisplay ();
		}


		#region Bresenham's line algorithm
		// https://rosettacode.org/wiki/Bitmap/Bresenham%27s_line_algorithm#C.23

		int ipart (decimal x) { return (int)x; }


		decimal fpart (decimal x)
		{
			if (x < 0) return (1 - (x - Math.Floor (x)));
			return (x - Math.Floor (x));
		}

		/// <summary>
		/// Draws a line between two points in screen space.  Can be diagonals.
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="symbol">The symbol to use for the line</param>
		public void DrawLine (Point start, Point end, Rune symbol)
		{
			if (Equals (start, end)) {
				return;
			}

			int x0 = start.X;
			int y0 = start.Y;
			int x1 = end.X;
			int y1 = end.Y;

			int dx = Math.Abs (x1 - x0), sx = x0 < x1 ? 1 : -1;
			int dy = Math.Abs (y1 - y0), sy = y0 < y1 ? 1 : -1;
			int err = (dx > dy ? dx : -dy) / 2, e2;

			while (true) {

				AddRune (x0, y0, symbol);

				if (x0 == x1 && y0 == y1) break;
				e2 = err;
				if (e2 > -dx) { err -= dy; x0 += sx; }
				if (e2 < dy) { err += dx; y0 += sy; }
			}
		}

		#endregion
	}

	/// <summary>
	/// Describes an overlay element that is rendered after series.
	/// Annotations can be positioned either in screen space (e.g.
	/// a legend) or in graph space (e.g. a line showing high point)
	/// </summary>
	public interface IAnnotation {
		/// <summary>
		/// True if annotation should be drawn before <see cref="ISeries"/>
		/// </summary>
		bool BeforeSeries { get; }

		/// <summary>
		/// Called once after series have been rendered.
		/// </summary>
		/// <param name="graph"></param>
		/// <param name="driver"></param>
		/// <param name="screenBounds"></param>
		void Render (GraphView graph, ConsoleDriver driver, Rect screenBounds);
	}

	/// <summary>
	/// Displays text at a given position (in screen space or graph space)
	/// </summary>
	public class TextAnnotation : IAnnotation {

		/// <summary>
		/// The location on screen to draw the <see cref="Text"/> regardless
		/// of scroll/zoom settings.  This overrides <see cref="GraphPosition"/>
		/// if specified.
		/// </summary>
		public Point? ScreenPosition { get; set; }

		/// <summary>
		/// The location in graph space to draw the <see cref="Text"/>.  This
		/// annotation will only show if the point is in the current viewable
		/// area of the graph presented in the <see cref="GraphView"/>
		/// </summary>
		public PointF GraphPosition { get; set; }

		/// <summary>
		/// Text to display on the graph
		/// </summary>
		/// <value></value>
		public string Text { get; set; }

		/// <summary>
		/// True to add text before plotting series.  Defaults to false
		/// </summary>
		public bool BeforeSeries { get; set; }

		/// <summary>
		/// Draws the annotation
		/// </summary>
		/// <param name="graph"></param>
		/// <param name="driver"></param>
		/// <param name="screenBounds"></param>
		public void Render (GraphView graph, ConsoleDriver driver, Rect screenBounds)
		{
			if (ScreenPosition.HasValue) {
				DrawText (graph, driver, screenBounds, ScreenPosition.Value.X, ScreenPosition.Value.Y);
				return;
			}

			if (GraphPosition != null) {
				var screenPos = graph.GraphSpaceToScreen (GraphPosition);

				DrawText (graph, driver, screenBounds, screenPos.X, screenPos.Y);
			}
		}

		/// <summary>
		/// Draws the <see cref="Text"/> at the given coordinates with truncation to avoid
		/// spilling over <paramref name="screenBounds"/> of the <paramref name="graph"/>
		/// </summary>
		/// <param name="graph"></param>
		/// <param name="driver"></param>
		/// <param name="screenBounds"></param>
		/// <param name="x">Screen x position to start drawing string</param>
		/// <param name="y">Screen y position to start drawing string</param>
		protected void DrawText (GraphView graph, ConsoleDriver driver, Rect screenBounds, int x, int y)
		{
			// the draw point is out of control bounds
			if (!screenBounds.Contains (new Point (x, y))) {
				return;
			}

			// There is no text to draw
			if (string.IsNullOrWhiteSpace (Text)) {
				return;
			}

			graph.Move (x, y);

			int availableWidth = screenBounds.Width - x;

			if (availableWidth <= 0) {
				return;
			}

			if (Text.Length < availableWidth) {
				driver.AddStr (Text);
			} else {
				driver.AddStr (Text.Substring (0, availableWidth));
			}
		}
	}

	/// <summary>
	/// A box containing symbol definitions e.g. meanings for colors in a graph.
	/// The 'Key' to the graph
	/// </summary>
	public class LegendAnnotation : IAnnotation {

		/// <summary>
		/// True to draw a solid border around the legend.
		/// Defaults to true
		/// </summary>
		public bool Border { get; set; } = true;

		/// <summary>
		/// Defines the screen area available for the legend to render in
		/// </summary>
		public Rect Bounds { get; set; }

		/// <summary>
		/// Returns false i.e. Lengends render after series
		/// </summary>
		public bool BeforeSeries =>false;

		/// <summary>
		/// Ordered collection of entries that are rendered in the legend.
		/// </summary>
		List<Tuple<GraphCellToRender, string>> entries = new List<Tuple<GraphCellToRender, string>>();

		/// <summary>
		/// Creates a new empty legend at the given screen coordinates
		/// </summary>
		/// <param name="legendBounds">Defines the area available for the legend to render in
		/// (within the graph).  This is in screen units (i.e. not graph space)</param>
		public LegendAnnotation (Rect legendBounds)
		{
			Bounds = legendBounds;
		}

		/// <summary>
		/// Draws the Legend and all entries into the area within <see cref="Bounds"/>
		/// </summary>
		/// <param name="graph"></param>
		/// <param name="driver"></param>
		/// <param name="screenBounds"></param>
		public void Render (GraphView graph, ConsoleDriver driver, Rect screenBounds)
		{
			if (Border) {
				graph.DrawFrame (Bounds, 0, true);
			}

			// start the legend at
			int y = Bounds.Top + (Border ? 1 : 0);
			int x = Bounds.Left + (Border ? 1 : 0);

			// how much horizontal space is available for writing legend entries?
			int availableWidth = Bounds.Width - (Border ? 2 : 0);
			int availableHeight = Bounds.Height - (Border ? 2 : 0);

			int linesDrawn = 0;

			foreach (var entry in entries) {
				
				if (entry.Item1.Color.HasValue) {
					driver.SetAttribute (entry.Item1.Color.Value);
				} else {
					graph.SetDriverColorToGraphColor (driver);
				}

				// add the symbol
				graph.AddRune (x,y + linesDrawn, entry.Item1.Rune);

				// switch to normal coloring (for the text)
				graph.SetDriverColorToGraphColor (driver);

				// add the text
				graph.Move (x + 1, y + linesDrawn);

				string str = TruncateOrPad(entry.Item2,availableWidth-1,TextAlignment.Left);				
				driver.AddStr (str);

				linesDrawn++;

				// Legend has run out of space
				if(linesDrawn >= availableHeight) {
					break;
				}
			}
		}

		private string TruncateOrPad (string text, int width, TextAlignment alignment)
		{
			if (string.IsNullOrEmpty (text))
				return text;

			// if value is not wide enough
			if (text.Sum (c => Rune.ColumnWidth (c)) < width) {

				// pad it out with spaces to the given alignment
				int toPad = width - (text.Sum (c => Rune.ColumnWidth (c)));

				switch (alignment) {

				case TextAlignment.Left:
					return text + new string (' ', toPad);
				case TextAlignment.Right:
					return new string (' ', toPad) + text;

				// TODO: With single line cells, centered and justified are the same right?
				case TextAlignment.Centered:
				case TextAlignment.Justified:
					return
						new string (' ', (int)Math.Floor (toPad / 2.0)) + // round down
						text +
						 new string (' ', (int)Math.Ceiling (toPad / 2.0)); // round up
				}
			}

			// value is too wide
			return new string (text.TakeWhile (c => (width -= Rune.ColumnWidth (c)) > 0).ToArray ());
		}

		/// <summary>
		/// Adds an entry into the legend.  Duplicate entries are permissable
		/// </summary>
		/// <param name="graphCellToRender">The symbol appearing on the graph that should appear in the legend</param>
		/// <param name="text">Text to render on this line of the legend.  Will be truncated
		/// if outside of Legend <see cref="Bounds"/></param>
		public void AddEntry (GraphCellToRender graphCellToRender, string text)
		{
			entries.Add (Tuple.Create (graphCellToRender, text));
		}
	}


	/// <summary>
	/// Describes a series of data that can be rendered into a <see cref="GraphView"/>>
	/// </summary>
	public interface ISeries {

		/// <summary>
		/// Draws the <paramref name="graphBounds"/> section of a series into the
		/// <paramref name="graph"/> view <paramref name="bounds"/>
		/// </summary>
		/// <param name="graph">Graph series is to be drawn onto</param>
		/// <param name="driver"></param>
		/// <param name="bounds">Visible area of the graph in Console Screen units (excluding margins)</param>
		/// <param name="graphBounds">Visible area of the graph in Graph space units</param>
		void DrawSeries (GraphView graph, ConsoleDriver driver, Rect bounds, RectangleF graphBounds);
	}

	/// <summary>
	/// Describes how to render a single row/column of a <see cref="GraphView"/> based
	/// on the value(s) in <see cref="ISeries"/> at that location
	/// </summary>
	public class GraphCellToRender {

		/// <summary>
		/// The character to render in the console
		/// </summary>
		public Rune Rune { get; set; }

		/// <summary>
		/// Optional color to render the <see cref="Rune"/> with
		/// </summary>
		public Attribute? Color { get; set; }

		/// <summary>
		/// Creates instance and sets <see cref="Rune"/> with default graph coloring
		/// </summary>
		/// <param name="rune"></param>
		public GraphCellToRender (Rune rune)
		{
			Rune = rune;
		}
		/// <summary>
		/// Creates instance and sets <see cref="Rune"/> with custom graph coloring
		/// </summary>
		/// <param name="rune"></param>
		/// <param name="color"></param>
		public GraphCellToRender (Rune rune, Attribute color) : this (rune)
		{
			Color = color;
		}
		/// <summary>
		/// Creates instance and sets <see cref="Rune"/> and <see cref="Color"/> (or default if null)
		/// </summary>
		public GraphCellToRender (Rune rune, Attribute? color) : this (rune)
		{
			Color = color;
		}
	}

	/// <summary>
	/// Series composed of any number of discrete data points 
	/// </summary>
	public class ScatterSeries : ISeries {
		/// <summary>
		/// Collection of each discrete point in the series
		/// </summary>
		/// <returns></returns>
		public List<PointF> Points { get; set; } = new List<PointF> ();

		/// <summary>
		/// The color and character that will be rendered in the console
		/// when there are point(s) in the corresponding graph space.
		/// Defaults to uncolored 'x'
		/// </summary>
		public GraphCellToRender Fill { get; set; } = new GraphCellToRender ('x');

		/// <summary>
		/// Draws all points directly onto the graph
		/// </summary>
		public void DrawSeries (GraphView graph, ConsoleDriver driver, Rect bounds, RectangleF graphBounds)
		{
			if (Fill.Color.HasValue) {
				driver.SetAttribute (Fill.Color.Value);
			}

			foreach (var p in Points.Where (p => graphBounds.Contains (p))) {

				var screenPoint = graph.GraphSpaceToScreen (p);
				graph.AddRune(screenPoint.X, screenPoint.Y, Fill.Rune);
			}

		}

	}


	/// <summary>
	/// Collection of <see cref="BarSeries"/> in which bars are clustered by category
	/// </summary>
	public class MultiBarSeries : ISeries {

		BarSeries [] subSeries;

		/// <summary>
		/// Sub collections.  Each series contains the bars for a different category.  Thus 
		/// SubSeries[0].Bars[0] is the first bar on the axis and SubSeries[1].Bars[0] is the
		/// second etc
		/// </summary>
		public IReadOnlyCollection<BarSeries> SubSeries { get => new ReadOnlyCollection<BarSeries> (subSeries); }

		/// <summary>
		/// The number of units of graph space between bars.  Should be 
		/// less than <see cref="BarSeries.BarEvery"/>
		/// </summary>
		public float Spacing { get; }

		/// <summary>
		/// Creates a new series of clustered bars.
		/// </summary>
		/// <param name="numberOfBarsPerCategory">Each category has this many bars</param>
		/// <param name="barsEvery">How far appart to put each category (in graph space)</param>
		/// <param name="spacing">How much spacing between bars in a category (should be less than <paramref name="barsEvery"/>/<paramref name="numberOfBarsPerCategory"/>)</param>
		/// <param name="colors">Array of colors that define bar colour in each category.  Length must match <paramref name="numberOfBarsPerCategory"/></param>
		public MultiBarSeries (int numberOfBarsPerCategory, float barsEvery, float spacing, Attribute [] colors = null)
		{
			subSeries = new BarSeries [numberOfBarsPerCategory];

			if (colors != null && colors.Length != numberOfBarsPerCategory) {
				throw new ArgumentException ("Number of colours must match the number of bars", nameof (numberOfBarsPerCategory));
			}


			for (int i = 0; i < numberOfBarsPerCategory; i++) {
				subSeries [i] = new BarSeries ();
				subSeries [i].BarEvery = barsEvery;
				subSeries [i].Offset = i * spacing;

				if (colors != null) {
					subSeries [i].OverrideBarColor = colors [i];
				}
			}
			Spacing = spacing;
		}

		/// <summary>
		/// Adds a new cluster of bars
		/// </summary>
		/// <param name="label"></param>
		/// <param name="fill"></param>
		/// <param name="values">Values for each bar in category, must match the number of bars per category</param>
		public void AddBars (string label, Rune fill, params float [] values)
		{
			if (values.Length != subSeries.Length) {
				throw new ArgumentException ("Number of values must match the number of bars per category", nameof (values));
			}

			for (int i = 0; i < values.Length; i++) {
				subSeries [i].Bars.Add (new BarSeries.Bar (label,
					new GraphCellToRender (fill), values [i]));
			}
		}

		public void DrawSeries (GraphView graph, ConsoleDriver driver, Rect bounds, RectangleF graphBounds)
		{
			foreach (var bar in subSeries) {
				bar.DrawSeries (graph, driver, bounds, graphBounds);
			}

		}
	}

	/// <summary>
	/// Series of bars positioned at regular intervals
	/// </summary>
	public class BarSeries : ISeries {

		/// <summary>
		/// Ordered collection of graph bars to position along axis
		/// </summary>
		public List<Bar> Bars { get; set; } = new List<Bar> ();

		/// <summary>
		/// Determines the spacing of bars along the axis. Defaults to 1 i.e. 
		/// every 1 unit of graph space a bar is rendered.  Note that you should
		/// also consider <see cref="GraphView.CellSize"/> when changing this.
		/// </summary>
		public float BarEvery { get; set; } = 1;

		/// <summary>
		/// Direction bars protrude from the corresponding axis.
		/// Defaults to vertical
		/// </summary>
		public Orientation Orientation { get; set; } = Orientation.Vertical;

		/// <summary>
		/// The number of units of graph space along the axis before rendering the first bar
		/// (and subsequent bars - see <see cref="BarEvery"/>).  Defaults to 0
		/// </summary>
		public float Offset { get; internal set; } = 0;

		/// <summary>
		/// Overrides the <see cref="Bar.Fill"/> and <see cref="Bar.ColorGetter"/>
		/// with a fixed color
		/// </summary>
		public Attribute? OverrideBarColor { get; internal set; }

		/// <summary>
		/// Applies any color overriding
		/// </summary>
		/// <param name="graphCellToRender"></param>
		/// <returns></returns>
		protected virtual GraphCellToRender AdjustColor (GraphCellToRender graphCellToRender)
		{
			if (OverrideBarColor.HasValue) {
				graphCellToRender.Color = OverrideBarColor;
			}

			return graphCellToRender;
		}

		public virtual void DrawSeries (GraphView graph, ConsoleDriver driver, Rect bounds, RectangleF graphBounds)
		{
			for(int i = 0;i<Bars.Count;i++) {

				float xStart = Orientation == Orientation.Horizontal ? 0 : Offset + (i * BarEvery);
				float yStart = Orientation == Orientation.Horizontal ? Offset + (i * BarEvery) :0 ;

				float endX = Orientation == Orientation.Horizontal ? Bars[i].Value : xStart;
				float endY = Orientation == Orientation.Horizontal ? yStart : Bars [i].Value;

				var adjusted = AdjustColor (Bars [i].Fill);

				if (adjusted.Color.HasValue) {
					driver.SetAttribute (adjusted.Color.Value);
				}
				
				graph.DrawLine (
					graph.GraphSpaceToScreen (new PointF (xStart, yStart)),
					graph.GraphSpaceToScreen (new PointF (endX,endY))
					, adjusted.Rune);
			}

			// todo draw labels
		}

		/// <summary>
		/// A single bar in a <see cref="BarSeries"/>
		/// </summary>
		public class Bar {

			/// <summary>
			/// Optional text that describes the bar.  This can be added as a label on the axis by setting
			/// <see cref="Axis.LabelGetter"/> = <see cref="BarSeries.GetLabelText(AxisIncrementToRender)"/>
			/// </summary>
			public string Text { get; set; }

			/// <summary>
			/// The color and character that will be rendered in the console
			/// when the bar extends over it
			/// </summary>
			public GraphCellToRender Fill { get; set; }

			/// <summary>
			/// The value in graph space X/Y (depending on <see cref="Orientation"/>) to which the bar extends.
			/// </summary>
			public float Value { get; }

			/// <summary>
			/// Creates a new instance of a single bar rendered in the given <paramref name="fill"/> that extends
			/// out <paramref name="value"/> graph space units in the default <see cref="Orientation"/>
			/// </summary>
			/// <param name="text"></param>
			/// <param name="fill"></param>
			/// <param name="value"></param>
			public Bar (string text, GraphCellToRender fill, float value)
			{
				Text = text;
				Fill = fill;
				Value = value;
			}
		}
	}

	/// <summary>
	/// Sequence of lines to connect points e.g. of a <see cref="ScatterSeries"/>
	/// </summary>
	public class PathAnnotation : IAnnotation {

		/// <summary>
		/// Points that should be connected.  Lines will be drawn between points in the order
		/// they appear in the list
		/// </summary>
		public List<PointF> Points { get; set; } = new List<PointF> ();

		/// <summary>
		/// Color for the line that connects points
		/// </summary>
		public Attribute? LineColor { get; set; }

		/// <summary>
		/// The symbol that gets drawn along the line, defaults to '.'
		/// </summary>
		public Rune LineRune { get; set; } = new Rune ('.');

		/// <summary>
		/// True to add line before plotting series.  Defaults to false
		/// </summary>
		public bool BeforeSeries { get; set; }


		/// <summary>
		/// Draws lines connecting each of the <see cref="Points"/>
		/// </summary>
		/// <param name="graph"></param>
		/// <param name="driver"></param>
		/// <param name="screenBounds"></param>
		public void Render (GraphView graph, ConsoleDriver driver, Rect screenBounds)
		{
			View.Driver.SetAttribute (LineColor ?? graph.ColorScheme.Normal);

			foreach (var line in PointsToLines ()) {

				var start = graph.GraphSpaceToScreen (line.Start);
				var end = graph.GraphSpaceToScreen (line.End);
				graph.DrawLine (start,end, LineRune);
			}
		}

		/// <summary>
		/// Generates lines joining <see cref="Points"/> 
		/// </summary>
		/// <returns></returns>
		private IEnumerable<LineD> PointsToLines ()
		{
			for (int i = 0; i < Points.Count - 1; i++) {

				var line = new LineD (Points [i], Points [i + 1]);
				if (line.Start != null && line.End != null) {
					yield return line;
				}
			}
		}



		/// <summary>
		/// Describes two points in graph space and a line between them
		/// </summary>
		public class LineD {
			/// <summary>
			/// The start of the line
			/// </summary>
			public PointF Start { get; }

			/// <summary>
			/// The end point of the line
			/// </summary>
			public PointF End { get; }

			/// <summary>
			/// Creates a new point at the given coordinates
			/// </summary>
			public LineD (PointF start, PointF end)
			{
				this.Start = start;
				this.End = end;
			}
		}
	}
	/// <summary>
	/// Renders a continuous line with grid line ticks and labels
	/// </summary>
	public abstract class Axis {
		/// <summary>
		/// Default value for <see cref="ShowLabelsEvery"/>
		/// </summary>
		const uint DefaultShowLabelsEvery = 5;

		/// <summary>
		/// Direction of the axis
		/// </summary>
		/// <value></value>
		public Orientation Orientation { get; }

		/// <summary>
		/// Number of units of graph space between ticks on axis
		/// </summary>
		/// <value></value>
		public float Increment { get; set; } = 1;

		/// <summary>
		/// The number of <see cref="Increment"/> before an label is added.
		/// 0 = never show labels
		/// </summary>
		public uint ShowLabelsEvery { get; set; } = DefaultShowLabelsEvery;

		/// <summary>
		/// True to render axis.  Defaults to true
		/// </summary>
		public bool Visible { get; set; } = true;

		/// <summary>
		/// Allows you to control what label text is rendered for a given <see cref="Increment"/>
		/// when <see cref="ShowLabelsEvery"/> is above 0
		/// </summary>
		public LabelGetterDelegate LabelGetter;

		/// <summary>
		/// Displayed below/to left of labels (see <see cref="Orientation"/>).
		/// If text is not visible, check <see cref="GraphView.MarginBottom"/> / <see cref="GraphView.MarginLeft"/>
		/// </summary>
		public string Text;

		/// <summary>
		/// Populates base properties and sets the read only <see cref="Orientation"/>
		/// </summary>
		/// <param name="orientation"></param>
		protected Axis (Orientation orientation)
		{
			Orientation = orientation;
		}

		/// <summary>
		/// Draws the solid line of the axis
		/// </summary>
		/// <param name="driver"></param>
		/// <param name="graph"></param>
		/// <param name="bounds"></param>
		public abstract void DrawAxisLine (ConsoleDriver driver, GraphView graph, Rect bounds);

		/// <summary>
		/// Draws labels and axis <see cref="Increment"/> ticks
		/// </summary>
		/// <param name="driver"></param>
		/// <param name="graph"></param>
		/// <param name="bounds"></param>

		public abstract void DrawAxisLabels (ConsoleDriver driver, GraphView graph, Rect bounds);

		/// <summary>
		/// Resets all configurable properties of the axis to default values
		/// </summary>
		public virtual void Reset ()
		{
			Increment = 1;
			ShowLabelsEvery = DefaultShowLabelsEvery;
			Visible = true;
			Text = "";
		}
	}

	/// <summary>
	/// The horizontal (x axis) of a <see cref="GraphView"/>
	/// </summary>
	class HorizontalAxis : Axis {

		/// <summary>
		/// Creates a new instance of axis with an <see cref="Orientation"/> of <see cref="Orientation.Horizontal"/>
		/// </summary>
		public HorizontalAxis () : base (Orientation.Horizontal)
		{
			LabelGetter = DefaultLabelGetter;
		}

		/// <inheritdoc/>
		public override void Reset ()
		{
			base.Reset ();

			LabelGetter = DefaultLabelGetter;
		}

		private string DefaultLabelGetter (AxisIncrementToRender toRender)
		{
			return toRender.GraphSpace.X.ToString ("N0");
		}

		/// <summary>
		/// Draws the horizontal axis line
		/// </summary>
		/// <param name="driver"></param>
		/// <param name="graph"></param>
		/// <param name="bounds"></param>
		public override void DrawAxisLine (ConsoleDriver driver, GraphView graph, Rect bounds)
		{
			if (!Visible) {
				return;
			}

			graph.Move (0, 0);

			var y = GetAxisYPosition (graph, bounds);

			for (int i = 0; i < bounds.Width; i++) {

				graph.Move (i, y);
				driver.AddRune (driver.HLine);
			}
		}

		/// <summary>
		/// Draws the horizontal x axis labels and <see cref="Axis.Increment"/> ticks
		/// </summary>
		public override void DrawAxisLabels (ConsoleDriver driver, GraphView graph, Rect bounds)
		{
			if (!Visible) {
				return;
			}

			var labels = GetLabels (graph, bounds);

			foreach (var label in labels) {

				graph.Move (label.ScreenLocation.X, label.ScreenLocation.Y);

				// draw the tick on the axis
				driver.AddRune (driver.TopTee);

				// and the label text
				if (!string.IsNullOrWhiteSpace (label.Text)) {

					// center the label but don't draw it outside bounds of the graph
					int drawAtX = Math.Max (0, label.ScreenLocation.X - (label.Text.Length / 2));
					string toRender = label.Text;

					// this is how much space is left
					int xSpaceAvailable = graph.Bounds.Width - drawAtX;

					// There is no space for the label at all!
					if (xSpaceAvailable <= 0) {
						continue;
					}

					// if we are close to right side of graph, don't overspill
					if (toRender.Length > xSpaceAvailable) {
						toRender = toRender.Substring (0, xSpaceAvailable);
					}

					graph.Move (drawAtX, Math.Min (label.ScreenLocation.Y + 1, bounds.Height - 1));
					driver.AddStr (toRender);
				}
			}

			// if there is a title
			if (!string.IsNullOrWhiteSpace (Text)) {

				string toRender = Text;

				// if label is too long
				if (toRender.Length > graph.Bounds.Width) {
					toRender = toRender.Substring (0, graph.Bounds.Width);
				}

				graph.Move (graph.Bounds.Width / 2 - (toRender.Length / 2), graph.Bounds.Height - 1);
				driver.AddStr (toRender);
			}
		}

		private IEnumerable<AxisIncrementToRender> GetLabels (GraphView graph, Rect bounds)
		{
			// if no labels
			if (Increment == 0) {
				yield break;
			}

			int labels = 0;
			int y = GetAxisYPosition (graph, bounds);

			for (int i = 0; i < bounds.Width; i++) {

				// what bit of the graph is supposed to go here?
				var graphSpace = graph.ScreenToGraphSpace (i, y);

				if (Math.Abs (graphSpace.X) % Increment < graph.CellSize.X) {
					var toRender = new AxisIncrementToRender (Orientation, new Point (i, y), graphSpace);

					if (ShowLabelsEvery != 0) {

						// if this increment also needs a label
						if (labels++ % ShowLabelsEvery == 0) {
							toRender.Text = LabelGetter (toRender);
						};
					}

					yield return toRender;
				}
			}
		}
		/// <summary>
		/// Returns the Y screen position of the origin (typically 0,0) of graph space.
		/// Return value is bounded by the screen i.e. the axis is always rendered even
		/// if the origin is offscreen.
		/// </summary>
		/// <param name="graph"></param>
		/// <param name="bounds"></param>
		private int GetAxisYPosition (GraphView graph, Rect bounds)
		{
			// find the origin of the graph in screen space (this allows for 'crosshair' style
			// graphs where positive and negative numbers visible
			var origin = graph.GraphSpaceToScreen (new PointF (0, 0));

			// float the X axis so that it accurately represents the origin of the graph
			// but anchor it to top/bottom if the origin is offscreen
			return Math.Min (Math.Max (0, origin.Y), bounds.Height - ((int)graph.MarginBottom + 1));
		}
	}

	/// <summary>
	/// The vertical (i.e. Y axis) of a <see cref="GraphView"/>
	/// </summary>
	class VerticalAxis : Axis {

		private int GetLabelThickness (IEnumerable<AxisIncrementToRender> labels)
		{
			var l = labels.ToArray ();
			if (l.Length == 0) {
				return 1;
			}

			return l.Max (s => s.Text.Length);
		}

		/// <summary>
		/// Creates a new <see cref="Orientation.Vertical"/> axis
		/// </summary>
		public VerticalAxis () : base (Orientation.Vertical)
		{
			LabelGetter = DefaultLabelGetter;
		}

		/// <inheritdoc/>
		public override void Reset ()
		{
			base.Reset ();

			LabelGetter = DefaultLabelGetter;
		}
		private string DefaultLabelGetter (AxisIncrementToRender toRender)
		{
			return toRender.GraphSpace.Y.ToString ("N0");
		}

		/// <summary>
		/// Draws the vertical axis line
		/// </summary>
		/// <param name="driver"></param>
		/// <param name="graph"></param>
		/// <param name="bounds"></param>
		public override void DrawAxisLine (ConsoleDriver driver, GraphView graph, Rect bounds)
		{
			if (!Visible) {
				return;
			}

			var x = GetAxisXPosition (graph, bounds);

			// Draw solid line
			for (int i = 0; i < bounds.Height; i++) {
				graph.Move (x, i);
				driver.AddRune (driver.VLine);
			}
		}


		/// <summary>
		/// Draws axis <see cref="Axis.Increment"/> markers and labels
		/// </summary>
		/// <param name="driver"></param>
		/// <param name="graph"></param>
		/// <param name="bounds"></param>
		public override void DrawAxisLabels (ConsoleDriver driver, GraphView graph, Rect bounds)
		{
			if (!Visible) {
				return;
			}

			var x = GetAxisXPosition (graph, bounds);
			var labels = GetLabels (graph, bounds);
			var labelThickness = GetLabelThickness (labels);

			foreach (var label in labels) {

				graph.Move (label.ScreenLocation.X, label.ScreenLocation.Y);

				// draw the tick on the axis
				driver.AddRune (driver.RightTee);

				// and the label text
				if (!string.IsNullOrWhiteSpace (label.Text)) {
					graph.Move (Math.Max (0, x - labelThickness), label.ScreenLocation.Y);
					driver.AddStr (label.Text);
				}
			}

			// if there is a title
			if (!string.IsNullOrWhiteSpace (Text)) {

				string toRender = Text;

				// if label is too long
				if (toRender.Length > graph.Bounds.Height) {
					toRender = toRender.Substring (0, graph.Bounds.Height);
				}

				// Draw it 1 letter at a time vertically down row 0 of the control
				int startDrawingAtY = graph.Bounds.Height / 2 - (toRender.Length / 2);

				for (int i = 0; i < toRender.Length; i++) {

					graph.Move (0, startDrawingAtY + i);
					driver.AddRune (toRender [i]);
				}

			}
		}

		private IEnumerable<AxisIncrementToRender> GetLabels (GraphView graph, Rect bounds)
		{
			// if no labels
			if (Increment == 0) {
				yield break;
			}

			int labels = 0;

			int x = GetAxisXPosition (graph, bounds);

			for (int i = 0; i < bounds.Height; i++) {

				// what bit of the graph is supposed to go here?
				var graphSpace = graph.ScreenToGraphSpace (x, i);

				if (Math.Abs (graphSpace.Y) % Increment < graph.CellSize.Y) {
					var toRender = new AxisIncrementToRender (Orientation, new Point (x, i), graphSpace);

					// and the label (if we are due one)
					if (ShowLabelsEvery != 0) {

						// if this increment also needs a label
						if (labels++ % ShowLabelsEvery == 0) {
							toRender.Text = LabelGetter (toRender);
						};

						yield return toRender;
					}

				}

			}
		}

		/// <summary>
		/// Returns the X screen position of the origin (typically 0,0) of graph space.
		/// Return value is bounded by the screen i.e. the axis is always rendered even
		/// if the origin is offscreen.
		/// </summary>
		/// <param name="graph"></param>
		/// <param name="bounds"></param>
		private int GetAxisXPosition (GraphView graph, Rect bounds)
		{
			// find the origin of the graph in screen space (this allows for 'crosshair' style
			// graphs where positive and negative numbers visible
			var origin = graph.GraphSpaceToScreen (new PointF (0, 0));

			// float the Y axis so that it accurately represents the origin of the graph
			// but anchor it to left/right if the origin is offscreen
			return Math.Min (Math.Max ((int)graph.MarginLeft, origin.X), bounds.Width - 1);
		}
	}


	/// <summary>
	/// A location on an axis of a <see cref="GraphView"/> that may
	/// or may not have a label associated with it
	/// </summary>
	public class AxisIncrementToRender {

		/// <summary>
		/// Direction of the parent axis
		/// </summary>
		public Orientation Orientation { get; }

		/// <summary>
		/// Location in the <see cref="Axis"/> that the axis increment appears
		/// </summary>
		public Point ScreenLocation { get; }

		/// <summary>
		/// The volume of graph that is represented by this screen coordinate
		/// </summary>
		public RectangleF GraphSpace { get; }

		private string _text = "";

		/// <summary>
		/// The text (if any) that should be displayed at this axis increment
		/// </summary>
		/// <value></value>
		internal string Text {
			get => _text;
			set { _text = value ?? ""; }
		}

		/// <summary>
		/// Describe a new section of an axis that requires an axis increment
		/// symbol and/or label
		/// </summary>
		/// <param name="orientation"></param>
		/// <param name="screen"></param>
		/// <param name="graphSpace"></param>
		public AxisIncrementToRender (Orientation orientation, Point screen, RectangleF graphSpace)
		{
			Orientation = orientation;
			ScreenLocation = screen;
			GraphSpace = graphSpace;
		}
	}

	/// <summary>
	/// Determines what should be displayed at a given label
	/// </summary>
	/// <param name="toRender">The axis increment to which the label is attached</param>
	/// <returns></returns>
	public delegate string LabelGetterDelegate (AxisIncrementToRender toRender);

	/// <summary>
	/// Direction of an element (horizontal or vertical)
	/// </summary>
	public enum Orientation {

		/// <summary>
		/// Left to right 
		/// </summary>
		Horizontal,

		/// <summary>
		/// Bottom to top
		/// </summary>
		Vertical
	}
}