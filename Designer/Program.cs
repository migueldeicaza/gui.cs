﻿using System;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace Designer {
	class Surface : Window {
		public Surface () : base ("Designer")
		{
		}
	}

	class MainClass {
		public static void Main (string [] args)
		{
			Application.Init ();

			var menu = new MenuBar (new MenuBarItem [] {
				new MenuBarItem ("_File", new MenuItem [] {
					new MenuItem ("_Quit", "", () => { Application.RequestStop (); })
				}),
				new MenuBarItem ("_Edit", new MenuItem [] {
					new MenuItem ("_Copy", "", null),
					new MenuItem ("C_ut", "", null),
					new MenuItem ("_Paste", "", null)
				}),
			});

			var login = new Label ("Login: ") { X = 3, Y = 6 };
			var password = new Label ("Password: ") {
				X = Pos.Left (login),
				Y = Pos.Bottom (login) + 1
			};

			var surface = new Surface () {
				X = 0,
				Y = 1,
				Width = Dim.Percent (80),
				Height = Dim.Fill ()
			};

			var loginText = new TextField("") {
				X = Pos.Right(password),
				Y = Pos.Top(login),
				Width = 40,
				ColorScheme = new ColorScheme() {
					Focus = Attribute.Make(Color.BrightYellow, Color.DarkGray),
					Normal = Attribute.Make(Color.Green, Color.BrightYellow),
					HotFocus = Attribute.Make(Color.BrightBlue, Color.Brown),
					HotNormal = Attribute.Make(Color.Red, Color.BrightRed),
				},
			};

			//Application.Top.Add (menu);
			Application.Top.Add (login, password, loginText);
			Application.Run ();
		}
	}
}
