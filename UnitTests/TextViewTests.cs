using System;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Terminal.Gui.Views {
	public class TextViewTests {
		private static TextView _textView;
		readonly ITestOutputHelper output;

		public TextViewTests (ITestOutputHelper output)
		{
			this.output = output;
		}

		// This class enables test functions annotated with the [InitShutdown] attribute
		// to have a function called before the test function is called and after.
		// 
		// This is necessary because a) Application is a singleton and Init/Shutdown must be called
		// as a pair, and b) all unit test functions should be atomic.
		[AttributeUsage (AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
		public class InitShutdown : Xunit.Sdk.BeforeAfterTestAttribute {

			public override void Before (MethodInfo methodUnderTest)
			{
				if (_textView != null) {
					throw new InvalidOperationException ("After did not run.");
				}

				Application.Init (new FakeDriver (), new FakeMainLoop (() => FakeConsole.ReadKey (true)));

				//                   1         2         3 
				//         01234567890123456789012345678901=32 (Length)
				var txt = "TAB to jump between text fields.";
				var buff = new byte [txt.Length];
				for (int i = 0; i < txt.Length; i++) {
					buff [i] = (byte)txt [i];
				}
				var ms = new System.IO.MemoryStream (buff).ToArray ();
				_textView = new TextView () { Width = 30, Height = 10 };
				_textView.Text = ms;
			}

			public override void After (MethodInfo methodUnderTest)
			{
				_textView = null;
				Application.Shutdown ();
			}
		}

		[Fact]
		[InitShutdown]
		public void Changing_Selection_Or_CursorPosition_Update_SelectedLength_And_SelectedText ()
		{
			_textView.SelectionStartColumn = 2;
			_textView.SelectionStartRow = 0;
			Assert.Equal (0, _textView.CursorPosition.X);
			Assert.Equal (0, _textView.CursorPosition.Y);
			Assert.Equal (2, _textView.SelectedLength);
			Assert.Equal ("TA", _textView.SelectedText);
			_textView.CursorPosition = new Point (20, 0);
			Assert.Equal (2, _textView.SelectionStartColumn);
			Assert.Equal (0, _textView.SelectionStartRow);
			Assert.Equal (18, _textView.SelectedLength);
			Assert.Equal ("B to jump between ", _textView.SelectedText);
		}

		[Fact]
		[InitShutdown]
		public void Selection_With_Value_Less_Than_Zero_Changes_To_Zero ()
		{
			_textView.SelectionStartColumn = -2;
			_textView.SelectionStartRow = -2;
			Assert.Equal (0, _textView.SelectionStartColumn);
			Assert.Equal (0, _textView.SelectionStartRow);
			Assert.Equal (0, _textView.SelectedLength);
			Assert.Equal ("", _textView.SelectedText);
		}

		[Fact]
		[InitShutdown]
		public void Selection_With_Value_Greater_Than_Text_Length_Changes_To_Text_Length ()
		{
			_textView.CursorPosition = new Point (2, 0);
			_textView.SelectionStartColumn = 33;
			_textView.SelectionStartRow = 1;
			Assert.Equal (32, _textView.SelectionStartColumn);
			Assert.Equal (0, _textView.SelectionStartRow);
			Assert.Equal (30, _textView.SelectedLength);
			Assert.Equal ("B to jump between text fields.", _textView.SelectedText);
		}

		[Fact]
		[InitShutdown]
		public void Selection_With_Empty_Text ()
		{
			_textView = new TextView ();
			_textView.CursorPosition = new Point (2, 0);
			_textView.SelectionStartColumn = 33;
			_textView.SelectionStartRow = 1;
			Assert.Equal (0, _textView.SelectionStartColumn);
			Assert.Equal (0, _textView.SelectionStartRow);
			Assert.Equal (0, _textView.SelectedLength);
			Assert.Equal ("", _textView.SelectedText);
		}

		[Fact]
		[InitShutdown]
		public void Selection_And_CursorPosition_With_Value_Greater_Than_Text_Length_Changes_Both_To_Text_Length ()
		{
			_textView.CursorPosition = new Point (33, 2);
			_textView.SelectionStartColumn = 33;
			_textView.SelectionStartRow = 33;
			Assert.Equal (32, _textView.CursorPosition.X);
			Assert.Equal (0, _textView.CursorPosition.Y);
			Assert.Equal (32, _textView.SelectionStartColumn);
			Assert.Equal (0, _textView.SelectionStartRow);
			Assert.Equal (0, _textView.SelectedLength);
			Assert.Equal ("", _textView.SelectedText);
		}

		[Fact]
		[InitShutdown]
		public void CursorPosition_With_Value_Less_Than_Zero_Changes_To_Zero ()
		{
			_textView.CursorPosition = new Point (-1, -1);
			Assert.Equal (0, _textView.CursorPosition.X);
			Assert.Equal (0, _textView.CursorPosition.Y);
			Assert.Equal (0, _textView.SelectedLength);
			Assert.Equal ("", _textView.SelectedText);
		}

		[Fact]
		[InitShutdown]
		public void CursorPosition_With_Value_Greater_Than_Text_Length_Changes_To_Text_Length ()
		{
			_textView.CursorPosition = new Point (33, 1);
			Assert.Equal (32, _textView.CursorPosition.X);
			Assert.Equal (0, _textView.CursorPosition.Y);
			Assert.Equal (0, _textView.SelectedLength);
			Assert.Equal ("", _textView.SelectedText);
		}

		[Fact]
		[InitShutdown]
		public void WordForward_With_No_Selection ()
		{
			_textView.CursorPosition = new Point (0, 0);
			var iteration = 0;

			while (_textView.CursorPosition.X < _textView.Text.Length) {
				_textView.ProcessKey (new KeyEvent (Key.CursorRight | Key.CtrlMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (4, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 1:
					Assert.Equal (7, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 2:
					Assert.Equal (12, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 3:
					Assert.Equal (20, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 4:
					Assert.Equal (25, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 5:
					Assert.Equal (32, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void WordBackward_With_No_Selection ()
		{
			_textView.CursorPosition = new Point (_textView.Text.Length, 0);
			var iteration = 0;

			while (_textView.CursorPosition.X > 0) {
				_textView.ProcessKey (new KeyEvent (Key.CursorLeft | Key.CtrlMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (25, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 1:
					Assert.Equal (20, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 2:
					Assert.Equal (12, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 3:
					Assert.Equal (7, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 4:
					Assert.Equal (4, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 5:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void WordForward_With_Selection ()
		{
			_textView.CursorPosition = new Point (0, 0);
			_textView.SelectionStartColumn = 0;
			_textView.SelectionStartRow = 0;
			var iteration = 0;

			while (_textView.CursorPosition.X < _textView.Text.Length) {
				_textView.ProcessKey (new KeyEvent (Key.CursorRight | Key.CtrlMask | Key.ShiftMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (4, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (4, _textView.SelectedLength);
					Assert.Equal ("TAB ", _textView.SelectedText);
					break;
				case 1:
					Assert.Equal (7, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (7, _textView.SelectedLength);
					Assert.Equal ("TAB to ", _textView.SelectedText);
					break;
				case 2:
					Assert.Equal (12, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (12, _textView.SelectedLength);
					Assert.Equal ("TAB to jump ", _textView.SelectedText);
					break;
				case 3:
					Assert.Equal (20, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (20, _textView.SelectedLength);
					Assert.Equal ("TAB to jump between ", _textView.SelectedText);
					break;
				case 4:
					Assert.Equal (25, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (25, _textView.SelectedLength);
					Assert.Equal ("TAB to jump between text ", _textView.SelectedText);
					break;
				case 5:
					Assert.Equal (32, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (32, _textView.SelectedLength);
					Assert.Equal ("TAB to jump between text fields.", _textView.SelectedText);
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void WordBackward_With_Selection ()
		{
			_textView.CursorPosition = new Point (_textView.Text.Length, 0);
			_textView.SelectionStartColumn = _textView.Text.Length;
			_textView.SelectionStartRow = 0;
			var iteration = 0;

			while (_textView.CursorPosition.X > 0) {
				_textView.ProcessKey (new KeyEvent (Key.CursorLeft | Key.CtrlMask | Key.ShiftMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (25, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (32, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (7, _textView.SelectedLength);
					Assert.Equal ("fields.", _textView.SelectedText);
					break;
				case 1:
					Assert.Equal (20, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (32, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (12, _textView.SelectedLength);
					Assert.Equal ("text fields.", _textView.SelectedText);
					break;
				case 2:
					Assert.Equal (12, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (32, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (20, _textView.SelectedLength);
					Assert.Equal ("between text fields.", _textView.SelectedText);
					break;
				case 3:
					Assert.Equal (7, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (32, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (25, _textView.SelectedLength);
					Assert.Equal ("jump between text fields.", _textView.SelectedText);
					break;
				case 4:
					Assert.Equal (4, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (32, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (28, _textView.SelectedLength);
					Assert.Equal ("to jump between text fields.", _textView.SelectedText);
					break;
				case 5:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (32, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (32, _textView.SelectedLength);
					Assert.Equal ("TAB to jump between text fields.", _textView.SelectedText);
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void WordForward_With_The_Same_Values_For_SelectedStart_And_CursorPosition_And_Not_Starting_At_Beginning_Of_The_Text ()
		{
			_textView.CursorPosition = new Point (10, 0);
			_textView.SelectionStartColumn = 10;
			_textView.SelectionStartRow = 0;
			var iteration = 0;

			while (_textView.CursorPosition.X < _textView.Text.Length) {
				_textView.ProcessKey (new KeyEvent (Key.CursorRight | Key.CtrlMask | Key.ShiftMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (12, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (10, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (2, _textView.SelectedLength);
					Assert.Equal ("p ", _textView.SelectedText);
					break;
				case 1:
					Assert.Equal (20, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (10, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (10, _textView.SelectedLength);
					Assert.Equal ("p between ", _textView.SelectedText);
					break;
				case 2:
					Assert.Equal (25, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (10, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (15, _textView.SelectedLength);
					Assert.Equal ("p between text ", _textView.SelectedText);
					break;
				case 3:
					Assert.Equal (32, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (10, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (22, _textView.SelectedLength);
					Assert.Equal ("p between text fields.", _textView.SelectedText);
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void WordBackward_With_The_Same_Values_For_SelectedStart_And_CursorPosition_And_Not_Starting_At_Beginning_Of_The_Text ()
		{
			_textView.CursorPosition = new Point (10, 0);
			_textView.SelectionStartColumn = 10;
			_textView.SelectionStartRow = 0;
			var iteration = 0;

			while (_textView.CursorPosition.X > 0) {
				_textView.ProcessKey (new KeyEvent (Key.CursorLeft | Key.CtrlMask | Key.ShiftMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (7, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (10, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (3, _textView.SelectedLength);
					Assert.Equal ("jum", _textView.SelectedText);
					break;
				case 1:
					Assert.Equal (4, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (10, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (6, _textView.SelectedLength);
					Assert.Equal ("to jum", _textView.SelectedText);
					break;
				case 2:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (10, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (10, _textView.SelectedLength);
					Assert.Equal ("TAB to jum", _textView.SelectedText);
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void WordForward_With_No_Selection_And_With_More_Than_Only_One_Whitespace_And_With_Only_One_Letter ()
		{
			//                          1         2         3         4         5    
			//                0123456789012345678901234567890123456789012345678901234=55 (Length)
			_textView.Text = "TAB   t  o  jump         b  etween    t ext   f ields .";
			_textView.CursorPosition = new Point (0, 0);
			var iteration = 0;

			while (_textView.CursorPosition.X < _textView.Text.Length) {
				_textView.ProcessKey (new KeyEvent (Key.CursorRight | Key.CtrlMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (6, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 1:
					Assert.Equal (9, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 2:
					Assert.Equal (12, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 3:
					Assert.Equal (25, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 4:
					Assert.Equal (28, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 5:
					Assert.Equal (38, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 6:
					Assert.Equal (40, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 7:
					Assert.Equal (46, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 8:
					Assert.Equal (48, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 9:
					Assert.Equal (55, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void WordBackward_With_No_Selection_And_With_More_Than_Only_One_Whitespace_And_With_Only_One_Letter ()
		{
			//                          1         2         3         4         5    
			//                0123456789012345678901234567890123456789012345678901234=55 (Length)
			_textView.Text = "TAB   t  o  jump         b  etween    t ext   f ields .";
			_textView.CursorPosition = new Point (_textView.Text.Length, 0);
			var iteration = 0;

			while (_textView.CursorPosition.X > 0) {
				_textView.ProcessKey (new KeyEvent (Key.CursorLeft | Key.CtrlMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (54, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 1:
					Assert.Equal (48, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 2:
					Assert.Equal (46, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 3:
					Assert.Equal (40, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 4:
					Assert.Equal (38, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 5:
					Assert.Equal (28, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 6:
					Assert.Equal (25, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 7:
					Assert.Equal (12, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 8:
					Assert.Equal (9, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 9:
					Assert.Equal (6, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				case 10:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (0, _textView.SelectedLength);
					Assert.Equal ("", _textView.SelectedText);
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void WordBackward_Multiline_With_Selection ()
		{
			//		          4         3          2         1
			//		  87654321098765432109876 54321098765432109876543210-Length
			//			    1         2              1         2
			//                01234567890123456789012  0123456789012345678901234
			_textView.Text = "This is the first line.\nThis is the second line.";

			_textView.MoveEnd ();
			_textView.SelectionStartColumn = _textView.CurrentColumn;
			_textView.SelectionStartRow = _textView.CurrentRow;
			var iteration = 0;
			bool iterationsFinished = false;

			while (!iterationsFinished) {
				_textView.ProcessKey (new KeyEvent (Key.CursorLeft | Key.CtrlMask | Key.ShiftMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (19, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal (24, _textView.SelectionStartColumn);
					Assert.Equal (1, _textView.SelectionStartRow);
					Assert.Equal (5, _textView.SelectedLength);
					Assert.Equal ("line.", _textView.SelectedText);
					break;
				case 1:
					Assert.Equal (12, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal (24, _textView.SelectionStartColumn);
					Assert.Equal (1, _textView.SelectionStartRow);
					Assert.Equal (12, _textView.SelectedLength);
					Assert.Equal ("second line.", _textView.SelectedText);
					break;
				case 2:
					Assert.Equal (8, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal (24, _textView.SelectionStartColumn);
					Assert.Equal (1, _textView.SelectionStartRow);
					Assert.Equal (16, _textView.SelectedLength);
					Assert.Equal ("the second line.", _textView.SelectedText);
					break;
				case 3:
					Assert.Equal (5, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal (24, _textView.SelectionStartColumn);
					Assert.Equal (1, _textView.SelectionStartRow);
					Assert.Equal (19, _textView.SelectedLength);
					Assert.Equal ("is the second line.", _textView.SelectedText);
					break;
				case 4:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal (24, _textView.SelectionStartColumn);
					Assert.Equal (1, _textView.SelectionStartRow);
					Assert.Equal (24, _textView.SelectedLength);
					Assert.Equal ("This is the second line.", _textView.SelectedText);
					break;
				case 5:
					Assert.Equal (23, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (24, _textView.SelectionStartColumn);
					Assert.Equal (1, _textView.SelectionStartRow);
					Assert.Equal (25, _textView.SelectedLength);
					Assert.Equal ("\nThis is the second line.", _textView.SelectedText);
					break;
				case 6:
					Assert.Equal (18, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (24, _textView.SelectionStartColumn);
					Assert.Equal (1, _textView.SelectionStartRow);
					Assert.Equal (30, _textView.SelectedLength);
					Assert.Equal ("line.\nThis is the second line.", _textView.SelectedText);
					break;
				case 7:
					Assert.Equal (12, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (24, _textView.SelectionStartColumn);
					Assert.Equal (1, _textView.SelectionStartRow);
					Assert.Equal (36, _textView.SelectedLength);
					Assert.Equal ("first line.\nThis is the second line.", _textView.SelectedText);
					break;
				case 8:
					Assert.Equal (8, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (24, _textView.SelectionStartColumn);
					Assert.Equal (1, _textView.SelectionStartRow);
					Assert.Equal (40, _textView.SelectedLength);
					Assert.Equal ("the first line.\nThis is the second line.", _textView.SelectedText);
					break;
				case 9:
					Assert.Equal (5, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (24, _textView.SelectionStartColumn);
					Assert.Equal (1, _textView.SelectionStartRow);
					Assert.Equal (43, _textView.SelectedLength);
					Assert.Equal ("is the first line.\nThis is the second line.", _textView.SelectedText);
					break;
				case 10:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (24, _textView.SelectionStartColumn);
					Assert.Equal (1, _textView.SelectionStartRow);
					Assert.Equal (48, _textView.SelectedLength);
					Assert.Equal ("This is the first line.\nThis is the second line.", _textView.SelectedText);
					break;
				default:
					iterationsFinished = true;
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void WordForward_Multiline_With_Selection ()
		{
			//			    1         2          3         4
			//		  01234567890123456789012 34567890123456789012345678-Length
			//			    1         2              1         2
			//                01234567890123456789012  0123456789012345678901234
			_textView.Text = "This is the first line.\nThis is the second line.";

			_textView.SelectionStartColumn = _textView.CurrentColumn;
			_textView.SelectionStartRow = _textView.CurrentRow;
			var iteration = 0;
			bool iterationsFinished = false;

			while (!iterationsFinished) {
				_textView.ProcessKey (new KeyEvent (Key.CursorRight | Key.CtrlMask | Key.ShiftMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (5, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (5, _textView.SelectedLength);
					Assert.Equal ("This ", _textView.SelectedText);
					break;
				case 1:
					Assert.Equal (8, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (8, _textView.SelectedLength);
					Assert.Equal ("This is ", _textView.SelectedText);
					break;
				case 2:
					Assert.Equal (12, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (12, _textView.SelectedLength);
					Assert.Equal ("This is the ", _textView.SelectedText);
					break;
				case 3:
					Assert.Equal (18, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (18, _textView.SelectedLength);
					Assert.Equal ("This is the first ", _textView.SelectedText);
					break;
				case 4:
					Assert.Equal (23, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (23, _textView.SelectedLength);
					Assert.Equal ("This is the first line.", _textView.SelectedText);
					break;
				case 5:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (24, _textView.SelectedLength);
					Assert.Equal ("This is the first line.\n", _textView.SelectedText);
					break;
				case 6:
					Assert.Equal (5, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (29, _textView.SelectedLength);
					Assert.Equal ("This is the first line.\nThis ", _textView.SelectedText);
					break;
				case 7:
					Assert.Equal (8, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (32, _textView.SelectedLength);
					Assert.Equal ("This is the first line.\nThis is ", _textView.SelectedText);
					break;
				case 8:
					Assert.Equal (12, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (36, _textView.SelectedLength);
					Assert.Equal ("This is the first line.\nThis is the ", _textView.SelectedText);
					break;
				case 9:
					Assert.Equal (19, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (43, _textView.SelectedLength);
					Assert.Equal ("This is the first line.\nThis is the second ", _textView.SelectedText);
					break;
				case 10:
					Assert.Equal (24, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal (0, _textView.SelectionStartColumn);
					Assert.Equal (0, _textView.SelectionStartRow);
					Assert.Equal (48, _textView.SelectedLength);
					Assert.Equal ("This is the first line.\nThis is the second line.", _textView.SelectedText);
					break;
				default:
					iterationsFinished = true;
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void Kill_To_End_Delete_Forwards_And_Copy_To_The_Clipboard ()
		{
			_textView.Text = "This is the first line.\nThis is the second line.";
			var iteration = 0;
			bool iterationsFinished = false;

			while (!iterationsFinished) {
				_textView.ProcessKey (new KeyEvent (Key.DeleteChar | Key.CtrlMask | Key.ShiftMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ($"{System.Environment.NewLine}This is the second line.", _textView.Text);
					break;
				case 1:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("This is the second line.", _textView.Text);
					break;
				case 2:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("", _textView.Text);
					_textView.ProcessKey (new KeyEvent (Key.Y | Key.CtrlMask, new KeyModifiers ())); // Paste
					Assert.Equal ("This is the second line.", _textView.Text);
					break;
				default:
					iterationsFinished = true;
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void Kill_To_Start_Delete_Backwards_And_Copy_To_The_Clipboard ()
		{
			_textView.Text = "This is the first line.\nThis is the second line.";
			_textView.MoveEnd ();
			var iteration = 0;
			bool iterationsFinished = false;

			while (!iterationsFinished) {
				_textView.ProcessKey (new KeyEvent (Key.Backspace | Key.CtrlMask | Key.ShiftMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal ($"This is the first line.{System.Environment.NewLine}", _textView.Text);
					break;
				case 1:
					Assert.Equal (23, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("This is the first line.", _textView.Text);
					break;
				case 2:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("", _textView.Text);
					_textView.ProcessKey (new KeyEvent (Key.Y | Key.CtrlMask, new KeyModifiers ())); // Paste
					Assert.Equal ("This is the first line.", _textView.Text);
					break;
				default:
					iterationsFinished = true;
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void Kill_Delete_WordForward ()
		{
			_textView.Text = "This is the first line.";
			var iteration = 0;
			bool iterationsFinished = false;

			while (!iterationsFinished) {
				_textView.ProcessKey (new KeyEvent (Key.DeleteChar | Key.CtrlMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("is the first line.", _textView.Text);
					break;
				case 1:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("the first line.", _textView.Text);
					break;
				case 2:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("first line.", _textView.Text);
					break;
				case 3:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("line.", _textView.Text);
					break;
				case 4:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("", _textView.Text);
					break;
				default:
					iterationsFinished = true;
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void Kill_Delete_WordBackward ()
		{
			_textView.Text = "This is the first line.";
			_textView.MoveEnd ();
			var iteration = 0;
			bool iterationsFinished = false;

			while (!iterationsFinished) {
				_textView.ProcessKey (new KeyEvent (Key.Backspace | Key.CtrlMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (18, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("This is the first ", _textView.Text);
					break;
				case 1:
					Assert.Equal (12, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("This is the ", _textView.Text);
					break;
				case 2:
					Assert.Equal (8, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("This is ", _textView.Text);
					break;
				case 3:
					Assert.Equal (5, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("This ", _textView.Text);
					break;
				case 4:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("", _textView.Text);
					break;
				default:
					iterationsFinished = true;
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void Kill_Delete_WordForward_Multiline ()
		{
			_textView.Text = "This is the first line.\nThis is the second line.";
			_textView.Width = 4;
			var iteration = 0;
			bool iterationsFinished = false;

			while (!iterationsFinished) {
				_textView.ProcessKey (new KeyEvent (Key.DeleteChar | Key.CtrlMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("is the first line." + Environment.NewLine
						+ "This is the second line.", _textView.Text);
					break;
				case 1:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("the first line." + Environment.NewLine
						+ "This is the second line.", _textView.Text);
					break;
				case 2:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("first line." + Environment.NewLine
						+ "This is the second line.", _textView.Text);
					break;
				case 3:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("line." + Environment.NewLine
						+ "This is the second line.", _textView.Text);
					break;
				case 4:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("" + Environment.NewLine
						+ "This is the second line.", _textView.Text);
					break;
				case 5:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("This is the second line.", _textView.Text);
					break;
				case 6:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("is the second line.", _textView.Text);
					break;
				case 7:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("the second line.", _textView.Text);
					break;
				case 8:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("second line.", _textView.Text);
					break;
				case 9:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("line.", _textView.Text);
					break;
				case 10:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("", _textView.Text);
					break;
				default:
					iterationsFinished = true;
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void Kill_Delete_WordBackward_Multiline ()
		{
			_textView.Text = "This is the first line.\nThis is the second line.";
			_textView.Width = 4;
			_textView.MoveEnd ();
			var iteration = 0;
			bool iterationsFinished = false;

			while (!iterationsFinished) {
				_textView.ProcessKey (new KeyEvent (Key.Backspace | Key.CtrlMask, new KeyModifiers ()));
				switch (iteration) {
				case 0:
					Assert.Equal (19, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal ("This is the first line." + Environment.NewLine
						+ "This is the second ", _textView.Text);
					break;
				case 1:
					Assert.Equal (12, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal ("This is the first line." + Environment.NewLine
						+ "This is the ", _textView.Text);
					break;
				case 2:
					Assert.Equal (8, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal ("This is the first line." + Environment.NewLine
						+ "This is ", _textView.Text);
					break;
				case 3:
					Assert.Equal (5, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal ("This is the first line." + Environment.NewLine
						+ "This ", _textView.Text);
					break;
				case 4:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (1, _textView.CursorPosition.Y);
					Assert.Equal ("This is the first line." + Environment.NewLine, _textView.Text);
					break;
				case 5:
					Assert.Equal (23, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("This is the first line.", _textView.Text);
					break;
				case 6:
					Assert.Equal (18, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("This is the first ", _textView.Text);
					break;
				case 7:
					Assert.Equal (12, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("This is the ", _textView.Text);
					break;
				case 8:
					Assert.Equal (8, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("This is ", _textView.Text);
					break;
				case 9:
					Assert.Equal (5, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("This ", _textView.Text);
					break;
				case 10:
					Assert.Equal (0, _textView.CursorPosition.X);
					Assert.Equal (0, _textView.CursorPosition.Y);
					Assert.Equal ("", _textView.Text);
					break;
				default:
					iterationsFinished = true;
					break;
				}
				iteration++;
			}
		}

		[Fact]
		[InitShutdown]
		public void Copy_Or_Cut_Null_If_No_Selection ()
		{
			_textView.SelectionStartColumn = 0;
			_textView.SelectionStartRow = 0;
			_textView.ProcessKey (new KeyEvent (Key.C | Key.CtrlMask, new KeyModifiers ())); // Copy
			Assert.Equal ("", _textView.SelectedText);
			_textView.ProcessKey (new KeyEvent (Key.W | Key.CtrlMask, new KeyModifiers ())); // Cut
			Assert.Equal ("", _textView.SelectedText);
		}

		[Fact]
		[InitShutdown]
		public void Copy_Or_Cut_Not_Null_If_Has_Selection ()
		{
			_textView.SelectionStartColumn = 20;
			_textView.SelectionStartRow = 0;
			_textView.CursorPosition = new Point (24, 0);
			_textView.ProcessKey (new KeyEvent (Key.C | Key.CtrlMask, new KeyModifiers ())); // Copy
			Assert.Equal ("text", _textView.SelectedText);
			_textView.ProcessKey (new KeyEvent (Key.W | Key.CtrlMask, new KeyModifiers ())); // Cut
			Assert.Equal ("", _textView.SelectedText);
		}

		[Fact]
		[InitShutdown]
		public void Copy_Or_Cut_And_Paste_With_Selection ()
		{
			_textView.SelectionStartColumn = 20;
			_textView.SelectionStartRow = 0;
			_textView.CursorPosition = new Point (24, 0);
			_textView.ProcessKey (new KeyEvent (Key.C | Key.CtrlMask, new KeyModifiers ())); // Copy
			Assert.Equal ("text", _textView.SelectedText);
			Assert.Equal ("TAB to jump between text fields.", _textView.Text);
			_textView.ProcessKey (new KeyEvent (Key.Y | Key.CtrlMask, new KeyModifiers ())); // Paste
			Assert.Equal ("TAB to jump between text fields.", _textView.Text);
			_textView.SelectionStartColumn = 20;
			_textView.SelectionStartRow = 0;
			_textView.ProcessKey (new KeyEvent (Key.W | Key.CtrlMask, new KeyModifiers ())); // Cut
			_textView.ProcessKey (new KeyEvent (Key.Y | Key.CtrlMask, new KeyModifiers ())); // Paste
			Assert.Equal ("TAB to jump between text fields.", _textView.Text);
		}

		[Fact]
		[InitShutdown]
		public void Copy_Or_Cut_And_Paste_With_No_Selection ()
		{
			_textView.SelectionStartColumn = 20;
			_textView.SelectionStartRow = 0;
			_textView.CursorPosition = new Point (24, 0);
			_textView.ProcessKey (new KeyEvent (Key.C | Key.CtrlMask, new KeyModifiers ())); // Copy
			Assert.Equal ("text", _textView.SelectedText);
			Assert.Equal ("TAB to jump between text fields.", _textView.Text);
			_textView.SelectionStartColumn = 0;
			_textView.SelectionStartRow = 0;
			Assert.True (_textView.Selecting);
			_textView.Selecting = false;
			_textView.ProcessKey (new KeyEvent (Key.Y | Key.CtrlMask, new KeyModifiers ())); // Paste
			Assert.Equal ("TAB to jump between texttext fields.", _textView.Text);
			_textView.SelectionStartColumn = 24;
			_textView.SelectionStartRow = 0;
			_textView.ProcessKey (new KeyEvent (Key.W | Key.CtrlMask, new KeyModifiers ())); // Cut
			Assert.Equal ("", _textView.SelectedText);
			Assert.Equal ("TAB to jump between text fields.", _textView.Text);
			_textView.SelectionStartColumn = 0;
			_textView.SelectionStartRow = 0;
			Assert.True (_textView.Selecting);
			_textView.Selecting = false;
			_textView.ProcessKey (new KeyEvent (Key.Y | Key.CtrlMask, new KeyModifiers ())); // Paste
			Assert.Equal ("TAB to jump between texttext fields.", _textView.Text);
		}

		[Fact]
		[InitShutdown]
		public void Cut_Not_Allowed_If_ReadOnly_Is_True ()
		{
			_textView.ReadOnly = true;
			_textView.SelectionStartColumn = 20;
			_textView.SelectionStartRow = 0;
			_textView.CursorPosition = new Point (24, 0);
			_textView.ProcessKey (new KeyEvent (Key.C | Key.CtrlMask, new KeyModifiers ())); // Copy
			Assert.Equal ("text", _textView.SelectedText);
			_textView.ProcessKey (new KeyEvent (Key.W | Key.CtrlMask, new KeyModifiers ())); // Selecting is set to false after Cut.
			Assert.Equal ("", _textView.SelectedText);
			_textView.ReadOnly = false;
			Assert.False (_textView.Selecting);
			_textView.Selecting = true; // Needed to set Selecting to true.
			_textView.ProcessKey (new KeyEvent (Key.C | Key.CtrlMask, new KeyModifiers ())); // Copy
			Assert.Equal ("text", _textView.SelectedText);
			_textView.ProcessKey (new KeyEvent (Key.W | Key.CtrlMask, new KeyModifiers ())); // Cut
			Assert.Equal ("", _textView.SelectedText);
		}

		[Fact]
		[InitShutdown]
		public void Paste_Always_Clear_The_SelectedText ()
		{
			_textView.SelectionStartColumn = 20;
			_textView.SelectionStartRow = 0;
			_textView.CursorPosition = new Point (24, 0);
			_textView.ProcessKey (new KeyEvent (Key.C | Key.CtrlMask, new KeyModifiers ())); // Copy
			Assert.Equal ("text", _textView.SelectedText);
			_textView.ProcessKey (new KeyEvent (Key.Y | Key.CtrlMask, new KeyModifiers ())); // Paste
			Assert.Equal ("", _textView.SelectedText);
		}

		[Fact]
		[InitShutdown]
		public void TextChanged_Event ()
		{
			_textView.TextChanged += () => {
				if (_textView.Text == "changing") {
					Assert.Equal ("changing", _textView.Text);
					_textView.Text = "changed";
				}
			};

			_textView.Text = "changing";
			Assert.Equal ("changed", _textView.Text);
		}

		[Fact]
		[InitShutdown]
		public void Used_Is_True_By_Default ()
		{
			_textView.CursorPosition = new Point (10, 0);
			Assert.Equal ("TAB to jump between text fields.", _textView.Text);
			_textView.ProcessKey (new KeyEvent ((Key)0x75, new KeyModifiers ())); // u
			Assert.Equal ("TAB to jumup between text fields.", _textView.Text);
			_textView.ProcessKey (new KeyEvent ((Key)0x73, new KeyModifiers ())); // s
			Assert.Equal ("TAB to jumusp between text fields.", _textView.Text);
			_textView.ProcessKey (new KeyEvent ((Key)0x65, new KeyModifiers ())); // e
			Assert.Equal ("TAB to jumusep between text fields.", _textView.Text);
			_textView.ProcessKey (new KeyEvent ((Key)0x64, new KeyModifiers ())); // d
			Assert.Equal ("TAB to jumusedp between text fields.", _textView.Text);
		}

		[Fact]
		[InitShutdown]
		public void Used_Is_False ()
		{
			_textView.Used = false;
			_textView.CursorPosition = new Point (10, 0);
			Assert.Equal ("TAB to jump between text fields.", _textView.Text);
			_textView.ProcessKey (new KeyEvent ((Key)0x75, new KeyModifiers ())); // u
			Assert.Equal ("TAB to jumu between text fields.", _textView.Text);
			_textView.ProcessKey (new KeyEvent ((Key)0x73, new KeyModifiers ())); // s
			Assert.Equal ("TAB to jumusbetween text fields.", _textView.Text);
			_textView.ProcessKey (new KeyEvent ((Key)0x65, new KeyModifiers ())); // e
			Assert.Equal ("TAB to jumuseetween text fields.", _textView.Text);
			_textView.ProcessKey (new KeyEvent ((Key)0x64, new KeyModifiers ())); // d
			Assert.Equal ("TAB to jumusedtween text fields.", _textView.Text);
		}

		[Fact]
		[InitShutdown]
		public void Copy_Without_Selection ()
		{
			_textView.Text = "This is the first line.\nThis is the second line.\n";
			_textView.CursorPosition = new Point (0, _textView.Lines - 1);
			_textView.ProcessKey (new KeyEvent (Key.C | Key.CtrlMask, new KeyModifiers ())); // Copy
			_textView.ProcessKey (new KeyEvent (Key.Y | Key.CtrlMask, new KeyModifiers ())); // Paste
			Assert.Equal ($"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}{Environment.NewLine}", _textView.Text);
			_textView.CursorPosition = new Point (3, 1);
			_textView.ProcessKey (new KeyEvent (Key.C | Key.CtrlMask, new KeyModifiers ())); // Copy
			_textView.ProcessKey (new KeyEvent (Key.Y | Key.CtrlMask, new KeyModifiers ())); // Paste
			Assert.Equal ($"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the second line.{Environment.NewLine}{Environment.NewLine}", _textView.Text);
			Assert.Equal (new Point (3, 2), _textView.CursorPosition);
			_textView.ProcessKey (new KeyEvent (Key.Y | Key.CtrlMask, new KeyModifiers ())); // Paste
			Assert.Equal ($"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the second line.{Environment.NewLine}This is the second line.{Environment.NewLine}{Environment.NewLine}", _textView.Text);
			Assert.Equal (new Point (3, 3), _textView.CursorPosition);
		}

		[Fact]
		[InitShutdown]
		public void TabWidth_Setting_To_Zero_Changes_AllowsTab_To_False_If_True ()
		{
			Assert.Equal (4, _textView.TabWidth);
			Assert.True (_textView.AllowsTab);
			Assert.True (_textView.AllowsReturn);
			Assert.True (_textView.Multiline);
			_textView.TabWidth = -1;
			Assert.Equal (0, _textView.TabWidth);
			Assert.False (_textView.AllowsTab);
			Assert.False (_textView.AllowsReturn);
			Assert.False (_textView.Multiline);
			_textView.ProcessKey (new KeyEvent (Key.Tab, new KeyModifiers ()));
			Assert.Equal ("TAB to jump between text fields.", _textView.Text);
			_textView.ProcessKey (new KeyEvent (Key.BackTab, new KeyModifiers ()));
			Assert.Equal ("TAB to jump between text fields.", _textView.Text);
		}

		[Fact]
		[InitShutdown]
		public void AllowsTab_Setting_To_True_Changes_TabWidth_To_Default_If_It_Is_Zero ()
		{
			_textView.TabWidth = 0;
			Assert.Equal (0, _textView.TabWidth);
			Assert.False (_textView.AllowsTab);
			Assert.False (_textView.AllowsReturn);
			Assert.False (_textView.Multiline);
			_textView.AllowsTab = true;
			Assert.True (_textView.AllowsTab);
			Assert.Equal (4, _textView.TabWidth);
			Assert.True (_textView.AllowsReturn);
			Assert.True (_textView.Multiline);
		}

		[Fact]
		[InitShutdown]
		public void AllowsReturn_Setting_To_True_Changes_Multiline_To_True_If_It_Is_False ()
		{
			Assert.True (_textView.AllowsReturn);
			Assert.True (_textView.Multiline);
			Assert.Equal (4, _textView.TabWidth);
			Assert.True (_textView.AllowsTab);
			_textView.ProcessKey (new KeyEvent (Key.Enter, new KeyModifiers ()));
			Assert.Equal (Environment.NewLine +
				"TAB to jump between text fields.", _textView.Text);

			_textView.AllowsReturn = false;
			Assert.False (_textView.AllowsReturn);
			Assert.False (_textView.Multiline);
			Assert.Equal (0, _textView.TabWidth);
			Assert.False (_textView.AllowsTab);
			_textView.ProcessKey (new KeyEvent (Key.Enter, new KeyModifiers ()));
			Assert.Equal (Environment.NewLine +
				"TAB to jump between text fields.", _textView.Text);
		}

		[Fact]
		[InitShutdown]
		public void Multiline_Setting_Changes_AllowsReturn_And_AllowsTab_And_Height ()
		{
			Assert.True (_textView.Multiline);
			Assert.True (_textView.AllowsReturn);
			Assert.Equal (4, _textView.TabWidth);
			Assert.True (_textView.AllowsTab);
			Assert.Equal ("Dim.Absolute(30)", _textView.Width.ToString ());
			Assert.Equal ("Dim.Absolute(10)", _textView.Height.ToString ());

			_textView.Multiline = false;
			Assert.False (_textView.Multiline);
			Assert.False (_textView.AllowsReturn);
			Assert.Equal (0, _textView.TabWidth);
			Assert.False (_textView.AllowsTab);
			Assert.Equal ("Dim.Absolute(30)", _textView.Width.ToString ());
			Assert.Equal ("Dim.Absolute(1)", _textView.Height.ToString ());

			_textView.Multiline = true;
			Assert.True (_textView.Multiline);
			Assert.True (_textView.AllowsReturn);
			Assert.Equal (4, _textView.TabWidth);
			Assert.True (_textView.AllowsTab);
			Assert.Equal ("Dim.Absolute(30)", _textView.Width.ToString ());
			Assert.Equal ("Dim.Absolute(10)", _textView.Height.ToString ());
		}

		[Fact]
		[InitShutdown]
		public void Tab_Test_Follow_By_BackTab ()
		{
			Application.Top.Add (_textView);

			Application.Iteration += () => {
				var width = _textView.Bounds.Width - 1;
				Assert.Equal (30, width + 1);
				Assert.Equal (10, _textView.Height);
				_textView.Text = "";
				var col = 0;
				var leftCol = 0;
				var tabWidth = _textView.TabWidth;
				while (col < 100) {
					col++;
					_textView.ProcessKey (new KeyEvent (Key.Tab, new KeyModifiers ()));
					Assert.Equal (new Point (col, 0), _textView.CursorPosition);
					leftCol = GetLeftCol (leftCol);
					Assert.Equal (leftCol, _textView.LeftColumn);
				}
				while (col > 0) {
					col--;
					_textView.ProcessKey (new KeyEvent (Key.BackTab, new KeyModifiers ()));
					Assert.Equal (new Point (col, 0), _textView.CursorPosition);
					leftCol = GetLeftCol (leftCol);
					Assert.Equal (leftCol, _textView.LeftColumn);
				}

				Application.Top.Remove (_textView);
				Application.RequestStop ();
			};

			Application.Run ();
		}

		[Fact]
		[InitShutdown]
		public void BackTab_Test_Follow_By_Tab ()
		{
			Application.Top.Add (_textView);

			Application.Iteration += () => {
				var width = _textView.Bounds.Width - 1;
				Assert.Equal (30, width + 1);
				Assert.Equal (10, _textView.Height);
				_textView.Text = "";
				for (int i = 0; i < 100; i++) {
					_textView.Text += "\t";
				}
				var col = 100;
				var tabWidth = _textView.TabWidth;
				var leftCol = _textView.LeftColumn;
				_textView.MoveEnd ();
				Assert.Equal (new Point (col, 0), _textView.CursorPosition);
				leftCol = GetLeftCol (leftCol);
				Assert.Equal (leftCol, _textView.LeftColumn);
				while (col > 0) {
					col--;
					_textView.ProcessKey (new KeyEvent (Key.BackTab, new KeyModifiers ()));
					Assert.Equal (new Point (col, 0), _textView.CursorPosition);
					leftCol = GetLeftCol (leftCol);
					Assert.Equal (leftCol, _textView.LeftColumn);
				}
				while (col < 100) {
					col++;
					_textView.ProcessKey (new KeyEvent (Key.Tab, new KeyModifiers ()));
					Assert.Equal (new Point (col, 0), _textView.CursorPosition);
					leftCol = GetLeftCol (leftCol);
					Assert.Equal (leftCol, _textView.LeftColumn);
				}

				Application.Top.Remove (_textView);
				Application.RequestStop ();
			};

			Application.Run ();
		}

		[Fact]
		[InitShutdown]
		public void Tab_Test_Follow_By_CursorLeft_And_Then_Follow_By_CursorRight ()
		{
			Application.Top.Add (_textView);

			Application.Iteration += () => {
				var width = _textView.Bounds.Width - 1;
				Assert.Equal (30, width + 1);
				Assert.Equal (10, _textView.Height);
				_textView.Text = "";
				var col = 0;
				var leftCol = 0;
				var tabWidth = _textView.TabWidth;
				while (col < 100) {
					col++;
					_textView.ProcessKey (new KeyEvent (Key.Tab, new KeyModifiers ()));
					Assert.Equal (new Point (col, 0), _textView.CursorPosition);
					leftCol = GetLeftCol (leftCol);
					Assert.Equal (leftCol, _textView.LeftColumn);
				}
				while (col > 0) {
					col--;
					_textView.ProcessKey (new KeyEvent (Key.CursorLeft, new KeyModifiers ()));
					Assert.Equal (new Point (col, 0), _textView.CursorPosition);
					leftCol = GetLeftCol (leftCol);
					Assert.Equal (leftCol, _textView.LeftColumn);
				}
				while (col < 100) {
					col++;
					_textView.ProcessKey (new KeyEvent (Key.CursorRight, new KeyModifiers ()));
					Assert.Equal (new Point (col, 0), _textView.CursorPosition);
					leftCol = GetLeftCol (leftCol);
					Assert.Equal (leftCol, _textView.LeftColumn);
				}

				Application.Top.Remove (_textView);
				Application.RequestStop ();
			};

			Application.Run ();
		}

		[Fact]
		[InitShutdown]
		public void Tab_Test_Follow_By_BackTab_With_Text ()
		{
			Application.Top.Add (_textView);

			Application.Iteration += () => {
				var width = _textView.Bounds.Width - 1;
				Assert.Equal (30, width + 1);
				Assert.Equal (10, _textView.Height);
				var col = 0;
				var leftCol = 0;
				Assert.Equal (new Point (col, 0), _textView.CursorPosition);
				Assert.Equal (leftCol, _textView.LeftColumn);
				while (col < 100) {
					col++;
					_textView.ProcessKey (new KeyEvent (Key.Tab, new KeyModifiers ()));
					Assert.Equal (new Point (col, 0), _textView.CursorPosition);
					leftCol = GetLeftCol (leftCol);
					Assert.Equal (leftCol, _textView.LeftColumn);
				}
				while (col > 0) {
					col--;
					_textView.ProcessKey (new KeyEvent (Key.BackTab, new KeyModifiers ()));
					Assert.Equal (new Point (col, 0), _textView.CursorPosition);
					leftCol = GetLeftCol (leftCol);
					Assert.Equal (leftCol, _textView.LeftColumn);
				}

				Application.Top.Remove (_textView);
				Application.RequestStop ();
			};

			Application.Run ();
		}

		[Fact]
		[InitShutdown]
		public void Tab_Test_Follow_By_Home_And_Then_Follow_By_End_And_Then_Follow_By_BackTab_With_Text ()
		{
			Application.Top.Add (_textView);

			Application.Iteration += () => {
				var width = _textView.Bounds.Width - 1;
				Assert.Equal (30, width + 1);
				Assert.Equal (10, _textView.Height);
				var col = 0;
				var leftCol = 0;
				Assert.Equal (new Point (col, 0), _textView.CursorPosition);
				Assert.Equal (leftCol, _textView.LeftColumn);
				Assert.Equal ("TAB to jump between text fields.", _textView.Text);
				Assert.Equal (32, _textView.Text.Length);
				while (col < 100) {
					col++;
					_textView.ProcessKey (new KeyEvent (Key.Tab, new KeyModifiers ()));
					Assert.Equal (new Point (col, 0), _textView.CursorPosition);
					leftCol = GetLeftCol (leftCol);
					Assert.Equal (leftCol, _textView.LeftColumn);
				}
				_textView.ProcessKey (new KeyEvent (Key.Home, new KeyModifiers ()));
				col = 0;
				Assert.Equal (new Point (col, 0), _textView.CursorPosition);
				leftCol = 0;
				Assert.Equal (leftCol, _textView.LeftColumn);

				_textView.ProcessKey (new KeyEvent (Key.End, new KeyModifiers ()));
				col = _textView.Text.Length;
				Assert.Equal (132, _textView.Text.Length);
				Assert.Equal (new Point (col, 0), _textView.CursorPosition);
				leftCol = GetLeftCol (leftCol);
				Assert.Equal (leftCol, _textView.LeftColumn);
				var txt = _textView.Text;
				while (col - 1 > 0 && txt [col - 1] != '\t') {
					col--;
				}
				_textView.CursorPosition = new Point (col, 0);
				leftCol = GetLeftCol (leftCol);
				while (col > 0) {
					col--;
					_textView.ProcessKey (new KeyEvent (Key.BackTab, new KeyModifiers ()));
					Assert.Equal (new Point (col, 0), _textView.CursorPosition);
					leftCol = GetLeftCol (leftCol);
					Assert.Equal (leftCol, _textView.LeftColumn);
				}
				Assert.Equal ("TAB to jump between text fields.", _textView.Text);
				Assert.Equal (32, _textView.Text.Length);

				Application.Top.Remove (_textView);
				Application.RequestStop ();
			};

			Application.Run ();
		}

		[Fact]
		[InitShutdown]
		public void Tab_Test_Follow_By_CursorLeft_And_Then_Follow_By_CursorRight_With_Text ()
		{
			Application.Top.Add (_textView);

			Application.Iteration += () => {
				var width = _textView.Bounds.Width - 1;
				Assert.Equal (30, width + 1);
				Assert.Equal (10, _textView.Height);
				Assert.Equal ("TAB to jump between text fields.", _textView.Text);
				var col = 0;
				var leftCol = 0;
				var tabWidth = _textView.TabWidth;
				while (col < 100) {
					col++;
					_textView.ProcessKey (new KeyEvent (Key.Tab, new KeyModifiers ()));
					Assert.Equal (new Point (col, 0), _textView.CursorPosition);
					leftCol = GetLeftCol (leftCol);
					Assert.Equal (leftCol, _textView.LeftColumn);
				}
				Assert.Equal (132, _textView.Text.Length);
				while (col > 0) {
					col--;
					_textView.ProcessKey (new KeyEvent (Key.CursorLeft, new KeyModifiers ()));
					Assert.Equal (new Point (col, 0), _textView.CursorPosition);
					leftCol = GetLeftCol (leftCol);
					Assert.Equal (leftCol, _textView.LeftColumn);
				}
				while (col < 100) {
					col++;
					_textView.ProcessKey (new KeyEvent (Key.CursorRight, new KeyModifiers ()));
					Assert.Equal (new Point (col, 0), _textView.CursorPosition);
					leftCol = GetLeftCol (leftCol);
					Assert.Equal (leftCol, _textView.LeftColumn);
				}

				Application.Top.Remove (_textView);
				Application.RequestStop ();
			};

			Application.Run ();
		}

		private int GetLeftCol (int start)
		{
			var lines = _textView.Text.Split (Environment.NewLine);
			if (lines == null || lines.Length == 0) {
				return 0;
			}
			if (start == _textView.LeftColumn) {
				return start;
			}
			if (_textView.LeftColumn == _textView.CurrentColumn) {
				return _textView.CurrentColumn;
			}
			var cCol = _textView.CurrentColumn;
			var line = lines [_textView.CurrentRow];
			var lCount = cCol > line.Length - 1 ? line.Length - 1 : cCol;
			var width = _textView.Frame.Width;
			var tabWidth = _textView.TabWidth;
			var sumLength = 0;
			var col = 0;

			for (int i = lCount; i >= 0; i--) {
				var r = line [i];
				sumLength += Rune.ColumnWidth (r);
				if (r == '\t') {
					sumLength += tabWidth + 1;
				}
				if (sumLength > width) {
					if (cCol == line.Length) {
						col++;
					}
					break;
				} else if (cCol < line.Length && col > 0 && start < cCol && col == start) {
					break;
				}
				col = i;
			}

			return col;
		}

		[Fact]
		public void LoadFile_Throws_If_File_Is_Null ()
		{
			var tv = new TextView ();
			Assert.Throws<ArgumentNullException> (() => tv.LoadFile (null));
		}

		[Fact]
		public void LoadFile_Throws_If_File_Is_Empty ()
		{
			var tv = new TextView ();
			Assert.Throws<ArgumentException> (() => tv.LoadFile (""));
		}

		[Fact]
		public void LoadFile_Throws_If_File_Not_Exist ()
		{
			var tv = new TextView ();
			Assert.Throws<System.IO.FileNotFoundException> (() => tv.LoadFile ("blabla"));
		}

		[Fact]
		public void LoadStream_Throws_If_Stream_Is_Null ()
		{
			var tv = new TextView ();
			Assert.Throws<ArgumentNullException> (() => tv.LoadStream (null));
		}

		[Fact]
		public void LoadStream_Stream_Is_Empty ()
		{
			var tv = new TextView ();
			tv.LoadStream (new System.IO.MemoryStream ());
			Assert.Equal ("", tv.Text);
		}

		[Fact]
		public void LoadStream_CRLF ()
		{
			var text = "This is the first line.\r\nThis is the second line.\r\n";
			var tv = new TextView ();
			tv.LoadStream (new System.IO.MemoryStream (System.Text.Encoding.ASCII.GetBytes (text)));
			Assert.Equal ($"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}", tv.Text);
		}

		[Fact]
		public void LoadStream_LF ()
		{
			var text = "This is the first line.\nThis is the second line.\n";
			var tv = new TextView ();
			tv.LoadStream (new System.IO.MemoryStream (System.Text.Encoding.ASCII.GetBytes (text)));
			Assert.Equal ($"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}", tv.Text);
		}

		[Fact]
		public void StringToRunes_Slipts_CRLF ()
		{
			var text = "This is the first line.\r\nThis is the second line.\r\n";
			var tv = new TextView ();
			tv.Text = text;
			Assert.Equal ($"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}", tv.Text);
		}

		[Fact]
		public void StringToRunes_Slipts_LF ()
		{
			var text = "This is the first line.\nThis is the second line.\n";
			var tv = new TextView ();
			tv.Text = text;
			Assert.Equal ($"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}", tv.Text);
		}

		[Fact]
		public void CloseFile_Throws_If_FilePath_Is_Null ()
		{
			var tv = new TextView ();
			Assert.Throws<ArgumentNullException> (() => tv.CloseFile ());
		}

		[Fact]
		public void WordWrap_Gets_Sets ()
		{
			var tv = new TextView () { WordWrap = true };
			Assert.True (tv.WordWrap);
			tv.WordWrap = false;
			Assert.False (tv.WordWrap);
		}

		[Fact]
		public void WordWrap_True_Text_Always_Returns_Unwrapped ()
		{
			var text = "This is the first line.\nThis is the second line.\n";
			var tv = new TextView () { Width = 10 };
			tv.Text = text;
			Assert.Equal ($"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}", tv.Text);
			tv.WordWrap = true;
			Assert.Equal ($"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}", tv.Text);
		}

		[Fact]
		[InitShutdown]
		public void WordWrap_WrapModel_Output ()
		{
			//          0123456789
			var text = "This is the first line.\nThis is the second line.\n";
			var tv = new TextView () { Width = 10, Height = 10 };
			tv.Text = text;
			Assert.Equal ($"This is the first line.{Environment.NewLine}This is the second line.{Environment.NewLine}", tv.Text);
			tv.WordWrap = true;

			Application.Top.Add (tv);

			tv.Redraw (tv.Bounds);

			string expected = @"
This is 
the 
first 
line.
This is 
the 
second 
line.
";

			GraphViewTests.AssertDriverContentsAre (expected, output);
		}
	}
}
