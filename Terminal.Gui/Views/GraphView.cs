using NStack;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;

namespace Terminal.Gui {

	/// <summary>
	/// Control for rendering graphs (bar, scatter etc)
	/// </summary>
	public class GraphView : View {

		/// <summary>
		/// Horizontal axis
		/// </summary>
		/// <value></value>
		public Axis AxisX {get;} 

		/// <summary>
		/// Vertical axis
		/// </summary>
		/// <value></value>
		public Axis AxisY {get;} 

		/// <summary>
		/// Collection of data series that are rendered in the graph
		/// </summary>
		/// <returns></returns>
		public List<ISeries> Series {get;} = new List<ISeries>();

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
		public PointF ScrollOffset {get;set;} = new PointF(0,0);

		/// <summary>
		/// Translates console width/height into graph space. Defaults
		/// to 1 row/col of console space being 1 unit of graph space. 
		/// </summary>
		/// <returns></returns>
		public PointF CellSize {get;set;} = new PointF(1,1);

		/// <summary>
		/// Creates a new graph with a 1 to 1 graph space with absolute layout
		/// </summary>
		public GraphView()
		{
			CanFocus = true;

			AxisX = new HorizontalAxis();
		 	AxisY = new VerticalAxis();
		}

		///<inheritdoc/>
		public override void Redraw (Rect bounds)
		{
			Driver.SetAttribute (ColorScheme.Normal);

			Move (0, 0);

			// clear all old content
			for (int i = 0; i < Bounds.Height; i++) {
				Move (0, i);
				Driver.AddStr (new string (' ', Bounds.Width));
			}

			base.Redraw(bounds);

			AxisX.DrawAxisLine (Driver, this, Bounds);
			AxisY.DrawAxisLine (Driver, this, Bounds);

			AxisX.DrawAxisLabels (Driver, this, Bounds);
			AxisY.DrawAxisLabels (Driver, this, Bounds);

			for (int x= (int)MarginLeft;x<Bounds.Width;x++){
				for(int y=0;y<Bounds.Height - (int)MarginBottom;y++){

					var space = ScreenToGraphSpace(x,y);

					foreach(var s in Series){
						var rune = s.GetCellValueIfAny(space);
						
						if(rune.HasValue){
							Move(x,y);
							Driver.AddRune(rune.Value);
						}
					}
				}
			}
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
				(ScrollOffset.X - MarginLeft) + (col * CellSize.X),
				(ScrollOffset.Y - MarginBottom) + ((Bounds.Height - row) * CellSize.Y),
				CellSize.X, CellSize.Y);
		}

		/// <summary>
		/// Calculates the screen location for a given point in graph space.
		/// Bear in mind these This may not be off screen
		/// </summary>
		/// <param name="location">Point within the graph</param>
		/// <returns>Screen position (Row / Column) which would be used to render the <paramref name="location"/></returns>
		public Point GraphSpaceToScreen (PointF location)
		{
			return new Point (
				
				(int)((location.X - (ScrollOffset.X - MarginLeft)) / CellSize.X),
				 // screen coordinates are top down while graph coordinates are bottom up
				 Bounds.Height - (int)((location.Y - (ScrollOffset.Y - MarginBottom)) / CellSize.Y) 
				);
		}


		/// <inheritdoc/>
		public override bool ProcessKey (KeyEvent keyEvent)
		{
			//&& Focused == tabsBar

			if (HasFocus && CanFocus ) {
				switch (keyEvent.Key) {

				case Key.CursorLeft:
					Scroll (-CellSize.X, 0);
					return true;
				case Key.CursorRight:
					Scroll (CellSize.X, 0);
					return true;
				case Key.CursorDown:
					Scroll (0, -CellSize.Y);
					return true;
				case Key.CursorUp:
					Scroll (0,CellSize.Y);
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
	}

	/// <summary>
	/// Describes a series of data that can be rendered into a <see cref="GraphView"/>>
	/// </summary>
	public interface ISeries 
	{
		/// <summary>
		/// Return the rune that should be drawn on the screen (if any)
		/// for the current position in the control
		/// </summary>
		/// <param name="graphSpace">Projection of the screen location into the chart graph space</param>
		Rune? GetCellValueIfAny(RectangleF graphSpace);
	}

	/// <summary>
	/// Series composed of any number of discrete data points 
	/// </summary>
	public class ScatterSeries : ISeries
	{
		/// <summary>
		/// Collection of each discrete point in the series
		/// </summary>
		/// <returns></returns>
		public List<PointF> Points {get;set;} = new List<PointF>();

		/// <summary>
		/// Returns a point symbol if the <paramref name="graphSpace"/> contains 
		/// any of the <see cref="Points"/>
		/// </summary>
		/// <param name="graphSpace"></param>
		/// <returns></returns>
		public Rune? GetCellValueIfAny (RectangleF graphSpace)
		{
			if(Points.Any(p=>graphSpace.Contains(p))){
				return 'x';
			}

			return null;
		}
	}

	/// <summary>
	/// Series of bars positioned at regular intervals
	/// </summary>
	public class BarSeries : ISeries {

		public List<Bar> Bars {get;set;}

		/// <summary>
		/// Determines the spacing of bars along the axis. Defaults to 1 i.e. 
		/// every 1 unit of graph space a bar is rendered.  Note that you should
		/// also consider <see cref="GraphView.CellSize"/> when changing this.
		/// </summary>
		public float BarEvery { get; set; } = 1;

		public Rune? GetCellValueIfAny (RectangleF graphSpace)
		{
			Bar bar = XLocationToBar (graphSpace);

			//if no bar should be rendered at this x position
			if(bar == null) {
				return null;
			}

			// and the bar is at least this high
			if (bar.Value >= graphSpace.Top) {

				return bar?.FillRune;
			}
			return null;
		}

		/// <summary>
		/// Translates a position on the x axis to the Bar (if any) that
		/// should be rendered
		/// </summary>
		/// <param name="graphSpace"></param>
		/// <returns></returns>
		private Bar XLocationToBar (RectangleF graphSpace)
		{
			// Position bars on x axis Bar1 at x=1, Bar2 at x=2 etc
			for (int i = 0; i < Bars.Count; i++) {

				float barXPosition = (i + 1f) * BarEvery;

				// if a bar contained in this cell's X axis of data space
				if (barXPosition >= graphSpace.X && barXPosition < graphSpace.Right) {
					return Bars [i];
				}
			}

			return null;
		}

		/// <summary>
		/// Returns the name of the bar (if any) that is rendered at this
		/// point in the x axis
		/// </summary>
		/// <param name="axisPoint"></param>
		/// <returns></returns>
		public string GetLabelText (AxisIncrementToRender axisPoint)
		{
			return XLocationToBar (axisPoint.GraphSpace)?.Name;
		}

		public class Bar {
			public string Name { get; }
			public Rune FillRune { get; }
			public double Value { get; }

			public Bar (string name, Rune fillRune, double value)
			{
				Name = name;
				FillRune = fillRune;
				Value = value;
			}

		}

	}

	/// <summary>
	/// Renders a continuous line with grid line ticks and labels
	/// </summary>
	public abstract class Axis
	{
		/// <summary>
		/// Direction of the axis
		/// </summary>
		/// <value></value>
		public Orientation Orientation {get;}
				
		/// <summary>
		/// Number of units of graph space between ticks on axis
		/// </summary>
		/// <value></value>
		public float Increment {get;set;} = 1;

		/// <summary>
		/// The number of <see cref="Increment"/> before an label is added.
		/// 0 = never show labels
		/// </summary>
		public uint ShowLabelsEvery { get; set; } = 5;
				
		/// <summary>
		/// Allows you to control what label text is rendered for a given <see cref="Increment"/>
		/// when <see cref="ShowLabelsEvery"/> is above 0
		/// </summary>
		public LabelGetterDelegate LabelGetter;

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
	}

	/// <summary>
	/// The horizontal (x axis) of a <see cref="GraphView"/>
	/// </summary>
	public class HorizontalAxis : Axis {
		public HorizontalAxis ():base(Orientation.Horizontal)
		{

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
			graph.Move (0, 0);
			driver.SetAttribute (graph.ColorScheme.Normal);

			var y = GetAxisYPosition (graph, bounds);

			for (int i = 0; i < bounds.Width; i++) {

				graph.Move (i, y);
				driver.AddRune (driver.HLine);
			}
		}

		/// <summary>
		/// Draws the horizontal x axis labels and <see cref="Axis.Increment"/> ticks
		/// </summary>
		public override void DrawAxisLabels (ConsoleDriver driver,GraphView graph,Rect bounds)
		{
			driver.SetAttribute (graph.ColorScheme.Normal);

			var labels = GetLabels (graph, bounds);

			foreach (var label in labels) {

				graph.Move (label.ScreenLocation.X, label.ScreenLocation.Y);

				// draw the tick on the axis
				driver.AddRune (driver.TopTee);

				// and the label text
				if (!string.IsNullOrWhiteSpace (label.Text)) {
					graph.Move (label.ScreenLocation.X - (label.Text.Length/2), label.ScreenLocation.Y+1);
					driver.AddStr (label.Text);
				}
			}
		}

		private IEnumerable<AxisIncrementToRender> GetLabels (GraphView graph, Rect bounds)
		{
			AxisIncrementToRender toRender = null;
			int labels = 0;
			int y = GetAxisYPosition (graph, bounds);

			for (int i = 0; i < bounds.Width; i++) {

				// what bit of the graph is supposed to go here?
				var graphSpace = graph.ScreenToGraphSpace (i,y);

				// if we are overdue rendering a label
				if (toRender == null || graphSpace.X > toRender.GraphSpace.X + Increment) {

					toRender = new AxisIncrementToRender (Orientation, new Point (i,y), graphSpace);

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
		/// Returns the Y screen position of the origin (typically 0,0) of graph space.
		/// Return value is bounded by the screen i.e. the axis is always rendered even
		/// if the origin is offscreen.
		/// </summary>
		/// <param name="graph"></param>
		/// <param name="bounds"></param>
		private int GetAxisYPosition(GraphView graph, Rect bounds)
		{
			// find the origin of the graph in screen space (this allows for 'crosshair' style
			// graphs where positive and negative numbers visible
			var origin = graph.GraphSpaceToScreen (new PointF (0, 0));

			// Float the X axis so that it accurately represents the origin of the graph
			// but anchor it to top/bottom if the origin is offscreen
			return Math.Min (Math.Max (0, origin.Y), bounds.Height - ((int)graph.MarginBottom+1));
		}
	}

	/// <summary>
	/// The vertical (i.e. Y axis) of a <see cref="GraphView"/>
	/// </summary>
	public class VerticalAxis : Axis {

		private int GetLabelThickness (IEnumerable<AxisIncrementToRender> labels)
		{
			var l = labels.ToArray();
			if(l.Length == 0){
				return 1;
			}

			return  l.Max(s=>s.Text.Length);
		}

		public VerticalAxis () :base(Orientation.Vertical)
		{
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
			var x = GetAxisXPosition (graph, bounds);

			// Draw solid line
			// draw axis bottom up
			for (int i = bounds.Height; i > 0; i--) {
				graph.Move (x, i);
				driver.AddRune (driver.VLine);
			}
		}


		/// <summary>
		/// Draws axis <see cref="Axis.Increment"/> markers and labels
		/// </summary>
		/// <param name="bounds"></param>
		public override void DrawAxisLabels (ConsoleDriver driver, GraphView graph, Rect bounds)
		{
			driver.SetAttribute (graph.ColorScheme.Normal);

			var x = GetAxisXPosition (graph, bounds);
			var labels = GetLabels(graph,bounds);
			var labelThickness = GetLabelThickness (labels);
				
			foreach(var label in labels) {

				graph.Move (label.ScreenLocation.X, label.ScreenLocation.Y);

				// draw the tick on the axis
				driver.AddRune (driver.RightTee);

				// and the label text
				if (!string.IsNullOrWhiteSpace (label.Text)) {
					graph.Move (x - labelThickness, label.ScreenLocation.Y);
					driver.AddStr (label.Text);
				}
			}
		}

		private IEnumerable<AxisIncrementToRender> GetLabels (GraphView graph,Rect bounds)
		{
			
			AxisIncrementToRender toRender = null;
			int labels = 0;

			int x = GetAxisXPosition (graph, bounds);

			for (int i = bounds.Height; i > 0; i--) {

				// what bit of the graph is supposed to go here?
				var graphSpace = graph.ScreenToGraphSpace (x,i);

				// if we are overdue rendering a label
				if (toRender == null || graphSpace.Y > toRender.GraphSpace.Y + Increment) {

					toRender = new AxisIncrementToRender (Orientation, new Point (x, i), graphSpace);

					// and the label (if we are due one)
					if (ShowLabelsEvery != 0) {

						// if this increment also needs a label
						if (labels++ % ShowLabelsEvery == 0) {
							toRender.Text = LabelGetter(toRender);
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

			// Float the Y axis so that it accurately represents the origin of the graph
			// but anchor it to left/right if the origin is offscreen
			return Math.Min (Math.Max ((int)graph.MarginLeft, origin.X), bounds.Width);
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
		public Point ScreenLocation { get;  }

		/// <summary>
		/// The volume of graph that is represented by this screen coordingate
		/// </summary>
		public RectangleF GraphSpace { get; }

		private string _text = "";

		/// <summary>
		/// The text (if any) that should be displayed at this axis increment
		/// </summary>
		/// <value></value>
		public string Text {
			get => _text;
			internal set { _text = value ?? ""; }
		}

		/// <summary>
		/// Describe a new section of an axis that requires an axis increment
		/// symbol and/or label
		/// </summary>
		/// <param name="orientation"></param>
		/// <param name="screen"></param>
		/// <param name="graphSpace"></param>
		public AxisIncrementToRender (Orientation orientation,Point screen, RectangleF graphSpace)
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
	public enum Orientation
	{
		
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