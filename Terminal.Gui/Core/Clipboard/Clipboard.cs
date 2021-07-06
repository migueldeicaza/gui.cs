using NStack;
using System;

namespace Terminal.Gui {
	/// <summary>
	/// Provides cut, copy, and paste support for the clipboard with OS interaction.
	/// </summary>
	public static class Clipboard {
		static ustring contents;

		/// <summary>
		/// Get or sets the operation system clipboard, otherwise the contents field.
		/// </summary>
		public static ustring Contents {
			get {
				try {
					if (IsSupported) {
						return Application.Driver.Clipboard.GetClipboardData ();
					} else {
						return contents;
					}
				} catch (Exception) {
					return contents;
				}
			}
			set {
				try {
					if (IsSupported) {
						Application.Driver.Clipboard.SetClipboardData (value.ToString ());
					}
					contents = value;
				} catch (Exception) {
					contents = value;
				}
			}
		}

		/// <summary>
		/// Returns true if the environmental dependencies are in place to interact with the OS clipboard.
		/// </summary>
		public static bool IsSupported { get; } = Application.Driver.Clipboard.IsSupported;

		/// <summary>
		/// Gets the operation system clipboard if possible.
		/// </summary>
		/// <param name="result">Clipboard contents read</param>
		/// <returns>true if it was possible to read the OS clipboard.</returns>
		public static bool TryGetClipboardData (out string result)
		{
			if (Application.Driver.Clipboard.TryGetClipboardData (out result)) {
				if (contents != result) {
					contents = result;
				}
				return true;
			}
			return false;
		}

		/// <summary>
		/// Sets the operation system clipboard if possible.
		/// </summary>
		/// <param name="text"></param>
		/// <returns>True if the clipboard content was set successfully.</returns>
		public static bool TrySetClipboardData (string text)
		{
			if (Application.Driver.Clipboard.TrySetClipboardData (text)) {
				contents = text;
				return true;
			}
			return false;
		}
	}
}
