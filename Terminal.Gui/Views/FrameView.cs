//
// Authors:
//   Miguel de Icaza (miguel@gnome.org)
//
// NOTE: FrameView is functionally identical to Window with the following exceptions. 
//  - Is not a Toplevel
//  - Does not support mouse dragging
//  - Does not support padding (but should)
//  - Does not support IEnumerable
// Any udpates done here should probably be done in Window as well; TODO: Merge these classes

using System;
using System.Linq;
using NStack;

namespace Terminal.Gui {
	/// <summary>
	/// The FrameView is a container frame that draws a frame around the contents. It is similar to
	/// a GroupBox in Windows.
	/// </summary>
	public class FrameView : View {
		View contentView;
		ustring title;

		/// <summary>
		/// The title to be displayed for this <see cref="FrameView"/>.
		/// </summary>
		/// <value>The title.</value>
		public ustring Title {
			get => title;
			set {
				title = value;
				SetNeedsDisplay ();
			}
		}

		/// <summary>
		/// ContentView is an internal implementation detail of Window. It is used to host Views added with <see cref="Add(View)"/>. 
		/// Its ONLY reason for being is to provide a simple way for Window to expose to those SubViews that the Window's Bounds 
		/// are actually deflated due to the border. 
		/// </summary>
		class ContentView : View {
			public ContentView (Rect frame) : base (frame) { }
			public ContentView () : base () { }
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Gui.FrameView"/> class using <see cref="LayoutStyle.Absolute"/> layout.
		/// </summary>
		/// <param name="frame">Frame.</param>
		/// <param name="title">Title.</param>
		public FrameView (Rect frame, ustring title = null) : base (frame)
		{
			var cFrame = new Rect (1, 1, Math.Max (frame.Width - 2, 0), Math.Max (frame.Height - 2, 0));
			Initialize (title, cFrame);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Gui.FrameView"/> class using <see cref="LayoutStyle.Computed"/> layout.
		/// </summary>
		/// <param name="frame">Frame.</param>
		/// <param name="title">Title.</param>
		/// /// <param name="views">Views.</param>
		public FrameView (Rect frame, ustring title, View [] views) : this (frame, title)
		{
			Initialize (title, frame, views);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Gui.FrameView"/> class using <see cref="LayoutStyle.Computed"/> layout.
		/// </summary>
		/// <param name="title">Title.</param>
		public FrameView (ustring title)
		{
			Initialize (title, Rect.Empty);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Gui.FrameView"/> class using <see cref="LayoutStyle.Computed"/> layout.
		/// </summary>
		public FrameView () : this (title: string.Empty) { }

		void Initialize (ustring title, Rect frame, View [] views = null)
		{
			this.title = title;
			if (frame == Rect.Empty) {
				const int wb = 1;
				contentView = new ContentView () {
					X = wb,
					Y = wb,
					Width = Dim.Fill (wb),
					Height = Dim.Fill (wb)
				};
			} else {
				contentView = new ContentView (frame);
			}
			if (views != null) {
				foreach (var view in views) {
					contentView.Add (view);
				}
			}
			if (Subviews?.Count == 0) {
				base.Add (contentView);
				contentView.Text = base.Text;
			}
		}

		void DrawFrame ()
		{
			DrawFrame (new Rect (0, 0, Frame.Width, Frame.Height), 0, fill: true);
		}

		/// <summary>
		/// Add the specified <see cref="View"/> to this container.
		/// </summary>
		/// <param name="view"><see cref="View"/> to add to this container</param>
		public override void Add (View view)
		{
			contentView.Add (view);
			if (view.CanFocus)
				CanFocus = true;
		}


		/// <summary>
		///   Removes a <see cref="View"/> from this container.
		/// </summary>
		/// <remarks>
		/// </remarks>
		public override void Remove (View view)
		{
			if (view == null)
				return;

			SetNeedsDisplay ();
			var touched = view.Frame;
			contentView.Remove (view);

			if (contentView.InternalSubviews.Count < 1)
				this.CanFocus = false;
		}

		/// <summary>
		///   Removes all <see cref="View"/>s from this container.
		/// </summary>
		/// <remarks>
		/// </remarks>
		public override void RemoveAll ()
		{
			contentView.RemoveAll ();
		}

		///<inheritdoc/>
		public override void Redraw (Rect bounds)
		{
			var padding = 0;
			var scrRect = ViewToScreen (new Rect (0, 0, Frame.Width, Frame.Height));

			if (!NeedDisplay.IsEmpty) {
				Driver.SetAttribute (ColorScheme.Normal);
				Driver.DrawWindowFrame (scrRect, padding + 1, padding + 1, padding + 1, padding + 1, border: true, fill: true);
			}

			var savedClip = ClipToBounds ();
			contentView.Redraw (contentView.Bounds);
			Driver.Clip = savedClip;

			ClearNeedsDisplay ();
			Driver.SetAttribute (ColorScheme.Normal);
			Driver.DrawWindowFrame (scrRect, padding + 1, padding + 1, padding + 1, padding + 1, border: true, fill: false);

			if (HasFocus)
				Driver.SetAttribute (ColorScheme.HotNormal);
			Driver.DrawWindowTitle (scrRect, Title, padding, padding, padding, padding);
			Driver.SetAttribute (ColorScheme.Normal);
		}

		/// <summary>
		///   The text displayed by the <see cref="Label"/>.
		/// </summary>
		public override ustring Text {
			get => contentView.Text;
			set {
				base.Text = value;
				if (contentView != null) {
					contentView.Text = value;
				}
			}
		}

		/// <summary>
		/// Controls the text-alignment property of the label, changing it will redisplay the <see cref="Label"/>.
		/// </summary>
		/// <value>The text alignment.</value>
		public override TextAlignment TextAlignment {
			get => contentView.TextAlignment;
			set {
				base.TextAlignment = contentView.TextAlignment = value;
			}
		}

		///<inheritdoc/>
		public override bool OnEnter (View view)
		{
			if (Subviews.Count == 0 || !Subviews.Any (subview => subview.CanFocus)) {
				Application.Driver?.SetCursorVisibility (CursorVisibility.Invisible);
			}

			return base.OnEnter (view);
		}
	}
}
