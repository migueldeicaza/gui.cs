using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace Terminal.Gui.Views {
	public class ScrollBarViewTests {

		// This class enables test functions annotated with the [InitShutdown] attribute
		// to have a function called before the test function is called and after.
		// 
		// This is necessary because a) Application is a singleton and Init/Shutdown must be called
		// as a pair, and b) all unit test functions should be atomic.
		[AttributeUsage (AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
		public class InitShutdown : Xunit.Sdk.BeforeAfterTestAttribute {

			public override void Before (MethodInfo methodUnderTest)
			{
				Debug.WriteLine ($"Before: {methodUnderTest.Name}");

				if (_hostView != null) {
					throw new InvalidOperationException ("After did not run.");
				}

				Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

				var top = Application.Top;

				ScrollBarViewTests._hostView = new HostView () {
					Width = Dim.Fill (),
					Height = Dim.Fill (),
					Top = 0,
					Lines = 30,
					Left = 0,
					Cols = 100
				};

				top.Add (ScrollBarViewTests._hostView);
			}

			public override void After (MethodInfo methodUnderTest)
			{
				Debug.WriteLine ($"After: {methodUnderTest.Name}");
				ScrollBarViewTests._hostView = null;
				Application.Shutdown ();
			}
		}

		public class HostView : View {
			public int Top { get; set; }
			public int Lines { get; set; }
			public int Left { get; set; }
			public int Cols { get; set; }
		}

		private static HostView _hostView;
		private ScrollBarView _scrollBar;
		private bool _added;

		private void AddHandlers ()
		{
			if (!_added) {
				_hostView.DrawContent += _hostView_DrawContent;
				_scrollBar.ChangedPosition += _scrollBar_ChangedPosition;
				_scrollBar.OtherScrollBarView.ChangedPosition += _scrollBar_OtherScrollBarView_ChangedPosition;
			}
			_added = true;
		}

		private void RemoveHandlers ()
		{
			if (_added) {
				_hostView.DrawContent -= _hostView_DrawContent;
				_scrollBar.ChangedPosition -= _scrollBar_ChangedPosition;
				_scrollBar.OtherScrollBarView.ChangedPosition -= _scrollBar_OtherScrollBarView_ChangedPosition;
			}
			_added = false;
		}

		private void _hostView_DrawContent (Rect obj)
		{
			_scrollBar.Size = _hostView.Lines;
			_scrollBar.Position = _hostView.Top;
			_scrollBar.OtherScrollBarView.Size = _hostView.Cols;
			_scrollBar.OtherScrollBarView.Position = _hostView.Left;
			_scrollBar.Refresh ();
		}

		private void _scrollBar_ChangedPosition ()
		{
			_hostView.Top = _scrollBar.Position;
			if (_hostView.Top != _scrollBar.Position) {
				_scrollBar.Position = _hostView.Top;
			}
			_hostView.SetNeedsDisplay ();
		}

		private void _scrollBar_OtherScrollBarView_ChangedPosition ()
		{
			_hostView.Left = _scrollBar.OtherScrollBarView.Position;
			if (_hostView.Left != _scrollBar.OtherScrollBarView.Position) {
				_scrollBar.OtherScrollBarView.Position = _hostView.Left;
			}
			_hostView.SetNeedsDisplay ();
		}

		[Fact]
		[InitShutdown]
		public void Hosting_A_Null_View_To_A_ScrollBarView_Throws_ArgumentNullException ()
		{
			Assert.Throws<ArgumentNullException> ("The host parameter can't be null.",
				() => new ScrollBarView (null, true));
			Assert.Throws<ArgumentNullException> ("The host parameter can't be null.",
				() => new ScrollBarView (null, false));
		}

		[Fact]
		[InitShutdown]
		public void Hosting_A_Null_SuperView_View_To_A_ScrollBarView_Throws_ArgumentNullException ()
		{
			Assert.Throws<ArgumentNullException> ("The host SuperView parameter can't be null.",
				() => new ScrollBarView (new View (), true));
			Assert.Throws<ArgumentNullException> ("The host SuperView parameter can't be null.",
				() => new ScrollBarView (new View (), false));
		}

		[Fact]
		[InitShutdown]
		public void Hosting_Two_Vertical_ScrollBarView_Throws_ArgumentException ()
		{
			var top = new Toplevel ();
			var host = new View ();
			top.Add (host);
			var v = new ScrollBarView (host, true);
			var h = new ScrollBarView (host, true);

			Assert.Throws<ArgumentException> (null, () => v.OtherScrollBarView = h);
			Assert.Throws<ArgumentException> (null, () => h.OtherScrollBarView = v);
		}

		[Fact]
		[InitShutdown]
		public void Hosting_Two_Horizontal_ScrollBarView_Throws_ArgumentException ()
		{
			var top = new Toplevel ();
			var host = new View ();
			top.Add (host);
			var v = new ScrollBarView (host, false);
			var h = new ScrollBarView (host, false);

			Assert.Throws<ArgumentException> (null, () => v.OtherScrollBarView = h);
			Assert.Throws<ArgumentException> (null, () => h.OtherScrollBarView = v);
		}

		[Fact]
		[InitShutdown]
		public void Scrolling_With_Default_Constructor_Do_Not_Scroll ()
		{
			var sbv = new ScrollBarView {
				Position = 1
			};
			Assert.NotEqual (1, sbv.Position);
			Assert.Equal (0, sbv.Position);
		}

		[Fact]
		[InitShutdown]
		public void Hosting_A_View_To_A_ScrollBarView ()
		{
			RemoveHandlers ();

			_scrollBar = new ScrollBarView (_hostView, true);

			Assert.True (_scrollBar.IsVertical);
			Assert.False (_scrollBar.OtherScrollBarView.IsVertical);

			Assert.Equal (_scrollBar.Position, _hostView.Top);
			Assert.NotEqual (_scrollBar.Size, _hostView.Lines);
			Assert.Equal (_scrollBar.OtherScrollBarView.Position, _hostView.Left);
			Assert.NotEqual (_scrollBar.OtherScrollBarView.Size, _hostView.Cols);

			AddHandlers ();
			_hostView.SuperView.LayoutSubviews ();
			_hostView.Redraw (_hostView.Bounds);

			Assert.Equal (_scrollBar.Position, _hostView.Top);
			Assert.Equal (_scrollBar.Size, _hostView.Lines + 1);
			Assert.Equal (_scrollBar.OtherScrollBarView.Position, _hostView.Left);
			Assert.Equal (_scrollBar.OtherScrollBarView.Size, _hostView.Cols + 1);
		}

		[Fact]
		[InitShutdown]
		public void ChangedPosition_Update_The_Hosted_View ()
		{
			Hosting_A_View_To_A_ScrollBarView ();

			AddHandlers ();

			_scrollBar.Position = 2;
			Assert.Equal (_scrollBar.Position, _hostView.Top);

			_scrollBar.OtherScrollBarView.Position = 5;
			Assert.Equal (_scrollBar.OtherScrollBarView.Position, _hostView.Left);
		}

		[Fact]
		[InitShutdown]
		public void ChangedPosition_Scrolling ()
		{
			Hosting_A_View_To_A_ScrollBarView ();

			AddHandlers ();

			for (int i = 0; i < _scrollBar.Size; i++) {
				_scrollBar.Position += 1;
				Assert.Equal (_scrollBar.Position, _hostView.Top);
			}
			for (int i = _scrollBar.Size - 1; i >= 0; i--) {
				_scrollBar.Position -= 1;
				Assert.Equal (_scrollBar.Position, _hostView.Top);
			}

			for (int i = 0; i < _scrollBar.OtherScrollBarView.Size; i++) {
				_scrollBar.OtherScrollBarView.Position += i;
				Assert.Equal (_scrollBar.OtherScrollBarView.Position, _hostView.Left);
			}
			for (int i = _scrollBar.OtherScrollBarView.Size - 1; i >= 0; i--) {
				_scrollBar.OtherScrollBarView.Position -= 1;
				Assert.Equal (_scrollBar.OtherScrollBarView.Position, _hostView.Left);
			}
		}

		[Fact]
		[InitShutdown]
		public void ChangedPosition_Negative_Value ()
		{
			Hosting_A_View_To_A_ScrollBarView ();

			AddHandlers ();

			_scrollBar.Position = -20;
			Assert.Equal (0, _scrollBar.Position);
			Assert.Equal (_scrollBar.Position, _hostView.Top);

			_scrollBar.OtherScrollBarView.Position = -50;
			Assert.Equal (0, _scrollBar.OtherScrollBarView.Position);
			Assert.Equal (_scrollBar.OtherScrollBarView.Position, _hostView.Left);
		}

		[Fact]
		[InitShutdown]
		public void DrawContent_Update_The_ScrollBarView_Position ()
		{
			Hosting_A_View_To_A_ScrollBarView ();

			AddHandlers ();

			_hostView.Top = 3;
			_hostView.Redraw (_hostView.Bounds);
			Assert.Equal (_scrollBar.Position, _hostView.Top);

			_hostView.Left = 6;
			_hostView.Redraw (_hostView.Bounds);
			Assert.Equal (_scrollBar.OtherScrollBarView.Position, _hostView.Left);
		}

		[Fact]
		[InitShutdown]
		public void OtherScrollBarView_Not_Null ()
		{
			Hosting_A_View_To_A_ScrollBarView ();

			AddHandlers ();

			Assert.NotNull (_scrollBar.OtherScrollBarView);
			Assert.NotEqual (_scrollBar, _scrollBar.OtherScrollBarView);
			Assert.Equal (_scrollBar.OtherScrollBarView.OtherScrollBarView, _scrollBar);
		}

		[Fact]
		[InitShutdown]
		public void ShowScrollIndicator_Check ()
		{
			Hosting_A_View_To_A_ScrollBarView ();

			AddHandlers ();

			Assert.True (_scrollBar.ShowScrollIndicator);
			Assert.True (_scrollBar.OtherScrollBarView.ShowScrollIndicator);
		}

		[Fact]
		[InitShutdown]
		public void KeepContentAlwaysInViewport_True ()
		{
			Hosting_A_View_To_A_ScrollBarView ();

			AddHandlers ();

			Assert.Equal (80, _hostView.Bounds.Width);
			Assert.Equal (25, _hostView.Bounds.Height);
			Assert.Equal (79, _scrollBar.OtherScrollBarView.Bounds.Width);
			Assert.Equal (24, _scrollBar.Bounds.Height);
			Assert.Equal (31, _scrollBar.Size);
			Assert.Equal (101, _scrollBar.OtherScrollBarView.Size);
			Assert.True (_scrollBar.ShowScrollIndicator);
			Assert.True (_scrollBar.OtherScrollBarView.ShowScrollIndicator);
			Assert.True (_scrollBar.Visible);
			Assert.True (_scrollBar.OtherScrollBarView.Visible);

			_scrollBar.Position = 50;
			Assert.Equal (_scrollBar.Position, _scrollBar.Size - _scrollBar.Bounds.Height);
			Assert.Equal (_scrollBar.Position, _hostView.Top);
			Assert.Equal (7, _scrollBar.Position);
			Assert.Equal (7, _hostView.Top);
			Assert.True (_scrollBar.ShowScrollIndicator);
			Assert.True (_scrollBar.OtherScrollBarView.ShowScrollIndicator);
			Assert.True (_scrollBar.Visible);
			Assert.True (_scrollBar.OtherScrollBarView.Visible);

			_scrollBar.OtherScrollBarView.Position = 150;
			Assert.Equal (_scrollBar.OtherScrollBarView.Position, _scrollBar.OtherScrollBarView.Size - _scrollBar.OtherScrollBarView.Bounds.Width);
			Assert.Equal (_scrollBar.OtherScrollBarView.Position, _hostView.Left);
			Assert.Equal (22, _scrollBar.OtherScrollBarView.Position);
			Assert.Equal (22, _hostView.Left);
			Assert.True (_scrollBar.ShowScrollIndicator);
			Assert.True (_scrollBar.OtherScrollBarView.ShowScrollIndicator);
			Assert.True (_scrollBar.Visible);
			Assert.True (_scrollBar.OtherScrollBarView.Visible);
		}

		[Fact]
		[InitShutdown]
		public void KeepContentAlwaysInViewport_False ()
		{
			Hosting_A_View_To_A_ScrollBarView ();

			AddHandlers ();

			_scrollBar.KeepContentAlwaysInViewport = false;
			_scrollBar.Position = 50;
			Assert.Equal (_scrollBar.Position, _scrollBar.Size - 1);
			Assert.Equal (_scrollBar.Position, _hostView.Top);
			Assert.Equal (30, _scrollBar.Position);
			Assert.Equal (30, _hostView.Top);

			_scrollBar.OtherScrollBarView.Position = 150;
			Assert.Equal (_scrollBar.OtherScrollBarView.Position, _scrollBar.OtherScrollBarView.Size - 1);
			Assert.Equal (_scrollBar.OtherScrollBarView.Position, _hostView.Left);
			Assert.Equal (100, _scrollBar.OtherScrollBarView.Position);
			Assert.Equal (100, _hostView.Left);
		}

		[Fact]
		[InitShutdown]
		public void AutoHideScrollBars_Check ()
		{
			Hosting_A_View_To_A_ScrollBarView ();

			AddHandlers ();

			_hostView.Redraw (_hostView.Bounds);
			Assert.True (_scrollBar.ShowScrollIndicator);
			Assert.True (_scrollBar.Visible);
			Assert.Equal ("Dim.Absolute(1)", _scrollBar.Width.ToString ());
			Assert.Equal (1, _scrollBar.Bounds.Width);
			Assert.Equal ("Dim.Combine(DimView(side=Height, target=HostView()({X=0,Y=0,Width=80,Height=25}))-Dim.Absolute(1))",
				_scrollBar.Height.ToString ());
			Assert.Equal (24, _scrollBar.Bounds.Height);
			Assert.True (_scrollBar.OtherScrollBarView.ShowScrollIndicator);
			Assert.True (_scrollBar.OtherScrollBarView.Visible);
			Assert.Equal ("Dim.Combine(DimView(side=Width, target=HostView()({X=0,Y=0,Width=80,Height=25}))-Dim.Absolute(1))",
				_scrollBar.OtherScrollBarView.Width.ToString ());
			Assert.Equal (79, _scrollBar.OtherScrollBarView.Bounds.Width);
			Assert.Equal ("Dim.Absolute(1)", _scrollBar.OtherScrollBarView.Height.ToString ());
			Assert.Equal (1, _scrollBar.OtherScrollBarView.Bounds.Height);

			_hostView.Lines = 10;
			_hostView.Redraw (_hostView.Bounds);
			Assert.False (_scrollBar.ShowScrollIndicator);
			Assert.False (_scrollBar.Visible);
			Assert.Equal ("Dim.Absolute(1)", _scrollBar.Width.ToString ());
			Assert.Equal (1, _scrollBar.Bounds.Width);
			Assert.Equal ("Dim.Combine(DimView(side=Height, target=HostView()({X=0,Y=0,Width=80,Height=25}))-Dim.Absolute(1))",
				_scrollBar.Height.ToString ());
			Assert.Equal (24, _scrollBar.Bounds.Height);
			Assert.True (_scrollBar.OtherScrollBarView.ShowScrollIndicator);
			Assert.True (_scrollBar.OtherScrollBarView.Visible);
			Assert.Equal ("Dim.Combine(DimView(side=Width, target=HostView()({X=0,Y=0,Width=80,Height=25}))-Dim.Absolute(0))",
				_scrollBar.OtherScrollBarView.Width.ToString ());
			Assert.Equal (80, _scrollBar.OtherScrollBarView.Bounds.Width);
			Assert.Equal ("Dim.Absolute(1)", _scrollBar.OtherScrollBarView.Height.ToString ());
			Assert.Equal (1, _scrollBar.OtherScrollBarView.Bounds.Height);

			_hostView.Cols = 60;
			_hostView.Redraw (_hostView.Bounds);
			Assert.False (_scrollBar.ShowScrollIndicator);
			Assert.False (_scrollBar.Visible);
			Assert.Equal ("Dim.Absolute(1)", _scrollBar.Width.ToString ());
			Assert.Equal (1, _scrollBar.Bounds.Width);
			Assert.Equal ("Dim.Combine(DimView(side=Height, target=HostView()({X=0,Y=0,Width=80,Height=25}))-Dim.Absolute(1))",
				_scrollBar.Height.ToString ());
			Assert.Equal (24, _scrollBar.Bounds.Height);
			Assert.False (_scrollBar.OtherScrollBarView.ShowScrollIndicator);
			Assert.False (_scrollBar.OtherScrollBarView.Visible);
			Assert.Equal ("Dim.Combine(DimView(side=Width, target=HostView()({X=0,Y=0,Width=80,Height=25}))-Dim.Absolute(0))",
				_scrollBar.OtherScrollBarView.Width.ToString ());
			Assert.Equal (80, _scrollBar.OtherScrollBarView.Bounds.Width);
			Assert.Equal ("Dim.Absolute(1)", _scrollBar.OtherScrollBarView.Height.ToString ());
			Assert.Equal (1, _scrollBar.OtherScrollBarView.Bounds.Height);

			_hostView.Lines = 40;
			_hostView.Redraw (_hostView.Bounds);
			Assert.True (_scrollBar.ShowScrollIndicator);
			Assert.True (_scrollBar.Visible);
			Assert.Equal ("Dim.Absolute(1)", _scrollBar.Width.ToString ());
			Assert.Equal (1, _scrollBar.Bounds.Width);
			Assert.Equal ("Dim.Combine(DimView(side=Height, target=HostView()({X=0,Y=0,Width=80,Height=25}))-Dim.Absolute(0))",
				_scrollBar.Height.ToString ());
			Assert.Equal (25, _scrollBar.Bounds.Height);
			Assert.False (_scrollBar.OtherScrollBarView.ShowScrollIndicator);
			Assert.False (_scrollBar.OtherScrollBarView.Visible);
			Assert.Equal ("Dim.Combine(DimView(side=Width, target=HostView()({X=0,Y=0,Width=80,Height=25}))-Dim.Absolute(0))",
				_scrollBar.OtherScrollBarView.Width.ToString ());
			Assert.Equal (80, _scrollBar.OtherScrollBarView.Bounds.Width);
			Assert.Equal ("Dim.Absolute(1)", _scrollBar.OtherScrollBarView.Height.ToString ());
			Assert.Equal (1, _scrollBar.OtherScrollBarView.Bounds.Height);

			_hostView.Cols = 120;
			_hostView.Redraw (_hostView.Bounds);
			Assert.True (_scrollBar.ShowScrollIndicator);
			Assert.True (_scrollBar.Visible);
			Assert.Equal ("Dim.Absolute(1)", _scrollBar.Width.ToString ());
			Assert.Equal (1, _scrollBar.Bounds.Width);
			Assert.Equal ("Dim.Combine(DimView(side=Height, target=HostView()({X=0,Y=0,Width=80,Height=25}))-Dim.Absolute(1))",
				_scrollBar.Height.ToString ());
			Assert.Equal (24, _scrollBar.Bounds.Height);
			Assert.True (_scrollBar.OtherScrollBarView.ShowScrollIndicator);
			Assert.True (_scrollBar.OtherScrollBarView.Visible);
			Assert.Equal ("Dim.Combine(DimView(side=Width, target=HostView()({X=0,Y=0,Width=80,Height=25}))-Dim.Absolute(1))",
				_scrollBar.OtherScrollBarView.Width.ToString ());
			Assert.Equal (79, _scrollBar.OtherScrollBarView.Bounds.Width);
			Assert.Equal ("Dim.Absolute(1)", _scrollBar.OtherScrollBarView.Height.ToString ());
			Assert.Equal (1, _scrollBar.OtherScrollBarView.Bounds.Height);
		}

		[Fact]
		public void Constructor_ShowBothScrollIndicator_False_And_IsVertical_True_Refresh_Does_Not_Throws_An_Object_Null_Exception ()
		{
			var exception = Record.Exception (() => {
				Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

				var top = Application.Top;

				var win = new Window () {
					X = 0,
					Y = 0,
					Width = Dim.Fill (),
					Height = Dim.Fill ()
				};

				List<string> source = new List<string> ();

				for (int i = 0; i < 50; i++) {
					source.Add ($"item {i}");
				}

				var listView = new ListView (source) {
					X = 0,
					Y = 0,
					Width = Dim.Fill (),
					Height = Dim.Fill ()
				};
				win.Add (listView);

				var newScrollBarView = new ScrollBarView (listView, true, false) {
					KeepContentAlwaysInViewport = true
				};
				win.Add (newScrollBarView);

				newScrollBarView.ChangedPosition += () => {
					listView.TopItem = newScrollBarView.Position;
					if (listView.TopItem != newScrollBarView.Position) {
						newScrollBarView.Position = listView.TopItem;
					}
					Assert.Equal (newScrollBarView.Position, listView.TopItem);
					listView.SetNeedsDisplay ();
				};

				listView.DrawContent += (e) => {
					newScrollBarView.Size = listView.Source.Count - 1;
					Assert.Equal (newScrollBarView.Size, listView.Source.Count);
					newScrollBarView.Position = listView.TopItem;
					Assert.Equal (newScrollBarView.Position, listView.TopItem);
					newScrollBarView.Refresh ();
				};

				top.Ready += () => {
					newScrollBarView.Position = 45;
					Assert.Equal (newScrollBarView.Position, newScrollBarView.Size - listView.TopItem + (listView.TopItem - listView.Bounds.Height));
					Assert.Equal (newScrollBarView.Position, listView.TopItem);
					Assert.Equal (27, newScrollBarView.Position);
					Assert.Equal (27, listView.TopItem);
					Application.RequestStop ();
				};

				top.Add (win);

				Application.Run ();

				Application.Shutdown ();

			});
			Assert.Null (exception);
		}

		[Fact]
		public void Constructor_ShowBothScrollIndicator_False_And_IsVertical_False_Refresh_Does_Not_Throws_An_Object_Null_Exception ()
		{
			var exception = Record.Exception (() => {
				Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

				var top = Application.Top;

				var win = new Window () {
					X = 0,
					Y = 0,
					Width = Dim.Fill (),
					Height = Dim.Fill ()
				};

				List<string> source = new List<string> ();

				for (int i = 0; i < 50; i++) {
					var text = $"item {i} - ";
					for (int j = 0; j < 160; j++) {
						var col = j.ToString ();
						text += col.Length == 1 ? col [0] : col [1];
					}
					source.Add (text);
				}

				var listView = new ListView (source) {
					X = 0,
					Y = 0,
					Width = Dim.Fill (),
					Height = Dim.Fill ()
				};
				win.Add (listView);

				var newScrollBarView = new ScrollBarView (listView, false, false) {
					KeepContentAlwaysInViewport = true
				};
				win.Add (newScrollBarView);

				newScrollBarView.ChangedPosition += () => {
					listView.LeftItem = newScrollBarView.Position;
					if (listView.LeftItem != newScrollBarView.Position) {
						newScrollBarView.Position = listView.LeftItem;
					}
					Assert.Equal (newScrollBarView.Position, listView.LeftItem);
					listView.SetNeedsDisplay ();
				};

				listView.DrawContent += (e) => {
					newScrollBarView.Size = listView.Maxlength - 1;
					Assert.Equal (newScrollBarView.Size, listView.Maxlength);
					newScrollBarView.Position = listView.LeftItem;
					Assert.Equal (newScrollBarView.Position, listView.LeftItem);
					newScrollBarView.Refresh ();
				};

				top.Ready += () => {
					newScrollBarView.Position = 100;
					Assert.Equal (newScrollBarView.Position, newScrollBarView.Size - listView.LeftItem + (listView.LeftItem - listView.Bounds.Width));
					Assert.Equal (newScrollBarView.Position, listView.LeftItem);
					Assert.Equal (92, newScrollBarView.Position);
					Assert.Equal (92, listView.LeftItem);
					Application.RequestStop ();
				};

				top.Add (win);

				Application.Run ();

				Application.Shutdown ();

			});
			Assert.Null (exception);
		}
	}
}
