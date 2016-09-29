using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordBot.Utility {
	public static class StringHelper {
		public static string Repeat(this string str, int times) {
			if (times <= 0) return string.Empty;
			string output = "";
			for (int i = 0; i < times; i++)
				output += str;
			return output;
		}

		public static string Repeat(this char str, int times) {
			if (times <= 0) return string.Empty;
			return new string(str, times);
		}
		
		public static string FormatThousands(int number) {
			var f = new NumberFormatInfo { NumberGroupSeparator=" " };
			return number.ToString("n0", f);
		}

		public static string FormatThousands(uint number) {
			var f = new NumberFormatInfo { NumberGroupSeparator=" " };
			return number.ToString("n0", f);
		}

		public static string FormatThousands(short number) {
			var f = new NumberFormatInfo { NumberGroupSeparator=" " };
			return number.ToString("n0", f);
		}

		public static string FormatThousands(ushort number) {
			var f = new NumberFormatInfo { NumberGroupSeparator=" " };
			return number.ToString("n0", f);
		}

		public static string FormatThousands(long number) {
			var f = new NumberFormatInfo { NumberGroupSeparator=" " };
			return number.ToString("n0", f);
		}

		public static string FormatThousands(ulong number) {
			var f = new NumberFormatInfo { NumberGroupSeparator=" " };
			return number.ToString("n0", f);
		}

		public static string FormatThousands(decimal number) {
			var f = new NumberFormatInfo { NumberGroupSeparator=" " };
			return number.ToString("n0", f);
		}

		public static string FormatThousands(float number, byte digits = 7) {
			var f = new NumberFormatInfo { NumberGroupSeparator=" " };
			return number.ToString("n"+digits, f);
		}

		public static string FormatThousands(double number, byte digits = 16) {
			var f = new NumberFormatInfo { NumberGroupSeparator=" " };
			return number.ToString("n"+digits, f);
		}

		public static string FormatBytes(long bytes) {
			// Algorithm taken from
			// http://stackoverflow.com/a/281679/3163818
			string[] sizes = { "B", "KB", "MB", "GB" };
			int order = 0;
			while (bytes >= 1024 && ++order < sizes.Length) {
				bytes = bytes / 1024;
			}

			// Adjust the format string to your preferences. For example "{0:0.#}{1}" would
			// show a single decimal place, and no space.
			return string.Format("{0:0.##} {1}", bytes, sizes[order]);
		}

		public static string FormatBytes(ulong bytes) {
			string[] sizes = { "B", "KB", "MB", "GB" };
			int order = 0;
			while (bytes >= 1024 && ++order < sizes.Length) {
				bytes = bytes / 1024;
			}

			return string.Format("{0:0.##} {1}", bytes, sizes[order]);
		}

		public static string FormatTimespan(TimeSpan span) {
			string txt = string.Empty;

			if (span.TotalDays > 1) txt += Math.Floor(span.TotalDays) + (span.TotalDays >= 2 ? " days " : " day ");
			if (span.Hours > 1) txt += span.Hours + (span.Hours >= 2 ? " hours " : " hour ");
			if (span.Minutes > 1) txt += span.Minutes + (span.Minutes >= 2 ? " minutes " : " minute ");
			if (span.Seconds > 1) txt += span.Seconds + (span.Seconds >= 2 ? " seconds " : " second ");

			return txt == string.Empty ? "less than a second" : txt.Trim();
		}

		/// <summary>
		/// <para>Splits a message into an array.</para>
		/// <para>Keeps each message length to a maximum length of <paramref name="maxLength"/></para>
		/// <para>The returned array may contain multiple lines within the same message, seperated by the newline character (\n)</para>
		/// </summary>
		/// <param name="lines">The text to be combined into messages, seperated into lines</param>
		/// <param name="maxLength">The maximum length of each message. Discord's maximum per message is 2000</param>
		/// <returns>An array with messages that can be sent without "TooManyCharacters" error.</returns>
		public static string[] SplitMessage(string[] lines, int maxLength) {
			List<string> messages = new List<string>();
			string current = string.Empty;

			for (int i = 0; i < lines.Length; i++) {
				string str = lines[i];
				while (str != null) {

					// Check if theres room. +1 is to count for the newline character
					if (
						(current == string.Empty
						? current.Length + str.Length
						: current.Length + str.Length + 1
						) > maxLength) {
						// There isn't room, adjustment is needed
						if (str.Length > maxLength) {
							// This message is too long. Cut it
							if (current == string.Empty) {
								current = str.Substring(0, maxLength);
								str = str.Substring(maxLength);
							} else {
								int index = maxLength - current.Length;
								current += "\n" + str.Substring(0, index);
								str = str.Substring(index);
							}
							messages.Add(current);
							current = string.Empty;
							if (str == string.Empty)
								str = null;
						} else {
							// Message doesn't fit in this message, skip to next one
							messages.Add(current);
							current = str;
							str = null;
						}
					} else {
						// There is room, just jump right in
						if (current == string.Empty) current = str;
						else current += "\n" + str;
						str = null;
					}

				}
			}
			if (current != string.Empty)
				messages.Add(current);

			return messages.ToArray();
		}

		private static readonly Regex _uriQueryRegex = new Regex(@"[?|&]([\w\.]+)=([^?|^&]+)");
		public static IReadOnlyDictionary<string, string> ParseQuery(this Uri uri) {
			var match = _uriQueryRegex.Match(uri.PathAndQuery);
			var paramaters = new Dictionary<string, string>();
			while (match.Success) {
				paramaters.Add(match.Groups[1].Value, match.Groups[2].Value);
				match = match.NextMatch();
			}
			return paramaters;
		}
	}
}
