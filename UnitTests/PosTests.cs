using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using Terminal.Gui;
using Xunit;

// Alais Console to MockConsole so we don't accidentally use Console
using Console = Terminal.Gui.FakeConsole;

namespace Terminal.Gui.Core {
	public class PosTests {
		[Fact]
		public void New_Works ()
		{
			var pos = new Pos ();
			Assert.Equal ("Terminal.Gui.Pos", pos.ToString ());
		}

		[Fact]
		public void AnchorEnd_SetsValue ()
		{
			var n = 0;
			var pos = Pos.AnchorEnd ();
			Assert.Equal ($"Pos.AnchorEnd(margin={n})", pos.ToString ());

			n = 5;
			pos = Pos.AnchorEnd (n);
			Assert.Equal ($"Pos.AnchorEnd(margin={n})", pos.ToString ());
		}

		[Fact]
		public void AnchorEnd_Equal ()
		{
			var n1 = 0;
			var n2 = 0;

			var pos1 = Pos.AnchorEnd (n1);
			var pos2 = Pos.AnchorEnd (n2);
			Assert.Equal (pos1, pos2);

			// Test inequality
			n2 = 5;
			pos2 = Pos.AnchorEnd (n2);
			Assert.NotEqual (pos1, pos2);
		}

		[Fact]
		public void AnchorEnd_Negative_Throws ()
		{
			Pos pos;
			var n = -1;
			Assert.Throws<ArgumentException> (() => pos = Pos.AnchorEnd (n));
		}

		[Fact]
		public void At_SetsValue ()
		{
			var pos = Pos.At (0);
			Assert.Equal ("Pos.Absolute(0)", pos.ToString ());

			pos = Pos.At (5);
			Assert.Equal ("Pos.Absolute(5)", pos.ToString ());

			pos = Pos.At (-1);
			Assert.Equal ("Pos.Absolute(-1)", pos.ToString ());
		}

		[Fact]
		public void At_Equal ()
		{
			var n1 = 0;
			var n2 = 0;

			var pos1 = Pos.At (n1);
			var pos2 = Pos.At (n2);
			Assert.Equal (pos1, pos2);
		}

		[Fact]
		public void SetSide_Null_Throws ()
		{
			var pos = Pos.Left (null);
			Assert.Throws<NullReferenceException> (() => pos.ToString ());

			pos = Pos.X (null);
			Assert.Throws<NullReferenceException> (() => pos.ToString ());

			pos = Pos.Top (null);
			Assert.Throws<NullReferenceException> (() => pos.ToString ());

			pos = Pos.Y (null);
			Assert.Throws<NullReferenceException> (() => pos.ToString ());

			pos = Pos.Bottom (null);
			Assert.Throws<NullReferenceException> (() => pos.ToString ());

			pos = Pos.Right (null);
			Assert.Throws<NullReferenceException> (() => pos.ToString ());
		}

		// TODO: Test Left, Top, Right bottom Equal

		/// <summary>
		/// Tests Pos.Left, Pos.X, Pos.Top, Pos.Y, Pos.Right, and Pos.Bottom set operations
		/// </summary>
		[Fact]
		public void PosSide_SetsValue ()
		{
			string side; // used in format string
			var testRect = Rect.Empty;
			var testInt = 0;
			Pos pos;

			// Pos.Left
			side = "x";
			testInt = 0;
			testRect = Rect.Empty;
			pos = Pos.Left (new View ());
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			pos = Pos.Left (new View (testRect));
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testRect = new Rect (1, 2, 3, 4);
			pos = Pos.Left (new View (testRect));
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			// Pos.Left(win) + 0
			pos = Pos.Left (new View (testRect)) + testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testInt = 1;
			// Pos.Left(win) +1
			pos = Pos.Left (new View (testRect)) + testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testInt = -1;
			// Pos.Left(win) -1
			pos = Pos.Left (new View (testRect)) - testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			// Pos.X
			side = "x";
			testInt = 0;
			testRect = Rect.Empty;
			pos = Pos.X (new View ());
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			pos = Pos.X (new View (testRect));
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testRect = new Rect (1, 2, 3, 4);
			pos = Pos.X (new View (testRect));
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			// Pos.X(win) + 0
			pos = Pos.X (new View (testRect)) + testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testInt = 1;
			// Pos.X(win) +1
			pos = Pos.X (new View (testRect)) + testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testInt = -1;
			// Pos.X(win) -1
			pos = Pos.X (new View (testRect)) - testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			// Pos.Top
			side = "y";
			testInt = 0;
			testRect = Rect.Empty;
			pos = Pos.Top (new View ());
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			pos = Pos.Top (new View (testRect));
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testRect = new Rect (1, 2, 3, 4);
			pos = Pos.Top (new View (testRect));
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			// Pos.Top(win) + 0
			pos = Pos.Top (new View (testRect)) + testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testInt = 1;
			// Pos.Top(win) +1
			pos = Pos.Top (new View (testRect)) + testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testInt = -1;
			// Pos.Top(win) -1
			pos = Pos.Top (new View (testRect)) - testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			// Pos.Y
			side = "y";
			testInt = 0;
			testRect = Rect.Empty;
			pos = Pos.Y (new View ());
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			pos = Pos.Y (new View (testRect));
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testRect = new Rect (1, 2, 3, 4);
			pos = Pos.Y (new View (testRect));
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			// Pos.Y(win) + 0
			pos = Pos.Y (new View (testRect)) + testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testInt = 1;
			// Pos.Y(win) +1
			pos = Pos.Y (new View (testRect)) + testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testInt = -1;
			// Pos.Y(win) -1
			pos = Pos.Y (new View (testRect)) - testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			// Pos.Bottom
			side = "bottom";
			testRect = Rect.Empty;
			testInt = 0;
			pos = Pos.Bottom (new View ());
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			pos = Pos.Bottom (new View (testRect));
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testRect = new Rect (1, 2, 3, 4);
			pos = Pos.Bottom (new View (testRect));
			Assert.Equal ($"Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}})){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			// Pos.Bottom(win) + 0
			pos = Pos.Bottom (new View (testRect)) + testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testInt = 1;
			// Pos.Bottom(win) +1
			pos = Pos.Bottom (new View (testRect)) + testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());

			testInt = -1;
			// Pos.Bottom(win) -1
			pos = Pos.Bottom (new View (testRect)) - testInt;
			Assert.Equal ($"Pos.Combine(Pos.Combine(Pos.View(side={side}, target=View()({{X={testRect.X},Y={testRect.Y},Width={testRect.Width},Height={testRect.Height}}}))+Pos.Absolute(0)){(testInt < 0 ? '-' : '+')}Pos.Absolute({testInt}))", pos.ToString ());
		}

		// See: https://github.com/migueldeicaza/gui.cs/issues/504
		[Fact]
		public void LeftTopBottomRight_Win_ShouldNotThrow ()
		{
			// Setup Fake driver
			(Window win, Button button) setup ()
			{
				Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));
				Application.Iteration = () => {
					Application.RequestStop ();
				};
				var win = new Window ("window") {
					X = 0,
					Y = 0,
					Width = Dim.Fill (),
					Height = Dim.Fill (),
				};
				Application.Top.Add (win);

				var button = new Button ("button") {
					X = Pos.Center (),
				};
				win.Add (button);

				return (win, button);
			}

			Application.RunState rs;

			void cleanup (Application.RunState rs)
			{
				// Cleanup
				Application.End (rs);
				// Shutdown must be called to safely clean up Application if Init has been called
				Application.Shutdown ();
			}

			// Test cases:
			var app = setup ();
			app.button.Y = Pos.Left (app.win);
			rs = Application.Begin (Application.Top);
			// If Application.RunState is used then we must use Application.RunLoop with the rs parameter
			Application.RunLoop (rs);
			cleanup (rs);

			app = setup ();
			app.button.Y = Pos.X (app.win);
			rs = Application.Begin (Application.Top);
			// If Application.RunState is used then we must use Application.RunLoop with the rs parameter
			Application.RunLoop (rs);
			cleanup (rs);

			app = setup ();
			app.button.Y = Pos.Top (app.win);
			rs = Application.Begin (Application.Top);
			// If Application.RunState is used then we must use Application.RunLoop with the rs parameter
			Application.RunLoop (rs);
			cleanup (rs);

			app = setup ();
			app.button.Y = Pos.Y (app.win);
			rs = Application.Begin (Application.Top);
			// If Application.RunState is used then we must use Application.RunLoop with the rs parameter
			Application.RunLoop (rs);
			cleanup (rs);

			app = setup ();
			app.button.Y = Pos.Bottom (app.win);
			rs = Application.Begin (Application.Top);
			// If Application.RunState is used then we must use Application.RunLoop with the rs parameter
			Application.RunLoop (rs);
			cleanup (rs);

			app = setup ();
			app.button.Y = Pos.Right (app.win);
			rs = Application.Begin (Application.Top);
			// If Application.RunState is used then we must use Application.RunLoop with the rs parameter
			Application.RunLoop (rs);
			cleanup (rs);
		}

		[Fact]
		public void Center_SetsValue ()
		{
			var pos = Pos.Center ();
			Assert.Equal ("Pos.Center", pos.ToString ());
		}

		[Fact]
		public void Percent_SetsValue ()
		{
			float f = 0;
			var pos = Pos.Percent (f);
			Assert.Equal ($"Pos.Factor({f / 100:0.###})", pos.ToString ());
			f = 0.5F;
			pos = Pos.Percent (f);
			Assert.Equal ($"Pos.Factor({f / 100:0.###})", pos.ToString ());
			f = 100;
			pos = Pos.Percent (f);
			Assert.Equal ($"Pos.Factor({f / 100:0.###})", pos.ToString ());
		}

		[Fact]
		public void Percent_Equal ()
		{
			var n1 = 0;
			var n2 = 0;
			var pos1 = Pos.Percent (n1);
			var pos2 = Pos.Percent (n2);
			// BUGBUG: Pos.Percent should support equality 
			Assert.NotEqual (pos1, pos2);
		}

		[Fact]
		public void Percent_ThrowsOnIvalid ()
		{
			var pos = Pos.Percent (0);
			Assert.Throws<ArgumentException> (() => pos = Pos.Percent (-1));
			Assert.Throws<ArgumentException> (() => pos = Pos.Percent (101));
			Assert.Throws<ArgumentException> (() => pos = Pos.Percent (100.0001F));
			Assert.Throws<ArgumentException> (() => pos = Pos.Percent (1000001));
		}

		[Fact]
		public void Pos_Validation_Throws_If_NewValue_Is_PosAbsolute_And_OldValue_Is_Another_Type ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var t = Application.Top;

			var w = new Window ("w") {
				X = Pos.Left (t) + 2,
				Y = Pos.At (2)
			};
			var v = new View ("v") {
				X = Pos.Center (),
				Y = Pos.Percent (10)
			};

			w.Add (v);
			t.Add (w);

			t.Ready += () => {
				Assert.Equal (2, w.X = 2);
				Assert.Equal (2, w.Y = 2);
				Assert.Throws<ArgumentException> (() => v.X = 2);
				Assert.Throws<ArgumentException> (() => v.Y = 2);
			};

			Application.Iteration += () => Application.RequestStop ();

			Application.Run ();
			Application.Shutdown ();
		}

		[Fact]
		public void Pos_Validation_Do_Not_Throws_If_NewValue_Is_PosAbsolute_And_OldValue_Is_Null ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var t = Application.Top;

			var w = new Window (new Rect (1, 2, 4, 5), "w");
			t.Add (w);

			t.Ready += () => {
				Assert.Equal (2, w.X = 2);
				Assert.Equal (2, w.Y = 2);
			};

			Application.Iteration += () => Application.RequestStop ();

			Application.Run ();
			Application.Shutdown ();

		}

		[Fact]
		public void Pos_Validation_Do_Not_Throws_If_NewValue_Is_PosAbsolute_And_OldValue_Is_Another_Type_After_Sets_To_LayoutStyle_Absolute ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var t = Application.Top;

			var w = new Window ("w") {
				X = Pos.Left (t) + 2,
				Y = Pos.At (2)
			};
			var v = new View ("v") {
				X = Pos.Center (),
				Y = Pos.Percent (10)
			};

			w.Add (v);
			t.Add (w);

			t.Ready += () => {
				v.LayoutStyle = LayoutStyle.Absolute;
				Assert.Equal (2, v.X = 2);
				Assert.Equal (2, v.Y = 2);
			};

			Application.Iteration += () => Application.RequestStop ();

			Application.Run ();
			Application.Shutdown ();
		}

		// DONE: Test PosCombine
		// DONE: Test operators
		[Fact]
		public void PosCombine_Do_Not_Throws ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var t = Application.Top;

			var w = new Window ("w") {
				X = Pos.Left (t) + 2,
				Y = Pos.Top (t) + 2
			};
			var f = new FrameView ("f");
			var v1 = new View ("v1") {
				X = Pos.Left (w) + 2,
				Y = Pos.Top (w) + 2
			};
			var v2 = new View ("v2") {
				X = Pos.Left (v1) + 2,
				Y = Pos.Top (v1) + 2
			};

			f.Add (v1, v2);
			w.Add (f);
			t.Add (w);

			f.X = Pos.X (t) + Pos.X (v2) - Pos.X (v1);
			f.Y = Pos.Y (t) + Pos.Y (v2) - Pos.Y (v1);

			t.Ready += () => {
				Assert.Equal (0, t.Frame.X);
				Assert.Equal (0, t.Frame.Y);
				Assert.Equal (2, w.Frame.X);
				Assert.Equal (2, w.Frame.Y);
				Assert.Equal (2, f.Frame.X);
				Assert.Equal (2, f.Frame.Y);
				Assert.Equal (4, v1.Frame.X);
				Assert.Equal (4, v1.Frame.Y);
				Assert.Equal (6, v2.Frame.X);
				Assert.Equal (6, v2.Frame.Y);
			};

			Application.Iteration += () => Application.RequestStop ();

			Application.Run ();
			Application.Shutdown ();
		}

		[Fact]
		public void PosCombine_Will_Throws ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var t = Application.Top;

			var w = new Window ("w") {
				X = Pos.Left (t) + 2,
				Y = Pos.Top (t) + 2
			};
			var f = new FrameView ("f");
			var v1 = new View ("v1") {
				X = Pos.Left (w) + 2,
				Y = Pos.Top (w) + 2
			};
			var v2 = new View ("v2") {
				X = Pos.Left (v1) + 2,
				Y = Pos.Top (v1) + 2
			};

			f.Add (v1); // v2 not added
			w.Add (f);
			t.Add (w);

			f.X = Pos.X (v2) - Pos.X (v1);
			f.Y = Pos.Y (v2) - Pos.Y (v1);

			Assert.Throws<InvalidOperationException> (() => Application.Run ());
			Application.Shutdown ();
		}

		[Fact]
		public void Pos_Add_Operator ()
		{

			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var top = Application.Top;

			var view = new View () { X = 0, Y = 0, Width = 20, Height = 20 };
			var field = new TextField () { X = 0, Y = 0, Width = 20 };
			var count = 0;

			field.KeyDown += (k) => {
				if (k.KeyEvent.Key == Key.Enter) {
					field.Text = $"Label {count}";
					var label = new Label (field.Text) { X = 0, Y = field.Y, Width = 20 };
					view.Add (label);
					Assert.Equal ($"Label {count}", label.Text);
					Assert.Equal ($"Pos.Absolute({count})", label.Y.ToString ());

					Assert.Equal ($"Pos.Absolute({count})", field.Y.ToString ());
					field.Y += 1;
					count++;
					Assert.Equal ($"Pos.Absolute({count})", field.Y.ToString ());
				}
			};

			Application.Iteration += () => {
				while (count < 20) {
					field.OnKeyDown (new KeyEvent (Key.Enter, new KeyModifiers ()));
				}

				Application.RequestStop ();
			};

			var win = new Window ();
			win.Add (view);
			win.Add (field);

			top.Add (win);

			Application.Run (top);

			Assert.Equal (20, count);

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}

		[Fact]
		public void Pos_Subtract_Operator ()
		{

			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			var top = Application.Top;

			var view = new View () { X = 0, Y = 0, Width = 20, Height = 20 };
			var field = new TextField () { X = 0, Y = 0, Width = 20 };
			var count = 20;
			var listLabels = new List<Label> ();

			for (int i = 0; i < count; i++) {
				field.Text = $"Label {i}";
				var label = new Label (field.Text) { X = 0, Y = field.Y, Width = 20 };
				view.Add (label);
				Assert.Equal ($"Label {i}", label.Text);
				Assert.Equal ($"Pos.Absolute({i})", field.Y.ToString ());
				listLabels.Add (label);

				Assert.Equal ($"Pos.Absolute({i})", field.Y.ToString ());
				field.Y += 1;
				Assert.Equal ($"Pos.Absolute({i + 1})", field.Y.ToString ());
			}

			field.KeyDown += (k) => {
				if (k.KeyEvent.Key == Key.Enter) {
					Assert.Equal ($"Label {count - 1}", listLabels [count - 1].Text);
					view.Remove (listLabels [count - 1]);

					Assert.Equal ($"Pos.Absolute({count})", field.Y.ToString ());
					field.Y -= 1;
					count--;
					Assert.Equal ($"Pos.Absolute({count})", field.Y.ToString ());
				}
			};

			Application.Iteration += () => {
				while (count > 0) {
					field.OnKeyDown (new KeyEvent (Key.Enter, new KeyModifiers ()));
				}

				Application.RequestStop ();
			};

			var win = new Window ();
			win.Add (view);
			win.Add (field);

			top.Add (win);

			Application.Run (top);

			Assert.Equal (0, count);

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}
	}
}
