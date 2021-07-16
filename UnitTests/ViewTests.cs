using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Terminal.Gui;
using Xunit;

// Alias Console to MockConsole so we don't accidentally use Console
using Console = Terminal.Gui.FakeConsole;

namespace Terminal.Gui.Views {
	public class ViewTests {
		[Fact]
		public void New_Initializes ()
		{
			// Parameterless
			var r = new View ();
			Assert.NotNull (r);
			Assert.Equal (LayoutStyle.Computed, r.LayoutStyle);
			Assert.Equal ("View()({X=0,Y=0,Width=0,Height=0})", r.ToString ());
			Assert.False (r.CanFocus);
			Assert.False (r.HasFocus);
			Assert.Equal (new Rect (0, 0, 0, 0), r.Bounds);
			Assert.Equal (new Rect (0, 0, 0, 0), r.Frame);
			Assert.Null (r.Focused);
			Assert.Null (r.ColorScheme);
			Assert.Equal (Dim.Sized (0), r.Width);
			Assert.Equal (Dim.Sized (0), r.Height);
			// FIXED: Pos needs equality implemented
			Assert.Equal (Pos.At (0), r.X);
			Assert.Equal (Pos.At (0), r.Y);
			Assert.False (r.IsCurrentTop);
			Assert.Empty (r.Id);
			Assert.Empty (r.Subviews);
			Assert.False (r.WantContinuousButtonPressed);
			Assert.False (r.WantMousePositionReports);
			Assert.Null (r.SuperView);
			Assert.Null (r.MostFocused);
			Assert.Equal (TextDirection.LeftRight_TopBottom, r.TextDirection);

			// Empty Rect
			r = new View (Rect.Empty);
			Assert.NotNull (r);
			Assert.Equal (LayoutStyle.Absolute, r.LayoutStyle);
			Assert.Equal ("View()({X=0,Y=0,Width=0,Height=0})", r.ToString ());
			Assert.False (r.CanFocus);
			Assert.False (r.HasFocus);
			Assert.Equal (new Rect (0, 0, 0, 0), r.Bounds);
			Assert.Equal (new Rect (0, 0, 0, 0), r.Frame);
			Assert.Null (r.Focused);
			Assert.Null (r.ColorScheme);
			Assert.NotNull (r.Width);       // All view Dim are initialized now,
			Assert.NotNull (r.Height);      // avoiding Dim errors.
			Assert.NotNull (r.X);           // All view Pos are initialized now,
			Assert.NotNull (r.Y);           // avoiding Pos errors.
			Assert.False (r.IsCurrentTop);
			Assert.Empty (r.Id);
			Assert.Empty (r.Subviews);
			Assert.False (r.WantContinuousButtonPressed);
			Assert.False (r.WantMousePositionReports);
			Assert.Null (r.SuperView);
			Assert.Null (r.MostFocused);
			Assert.Equal (TextDirection.LeftRight_TopBottom, r.TextDirection);

			// Rect with values
			r = new View (new Rect (1, 2, 3, 4));
			Assert.NotNull (r);
			Assert.Equal (LayoutStyle.Absolute, r.LayoutStyle);
			Assert.Equal ("View()({X=1,Y=2,Width=3,Height=4})", r.ToString ());
			Assert.False (r.CanFocus);
			Assert.False (r.HasFocus);
			Assert.Equal (new Rect (0, 0, 3, 4), r.Bounds);
			Assert.Equal (new Rect (1, 2, 3, 4), r.Frame);
			Assert.Null (r.Focused);
			Assert.Null (r.ColorScheme);
			Assert.NotNull (r.Width);
			Assert.NotNull (r.Height);
			Assert.NotNull (r.X);
			Assert.NotNull (r.Y);
			Assert.False (r.IsCurrentTop);
			Assert.Empty (r.Id);
			Assert.Empty (r.Subviews);
			Assert.False (r.WantContinuousButtonPressed);
			Assert.False (r.WantMousePositionReports);
			Assert.Null (r.SuperView);
			Assert.Null (r.MostFocused);
			Assert.Equal (TextDirection.LeftRight_TopBottom, r.TextDirection);

			// Initializes a view with a vertical direction
			r = new View ("Vertical View", TextDirection.TopBottom_LeftRight);
			Assert.NotNull (r);
			Assert.Equal (LayoutStyle.Computed, r.LayoutStyle);
			Assert.Equal ("View()({X=0,Y=0,Width=1,Height=13})", r.ToString ());
			Assert.False (r.CanFocus);
			Assert.False (r.HasFocus);
			Assert.Equal (new Rect (0, 0, 1, 13), r.Bounds);
			Assert.Equal (new Rect (0, 0, 1, 13), r.Frame);
			Assert.Null (r.Focused);
			Assert.Null (r.ColorScheme);
			Assert.NotNull (r.Width);       // All view Dim are initialized now,
			Assert.NotNull (r.Height);      // avoiding Dim errors.
			Assert.NotNull (r.X);           // All view Pos are initialized now,
			Assert.NotNull (r.Y);           // avoiding Pos errors.
			Assert.False (r.IsCurrentTop);
			Assert.Empty (r.Id);
			Assert.Empty (r.Subviews);
			Assert.False (r.WantContinuousButtonPressed);
			Assert.False (r.WantMousePositionReports);
			Assert.Null (r.SuperView);
			Assert.Null (r.MostFocused);
			Assert.Equal (TextDirection.TopBottom_LeftRight, r.TextDirection);

		}

		[Fact]
		public void New_Methods_Return_False ()
		{
			var r = new View ();

			Assert.False (r.ProcessKey (new KeyEvent () { Key = Key.Unknown }));
			Assert.False (r.ProcessHotKey (new KeyEvent () { Key = Key.Unknown }));
			Assert.False (r.ProcessColdKey (new KeyEvent () { Key = Key.Unknown }));
			Assert.False (r.OnKeyDown (new KeyEvent () { Key = Key.Unknown }));
			Assert.False (r.OnKeyUp (new KeyEvent () { Key = Key.Unknown }));
			Assert.False (r.MouseEvent (new MouseEvent () { Flags = MouseFlags.AllEvents }));
			Assert.False (r.OnMouseEnter (new MouseEvent () { Flags = MouseFlags.AllEvents }));
			Assert.False (r.OnMouseLeave (new MouseEvent () { Flags = MouseFlags.AllEvents }));
			Assert.False (r.OnEnter (new View ()));
			Assert.False (r.OnLeave (new View ()));

			// TODO: Add more
		}

		[Fact]
		public void TopologicalSort_Missing_Add ()
		{
			var root = new View ();
			var sub1 = new View ();
			root.Add (sub1);
			var sub2 = new View ();
			sub1.Width = Dim.Width (sub2);

			Assert.Throws<InvalidOperationException> (() => root.LayoutSubviews ());

			sub2.Width = Dim.Width (sub1);

			Assert.Throws<InvalidOperationException> (() => root.LayoutSubviews ());
		}

		[Fact]
		public void TopologicalSort_Recursive_Ref ()
		{
			var root = new View ();
			var sub1 = new View ();
			root.Add (sub1);
			var sub2 = new View ();
			root.Add (sub2);
			sub2.Width = Dim.Width (sub2);
			Assert.Throws<InvalidOperationException> (() => root.LayoutSubviews ());
		}

		[Fact]
		public void Added_Removed ()
		{
			var v = new View (new Rect (0, 0, 10, 24));
			var t = new View ();

			v.Added += (View e) => {
				Assert.True (v.SuperView == e);
			};

			v.Removed += (View e) => {
				Assert.True (v.SuperView == null);
			};

			t.Add (v);
			Assert.True (t.Subviews.Count == 1);

			t.Remove (v);
			Assert.True (t.Subviews.Count == 0);
		}

		[Fact]
		public void Subviews_TabIndexes_AreEqual ()
		{
			var r = new View ();
			var v1 = new View () { CanFocus = true };
			var v2 = new View () { CanFocus = true };
			var v3 = new View () { CanFocus = true };

			r.Add (v1, v2, v3);

			Assert.True (r.Subviews.IndexOf (v1) == 0);
			Assert.True (r.Subviews.IndexOf (v2) == 1);
			Assert.True (r.Subviews.IndexOf (v3) == 2);

			Assert.True (r.TabIndexes.IndexOf (v1) == 0);
			Assert.True (r.TabIndexes.IndexOf (v2) == 1);
			Assert.True (r.TabIndexes.IndexOf (v3) == 2);

			Assert.Equal (r.Subviews.IndexOf (v1), r.TabIndexes.IndexOf (v1));
			Assert.Equal (r.Subviews.IndexOf (v2), r.TabIndexes.IndexOf (v2));
			Assert.Equal (r.Subviews.IndexOf (v3), r.TabIndexes.IndexOf (v3));
		}

		[Fact]
		public void BringSubviewToFront_Subviews_vs_TabIndexes ()
		{
			var r = new View ();
			var v1 = new View () { CanFocus = true };
			var v2 = new View () { CanFocus = true };
			var v3 = new View () { CanFocus = true };

			r.Add (v1, v2, v3);

			r.BringSubviewToFront (v1);
			Assert.True (r.Subviews.IndexOf (v1) == 2);
			Assert.True (r.Subviews.IndexOf (v2) == 0);
			Assert.True (r.Subviews.IndexOf (v3) == 1);

			Assert.True (r.TabIndexes.IndexOf (v1) == 0);
			Assert.True (r.TabIndexes.IndexOf (v2) == 1);
			Assert.True (r.TabIndexes.IndexOf (v3) == 2);
		}

		[Fact]
		public void BringSubviewForward_Subviews_vs_TabIndexes ()
		{
			var r = new View ();
			var v1 = new View () { CanFocus = true };
			var v2 = new View () { CanFocus = true };
			var v3 = new View () { CanFocus = true };

			r.Add (v1, v2, v3);

			r.BringSubviewForward (v1);
			Assert.True (r.Subviews.IndexOf (v1) == 1);
			Assert.True (r.Subviews.IndexOf (v2) == 0);
			Assert.True (r.Subviews.IndexOf (v3) == 2);

			Assert.True (r.TabIndexes.IndexOf (v1) == 0);
			Assert.True (r.TabIndexes.IndexOf (v2) == 1);
			Assert.True (r.TabIndexes.IndexOf (v3) == 2);
		}

		[Fact]
		public void SendSubviewToBack_Subviews_vs_TabIndexes ()
		{
			var r = new View ();
			var v1 = new View () { CanFocus = true };
			var v2 = new View () { CanFocus = true };
			var v3 = new View () { CanFocus = true };

			r.Add (v1, v2, v3);

			r.SendSubviewToBack (v3);
			Assert.True (r.Subviews.IndexOf (v1) == 1);
			Assert.True (r.Subviews.IndexOf (v2) == 2);
			Assert.True (r.Subviews.IndexOf (v3) == 0);

			Assert.True (r.TabIndexes.IndexOf (v1) == 0);
			Assert.True (r.TabIndexes.IndexOf (v2) == 1);
			Assert.True (r.TabIndexes.IndexOf (v3) == 2);
		}

		[Fact]
		public void SendSubviewBackwards_Subviews_vs_TabIndexes ()
		{
			var r = new View ();
			var v1 = new View () { CanFocus = true };
			var v2 = new View () { CanFocus = true };
			var v3 = new View () { CanFocus = true };

			r.Add (v1, v2, v3);

			r.SendSubviewBackwards (v3);
			Assert.True (r.Subviews.IndexOf (v1) == 0);
			Assert.True (r.Subviews.IndexOf (v2) == 2);
			Assert.True (r.Subviews.IndexOf (v3) == 1);

			Assert.True (r.TabIndexes.IndexOf (v1) == 0);
			Assert.True (r.TabIndexes.IndexOf (v2) == 1);
			Assert.True (r.TabIndexes.IndexOf (v3) == 2);
		}

		[Fact]
		public void TabIndex_Set_CanFocus_ValidValues ()
		{
			var r = new View ();
			var v1 = new View () { CanFocus = true };
			var v2 = new View () { CanFocus = true };
			var v3 = new View () { CanFocus = true };

			r.Add (v1, v2, v3);

			v1.TabIndex = 1;
			Assert.True (r.Subviews.IndexOf (v1) == 0);
			Assert.True (r.TabIndexes.IndexOf (v1) == 1);

			v1.TabIndex = 2;
			Assert.True (r.Subviews.IndexOf (v1) == 0);
			Assert.True (r.TabIndexes.IndexOf (v1) == 2);
		}

		[Fact]
		public void TabIndex_Set_CanFocus_HigherValues ()
		{
			var r = new View ();
			var v1 = new View () { CanFocus = true };
			var v2 = new View () { CanFocus = true };
			var v3 = new View () { CanFocus = true };

			r.Add (v1, v2, v3);

			v1.TabIndex = 3;
			Assert.True (r.Subviews.IndexOf (v1) == 0);
			Assert.True (r.TabIndexes.IndexOf (v1) == 2);
		}

		[Fact]
		public void TabIndex_Set_CanFocus_LowerValues ()
		{
			var r = new View ();
			var v1 = new View () { CanFocus = true };
			var v2 = new View () { CanFocus = true };
			var v3 = new View () { CanFocus = true };

			r.Add (v1, v2, v3);

			v1.TabIndex = -1;
			Assert.True (r.Subviews.IndexOf (v1) == 0);
			Assert.True (r.TabIndexes.IndexOf (v1) == 0);
		}

		[Fact]
		public void TabIndex_Set_CanFocus_False ()
		{
			var r = new View ();
			var v1 = new View () { CanFocus = true };
			var v2 = new View () { CanFocus = true };
			var v3 = new View () { CanFocus = true };

			r.Add (v1, v2, v3);

			v1.CanFocus = false;
			v1.TabIndex = 0;
			Assert.True (r.Subviews.IndexOf (v1) == 0);
			Assert.True (r.TabIndexes.IndexOf (v1) == 0);
			Assert.Equal (-1, v1.TabIndex);
		}

		[Fact]
		public void TabIndex_Set_CanFocus_False_To_True ()
		{
			var r = new View ();
			var v1 = new View ();
			var v2 = new View () { CanFocus = true };
			var v3 = new View () { CanFocus = true };

			r.Add (v1, v2, v3);

			v1.CanFocus = true;
			v1.TabIndex = 1;
			Assert.True (r.Subviews.IndexOf (v1) == 0);
			Assert.True (r.TabIndexes.IndexOf (v1) == 1);
		}

		[Fact]
		public void TabStop_And_CanFocus_Are_All_True ()
		{
			var r = new View ();
			var v1 = new View () { CanFocus = true };
			var v2 = new View () { CanFocus = true };
			var v3 = new View () { CanFocus = true };

			r.Add (v1, v2, v3);

			r.FocusNext ();
			Assert.True (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.False (v3.HasFocus);
			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.True (v2.HasFocus);
			Assert.False (v3.HasFocus);
			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.True (v3.HasFocus);
		}

		[Fact]
		public void TabStop_Are_All_True_And_CanFocus_Are_All_False ()
		{
			var r = new View ();
			var v1 = new View ();
			var v2 = new View ();
			var v3 = new View ();

			r.Add (v1, v2, v3);

			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.False (v3.HasFocus);
			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.False (v3.HasFocus);
			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.False (v3.HasFocus);
		}

		[Fact]
		public void TabStop_Are_All_False_And_CanFocus_Are_All_True ()
		{
			var r = new View ();
			var v1 = new View () { CanFocus = true, TabStop = false };
			var v2 = new View () { CanFocus = true, TabStop = false };
			var v3 = new View () { CanFocus = true, TabStop = false };

			r.Add (v1, v2, v3);

			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.False (v3.HasFocus);
			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.False (v3.HasFocus);
			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.False (v3.HasFocus);
		}

		[Fact]
		public void TabStop_And_CanFocus_Mixed_And_BothFalse ()
		{
			var r = new View ();
			var v1 = new View () { CanFocus = true, TabStop = false };
			var v2 = new View () { CanFocus = false, TabStop = true };
			var v3 = new View () { CanFocus = false, TabStop = false };

			r.Add (v1, v2, v3);

			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.False (v3.HasFocus);
			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.False (v3.HasFocus);
			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.False (v3.HasFocus);
		}

		[Fact]
		public void TabStop_All_True_And_Changing_CanFocus_Later ()
		{
			var r = new View ();
			var v1 = new View ();
			var v2 = new View ();
			var v3 = new View ();

			r.Add (v1, v2, v3);

			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.False (v3.HasFocus);

			v1.CanFocus = true;
			r.FocusNext ();
			Assert.True (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.False (v3.HasFocus);
			v2.CanFocus = true;
			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.True (v2.HasFocus);
			Assert.False (v3.HasFocus);
			v3.CanFocus = true;
			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.True (v3.HasFocus);
		}

		[Fact]
		public void TabStop_All_False_And_All_True_And_Changing_TabStop_Later ()
		{
			var r = new View ();
			var v1 = new View () { CanFocus = true, TabStop = false };
			var v2 = new View () { CanFocus = true, TabStop = false };
			var v3 = new View () { CanFocus = true, TabStop = false };

			r.Add (v1, v2, v3);

			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.False (v3.HasFocus);

			v1.TabStop = true;
			r.FocusNext ();
			Assert.True (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.False (v3.HasFocus);
			v2.TabStop = true;
			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.True (v2.HasFocus);
			Assert.False (v3.HasFocus);
			v3.TabStop = true;
			r.FocusNext ();
			Assert.False (v1.HasFocus);
			Assert.False (v2.HasFocus);
			Assert.True (v3.HasFocus);
		}

		[Fact]
		public void CanFocus_Set_Changes_TabIndex_And_TabStop ()
		{
			var r = new View ();
			var v1 = new View ("1");
			var v2 = new View ("2");
			var v3 = new View ("3");

			r.Add (v1, v2, v3);

			v2.CanFocus = true;
			Assert.Equal (r.TabIndexes.IndexOf (v2), v2.TabIndex);
			Assert.Equal (0, v2.TabIndex);
			Assert.True (v2.TabStop);

			v1.CanFocus = true;
			Assert.Equal (r.TabIndexes.IndexOf (v1), v1.TabIndex);
			Assert.Equal (1, v1.TabIndex);
			Assert.True (v1.TabStop);

			v1.TabIndex = 2;
			Assert.Equal (r.TabIndexes.IndexOf (v1), v1.TabIndex);
			Assert.Equal (1, v1.TabIndex);
			v3.CanFocus = true;
			Assert.Equal (r.TabIndexes.IndexOf (v1), v1.TabIndex);
			Assert.Equal (1, v1.TabIndex);
			Assert.Equal (r.TabIndexes.IndexOf (v3), v3.TabIndex);
			Assert.Equal (2, v3.TabIndex);
			Assert.True (v3.TabStop);

			v2.CanFocus = false;
			Assert.Equal (r.TabIndexes.IndexOf (v1), v1.TabIndex);
			Assert.Equal (1, v1.TabIndex);
			Assert.True (v1.TabStop);
			Assert.NotEqual (r.TabIndexes.IndexOf (v2), v2.TabIndex);
			Assert.Equal (-1, v2.TabIndex);
			Assert.False (v2.TabStop);
			Assert.Equal (r.TabIndexes.IndexOf (v3), v3.TabIndex);
			Assert.Equal (2, v3.TabIndex);
			Assert.True (v3.TabStop);
		}

		[Fact]
		public void Initialized_Event_Comparing_With_Added_Event ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var t = new Toplevel () { Id = "0", };

			var w = new Window () { Id = "t", Width = Dim.Fill (), Height = Dim.Fill () };
			var v1 = new View () { Id = "v1", Width = Dim.Fill (), Height = Dim.Fill () };
			var v2 = new View () { Id = "v2", Width = Dim.Fill (), Height = Dim.Fill () };
			var sv1 = new View () { Id = "sv1", Width = Dim.Fill (), Height = Dim.Fill () };

			int tc = 0, wc = 0, v1c = 0, v2c = 0, sv1c = 0;

			w.Added += (e) => {
				Assert.Equal (e.Frame.Width, w.Frame.Width);
				Assert.Equal (e.Frame.Height, w.Frame.Height);
			};
			v1.Added += (e) => {
				Assert.Equal (e.Frame.Width, v1.Frame.Width);
				Assert.Equal (e.Frame.Height, v1.Frame.Height);
			};
			v2.Added += (e) => {
				Assert.Equal (e.Frame.Width, v2.Frame.Width);
				Assert.Equal (e.Frame.Height, v2.Frame.Height);
			};
			sv1.Added += (e) => {
				Assert.Equal (e.Frame.Width, sv1.Frame.Width);
				Assert.Equal (e.Frame.Height, sv1.Frame.Height);
			};

			t.Initialized += (s, e) => {
				tc++;
				Assert.Equal (1, tc);
				Assert.Equal (1, wc);
				Assert.Equal (1, v1c);
				Assert.Equal (1, v2c);
				Assert.Equal (1, sv1c);

				Assert.True (t.CanFocus);
				Assert.True (w.CanFocus);
				Assert.False (v1.CanFocus);
				Assert.False (v2.CanFocus);
				Assert.False (sv1.CanFocus);

				Application.Refresh ();
			};
			w.Initialized += (s, e) => {
				wc++;
				Assert.Equal (t.Frame.Width, w.Frame.Width);
				Assert.Equal (t.Frame.Height, w.Frame.Height);
			};
			v1.Initialized += (s, e) => {
				v1c++;
				Assert.Equal (t.Frame.Width, v1.Frame.Width);
				Assert.Equal (t.Frame.Height, v1.Frame.Height);
			};
			v2.Initialized += (s, e) => {
				v2c++;
				Assert.Equal (t.Frame.Width, v2.Frame.Width);
				Assert.Equal (t.Frame.Height, v2.Frame.Height);
			};
			sv1.Initialized += (s, e) => {
				sv1c++;
				Assert.Equal (t.Frame.Width, sv1.Frame.Width);
				Assert.Equal (t.Frame.Height, sv1.Frame.Height);
				Assert.False (sv1.CanFocus);
				Assert.Throws<InvalidOperationException> (() => sv1.CanFocus = true);
				Assert.False (sv1.CanFocus);
			};

			v1.Add (sv1);
			w.Add (v1, v2);
			t.Add (w);

			Application.Iteration = () => {
				Application.Refresh ();
				t.Running = false;
			};

			Application.Run (t);
			Application.Shutdown ();

			Assert.Equal (1, tc);
			Assert.Equal (1, wc);
			Assert.Equal (1, v1c);
			Assert.Equal (1, v2c);
			Assert.Equal (1, sv1c);

			Assert.True (t.CanFocus);
			Assert.True (w.CanFocus);
			Assert.False (v1.CanFocus);
			Assert.False (v2.CanFocus);
			Assert.False (sv1.CanFocus);

			v1.CanFocus = true;
			Assert.False (sv1.CanFocus); // False because sv1 was disposed and it isn't a subview of v1.
		}

		[Fact]
		public void Initialized_Event_Will_Be_Invoked_When_Added_Dynamically ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var t = new Toplevel () { Id = "0", };

			var w = new Window () { Id = "t", Width = Dim.Fill (), Height = Dim.Fill () };
			var v1 = new View () { Id = "v1", Width = Dim.Fill (), Height = Dim.Fill () };
			var v2 = new View () { Id = "v2", Width = Dim.Fill (), Height = Dim.Fill () };

			int tc = 0, wc = 0, v1c = 0, v2c = 0, sv1c = 0;

			t.Initialized += (s, e) => {
				tc++;
				Assert.Equal (1, tc);
				Assert.Equal (1, wc);
				Assert.Equal (1, v1c);
				Assert.Equal (1, v2c);
				Assert.Equal (0, sv1c); // Added after t in the Application.Iteration.

				Assert.True (t.CanFocus);
				Assert.True (w.CanFocus);
				Assert.False (v1.CanFocus);
				Assert.False (v2.CanFocus);

				Application.Refresh ();
			};
			w.Initialized += (s, e) => {
				wc++;
				Assert.Equal (t.Frame.Width, w.Frame.Width);
				Assert.Equal (t.Frame.Height, w.Frame.Height);
			};
			v1.Initialized += (s, e) => {
				v1c++;
				Assert.Equal (t.Frame.Width, v1.Frame.Width);
				Assert.Equal (t.Frame.Height, v1.Frame.Height);
			};
			v2.Initialized += (s, e) => {
				v2c++;
				Assert.Equal (t.Frame.Width, v2.Frame.Width);
				Assert.Equal (t.Frame.Height, v2.Frame.Height);
			};
			w.Add (v1, v2);
			t.Add (w);

			Application.Iteration = () => {
				var sv1 = new View () { Id = "sv1", Width = Dim.Fill (), Height = Dim.Fill () };

				sv1.Initialized += (s, e) => {
					sv1c++;
					Assert.NotEqual (t.Frame.Width, sv1.Frame.Width);
					Assert.NotEqual (t.Frame.Height, sv1.Frame.Height);
					Assert.False (sv1.CanFocus);
					Assert.Throws<InvalidOperationException> (() => sv1.CanFocus = true);
					Assert.False (sv1.CanFocus);
				};

				v1.Add (sv1);

				Application.Refresh ();
				t.Running = false;
			};

			Application.Run (t);
			Application.Shutdown ();

			Assert.Equal (1, tc);
			Assert.Equal (1, wc);
			Assert.Equal (1, v1c);
			Assert.Equal (1, v2c);
			Assert.Equal (1, sv1c);

			Assert.True (t.CanFocus);
			Assert.True (w.CanFocus);
			Assert.False (v1.CanFocus);
			Assert.False (v2.CanFocus);
		}

		[Fact]
		public void CanFocus_Faced_With_Container ()
		{
			var t = new Toplevel ();
			var w = new Window ();
			var f = new FrameView ();
			var v = new View () { CanFocus = true };
			f.Add (v);
			w.Add (f);
			t.Add (w);

			Assert.True (t.CanFocus);
			Assert.True (w.CanFocus);
			Assert.True (f.CanFocus);
			Assert.True (v.CanFocus);

			f.CanFocus = false;
			Assert.False (f.CanFocus);
			Assert.True (v.CanFocus);

			v.CanFocus = false;
			Assert.False (f.CanFocus);
			Assert.False (v.CanFocus);

			v.CanFocus = true;
			Assert.False (f.CanFocus);
			Assert.True (v.CanFocus);
		}

		[Fact]
		public void CanFocus_Faced_With_Container_Before_Run ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var t = Application.Top;

			var w = new Window ("w");
			var f = new FrameView ("f");
			var v = new View ("v") { CanFocus = true };
			f.Add (v);
			w.Add (f);
			t.Add (w);

			Assert.True (t.CanFocus);
			Assert.True (w.CanFocus);
			Assert.True (f.CanFocus);
			Assert.True (v.CanFocus);

			f.CanFocus = false;
			Assert.False (f.CanFocus);
			Assert.True (v.CanFocus);

			v.CanFocus = false;
			Assert.False (f.CanFocus);
			Assert.False (v.CanFocus);

			v.CanFocus = true;
			Assert.False (f.CanFocus);
			Assert.True (v.CanFocus);

			Application.Iteration += () => Application.RequestStop ();

			Application.Run ();
			Application.Shutdown ();
		}

		[Fact]
		public void CanFocus_Faced_With_Container_After_Run ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var t = Application.Top;

			var w = new Window ("w");
			var f = new FrameView ("f");
			var v = new View ("v") { CanFocus = true };
			f.Add (v);
			w.Add (f);
			t.Add (w);

			t.Ready += () => {
				Assert.True (t.CanFocus);
				Assert.True (w.CanFocus);
				Assert.True (f.CanFocus);
				Assert.True (v.CanFocus);

				f.CanFocus = false;
				Assert.False (f.CanFocus);
				Assert.False (v.CanFocus);

				v.CanFocus = false;
				Assert.False (f.CanFocus);
				Assert.False (v.CanFocus);

				Assert.Throws<InvalidOperationException> (() => v.CanFocus = true);
				Assert.False (f.CanFocus);
				Assert.False (v.CanFocus);

				f.CanFocus = true;
				Assert.True (f.CanFocus);
				Assert.True (v.CanFocus);
			};

			Application.Iteration += () => Application.RequestStop ();

			Application.Run ();
			Application.Shutdown ();
		}

		[Fact]
		public void CanFocus_Container_ToFalse_Turns_All_Subviews_ToFalse_Too ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var t = Application.Top;

			var w = new Window ("w");
			var f = new FrameView ("f");
			var v1 = new View ("v1") { CanFocus = true };
			var v2 = new View ("v2") { CanFocus = true };
			f.Add (v1, v2);
			w.Add (f);
			t.Add (w);

			t.Ready += () => {
				Assert.True (t.CanFocus);
				Assert.True (w.CanFocus);
				Assert.True (f.CanFocus);
				Assert.True (v1.CanFocus);
				Assert.True (v2.CanFocus);

				w.CanFocus = false;
				Assert.True (w.CanFocus);
				Assert.False (f.CanFocus);
				Assert.False (v1.CanFocus);
				Assert.False (v2.CanFocus);
			};

			Application.Iteration += () => Application.RequestStop ();

			Application.Run ();
			Application.Shutdown ();
		}

		[Fact]
		public void CanFocus_Container_Toggling_All_Subviews_To_Old_Value_When_Is_True ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var t = Application.Top;

			var w = new Window ("w");
			var f = new FrameView ("f");
			var v1 = new View ("v1");
			var v2 = new View ("v2") { CanFocus = true };
			f.Add (v1, v2);
			w.Add (f);
			t.Add (w);

			t.Ready += () => {
				Assert.True (t.CanFocus);
				Assert.True (w.CanFocus);
				Assert.True (f.CanFocus);
				Assert.False (v1.CanFocus);
				Assert.True (v2.CanFocus);

				w.CanFocus = false;
				Assert.True (w.CanFocus);
				Assert.False (f.CanFocus);
				Assert.False (v1.CanFocus);
				Assert.False (v2.CanFocus);

				w.CanFocus = true;
				Assert.True (w.CanFocus);
				Assert.True (f.CanFocus);
				Assert.False (v1.CanFocus);
				Assert.True (v2.CanFocus);
			};

			Application.Iteration += () => Application.RequestStop ();

			Application.Run ();
			Application.Shutdown ();
		}


		[Fact]
		public void Navigation_With_Null_Focused_View ()
		{
			// Non-regression test for #882 (NullReferenceException during keyboard navigation when Focused is null)

			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			Application.Top.Ready += () => {
				Assert.Null (Application.Top.Focused);
			};

			// Keyboard navigation with tab
			Console.MockKeyPresses.Push (new ConsoleKeyInfo ('\t', ConsoleKey.Tab, false, false, false));

			Application.Iteration += () => Application.RequestStop ();

			Application.Run ();
			Application.Shutdown ();
		}

		[Fact]
		public void Multi_Thread_Toplevels ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var t = Application.Top;
			var w = new Window ();
			t.Add (w);

			int count = 0, count1 = 0, count2 = 0;
			bool log = false, log1 = false, log2 = false;
			bool fromTopStillKnowFirstIsRunning = false;
			bool fromTopStillKnowSecondIsRunning = false;
			bool fromFirstStillKnowSecondIsRunning = false;

			Application.MainLoop.AddTimeout (TimeSpan.FromMilliseconds (100), (_) => {
				count++;
				if (count1 == 5) {
					log1 = true;
				}
				if (count1 == 14 && count2 == 10 && count == 15) { // count2 is already stopped
					fromTopStillKnowFirstIsRunning = true;
				}
				if (count1 == 7 && count2 == 7 && count == 8) {
					fromTopStillKnowSecondIsRunning = true;
				}
				if (count == 30) {
					Assert.Equal (30, count);
					Assert.Equal (20, count1);
					Assert.Equal (10, count2);

					Assert.True (log);
					Assert.True (log1);
					Assert.True (log2);

					Assert.True (fromTopStillKnowFirstIsRunning);
					Assert.True (fromTopStillKnowSecondIsRunning);
					Assert.True (fromFirstStillKnowSecondIsRunning);

					Application.RequestStop ();
					return false;
				}
				return true;
			});

			t.Ready += FirstDialogToplevel;

			void FirstDialogToplevel ()
			{
				var od = new OpenDialog ();
				od.Ready += SecoundDialogToplevel;

				Application.MainLoop.AddTimeout (TimeSpan.FromMilliseconds (100), (_) => {
					count1++;
					if (count2 == 5) {
						log2 = true;
					}
					if (count2 == 4 && count1 == 5 && count == 5) {
						fromFirstStillKnowSecondIsRunning = true;
					}
					if (count1 == 20) {
						Assert.Equal (20, count1);
						Application.RequestStop ();
						return false;
					}
					return true;
				});

				Application.Run (od);
			}

			void SecoundDialogToplevel ()
			{
				var d = new Dialog ();

				Application.MainLoop.AddTimeout (TimeSpan.FromMilliseconds (100), (_) => {
					count2++;
					if (count < 30) {
						log = true;
					}
					if (count2 == 10) {
						Assert.Equal (10, count2);
						Application.RequestStop ();
						return false;
					}
					return true;
				});

				Application.Run (d);
			}

			Application.Run ();
			Application.Shutdown ();
		}

		[Fact]
		public void View_With_No_Difference_Between_An_Object_Initializer_And_A_Constructor ()
		{
			// Object Initializer
			var view = new View () {
				X = 1,
				Y = 2,
				Width = 3,
				Height = 4
			};
			Assert.Equal (1, view.X);
			Assert.Equal (2, view.Y);
			Assert.Equal (3, view.Width);
			Assert.Equal (4, view.Height);
			Assert.False (view.Frame.IsEmpty);
			Assert.Equal (new Rect (1, 2, 3, 4), view.Frame);
			Assert.False (view.Bounds.IsEmpty);
			Assert.Equal (new Rect (0, 0, 3, 4), view.Bounds);

			view.LayoutSubviews ();

			Assert.Equal (1, view.X);
			Assert.Equal (2, view.Y);
			Assert.Equal (3, view.Width);
			Assert.Equal (4, view.Height);
			Assert.False (view.Frame.IsEmpty);
			Assert.False (view.Bounds.IsEmpty);

			// Default Constructor
			view = new View ();
			Assert.Equal (0, view.X);
			Assert.Equal (0, view.Y);
			Assert.Equal (0, view.Width);
			Assert.Equal (0, view.Height);
			Assert.True (view.Frame.IsEmpty);
			Assert.True (view.Bounds.IsEmpty);

			// Constructor
			view = new View (1, 2, "");
			Assert.NotNull (view.X);
			Assert.NotNull (view.Y);
			Assert.NotNull (view.Width);
			Assert.NotNull (view.Height);
			Assert.False (view.Frame.IsEmpty);
			Assert.True (view.Bounds.IsEmpty);

			// Default Constructor and post assignment equivalent to Object Initializer
			view = new View ();
			view.X = 1;
			view.Y = 2;
			view.Width = 3;
			view.Height = 4;
			Assert.Equal (1, view.X);
			Assert.Equal (2, view.Y);
			Assert.Equal (3, view.Width);
			Assert.Equal (4, view.Height);
			Assert.False (view.Frame.IsEmpty);
			Assert.Equal (new Rect (1, 2, 3, 4), view.Frame);
			Assert.False (view.Bounds.IsEmpty);
			Assert.Equal (new Rect (0, 0, 3, 4), view.Bounds);
		}

		[Fact]
		public void FocusNearestView_Ensure_Focus_Ordered ()
		{
			var top = new Toplevel ();

			var win = new Window ();
			var winSubview = new View ("WindowSubview") {
				CanFocus = true
			};
			win.Add (winSubview);
			top.Add (win);

			var frm = new FrameView ();
			var frmSubview = new View ("FrameSubview") {
				CanFocus = true
			};
			frm.Add (frmSubview);
			top.Add (frm);

			top.ProcessKey (new KeyEvent (Key.Tab, new KeyModifiers ()));
			Assert.Equal ($"WindowSubview", top.MostFocused.Text);
			top.ProcessKey (new KeyEvent (Key.Tab, new KeyModifiers ()));
			Assert.Equal ("FrameSubview", top.MostFocused.Text);
			top.ProcessKey (new KeyEvent (Key.Tab, new KeyModifiers ()));
			Assert.Equal ($"WindowSubview", top.MostFocused.Text);

			top.ProcessKey (new KeyEvent (Key.BackTab | Key.ShiftMask, new KeyModifiers ()));
			Assert.Equal ("FrameSubview", top.MostFocused.Text);
			top.ProcessKey (new KeyEvent (Key.BackTab | Key.ShiftMask, new KeyModifiers ()));
			Assert.Equal ($"WindowSubview", top.MostFocused.Text);
		}

		[Fact]
		public void KeyPress_Handled_To_True_Prevents_Changes ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			Console.MockKeyPresses.Push (new ConsoleKeyInfo ('N', ConsoleKey.N, false, false, false));

			var top = Application.Top;

			var text = new TextField ("");
			text.KeyPress += (e) => {
				e.Handled = true;
				Assert.True (e.Handled);
				Assert.Equal (Key.N, e.KeyEvent.Key);
			};
			top.Add (text);

			Application.Iteration += () => {
				Console.MockKeyPresses.Push (new ConsoleKeyInfo ('N', ConsoleKey.N, false, false, false));
				Assert.Equal ("", text.Text);

				Application.RequestStop ();
			};

			Application.Run ();

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}

		[Fact]
		public void SetWidth_CanSetWidth ()
		{
			var top = new View () {
				X = 0,
				Y = 0,
				Width = 80,
			};

			var v = new View () {
				Width = Dim.Fill ()
			};
			top.Add (v);

			Assert.False (v.SetWidth (70, out int rWidth));
			Assert.Equal (70, rWidth);

			v.Width = Dim.Fill (1);
			Assert.False (v.SetWidth (70, out rWidth));
			Assert.Equal (69, rWidth);

			v.Width = null;
			Assert.True (v.SetWidth (70, out rWidth));
			Assert.Equal (70, rWidth);

			v.IsInitialized = true;
			v.Width = Dim.Fill (1);
			Assert.Throws<ArgumentException> (() => v.Width = 75);
			v.LayoutStyle = LayoutStyle.Absolute;
			v.Width = 75;
			Assert.True (v.SetWidth (60, out rWidth));
			Assert.Equal (60, rWidth);
		}

		[Fact]
		public void SetHeight_CanSetHeight ()
		{
			var top = new View () {
				X = 0,
				Y = 0,
				Height = 20
			};

			var v = new View () {
				Height = Dim.Fill ()
			};
			top.Add (v);

			Assert.False (v.SetHeight (10, out int rHeight));
			Assert.Equal (10, rHeight);

			v.Height = Dim.Fill (1);
			Assert.False (v.SetHeight (10, out rHeight));
			Assert.Equal (9, rHeight);

			v.Height = null;
			Assert.True (v.SetHeight (10, out rHeight));
			Assert.Equal (10, rHeight);

			v.IsInitialized = true;
			v.Height = Dim.Fill (1);
			Assert.Throws<ArgumentException> (() => v.Height = 15);
			v.LayoutStyle = LayoutStyle.Absolute;
			v.Height = 15;
			Assert.True (v.SetHeight (5, out rHeight));
			Assert.Equal (5, rHeight);
		}

		[Fact]
		public void GetCurrentWidth_CanSetWidth ()
		{
			var top = new View () {
				X = 0,
				Y = 0,
				Width = 80,
			};

			var v = new View () {
				Width = Dim.Fill ()
			};
			top.Add (v);

			Assert.False (v.GetCurrentWidth (out int cWidth));
			Assert.Equal (80, cWidth);

			v.Width = Dim.Fill (1);
			Assert.False (v.GetCurrentWidth (out cWidth));
			Assert.Equal (79, cWidth);
		}

		[Fact]
		public void GetCurrentHeight_CanSetHeight ()
		{
			var top = new View () {
				X = 0,
				Y = 0,
				Height = 20
			};

			var v = new View () {
				Height = Dim.Fill ()
			};
			top.Add (v);

			Assert.False (v.GetCurrentHeight (out int cHeight));
			Assert.Equal (20, cHeight);

			v.Height = Dim.Fill (1);
			Assert.False (v.GetCurrentHeight (out cHeight));
			Assert.Equal (19, cHeight);
		}

		[Fact]
		public void AutoSize_False_ResizeView_Is_Always_False ()
		{
			var label = new Label () { AutoSize = false };

			label.Text = "New text";

			Assert.False (label.AutoSize);
			Assert.Equal ("{X=0,Y=0,Width=0,Height=0}", label.Bounds.ToString ());
		}

		[Fact]
		public void AutoSize_True_ResizeView_With_Dim_Absolute ()
		{
			var label = new Label ();

			label.Text = "New text";

			Assert.True (label.AutoSize);
			Assert.Equal ("{X=0,Y=0,Width=8,Height=1}", label.Bounds.ToString ());
		}

		[Fact]
		public void AutoSize_True_ResizeView_With_Dim_Fill ()
		{
			var win = new Window (new Rect (0, 0, 30, 80), "");
			var label = new Label () { Width = Dim.Fill (), Height = Dim.Fill () };
			win.Add (label);

			label.Text = "New text\nNew line";
			win.LayoutSubviews ();

			Assert.True (label.AutoSize);
			Assert.Equal ("{X=0,Y=0,Width=28,Height=78}", label.Bounds.ToString ());
		}

		[Fact]
		public void AutoSize_True_SetWidthHeight_With_Dim_Fill_And_Dim_Absolute ()
		{
			var win = new Window (new Rect (0, 0, 30, 80), "");
			var label = new Label () { Width = Dim.Fill () };
			win.Add (label);

			label.Text = "New text\nNew line";
			win.LayoutSubviews ();

			Assert.True (label.AutoSize);
			Assert.Equal ("{X=0,Y=0,Width=28,Height=2}", label.Bounds.ToString ());
		}

		[Theory]
		[InlineData (1)]
		[InlineData (2)]
		[InlineData (3)]
		public void LabelChangeText_RendersCorrectly_Constructors (int choice)
		{
			var driver = new FakeDriver ();
			Application.Init (driver, new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			try {
				// Create a label with a short text 
				Label lbl;
				var text = "test";

				if (choice == 1) {
					// An object initializer should call the default constructor.
					lbl = new Label { Text = text };
				} else if (choice == 2) {
					// Calling the default constructor followed by the object initializer.
					lbl = new Label () { Text = text };
				} else {
					// Calling the Text constructor.
					lbl = new Label (text);
				}
				lbl.ColorScheme = new ColorScheme ();
				lbl.Redraw (lbl.Bounds);

				// should have the initial text
				Assert.Equal ('t', driver.Contents [0, 0, 0]);
				Assert.Equal ('e', driver.Contents [0, 1, 0]);
				Assert.Equal ('s', driver.Contents [0, 2, 0]);
				Assert.Equal ('t', driver.Contents [0, 3, 0]);
				Assert.Equal (' ', driver.Contents [0, 4, 0]);
			} finally {
				Application.Shutdown ();
			}
		}
	}
}
