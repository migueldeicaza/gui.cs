using NStack;
using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui;

namespace UICatalog {
	[ScenarioMetadata (Name: "Slider", Description: "Demonstrates all sorts of Sliders")]
	[ScenarioCategory ("Controls")]

	class Sliders : Scenario {
		public override void Setup ()
		{
			var leftView = new FrameView {
				X = 0,
				Y = 0,
				Width = Dim.Percent (50),
				Height = Dim.Fill (),
				ColorScheme = new ColorScheme { Normal = new Terminal.Gui.Attribute (Color.White, Color.Black) }
			};

			var rightView = new FrameView {
				X = Pos.Right (leftView),
				Y = 0,
				Width = Dim.Fill (),
				Height = Dim.Fill (),
			};

			Win.Add (leftView);
			Win.Add (rightView);

			#region LeftView
			{
				MakeSliders (leftView, new List<object> { 500, 1000, 1500, 2000, 2500, 3000 });
				leftView.FocusFirst ();
			}
			#endregion

			#region RightView
			{
				var label = new Label ("Sliders Options") { X = 0, Y = 0, Width = Dim.Fill (), TextAlignment = TextAlignment.Centered };
				rightView.Add (label);

				var slider = new Slider<string> () {
					X = Pos.Center (),
					Y = Pos.Bottom (label) + 1,
					Type = SliderType.Multiple,
				};

				slider.Style.SetAllAttributes (new Terminal.Gui.Attribute (Color.White, Color.Blue));
				slider.Style.SetAttr = new Terminal.Gui.Attribute (Color.BrightGreen);
				slider.Style.LegendSetAttr = new Terminal.Gui.Attribute (Color.Green);

				slider.ShowHeader = true;
				slider.Header = "Slider Config";
				slider.Options = new List<SliderOption<string>> {
					new SliderOption<string>{
						Legend="Borders"
					},
					new SliderOption<string>{
						Legend="Legends"
					},
					new SliderOption<string>{
						Legend="Header"
					},
					new SliderOption<string>{
						Legend="RangeSingle"
					}
				};

				slider.SetOption (1);
				slider.SetOption (2);

				rightView.Add (slider);

				slider.OptionsChanged += (options) => {
					foreach (var s in leftView.Subviews [0].Subviews.OfType<Slider> ()) {

						if (options.ContainsKey (0))
							s.ShowBorders = true;
						else
							s.ShowBorders = false;

						if (options.ContainsKey (1))
							s.ShowLegends = true;
						else
							s.ShowLegends = false;

						if (options.ContainsKey (2))
							s.ShowHeader = true;
						else
							s.ShowHeader = false;

						if (options.ContainsKey (3))
							s.RangeAllowSingle = true;
						else
							s.RangeAllowSingle = false;

					}
				};

				// COLOR

				var sliderColor = new Slider<(Color, Color)> () {
					X = Pos.Center (),
					Y = Pos.Bottom (slider) + 1,
					Type = SliderType.Single,
				};

				sliderColor.Style.SetAllAttributes (new Terminal.Gui.Attribute (Color.White, Color.Blue));
				sliderColor.Style.SetAttr = new Terminal.Gui.Attribute (Color.BrightGreen, Color.BrightGreen);
				sliderColor.Style.LegendSetAttr = new Terminal.Gui.Attribute (Color.Green, Color.Blue);

				sliderColor.ShowHeader = true;
				sliderColor.Header = "Slider Color";
				sliderColor.Options = new List<SliderOption<(Color, Color)>> {
					new SliderOption<(Color, Color)> {
						Data = (Color.Red,Color.BrightRed),
						Legend = "Red"
					},
					new SliderOption<(Color, Color)> {
						Data = (Color.Blue,Color.BrightBlue),
						Legend = "Blue"
					},
					new SliderOption<(Color, Color)> {
						Data = (Color.Green,Color.BrightGreen),
						Legend = "Green"
					},
					new SliderOption<(Color, Color)> {
						Data = (Color.Cyan,Color.BrightCyan),
						Legend = "Cyan"
					},
					new SliderOption<(Color, Color)> {
						Data = (Color.Brown,Color.BrightYellow),
						Legend = "Yellow"
					}
				};

				rightView.Add (sliderColor);

				sliderColor.OptionsChanged += (options) => {
					var data = options.First ().Value.Data;

					foreach (var s in leftView.Subviews [0].Subviews.OfType<Slider> ()) {
						s.Style.SetAttr = new Terminal.Gui.Attribute (data.Item1, data.Item1);
						s.Style.LegendSetAttr = new Terminal.Gui.Attribute (data.Item1, Color.Black);
						s.Style.RangeAttr = new Terminal.Gui.Attribute (data.Item2, data.Item2);
					}
				};

				// Set option after Eventhandler def, so it updates the sliders color.
				sliderColor.SetOption (2);

				// Slider Options Editor

				var labeleditor = new Label ("Slider Options Editor") { X = Pos.Center (), Y = Pos.Bottom (sliderColor) + 1 };

				var view = new View () { X = Pos.Center (), Y = Pos.Bottom (labeleditor) + 1, Width = 20, Height = 0 };
				var field = new TextField () { X = Pos.Center () + 1, Y = Pos.Bottom (view), Width = 20 };

				field.KeyDown += (k) => {
					if (k.KeyEvent.Key == Key.Enter && field.Text != ustring.Empty) {
						var vfield = new TextField (field.Text) { X = 1, Y = view.Bounds.Height, Width = 20 };

						field.Text = "";

						view.Add (vfield);

						view.Height = view.Bounds.Height + 1;

						var options = view.Subviews.OfType<TextField> ().Select (e => e.Text.ToString ()).ToList<object> ();

						foreach (var s in leftView.Subviews [0].Subviews) {
							s.Dispose ();
						}
						leftView.Subviews [0].RemoveAll ();

						k.Handled = true;

						MakeSliders (leftView, options);
					}
				};

				rightView.Add (labeleditor);
				rightView.Add (view);
				rightView.Add (field);
				//Win.Add (button);
			}
			#endregion
		}

		public void MakeSliders (View v, List<object> options)
		{
			var types = Enum.GetValues (typeof (SliderType)).Cast<SliderType> ().ToList ();

			View view = new Label ("Slider Types") { X = 0, Y = 0, Width = Dim.Fill (), TextAlignment = TextAlignment.Centered };
			v.Add (view);

			foreach (var type in types) {
				view = new Slider (type.ToString (), options) {
					X = Pos.Center (),
					Y = Pos.Bottom (view) + 1,
					Type = type
				};
				v.Add (view);
			};

			//v.FocusFirst ();
		}
	}
}
