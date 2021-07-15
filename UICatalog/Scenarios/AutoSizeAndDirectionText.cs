using Terminal.Gui;

namespace UICatalog {
	[ScenarioMetadata (Name: "AutoSize and Direction Text", Description: "Demonstrates the text auto-size and direction manipulation.")]
	[ScenarioCategory ("Text")]
	class AutoSizeAndDirectionText : Scenario {
		public override void Setup ()
		{
			var text = "Hello World";
			var color = Colors.Dialog;

			var labelH = new Label (text, TextDirection.LeftRight_TopBottom) {
				X = 1,
				Y = 1,
				ColorScheme = color
			};
			Win.Add (labelH);

			var labelV = new Label (text, TextDirection.TopBottom_LeftRight) {
				X = 70,
				Y = 1,
				ColorScheme = color
			};
			Win.Add (labelV);

			var editText = new TextView () {
				X = Pos.Center (),
				Y = Pos.Center (),
				Width = 20,
				Height = 5,
				ColorScheme = color,
				Text = text
			};

			editText.SetFocus ();

			Win.Add (editText);

			var ckbDirection = new CheckBox ("Toggle Direction") {
				X = Pos.Center (),
				Y = Pos.Center () + 3
			};
			ckbDirection.Toggled += (_) => {
				if (labelH.TextDirection == TextDirection.LeftRight_TopBottom) {
					labelH.TextDirection = TextDirection.TopBottom_LeftRight;
					labelV.TextDirection = TextDirection.LeftRight_TopBottom;
				} else {
					labelH.TextDirection = TextDirection.LeftRight_TopBottom;
					labelV.TextDirection = TextDirection.TopBottom_LeftRight;
				}
			};
			Win.Add (ckbDirection);

			var ckbAutoSize = new CheckBox ("Auto Size") {
				X = Pos.Center (),
				Y = Pos.Center () + 5,
				Checked = labelH.AutoSize = labelV.AutoSize
			};
			ckbAutoSize.Toggled += (_) => labelH.AutoSize = labelV.AutoSize = ckbAutoSize.Checked;
			Win.Add (ckbAutoSize);

			Win.KeyUp += (_) =>
				labelH.Text = labelV.Text = text = editText.Text.ToString ();
		}
	}
}