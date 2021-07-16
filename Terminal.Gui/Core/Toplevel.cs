//
// Toplevel.cs: Toplevel views can be modally executed
//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Terminal.Gui {
	/// <summary>
	/// Toplevel views can be modally executed.
	/// </summary>
	/// <remarks>
	///   <para>
	///     Toplevels can be modally executing views, started by calling <see cref="Application.Run(Toplevel, Func{Exception, bool})"/>. 
	///     They return control to the caller when <see cref="Application.RequestStop(Toplevel)"/> has 
	///     been called (which sets the <see cref="Toplevel.Running"/> property to false). 
	///   </para>
	///   <para>
	///     A Toplevel is created when an application initializes Terminal.Gui by calling <see cref="Application.Init(ConsoleDriver, IMainLoopDriver)"/>.
	///     The application Toplevel can be accessed via <see cref="Application.Top"/>. Additional Toplevels can be created 
	///     and run (e.g. <see cref="Dialog"/>s. To run a Toplevel, create the <see cref="Toplevel"/> and 
	///     call <see cref="Application.Run(Toplevel, Func{Exception, bool})"/>.
	///   </para>
	///   <para>
	///     Toplevels can also opt-in to more sophisticated initialization
	///     by implementing <see cref="ISupportInitialize"/>. When they do
	///     so, the <see cref="ISupportInitialize.BeginInit"/> and
	///     <see cref="ISupportInitialize.EndInit"/> methods will be called
	///     before running the view.
	///     If first-run-only initialization is preferred, the <see cref="ISupportInitializeNotification"/>
	///     can be implemented too, in which case the <see cref="ISupportInitialize"/>
	///     methods will only be called if <see cref="ISupportInitializeNotification.IsInitialized"/>
	///     is <see langword="false"/>. This allows proper <see cref="View"/> inheritance hierarchies
	///     to override base class layout code optimally by doing so only on first run,
	///     instead of on every run.
	///   </para>
	/// </remarks>
	public class Toplevel : View {
		/// <summary>
		/// Gets or sets whether the <see cref="MainLoop"/> for this <see cref="Toplevel"/> is running or not. 
		/// </summary>
		/// <remarks>
		///    Setting this property directly is discouraged. Use <see cref="Application.RequestStop"/> instead. 
		/// </remarks>
		public bool Running { get; set; }

		/// <summary>
		/// Fired once the Toplevel's <see cref="Application.RunState"/> has begin loaded.
		/// A Loaded event handler is a good place to finalize initialization before calling `<see cref="Application.RunLoop(Application.RunState, bool)"/>.
		/// </summary>
		public event Action Loaded;

		/// <summary>
		/// Fired once the Toplevel's <see cref="MainLoop"/> has started it's first iteration.
		/// Subscribe to this event to perform tasks when the <see cref="Toplevel"/> has been laid out and focus has been set.
		/// changes. A Ready event handler is a good place to finalize initialization after calling `<see cref="Application.Run(Func{Exception, bool})"/>(topLevel)`.
		/// </summary>
		public event Action Ready;

		/// <summary>
		/// Fired once the Toplevel's <see cref="Application.RunState"/> has begin unloaded.
		/// A Unloaded event handler is a good place to disposing after calling `<see cref="Application.End(Application.RunState)"/>.
		/// </summary>
		public event Action Unloaded;

		/// <summary>
		/// Invoked once the Toplevel's <see cref="Application.RunState"/> becomes the <see cref="Application.Current"/>.
		/// </summary>
		public event Action<Toplevel> Activate;

		/// <summary>
		/// Invoked once the Toplevel's <see cref="Application.RunState"/> ceases to be the <see cref="Application.Current"/>.
		/// </summary>
		public event Action<Toplevel> Deactivate;

		/// <summary>
		/// Invoked once the child Toplevel's <see cref="Application.RunState"/> is closed from the <see cref="Application.End(View)"/>
		/// </summary>
		public event Action<Toplevel> ChildClosed;

		/// <summary>
		/// Invoked once the last child Toplevel's <see cref="Application.RunState"/> is closed from the <see cref="Application.End(View)"/>
		/// </summary>
		public event Action AllChildClosed;

		/// <summary>
		/// Invoked once the Toplevel's <see cref="Application.RunState"/> is being closing from the <see cref="Application.RequestStop(Toplevel)"/>
		/// </summary>
		public event Action<ToplevelClosingEventArgs> Closing;

		/// <summary>
		/// Invoked once the Toplevel's <see cref="Application.RunState"/> is closed from the <see cref="Application.End(View)"/>
		/// </summary>
		public event Action<Toplevel> Closed;

		/// <summary>
		/// Invoked once the child Toplevel's <see cref="Application.RunState"/> has begin loaded.
		/// </summary>
		public event Action<Toplevel> ChildLoaded;

		/// <summary>
		/// Invoked once the child Toplevel's <see cref="Application.RunState"/> has begin unloaded.
		/// </summary>
		public event Action<Toplevel> ChildUnloaded;

		internal virtual void OnChildUnloaded (Toplevel top)
		{
			ChildUnloaded?.Invoke (top);
		}

		internal virtual void OnChildLoaded (Toplevel top)
		{
			ChildLoaded?.Invoke (top);
		}

		internal virtual void OnClosed (Toplevel top)
		{
			Closed?.Invoke (top);
		}

		internal virtual bool OnClosing (ToplevelClosingEventArgs ev)
		{
			Closing?.Invoke (ev);
			return ev.Cancel;
		}

		internal virtual void OnAllChildClosed ()
		{
			AllChildClosed?.Invoke ();
		}

		internal virtual void OnChildClosed (Toplevel top)
		{
			if (IsMdiContainer) {
				SetChildNeedsDisplay ();
			}
			ChildClosed?.Invoke (top);
		}

		internal virtual void OnDeactivate (Toplevel activated)
		{
			Deactivate?.Invoke (activated);
		}

		internal virtual void OnActivate (Toplevel deactivated)
		{
			Activate?.Invoke (deactivated);
		}

		/// <summary>
		/// Called from <see cref="Application.Begin(Toplevel)"/> before the <see cref="Toplevel"/> is redraws for the first time.
		/// </summary>
		internal virtual void OnLoaded ()
		{
			Loaded?.Invoke ();
		}

		/// <summary>
		/// Called from <see cref="Application.RunLoop"/> after the <see cref="Toplevel"/> has entered it's first iteration of the loop.
		/// </summary>
		internal virtual void OnReady ()
		{
			Ready?.Invoke ();
		}

		/// <summary>
		/// Called from <see cref="Application.End(Application.RunState)"/> before the <see cref="Toplevel"/> is disposed.
		/// </summary>
		internal virtual void OnUnloaded ()
		{
			Unloaded?.Invoke ();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Toplevel"/> class with the specified absolute layout.
		/// </summary>
		/// <param name="frame">A superview-relative rectangle specifying the location and size for the new Toplevel</param>
		public Toplevel (Rect frame) : base (frame)
		{
			Initialize ();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Toplevel"/> class with <see cref="LayoutStyle.Computed"/> layout, defaulting to full screen.
		/// </summary>
		public Toplevel () : base ()
		{
			Initialize ();
			Width = Dim.Fill ();
			Height = Dim.Fill ();
		}

		void Initialize ()
		{
			ColorScheme = Colors.TopLevel;
		}

		/// <summary>
		/// Convenience factory method that creates a new Toplevel with the current terminal dimensions.
		/// </summary>
		/// <returns>The create.</returns>
		public static Toplevel Create ()
		{
			return new Toplevel (new Rect (0, 0, Driver.Cols, Driver.Rows));
		}

		/// <summary>
		/// Gets or sets a value indicating whether this <see cref="Toplevel"/> can focus.
		/// </summary>
		/// <value><c>true</c> if can focus; otherwise, <c>false</c>.</value>
		public override bool CanFocus {
			get => true;
		}

		/// <summary>
		/// Determines whether the <see cref="Toplevel"/> is modal or not.
		/// Causes <see cref="ProcessKey(KeyEvent)"/> to propagate keys upwards
		/// by default unless set to <see langword="true"/>.
		/// </summary>
		public bool Modal { get; set; }

		/// <summary>
		/// Gets or sets the menu for this Toplevel
		/// </summary>
		public virtual MenuBar MenuBar { get; set; }

		/// <summary>
		/// Gets or sets the status bar for this Toplevel
		/// </summary>
		public virtual StatusBar StatusBar { get; set; }

		/// <summary>
		/// Gets or sets if this Toplevel is a Mdi container.
		/// </summary>
		public bool IsMdiContainer { get; set; }

		/// <summary>
		/// Gets or sets if this Toplevel is a Mdi child.
		/// </summary>
		public bool IsMdiChild {
			get {
				return Application.MdiTop != null && Application.MdiTop != this && !Modal;
			}
		}

		///<inheritdoc/>
		public override bool OnKeyDown (KeyEvent keyEvent)
		{
			if (base.OnKeyDown (keyEvent)) {
				return true;
			}

			switch (keyEvent.Key) {
			case Key.AltMask:
			case Key.AltMask | Key.Space:
			case Key.CtrlMask | Key.Space:
				if (MenuBar != null && MenuBar.OnKeyDown (keyEvent)) {
					return true;
				}
				break;
			}

			return false;
		}

		///<inheritdoc/>
		public override bool OnKeyUp (KeyEvent keyEvent)
		{
			if (base.OnKeyUp (keyEvent)) {
				return true;
			}

			switch (keyEvent.Key) {
			case Key.AltMask:
			case Key.AltMask | Key.Space:
			case Key.CtrlMask | Key.Space:
				if (MenuBar != null && MenuBar.OnKeyUp (keyEvent)) {
					return true;
				}
				break;
			}

			return false;
		}

		///<inheritdoc/>
		public override bool ProcessKey (KeyEvent keyEvent)
		{
			if (base.ProcessKey (keyEvent))
				return true;

			switch (ShortcutHelper.GetModifiersKey (keyEvent)) {
			case Key.Q | Key.CtrlMask:
				// FIXED: stop current execution of this container
				if (Application.MdiTop != null) {
					Application.MdiTop.RequestStop ();
				} else {
					Application.RequestStop ();
				}
				break;
			case Key.Z | Key.CtrlMask:
				Driver.Suspend ();
				return true;

#if false
			case Key.F5:
				Application.DebugDrawBounds = !Application.DebugDrawBounds;
				SetNeedsDisplay ();
				return true;
#endif
			case Key.Tab:
			case Key.CursorRight:
			case Key.CursorDown:
			case Key.I | Key.CtrlMask: // Unix
				var old = GetDeepestFocusedSubview (Focused);
				if (!FocusNext ())
					FocusNext ();
				if (old != Focused && old != Focused?.Focused) {
					old?.SetNeedsDisplay ();
					Focused?.SetNeedsDisplay ();
				} else {
					FocusNearestView (SuperView?.TabIndexes, Direction.Forward);
				}
				return true;
			case Key.BackTab | Key.ShiftMask:
			case Key.CursorLeft:
			case Key.CursorUp:
				old = GetDeepestFocusedSubview (Focused);
				if (!FocusPrev ())
					FocusPrev ();
				if (old != Focused && old != Focused?.Focused) {
					old?.SetNeedsDisplay ();
					Focused?.SetNeedsDisplay ();
				} else {
					FocusNearestView (SuperView?.TabIndexes?.Reverse (), Direction.Backward);
				}
				return true;
			case Key.Tab | Key.CtrlMask:
			case Key key when key == Application.AlternateForwardKey: // Needed on Unix
				if (Application.MdiTop == null) {
					Application.Top.FocusNext ();
					if (Application.Top.Focused == null) {
						Application.Top.FocusNext ();
					}
					Application.Top.SetNeedsDisplay ();
				} else {
					MoveNext ();
				}
				return true;
			case Key.Tab | Key.ShiftMask | Key.CtrlMask:
			case Key key when key == Application.AlternateBackwardKey: // Needed on Unix
				if (Application.MdiTop == null) {
					Application.Top.FocusPrev ();
					if (Application.Top.Focused == null) {
						Application.Top.FocusPrev ();
					}
					Application.Top.SetNeedsDisplay ();
				} else {
					MovePrevious ();
				}
				return true;
			case Key.L | Key.CtrlMask:
				Application.Refresh ();
				return true;
			}
			return false;
		}

		///<inheritdoc/>
		public override bool ProcessColdKey (KeyEvent keyEvent)
		{
			if (base.ProcessColdKey (keyEvent)) {
				return true;
			}

			if (ShortcutHelper.FindAndOpenByShortcut (keyEvent, this)) {
				return true;
			}
			return false;
		}

		View GetDeepestFocusedSubview (View view)
		{
			if (view == null) {
				return null;
			}

			foreach (var v in view.Subviews) {
				if (v.HasFocus) {
					return GetDeepestFocusedSubview (v);
				}
			}
			return view;
		}

		void FocusNearestView (IEnumerable<View> views, Direction direction)
		{
			if (views == null) {
				return;
			}

			bool found = false;
			bool focusProcessed = false;
			int idx = 0;

			foreach (var v in views) {
				if (v == this) {
					found = true;
				}
				if (found && v != this) {
					if (direction == Direction.Forward) {
						SuperView?.FocusNext ();
					} else {
						SuperView?.FocusPrev ();
					}
					focusProcessed = true;
					if (SuperView.Focused != null && SuperView.Focused != this) {
						return;
					}
				} else if (found && !focusProcessed && idx == views.Count () - 1) {
					views.ToList () [0].SetFocus ();
				}
				idx++;
			}
		}

		///<inheritdoc/>
		public override void Add (View view)
		{
			AddMenuStatusBar (view);
			base.Add (view);
		}

		internal void AddMenuStatusBar (View view)
		{
			if (view is MenuBar) {
				MenuBar = view as MenuBar;
			}
			if (view is StatusBar) {
				StatusBar = view as StatusBar;
			}
		}

		///<inheritdoc/>
		public override void Remove (View view)
		{
			if (this is Toplevel toplevel && toplevel.MenuBar != null) {
				RemoveMenuStatusBar (view);
			}
			base.Remove (view);
		}

		///<inheritdoc/>
		public override void RemoveAll ()
		{
			if (this == Application.Top) {
				MenuBar?.Dispose ();
				MenuBar = null;
				StatusBar?.Dispose ();
				StatusBar = null;
			}
			base.RemoveAll ();
		}

		internal void RemoveMenuStatusBar (View view)
		{
			if (view is MenuBar) {
				MenuBar?.Dispose ();
				MenuBar = null;
			}
			if (view is StatusBar) {
				StatusBar?.Dispose ();
				StatusBar = null;
			}
		}

		internal void EnsureVisibleBounds (Toplevel top, int x, int y, out int nx, out int ny)
		{
			nx = Math.Max (x, 0);
			int l;
			if (SuperView == null || SuperView is Toplevel) {
				l = Driver.Cols;
			} else {
				l = SuperView.Frame.Width;
			}
			nx = nx + top.Frame.Width > l ? Math.Max (l - top.Frame.Width, 0) : nx;
			SetWidth (top.Frame.Width, out int rWidth);
			if (rWidth < 0 && nx >= top.Frame.X) {
				nx = Math.Max (top.Frame.Right - 2, 0);
			}
			//System.Diagnostics.Debug.WriteLine ($"nx:{nx}, rWidth:{rWidth}");
			bool m, s;
			if (SuperView == null || SuperView.GetType () != typeof (Toplevel)) {
				m = Application.Top.MenuBar != null;
			} else {
				m = ((Toplevel)SuperView).MenuBar != null;
			}
			if (SuperView == null || SuperView is Toplevel) {
				l = m ? 1 : 0;
			} else {
				l = 0;
			}
			ny = Math.Max (y, l);
			if (SuperView == null || SuperView.GetType () != typeof (Toplevel)) {
				s = Application.Top.StatusBar != null && Application.Top.StatusBar.Visible;
			} else {
				s = ((Toplevel)SuperView).StatusBar != null && ((Toplevel)SuperView).StatusBar.Visible;
			}
			if (SuperView == null || SuperView is Toplevel) {
				l = s ? Driver.Rows - 1 : Driver.Rows;
			} else {
				l = s ? SuperView.Frame.Height - 1 : SuperView.Frame.Height;
			}
			ny = Math.Min (ny, l);
			ny = ny + top.Frame.Height > l ? Math.Max (l - top.Frame.Height, m ? 1 : 0) : ny;
			SetHeight (top.Frame.Height, out int rHeight);
			if (rHeight < 0 && ny >= top.Frame.Y) {
				ny = Math.Max (top.Frame.Bottom - 2, 0);
			}
			//System.Diagnostics.Debug.WriteLine ($"ny:{ny}, rHeight:{rHeight}");
		}

		internal void PositionToplevels ()
		{
			PositionToplevel (this);
			foreach (var top in Subviews) {
				if (top is Toplevel) {
					PositionToplevel ((Toplevel)top);
				}
			}
		}

		/// <summary>
		/// Virtual method which allow to be overridden to implement specific positions for inherited <see cref="Toplevel"/>.
		/// </summary>
		/// <param name="top">The toplevel.</param>
		public virtual void PositionToplevel (Toplevel top)
		{
			EnsureVisibleBounds (top, top.Frame.X, top.Frame.Y, out int nx, out int ny);
			if ((top?.SuperView != null || top != Application.Top)
				&& (nx > top.Frame.X || ny > top.Frame.Y) && top.LayoutStyle == LayoutStyle.Computed) {
				if ((top.X == null || top.X is Pos.PosAbsolute) && top.Bounds.X != nx) {
					top.X = nx;
				}
				if ((top.Y == null || top.Y is Pos.PosAbsolute) && top.Bounds.Y != ny) {
					top.Y = ny;
				}
			}

			View superView = null;
			StatusBar statusBar = null;

			if (top != Application.Top && Application.Top.StatusBar != null) {
				superView = Application.Top;
				statusBar = Application.Top.StatusBar;
			} else if (top?.SuperView != null && top.SuperView is Toplevel toplevel) {
				superView = top.SuperView;
				statusBar = toplevel.StatusBar;
			}
			if (statusBar != null) {
				if (ny + top.Frame.Height >= superView.Frame.Height - (statusBar.Visible ? 1 : 0)) {
					if (top.Height is Dim.DimFill) {
						top.Height = Dim.Fill (statusBar.Visible ? 1 : 0);
					}
				}
				if (superView == Application.Top) {
					top.SetRelativeLayout (superView.Frame);
				} else {
					superView.LayoutSubviews ();
				}
			}
			if (top.StatusBar != null) {
				if (top.StatusBar.Frame.Y != top.Frame.Height - (top.StatusBar.Visible ? 1 : 0)) {
					top.StatusBar.Y = top.Frame.Height - (top.StatusBar.Visible ? 1 : 0);
					top.LayoutSubviews ();
				}
				top.BringSubviewToFront (top.StatusBar);
			}
		}

		///<inheritdoc/>
		public override void Redraw (Rect bounds)
		{
			if (!Visible) {
				return;
			}

			if (!NeedDisplay.IsEmpty || ChildNeedsDisplay || LayoutNeeded) {
				Driver.SetAttribute (ColorScheme.Normal);

				// This is the Application.Top. Clear just the region we're being asked to redraw 
				// (the bounds passed to us).
				// Must be the screen-relative region to clear, not the bounds.
				Clear (Frame);
				Driver.SetAttribute (Colors.Base.Normal);

				if (LayoutStyle == LayoutStyle.Computed)
					SetRelativeLayout (Bounds);
				PositionToplevels ();
				LayoutSubviews ();

				if (this == Application.MdiTop) {
					foreach (var top in Application.MdiChildes.AsEnumerable ().Reverse ()) {
						if (top.Frame.IntersectsWith (bounds)) {
							if (top != this && !top.IsCurrentTop && !OutsideTopFrame (top) && top.Visible) {
								top.SetNeedsLayout ();
								top.SetNeedsDisplay (top.Bounds);
								top.Redraw (top.Bounds);
							}
						}
					}
				}

				foreach (var view in Subviews) {
					if (view.Frame.IntersectsWith (bounds) && !OutsideTopFrame (this)) {
						view.SetNeedsLayout ();
						view.SetNeedsDisplay (view.Bounds);
						//view.Redraw (view.Bounds);
					}
				}

				ClearLayoutNeeded ();
				ClearNeedsDisplay ();
			}

			base.Redraw (Bounds);
		}

		bool OutsideTopFrame (Toplevel top)
		{
			if (top.Frame.X > Driver.Cols || top.Frame.Y > Driver.Rows) {
				return true;
			}
			return false;
		}

		//
		// FIXED:It does not look like the event is raised on clicked-drag
		// need to figure that out.
		//
		internal static Point? dragPosition;
		Point start;

		///<inheritdoc/>
		public override bool MouseEvent (MouseEvent mouseEvent)
		{
			// FIXED:The code is currently disabled, because the
			// Driver.UncookMouse does not seem to have an effect if there is
			// a pending mouse event activated.

			int nx, ny;
			if (!dragPosition.HasValue && mouseEvent.Flags == (MouseFlags.Button1Pressed)) {
				// Only start grabbing if the user clicks on the title bar.
				if (mouseEvent.Y == 0) {
					start = new Point (mouseEvent.X, mouseEvent.Y);
					dragPosition = new Point ();
					nx = mouseEvent.X - mouseEvent.OfX;
					ny = mouseEvent.Y - mouseEvent.OfY;
					dragPosition = new Point (nx, ny);
					Application.GrabMouse (this);
				}

				//System.Diagnostics.Debug.WriteLine ($"Starting at {dragPosition}");
				return true;
			} else if (mouseEvent.Flags == (MouseFlags.Button1Pressed | MouseFlags.ReportMousePosition) ||
				mouseEvent.Flags == MouseFlags.Button3Pressed) {
				if (dragPosition.HasValue) {
					if (SuperView == null) {
						// Redraw the entire app window using just our Frame. Since we are 
						// Application.Top, and our Frame always == our Bounds (Location is always (0,0))
						// our Frame is actually view-relative (which is what Redraw takes).
						// We need to pass all the view bounds because since the windows was 
						// moved around, we don't know exactly what was the affected region.
						Application.Top.SetNeedsDisplay ();
					} else {
						SuperView.SetNeedsDisplay ();
					}
					EnsureVisibleBounds (this, mouseEvent.X + (SuperView == null ? mouseEvent.OfX - start.X : Frame.X - start.X),
						mouseEvent.Y + (SuperView == null ? mouseEvent.OfY : Frame.Y), out nx, out ny);

					dragPosition = new Point (nx, ny);
					LayoutSubviews ();
					Frame = new Rect (nx, ny, Frame.Width, Frame.Height);
					if (X == null || X is Pos.PosAbsolute) {
						X = nx;
					}
					if (Y == null || Y is Pos.PosAbsolute) {
						Y = ny;
					}
					//System.Diagnostics.Debug.WriteLine ($"nx:{nx},ny:{ny}");

					// FIXED: optimize, only SetNeedsDisplay on the before/after regions.
					SetNeedsDisplay ();
					return true;
				}
			}

			if (mouseEvent.Flags == MouseFlags.Button1Released && dragPosition.HasValue) {
				Application.UngrabMouse ();
				Driver.UncookMouse ();
				dragPosition = null;
			}

			//System.Diagnostics.Debug.WriteLine (mouseEvent.ToString ());
			return false;
		}

		/// <summary>
		/// Invoked by <see cref="Application.Begin"/> as part of the <see cref="Application.Run(Toplevel, Func{Exception, bool})"/> after
		/// the views have been laid out, and before the views are drawn for the first time.
		/// </summary>
		public virtual void WillPresent ()
		{
			FocusFirst ();
		}

		/// <summary>
		/// Move to the next Mdi child from the <see cref="Application.MdiTop"/>.
		/// </summary>
		public virtual void MoveNext ()
		{
			Application.MoveNext ();
		}

		/// <summary>
		/// Move to the previous Mdi child from the <see cref="Application.MdiTop"/>.
		/// </summary>
		public virtual void MovePrevious ()
		{
			Application.MovePrevious ();
		}

		/// <summary>
		/// Stops running this <see cref="Toplevel"/>.
		/// </summary>
		public virtual void RequestStop ()
		{
			if (IsMdiContainer && Running
				&& (Application.Current == this
				|| Application.Current?.Modal == false
				|| Application.Current?.Modal == true && Application.Current?.Running == false)) {

				foreach (var child in Application.MdiChildes) {
					var ev = new ToplevelClosingEventArgs (this);
					if (child.OnClosing (ev)) {
						return;
					}
					child.Running = false;
					Application.RequestStop (child);
				}
				Running = false;
				Application.RequestStop (this);
			} else if (IsMdiContainer && Running && Application.Current?.Modal == true && Application.Current?.Running == true) {
				var ev = new ToplevelClosingEventArgs (Application.Current);
				if (OnClosing (ev)) {
					return;
				}
				Application.RequestStop (Application.Current);
			} else if (!IsMdiContainer && Running && (!Modal || (Modal && Application.Current != this))) {
				var ev = new ToplevelClosingEventArgs (this);
				if (OnClosing (ev)) {
					return;
				}
				Running = false;
				Application.RequestStop (this);
			} else {
				Application.RequestStop (Application.Current);
			}
		}

		/// <summary>
		/// Stops running the <paramref name="top"/> <see cref="Toplevel"/>.
		/// </summary>
		/// <param name="top">The toplevel to request stop.</param>
		public virtual void RequestStop (Toplevel top)
		{
			top.RequestStop ();
		}

		///<inheritdoc/>
		public override void PositionCursor ()
		{
			if (!IsMdiContainer) {
				base.PositionCursor ();
				return;
			}

			if (Focused == null) {
				foreach (var top in Application.MdiChildes) {
					if (top != this && top.Visible) {
						top.SetFocus ();
						return;
					}
				}
			}
			base.PositionCursor ();
		}

		/// <summary>
		/// Gets the current visible toplevel Mdi child that match the arguments pattern.
		/// </summary>
		/// <param name="type">The type.</param>
		/// <param name="exclude">The strings to exclude.</param>
		/// <returns>The matched view.</returns>
		public View GetTopMdiChild (Type type = null, string [] exclude = null)
		{
			if (Application.MdiTop == null) {
				return null;
			}

			foreach (var top in Application.MdiChildes) {
				if (type != null && top.GetType () == type
					&& exclude?.Contains (top.Data.ToString ()) == false) {
					return top;
				} else if ((type != null && top.GetType () != type)
					|| (exclude?.Contains (top.Data.ToString ()) == true)) {
					continue;
				}
				return top;
			}
			return null;
		}

		/// <summary>
		/// Shows the Mdi child indicated by the <paramref name="top"/> setting as <see cref="Application.Current"/>.
		/// </summary>
		/// <param name="top">The toplevel.</param>
		/// <returns><see langword="true"/> if the toplevel can be showed.<see langword="false"/> otherwise.</returns>
		public virtual bool ShowChild (Toplevel top = null)
		{
			if (Application.MdiTop != null) {
				return Application.ShowChild (top == null ? this : top);
			}
			return false;
		}
	}

	/// <summary>
	/// Implements the <see cref="IEqualityComparer{T}"/> to comparing two <see cref="Toplevel"/> used by <see cref="StackExtensions"/>.
	/// </summary>
	public class ToplevelEqualityComparer : IEqualityComparer<Toplevel> {
		/// <summary>Determines whether the specified objects are equal.</summary>
		/// <param name="x">The first object of type <see cref="Toplevel" /> to compare.</param>
		/// <param name="y">The second object of type <see cref="Toplevel" /> to compare.</param>
		/// <returns>
		///     <see langword="true" /> if the specified objects are equal; otherwise, <see langword="false" />.</returns>
		public bool Equals (Toplevel x, Toplevel y)
		{
			if (y == null && x == null)
				return true;
			else if (x == null || y == null)
				return false;
			else if (x.Id == y.Id)
				return true;
			else
				return false;
		}

		/// <summary>Returns a hash code for the specified object.</summary>
		/// <param name="obj">The <see cref="Toplevel" /> for which a hash code is to be returned.</param>
		/// <returns>A hash code for the specified object.</returns>
		/// <exception cref="ArgumentNullException">The type of <paramref name="obj" /> is a reference type and <paramref name="obj" /> is <see langword="null" />.</exception>
		public int GetHashCode (Toplevel obj)
		{
			if (obj == null)
				throw new ArgumentNullException ();

			int hCode = 0;
			if (int.TryParse (obj.Id.ToString (), out int result)) {
				hCode = result;
			}
			return hCode.GetHashCode ();
		}
	}

	/// <summary>
	/// Implements the <see cref="IComparer{T}"/> to sort the <see cref="Toplevel"/> from the <see cref="Application.MdiChildes"/> if needed.
	/// </summary>
	public sealed class ToplevelComparer : IComparer<Toplevel> {
		/// <summary>Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.</summary>
		/// <param name="x">The first object to compare.</param>
		/// <param name="y">The second object to compare.</param>
		/// <returns>A signed integer that indicates the relative values of <paramref name="x" /> and <paramref name="y" />, as shown in the following table.Value Meaning Less than zero
		///             <paramref name="x" /> is less than <paramref name="y" />.Zero
		///             <paramref name="x" /> equals <paramref name="y" />.Greater than zero
		///             <paramref name="x" /> is greater than <paramref name="y" />.</returns>
		public int Compare (Toplevel x, Toplevel y)
		{
			if (ReferenceEquals (x, y))
				return 0;
			else if (x == null)
				return -1;
			else if (y == null)
				return 1;
			else
				return string.Compare (x.Id.ToString (), y.Id.ToString ());
		}
	}
	/// <summary>
	/// <see cref="EventArgs"/> implementation for the <see cref="Toplevel.Closing"/> event.
	/// </summary>
	public class ToplevelClosingEventArgs : EventArgs {
		/// <summary>
		/// The toplevel requesting stop.
		/// </summary>
		public View RequestingTop { get; }
		/// <summary>
		/// Provides an event cancellation option.
		/// </summary>
		public bool Cancel { get; set; }

		/// <summary>
		/// Initializes the event arguments with the requesting toplevel.
		/// </summary>
		/// <param name="requestingTop">The <see cref="RequestingTop"/>.</param>
		public ToplevelClosingEventArgs (Toplevel requestingTop)
		{
			RequestingTop = requestingTop;
		}
	}
}
