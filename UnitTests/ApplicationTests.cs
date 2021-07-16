using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

// Alias Console to MockConsole so we don't accidentally use Console
using Console = Terminal.Gui.FakeConsole;

namespace Terminal.Gui.Core {
	public class ApplicationTests {
		public ApplicationTests ()
		{
#if DEBUG_IDISPOSABLE
			Responder.Instances.Clear ();
#endif
		}

		[Fact]
		public void Init_Shutdown_Cleans_Up ()
		{
			// Verify initial state is per spec
			Pre_Init_State ();

			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

			// Verify post-Init state is correct
			Post_Init_State ();

			// MockDriver is always 80x25
			Assert.Equal (80, Application.Driver.Cols);
			Assert.Equal (25, Application.Driver.Rows);

			Application.Shutdown ();

			// Verify state is back to initial
			Pre_Init_State ();

		}

		void Pre_Init_State ()
		{
			Assert.Null (Application.Driver);
			Assert.Null (Application.Top);
			Assert.Null (Application.Current);
			Assert.Throws<ArgumentNullException> (() => Application.HeightAsBuffer == true);
			Assert.False (Application.AlwaysSetPosition);
			Assert.Null (Application.MainLoop);
			Assert.Null (Application.Iteration);
			Assert.Null (Application.RootMouseEvent);
			Assert.Null (Application.Resized);
		}

		void Post_Init_State ()
		{
			Assert.NotNull (Application.Driver);
			Assert.NotNull (Application.Top);
			Assert.NotNull (Application.Current);
			Assert.False (Application.HeightAsBuffer);
			Assert.False (Application.AlwaysSetPosition);
			Assert.NotNull (Application.MainLoop);
			Assert.Null (Application.Iteration);
			Assert.Null (Application.RootMouseEvent);
			Assert.Null (Application.Resized);
		}

		[Fact]
		public void RunState_Dispose_Cleans_Up ()
		{
			var rs = new Application.RunState (null);
			Assert.NotNull (rs);

			// Should not throw because Toplevel was null
			rs.Dispose ();

			var top = new Toplevel ();
			rs = new Application.RunState (top);
			Assert.NotNull (rs);

			// Should throw because there's no stack
			Assert.Throws<InvalidOperationException> (() => rs.Dispose ());
		}

		void Init ()
		{
			Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));
			Assert.NotNull (Application.Driver);
			Assert.NotNull (Application.MainLoop);
		}

		void Shutdown ()
		{
			Application.Shutdown ();
		}

		[Fact]
		public void Begin_End_Cleana_Up ()
		{
			// Setup Mock driver
			Init ();

			// Test null Toplevel
			Assert.Throws<ArgumentNullException> (() => Application.Begin (null));

			var top = new Toplevel ();
			var rs = Application.Begin (top);
			Assert.NotNull (rs);
			Assert.Equal (top, Application.Current);
			Application.End (rs);

			Assert.Null (Application.Current);
			Assert.NotNull (Application.Top);
			Assert.NotNull (Application.MainLoop);
			Assert.NotNull (Application.Driver);

			Shutdown ();

			Assert.Null (Application.Top);
			Assert.Null (Application.MainLoop);
			Assert.Null (Application.Driver);
		}

		[Fact]
		public void RequestStop_Stops ()
		{
			// Setup Mock driver
			Init ();

			var top = new Toplevel ();
			var rs = Application.Begin (top);
			Assert.NotNull (rs);
			Assert.Equal (top, Application.Current);

			Application.Iteration = () => {
				Application.RequestStop ();
			};

			Application.Run (top);

			Application.Shutdown ();
			Assert.Null (Application.Current);
			Assert.Null (Application.Top);
			Assert.Null (Application.MainLoop);
			Assert.Null (Application.Driver);
		}

		[Fact]
		public void RunningFalse_Stops ()
		{
			// Setup Mock driver
			Init ();

			var top = new Toplevel ();
			var rs = Application.Begin (top);
			Assert.NotNull (rs);
			Assert.Equal (top, Application.Current);

			Application.Iteration = () => {
				top.Running = false;
			};

			Application.Run (top);

			Application.Shutdown ();
			Assert.Null (Application.Current);
			Assert.Null (Application.Top);
			Assert.Null (Application.MainLoop);
			Assert.Null (Application.Driver);
		}


		[Fact]
		public void KeyUp_Event ()
		{
			// Setup Mock driver
			Init ();

			// Setup some fake keypresses (This)
			var input = "Tests";

			// Put a control-q in at the end
			Console.MockKeyPresses.Push (new ConsoleKeyInfo ('q', ConsoleKey.Q, shift: false, alt: false, control: true));
			foreach (var c in input.Reverse ()) {
				if (char.IsLetter (c)) {
					Console.MockKeyPresses.Push (new ConsoleKeyInfo (c, (ConsoleKey)char.ToUpper (c), shift: char.IsUpper (c), alt: false, control: false));
				} else {
					Console.MockKeyPresses.Push (new ConsoleKeyInfo (c, (ConsoleKey)c, shift: false, alt: false, control: false));
				}
			}

			int stackSize = Console.MockKeyPresses.Count;

			int iterations = 0;
			Application.Iteration = () => {
				iterations++;
				// Stop if we run out of control...
				if (iterations > 10) {
					Application.RequestStop ();
				}
			};

			int keyUps = 0;
			var output = string.Empty;
			Application.Top.KeyUp += (View.KeyEventEventArgs args) => {
				if (args.KeyEvent.Key != (Key.CtrlMask | Key.Q)) {
					output += (char)args.KeyEvent.KeyValue;
				}
				keyUps++;
			};

			Application.Run (Application.Top);

			// Input string should match output
			Assert.Equal (input, output);

			// # of key up events should match stack size
			//Assert.Equal (stackSize, keyUps);
			// We can't use numbers variables on the left side of an Assert.Equal/NotEqual,
			// it must be literal (Linux only).
			Assert.Equal (6, keyUps);

			// # of key up events should match # of iterations
			Assert.Equal (stackSize, iterations);

			Application.Shutdown ();
			Assert.Null (Application.Current);
			Assert.Null (Application.Top);
			Assert.Null (Application.MainLoop);
			Assert.Null (Application.Driver);
		}

		[Fact]
		public void Loaded_Ready_Unlodaded_Events ()
		{
			Init ();
			var top = Application.Top;
			var count = 0;
			top.Loaded += () => count++;
			top.Ready += () => count++;
			top.Unloaded += () => count++;
			Application.Iteration = () => Application.RequestStop ();
			Application.Run ();
			Application.Shutdown ();
			Assert.Equal (3, count);
		}

		[Fact]
		public void Shutdown_Allows_Async ()
		{
			static async Task TaskWithAsyncContinuation ()
			{
				await Task.Yield ();
				await Task.Yield ();
			}

			Init ();
			Application.Shutdown ();

			var task = TaskWithAsyncContinuation ();
			Thread.Sleep (20);
			Assert.True (task.IsCompletedSuccessfully);
		}

		[Fact]
		public void Shutdown_Resets_SyncContext ()
		{
			Init ();
			Application.Shutdown ();
			Assert.Null (SynchronizationContext.Current);
		}

		[Fact]
		public void AlternateForwardKey_AlternateBackwardKey_Tests ()
		{
			Init ();

			var top = Application.Top;
			var w1 = new Window ();
			var v1 = new TextField ();
			var v2 = new TextView ();
			w1.Add (v1, v2);

			var w2 = new Window ();
			var v3 = new CheckBox ();
			var v4 = new Button ();
			w2.Add (v3, v4);

			top.Add (w1, w2);

			Application.Iteration += () => {
				Assert.True (v1.HasFocus);
				// Using default keys.
				top.ProcessKey (new KeyEvent (Key.CtrlMask | Key.Tab,
					new KeyModifiers () { Ctrl = true }));
				Assert.True (v2.HasFocus);
				top.ProcessKey (new KeyEvent (Key.CtrlMask | Key.Tab,
					new KeyModifiers () { Ctrl = true }));
				Assert.True (v3.HasFocus);
				top.ProcessKey (new KeyEvent (Key.CtrlMask | Key.Tab,
					new KeyModifiers () { Ctrl = true }));
				Assert.True (v4.HasFocus);
				top.ProcessKey (new KeyEvent (Key.CtrlMask | Key.Tab,
					new KeyModifiers () { Ctrl = true }));
				Assert.True (v1.HasFocus);

				top.ProcessKey (new KeyEvent (Key.ShiftMask | Key.CtrlMask | Key.Tab,
					new KeyModifiers () { Shift = true, Ctrl = true }));
				Assert.True (v4.HasFocus);
				top.ProcessKey (new KeyEvent (Key.ShiftMask | Key.CtrlMask | Key.Tab,
					new KeyModifiers () { Shift = true, Ctrl = true }));
				Assert.True (v3.HasFocus);
				top.ProcessKey (new KeyEvent (Key.ShiftMask | Key.CtrlMask | Key.Tab,
					new KeyModifiers () { Shift = true, Ctrl = true }));
				Assert.True (v2.HasFocus);
				top.ProcessKey (new KeyEvent (Key.ShiftMask | Key.CtrlMask | Key.Tab,
					new KeyModifiers () { Shift = true, Ctrl = true }));
				Assert.True (v1.HasFocus);

				top.ProcessKey (new KeyEvent (Key.CtrlMask | Key.PageDown,
					new KeyModifiers () { Ctrl = true }));
				Assert.True (v2.HasFocus);
				top.ProcessKey (new KeyEvent (Key.CtrlMask | Key.PageDown,
					new KeyModifiers () { Ctrl = true }));
				Assert.True (v3.HasFocus);
				top.ProcessKey (new KeyEvent (Key.CtrlMask | Key.PageDown,
					new KeyModifiers () { Ctrl = true }));
				Assert.True (v4.HasFocus);
				top.ProcessKey (new KeyEvent (Key.CtrlMask | Key.PageDown,
					new KeyModifiers () { Ctrl = true }));
				Assert.True (v1.HasFocus);

				top.ProcessKey (new KeyEvent (Key.CtrlMask | Key.PageUp,
					new KeyModifiers () { Ctrl = true }));
				Assert.True (v4.HasFocus);
				top.ProcessKey (new KeyEvent (Key.CtrlMask | Key.PageUp,
					new KeyModifiers () { Ctrl = true }));
				Assert.True (v3.HasFocus);
				top.ProcessKey (new KeyEvent (Key.CtrlMask | Key.PageUp,
					new KeyModifiers () { Ctrl = true }));
				Assert.True (v2.HasFocus);
				top.ProcessKey (new KeyEvent (Key.CtrlMask | Key.PageUp,
					new KeyModifiers () { Ctrl = true }));
				Assert.True (v1.HasFocus);

				// Using another's alternate keys.
				Application.AlternateForwardKey = Key.F7;
				Application.AlternateBackwardKey = Key.F6;

				top.ProcessKey (new KeyEvent (Key.F7, new KeyModifiers ()));
				Assert.True (v2.HasFocus);
				top.ProcessKey (new KeyEvent (Key.F7, new KeyModifiers ()));
				Assert.True (v3.HasFocus);
				top.ProcessKey (new KeyEvent (Key.F7, new KeyModifiers ()));
				Assert.True (v4.HasFocus);
				top.ProcessKey (new KeyEvent (Key.F7, new KeyModifiers ()));
				Assert.True (v1.HasFocus);

				top.ProcessKey (new KeyEvent (Key.F6, new KeyModifiers ()));
				Assert.True (v4.HasFocus);
				top.ProcessKey (new KeyEvent (Key.F6, new KeyModifiers ()));
				Assert.True (v3.HasFocus);
				top.ProcessKey (new KeyEvent (Key.F6, new KeyModifiers ()));
				Assert.True (v2.HasFocus);
				top.ProcessKey (new KeyEvent (Key.F6, new KeyModifiers ()));
				Assert.True (v1.HasFocus);

				Application.RequestStop ();
			};

			Application.Run (top);

			// Shutdown must be called to safely clean up Application if Init has been called
			Application.Shutdown ();
		}

		[Fact]
		public void Application_RequestStop_With_Params_On_A_Not_MdiContainer_Always_Use_The_Application_Current ()
		{
			Init ();

			var top1 = new Toplevel ();
			var top2 = new Toplevel ();
			var top3 = new Window ();
			var top4 = new Window ();
			var d = new Dialog ();

			// top1, top2, top3, d1 = 4
			var iterations = 4;

			top1.Ready += () => {
				Assert.Null (Application.MdiChildes);
				Application.Run (top2);
			};
			top2.Ready += () => {
				Assert.Null (Application.MdiChildes);
				Application.Run (top3);
			};
			top3.Ready += () => {
				Assert.Null (Application.MdiChildes);
				Application.Run (top4);
			};
			top4.Ready += () => {
				Assert.Null (Application.MdiChildes);
				Application.Run (d);
			};

			d.Ready += () => {
				Assert.Null (Application.MdiChildes);
				// This will close the d because on a not MdiContainer the Application.Current it always used.
				Application.RequestStop (top1);
				Assert.True (Application.Current == d);
			};

			d.Closed += (e) => Application.RequestStop (top1);

			Application.Iteration += () => {
				Assert.Null (Application.MdiChildes);
				if (iterations == 4) {
					Assert.True (Application.Current == d);
				} else if (iterations == 3) {
					Assert.True (Application.Current == top4);
				} else if (iterations == 2) {
					Assert.True (Application.Current == top3);
				} else if (iterations == 1) {
					Assert.True (Application.Current == top2);
				} else {
					Assert.True (Application.Current == top1);
				}
				Application.RequestStop (top1);
				iterations--;
			};

			Application.Run (top1);

			Assert.Null (Application.MdiChildes);

			Application.Shutdown ();
		}

		class Mdi : Toplevel {
			public Mdi ()
			{
				IsMdiContainer = true;
			}
		}

		[Fact]
		public void MdiContainer_With_Toplevel_RequestStop_Balanced ()
		{
			Init ();

			var mdi = new Mdi ();
			var c1 = new Toplevel ();
			var c2 = new Window ();
			var c3 = new Window ();
			var d = new Dialog ();

			// MdiChild = c1, c2, c3
			// d1 = 1
			var iterations = 4;

			mdi.Ready += () => {
				Assert.Empty (Application.MdiChildes);
				Application.Run (c1);
			};
			c1.Ready += () => {
				Assert.Single (Application.MdiChildes);
				Application.Run (c2);
			};
			c2.Ready += () => {
				Assert.Equal (2, Application.MdiChildes.Count);
				Application.Run (c3);
			};
			c3.Ready += () => {
				Assert.Equal (3, Application.MdiChildes.Count);
				Application.Run (d);
			};

			// More easy because the Mdi Container handles all at once
			d.Ready += () => {
				Assert.Equal (3, Application.MdiChildes.Count);
				// This will not close the MdiContainer because d is a modal toplevel and will be closed.
				mdi.RequestStop ();
			};

			// Now this will close the MdiContainer propagating through the MdiChildes.
			d.Closed += (e) => {
				mdi.RequestStop ();
			};

			Application.Iteration += () => {
				if (iterations == 4) {
					// The Dialog was not closed before and will be closed now.
					Assert.True (Application.Current == d);
					Assert.False (d.Running);
				} else {
					Assert.Equal (iterations, Application.MdiChildes.Count);
					for (int i = 0; i < iterations; i++) {
						Assert.Equal ((iterations - i + 1).ToString (), Application.MdiChildes [i].Id);
					}
				}
				iterations--;
			};

			Application.Run (mdi);

			Assert.Empty (Application.MdiChildes);

			Application.Shutdown ();
		}

		[Fact]
		public void MdiContainer_With_Application_RequestStop_MdiTop_With_Params ()
		{
			Init ();

			var mdi = new Mdi ();
			var c1 = new Toplevel ();
			var c2 = new Window ();
			var c3 = new Window ();
			var d = new Dialog ();

			// MdiChild = c1, c2, c3
			// d1 = 1
			var iterations = 4;

			mdi.Ready += () => {
				Assert.Empty (Application.MdiChildes);
				Application.Run (c1);
			};
			c1.Ready += () => {
				Assert.Single (Application.MdiChildes);
				Application.Run (c2);
			};
			c2.Ready += () => {
				Assert.Equal (2, Application.MdiChildes.Count);
				Application.Run (c3);
			};
			c3.Ready += () => {
				Assert.Equal (3, Application.MdiChildes.Count);
				Application.Run (d);
			};

			// Also easy because the Mdi Container handles all at once
			d.Ready += () => {
				Assert.Equal (3, Application.MdiChildes.Count);
				// This will not close the MdiContainer because d is a modal toplevel
				Application.RequestStop (mdi);
			};

			// Now this will close the MdiContainer propagating through the MdiChildes.
			d.Closed += (e) => Application.RequestStop (mdi);

			Application.Iteration += () => {
				if (iterations == 4) {
					// The Dialog was not closed before and will be closed now.
					Assert.True (Application.Current == d);
					Assert.False (d.Running);
				} else {
					Assert.Equal (iterations, Application.MdiChildes.Count);
					for (int i = 0; i < iterations; i++) {
						Assert.Equal ((iterations - i + 1).ToString (), Application.MdiChildes [i].Id);
					}
				}
				iterations--;
			};

			Application.Run (mdi);

			Assert.Empty (Application.MdiChildes);

			Application.Shutdown ();
		}

		[Fact]
		public void MdiContainer_With_Application_RequestStop_MdiTop_Without_Params ()
		{
			Init ();

			var mdi = new Mdi ();
			var c1 = new Toplevel ();
			var c2 = new Window ();
			var c3 = new Window ();
			var d = new Dialog ();

			// MdiChild = c1, c2, c3 = 3
			// d1 = 1
			var iterations = 4;

			mdi.Ready += () => {
				Assert.Empty (Application.MdiChildes);
				Application.Run (c1);
			};
			c1.Ready += () => {
				Assert.Single (Application.MdiChildes);
				Application.Run (c2);
			};
			c2.Ready += () => {
				Assert.Equal (2, Application.MdiChildes.Count);
				Application.Run (c3);
			};
			c3.Ready += () => {
				Assert.Equal (3, Application.MdiChildes.Count);
				Application.Run (d);
			};

			//More harder because it's sequential.
			d.Ready += () => {
				Assert.Equal (3, Application.MdiChildes.Count);
				// Close the Dialog
				Application.RequestStop ();
			};

			// Now this will close the MdiContainer propagating through the MdiChildes.
			d.Closed += (e) => Application.RequestStop (mdi);

			Application.Iteration += () => {
				if (iterations == 4) {
					// The Dialog still is the current top and we can't request stop to MdiContainer
					// because we are not using parameter calls.
					Assert.True (Application.Current == d);
					Assert.False (d.Running);
				} else {
					Assert.Equal (iterations, Application.MdiChildes.Count);
					for (int i = 0; i < iterations; i++) {
						Assert.Equal ((iterations - i + 1).ToString (), Application.MdiChildes [i].Id);
					}
				}
				iterations--;
			};

			Application.Run (mdi);

			Assert.Empty (Application.MdiChildes);

			Application.Shutdown ();
		}

		[Fact]
		public void IsMdiChild_Testing ()
		{
			Init ();

			var mdi = new Mdi ();
			var c1 = new Toplevel ();
			var c2 = new Window ();
			var c3 = new Window ();
			var d = new Dialog ();

			Application.Iteration += () => {
				Assert.False (mdi.IsMdiChild);
				Assert.True (c1.IsMdiChild);
				Assert.True (c2.IsMdiChild);
				Assert.True (c3.IsMdiChild);
				Assert.False (d.IsMdiChild);

				mdi.RequestStop ();
			};

			Application.Run (mdi);

			Application.Shutdown ();
		}

		[Fact]
		public void Modal_Toplevel_Can_Open_Another_Modal_Toplevel_But_RequestStop_To_The_Caller_Also_Sets_Current_Running_To_False_Too ()
		{
			Init ();

			var mdi = new Mdi ();
			var c1 = new Toplevel ();
			var c2 = new Window ();
			var c3 = new Window ();
			var d1 = new Dialog ();
			var d2 = new Dialog ();

			// MdiChild = c1, c2, c3 = 3
			// d1, d2 = 2
			var iterations = 5;

			mdi.Ready += () => {
				Assert.Empty (Application.MdiChildes);
				Application.Run (c1);
			};
			c1.Ready += () => {
				Assert.Single (Application.MdiChildes);
				Application.Run (c2);
			};
			c2.Ready += () => {
				Assert.Equal (2, Application.MdiChildes.Count);
				Application.Run (c3);
			};
			c3.Ready += () => {
				Assert.Equal (3, Application.MdiChildes.Count);
				Application.Run (d1);
			};
			d1.Ready += () => {
				Assert.Equal (3, Application.MdiChildes.Count);
				Application.Run (d2);
			};

			d2.Ready += () => {
				Assert.Equal (3, Application.MdiChildes.Count);
				Assert.True (Application.Current == d2);
				Assert.True (Application.Current.Running);
				// Trying to close the Dialog1
				d1.RequestStop ();
			};

			// Now this will close the MdiContainer propagating through the MdiChildes.
			d1.Closed += (e) => {
				Assert.True (Application.Current == d1);
				Assert.False (Application.Current.Running);
				mdi.RequestStop ();
			};

			Application.Iteration += () => {
				if (iterations == 5) {
					// The Dialog2 still is the current top and we can't request stop to MdiContainer
					// because Dialog2 and Dialog1 must be closed first.
					// Dialog2 will be closed in this iteration.
					Assert.True (Application.Current == d2);
					Assert.False (Application.Current.Running);
					Assert.False (d1.Running);
				} else if (iterations == 4) {
					// Dialog1 will be closed in this iteration.
					Assert.True (Application.Current == d1);
					Assert.False (Application.Current.Running);
				} else {
					Assert.Equal (iterations, Application.MdiChildes.Count);
					for (int i = 0; i < iterations; i++) {
						Assert.Equal ((iterations - i + 1).ToString (), Application.MdiChildes [i].Id);
					}
				}
				iterations--;
			};

			Application.Run (mdi);

			Assert.Empty (Application.MdiChildes);

			Application.Shutdown ();
		}

		[Fact]
		public void Modal_Toplevel_Can_Open_Another_Not_Modal_Toplevel_But_RequestStop_To_The_Caller_Also_Sets_Current_Running_To_False_Too ()
		{
			Init ();

			var mdi = new Mdi ();
			var c1 = new Toplevel ();
			var c2 = new Window ();
			var c3 = new Window ();
			var d1 = new Dialog ();
			var c4 = new Toplevel ();

			// MdiChild = c1, c2, c3, c4 = 4
			// d1 = 1
			var iterations = 5;

			mdi.Ready += () => {
				Assert.Empty (Application.MdiChildes);
				Application.Run (c1);
			};
			c1.Ready += () => {
				Assert.Single (Application.MdiChildes);
				Application.Run (c2);
			};
			c2.Ready += () => {
				Assert.Equal (2, Application.MdiChildes.Count);
				Application.Run (c3);
			};
			c3.Ready += () => {
				Assert.Equal (3, Application.MdiChildes.Count);
				Application.Run (d1);
			};
			d1.Ready += () => {
				Assert.Equal (3, Application.MdiChildes.Count);
				Application.Run (c4);
			};

			c4.Ready += () => {
				Assert.Equal (4, Application.MdiChildes.Count);
				// Trying to close the Dialog1
				d1.RequestStop ();
			};

			// Now this will close the MdiContainer propagating through the MdiChildes.
			d1.Closed += (e) => {
				mdi.RequestStop ();
			};

			Application.Iteration += () => {
				if (iterations == 5) {
					// The Dialog2 still is the current top and we can't request stop to MdiContainer
					// because Dialog2 and Dialog1 must be closed first.
					// Using request stop here will call the Dialog again without need
					Assert.True (Application.Current == d1);
					Assert.False (Application.Current.Running);
					Assert.True (c4.Running);
				} else {
					Assert.Equal (iterations, Application.MdiChildes.Count);
					for (int i = 0; i < iterations; i++) {
						Assert.Equal ((iterations - i + (iterations == 4 && i == 0 ? 2 : 1)).ToString (),
							Application.MdiChildes [i].Id);
					}
				}
				iterations--;
			};

			Application.Run (mdi);

			Assert.Empty (Application.MdiChildes);

			Application.Shutdown ();
		}

		[Fact]
		public void MoveCurrent_Returns_False_If_The_Current_And_Top_Parameter_Are_Both_With_Running_Set_To_False ()
		{
			Init ();

			var mdi = new Mdi ();
			var c1 = new Toplevel ();
			var c2 = new Window ();
			var c3 = new Window ();

			// MdiChild = c1, c2, c3
			var iterations = 3;

			mdi.Ready += () => {
				Assert.Empty (Application.MdiChildes);
				Application.Run (c1);
			};
			c1.Ready += () => {
				Assert.Single (Application.MdiChildes);
				Application.Run (c2);
			};
			c2.Ready += () => {
				Assert.Equal (2, Application.MdiChildes.Count);
				Application.Run (c3);
			};
			c3.Ready += () => {
				Assert.Equal (3, Application.MdiChildes.Count);
				c3.RequestStop ();
				c1.RequestStop ();
			};
			// Now this will close the MdiContainer propagating through the MdiChildes.
			c1.Closed += (e) => {
				mdi.RequestStop ();
			};
			Application.Iteration += () => {
				if (iterations == 3) {
					// The Current still is c3 because Current.Running is false.
					Assert.True (Application.Current == c3);
					Assert.False (Application.Current.Running);
					// But the childes order were reorder by Running = false
					Assert.True (Application.MdiChildes [0] == c3);
					Assert.True (Application.MdiChildes [1] == c1);
					Assert.True (Application.MdiChildes [^1] == c2);
				} else if (iterations == 2) {
					// The Current is c1 and Current.Running is false.
					Assert.True (Application.Current == c1);
					Assert.False (Application.Current.Running);
					Assert.True (Application.MdiChildes [0] == c1);
					Assert.True (Application.MdiChildes [^1] == c2);
				} else if (iterations == 1) {
					// The Current is c2 and Current.Running is false.
					Assert.True (Application.Current == c2);
					Assert.False (Application.Current.Running);
					Assert.True (Application.MdiChildes [^1] == c2);
				} else {
					// The Current is mdi.
					Assert.True (Application.Current == mdi);
					Assert.Empty (Application.MdiChildes);
				}
				iterations--;
			};

			Application.Run (mdi);

			Assert.Empty (Application.MdiChildes);

			Application.Shutdown ();
		}

		[Fact]
		public void MdiContainer_Throws_If_More_Than_One ()
		{
			Init ();

			var mdi = new Mdi ();
			var mdi2 = new Mdi ();

			mdi.Ready += () => {
				Assert.Throws<InvalidOperationException> (() => Application.Run (mdi2));
				mdi.RequestStop ();
			};

			Application.Run (mdi);

			Application.Shutdown ();
		}

		[Fact]
		public void MdiContainer_Open_And_Close_Modal_And_Open_Not_Modal_Toplevels_Randomly ()
		{
			Init ();

			var mdi = new Mdi ();
			var logger = new Toplevel ();

			var iterations = 1; // The logger
			var running = true;
			var stageCompleted = true;
			var allStageClosed = false;
			var mdiRequestStop = false;

			mdi.Ready += () => {
				Assert.Empty (Application.MdiChildes);
				Application.Run (logger);
			};

			logger.Ready += () => Assert.Single (Application.MdiChildes);

			Application.Iteration += () => {
				if (stageCompleted && running) {
					stageCompleted = false;
					var stage = new Window () { Modal = true };

					stage.Ready += () => {
						Assert.Equal (iterations, Application.MdiChildes.Count);
						stage.RequestStop ();
					};

					stage.Closed += (_) => {
						if (iterations == 11) {
							allStageClosed = true;
						}
						Assert.Equal (iterations, Application.MdiChildes.Count);
						if (running) {
							stageCompleted = true;

							var rpt = new Window ();

							rpt.Ready += () => {
								iterations++;
								Assert.Equal (iterations, Application.MdiChildes.Count);
							};

							Application.Run (rpt);
						}
					};

					Application.Run (stage);

				} else if (iterations == 11 && running) {
					running = false;
					Assert.Equal (iterations, Application.MdiChildes.Count);

				} else if (!mdiRequestStop && running && !allStageClosed) {
					Assert.Equal (iterations, Application.MdiChildes.Count);

				} else if (!mdiRequestStop && !running && allStageClosed) {
					Assert.Equal (iterations, Application.MdiChildes.Count);
					mdiRequestStop = true;
					mdi.RequestStop ();
				} else {
					Assert.Empty (Application.MdiChildes);
				}
			};

			Application.Run (mdi);

			Assert.Empty (Application.MdiChildes);

			Application.Shutdown ();
		}

		[Fact]
		public void AllChildClosed_Event_Test ()
		{
			Init ();

			var mdi = new Mdi ();
			var c1 = new Toplevel ();
			var c2 = new Window ();
			var c3 = new Window ();

			// MdiChild = c1, c2, c3
			var iterations = 3;

			mdi.Ready += () => {
				Assert.Empty (Application.MdiChildes);
				Application.Run (c1);
			};
			c1.Ready += () => {
				Assert.Single (Application.MdiChildes);
				Application.Run (c2);
			};
			c2.Ready += () => {
				Assert.Equal (2, Application.MdiChildes.Count);
				Application.Run (c3);
			};
			c3.Ready += () => {
				Assert.Equal (3, Application.MdiChildes.Count);
				c3.RequestStop ();
				c2.RequestStop ();
				c1.RequestStop ();
			};
			// Now this will close the MdiContainer when all MdiChildes was closed
			mdi.AllChildClosed += () => {
				mdi.RequestStop ();
			};
			Application.Iteration += () => {
				if (iterations == 3) {
					// The Current still is c3 because Current.Running is false.
					Assert.True (Application.Current == c3);
					Assert.False (Application.Current.Running);
					// But the childes order were reorder by Running = false
					Assert.True (Application.MdiChildes [0] == c3);
					Assert.True (Application.MdiChildes [1] == c2);
					Assert.True (Application.MdiChildes [^1] == c1);
				} else if (iterations == 2) {
					// The Current is c2 and Current.Running is false.
					Assert.True (Application.Current == c2);
					Assert.False (Application.Current.Running);
					Assert.True (Application.MdiChildes [0] == c2);
					Assert.True (Application.MdiChildes [^1] == c1);
				} else if (iterations == 1) {
					// The Current is c1 and Current.Running is false.
					Assert.True (Application.Current == c1);
					Assert.False (Application.Current.Running);
					Assert.True (Application.MdiChildes [^1] == c1);
				} else {
					// The Current is mdi.
					Assert.True (Application.Current == mdi);
					Assert.False (Application.Current.Running);
					Assert.Empty (Application.MdiChildes);
				}
				iterations--;
			};

			Application.Run (mdi);

			Assert.Empty (Application.MdiChildes);

			Application.Shutdown ();
		}

		[Fact]
		public void SetCurrentAsTop_Run_A_Not_Modal_Toplevel_Make_It_The_Current_Application_Top ()
		{
			Init ();

			var t1 = new Toplevel ();
			var t2 = new Toplevel ();
			var t3 = new Toplevel ();
			var d = new Dialog ();
			var t4 = new Toplevel ();

			// t1, t2, t3, d, t4
			var iterations = 5;

			t1.Ready += () => {
				Assert.Equal (t1, Application.Top);
				Application.Run (t2);
			};
			t2.Ready += () => {
				Assert.Equal (t2, Application.Top);
				Application.Run (t3);
			};
			t3.Ready += () => {
				Assert.Equal (t3, Application.Top);
				Application.Run (d);
			};
			d.Ready += () => {
				Assert.Equal (t3, Application.Top);
				Application.Run (t4);
			};
			t4.Ready += () => {
				Assert.Equal (t4, Application.Top);
				t4.RequestStop ();
				d.RequestStop ();
				t3.RequestStop ();
				t2.RequestStop ();
			};
			// Now this will close the MdiContainer when all MdiChildes was closed
			t2.Closed += (_) => {
				t1.RequestStop ();
			};
			Application.Iteration += () => {
				if (iterations == 5) {
					// The Current still is t4 because Current.Running is false.
					Assert.Equal (t4, Application.Current);
					Assert.False (Application.Current.Running);
					Assert.Equal (t4, Application.Top);
				} else if (iterations == 4) {
					// The Current is d and Current.Running is false.
					Assert.Equal (d, Application.Current);
					Assert.False (Application.Current.Running);
					Assert.Equal (t4, Application.Top);
				} else if (iterations == 3) {
					// The Current is t3 and Current.Running is false.
					Assert.Equal (t3, Application.Current);
					Assert.False (Application.Current.Running);
					Assert.Equal (t3, Application.Top);
				} else if (iterations == 2) {
					// The Current is t2 and Current.Running is false.
					Assert.Equal (t2, Application.Current);
					Assert.False (Application.Current.Running);
					Assert.Equal (t2, Application.Top);
				} else {
					// The Current is t1.
					Assert.Equal (t1, Application.Current);
					Assert.False (Application.Current.Running);
					Assert.Equal (t1, Application.Top);
				}
				iterations--;
			};

			Application.Run (t1);

			Assert.Equal (t1, Application.Top);

			Application.Shutdown ();

			Assert.Null (Application.Top);
		}
	}
}
