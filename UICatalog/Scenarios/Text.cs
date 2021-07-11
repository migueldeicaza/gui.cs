using System;
using System.Text;
using Terminal.Gui;
using Terminal.Gui.TextValidateProviders;



namespace UICatalog {
	[ScenarioMetadata (Name: "Text Input Controls", Description: "Tests all text input controls")]
	[ScenarioCategory ("Controls")]
	[ScenarioCategory ("Bug Repro")]
	class Text : Scenario {
		public override void Setup ()
		{
			var s = "TAB to jump between text fields.";
			var textField = new TextField (s) {
				X = 1,
				Y = 1,
				Width = Dim.Percent (50),
				//ColorScheme = Colors.Dialog
			};
			Win.Add (textField);

			var labelMirroringTextField = new Label (textField.Text) {
				X = Pos.Right (textField) + 1,
				Y = Pos.Top (textField),
				Width = Dim.Fill (1)
			};
			Win.Add (labelMirroringTextField);

			textField.TextChanged += (prev) => {
				labelMirroringTextField.Text = textField.Text;
			};

			var textView = new TextView () {
				X = 1,
				Y = 3,
				Width = Dim.Percent (50),
				Height = Dim.Percent (30),
				ColorScheme = Colors.Dialog
			};
			textView.Text = s;
			Win.Add (textView);

			var labelMirroringTextView = new Label (textView.Text) {
				X = Pos.Right (textView) + 1,
				Y = Pos.Top (textView),
				Width = Dim.Fill (1),
				Height = Dim.Height (textView),
			};
			Win.Add (labelMirroringTextView);

			textView.TextChanged += () => {
				labelMirroringTextView.Text = textView.Text;
			};

			var btnMultiline = new Button ("Toggle Multiline") {
				X = Pos.Right (textView) + 1,
				Y = Pos.Top (textView) + 1
			};
			btnMultiline.Clicked += () => textView.Multiline = !textView.Multiline;
			Win.Add (btnMultiline);

			// BUGBUG: 531 - TAB doesn't go to next control from HexView
			var hexView = new HexView (new System.IO.MemoryStream (Encoding.ASCII.GetBytes (s))) {
				X = 1,
				Y = Pos.Bottom (textView) + 1,
				Width = Dim.Fill (1),
				Height = Dim.Percent (30),
				//ColorScheme = Colors.Dialog
			};
			Win.Add (hexView);

			var dateField = new DateField (System.DateTime.Now) {
				X = 1,
				Y = Pos.Bottom (hexView) + 1,
				Width = 20,
				//ColorScheme = Colors.Dialog,
				IsShortFormat = false,
			
			};
			Win.Add (dateField);

			var labelMirroringDateField = new Label (dateField.Text) {
				X = Pos.Right (dateField) + 1,
				Y = Pos.Top (dateField),
				Width = Dim.Width (dateField),
				Height = Dim.Height (dateField),
			};
			Win.Add (labelMirroringDateField);

			dateField.TextChanged += (prev) => {
				labelMirroringDateField.Text = dateField.Text;
			};
			

			_timeField = new TimeField (DateTime.Now.TimeOfDay) {
				X = Pos.Right (labelMirroringDateField) + 5,
				Y = Pos.Bottom (hexView) + 1,
				Width = 20,
				//ColorScheme = Colors.Dialog,
				IsShortFormat = false
			};
			Win.Add (_timeField);

			_labelMirroringTimeField = new Label (_timeField.Text) {
				X = Pos.Right (_timeField) + 1,
				Y = Pos.Top (_timeField),
				Width = Dim.Width (_timeField),
				Height = Dim.Height (_timeField),
			};
			Win.Add (_labelMirroringTimeField);

			_timeField.TimeChanged += TimeChanged;

			// MaskedTextProvider
			var netProviderLabel = new Label (".Net MaskedTextProvider [ 999 000 LLL >LLL| AAA aaa ]") {
				X = Pos.Left (dateField),
				Y = Pos.Bottom (dateField) + 1
			};
			Win.Add (netProviderLabel);

			var netProvider = new NetMaskedTextProvider ("999 000 LLL > LLL | AAA aaa");

			var netProviderField = new TextValidateField (netProvider) {
				X = Pos.Right (netProviderLabel) + 1,
				Y = Pos.Y (netProviderLabel)
			};

			Win.Add (netProviderField);

			// TextRegexProvider
			var regexProvider = new Label ("Gui.cs TextRegexProvider [ ^([0-9]?[0-9]?[0-9]|1000)$ ]") {
				X = Pos.Left (netProviderLabel),
				Y = Pos.Bottom (netProviderLabel) + 1
			};
			Win.Add (regexProvider);

			var provider2 = new TextRegexProvider ("^([0-9]?[0-9]?[0-9]|1000)$") { ValidateOnInput = false };
			var regexProviderField = new TextValidateField (provider2) {
				X = Pos.Right (regexProvider) + 1,
				Y = Pos.Y (regexProvider),
				Width = 30,
				TextAlignment = TextAlignment.Centered
			};

			Win.Add (regexProviderField);
		}

		TimeField _timeField;
		Label _labelMirroringTimeField;

		private void TimeChanged (DateTimeEventArgs<TimeSpan> e)
		{
			_labelMirroringTimeField.Text = _timeField.Text;
		}
	}
}
