
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace DiscordBot.Utility {
	public static class LogHelper {
		public static List<string> log { get; private set; } = new List<string>();

		private static string GetTimeStamp() {
			return DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss]");
		}

		private static ConsoleColor GetTimeColor() {
			return ConsoleColor.Gray;
		}

		private static string GetTypeTag(LogType type) {
			switch (type) {
				case LogType.Error: return "ERROR";
				case LogType.Failure: return "FAILURE";
				case LogType.Information: return "INFO";
				case LogType.Success: return "SUCCESS";
				case LogType.Warning: return "WARNING";
				case LogType.ExceptionHeader: return "!";
				case LogType.RawText: return "";
				default: throw new ArgumentException("Invalid LogType for log message!", "type");
			}
		}

		private static ConsoleColor GetTypeColor(LogType type) {
			switch (type) {
				case LogType.Error: return ConsoleColor.Red;
				case LogType.Failure: return ConsoleColor.Red;
				case LogType.Information: return ConsoleColor.White;
				case LogType.Success: return ConsoleColor.Green;
				case LogType.Warning: return ConsoleColor.Yellow;
				case LogType.ExceptionHeader: return ConsoleColor.Red;
				case LogType.RawText: return ConsoleColor.White;
				default: throw new ArgumentException("Invalid LogType for log message!", "type");
			}
		}

		public static void LogError(object o, [CallerFilePath] string callerPath = "") {
			Log(o, LogType.Error, callerPath);
			UserInterface.DrawInputLine();
		}

		public static void LogFailure(object o, [CallerFilePath] string callerPath = "") {
			Log(o, LogType.Failure, callerPath);
			UserInterface.DrawInputLine();
		}

		public static void LogInformation(object o, [CallerFilePath] string callerPath = "") {
			Log(o, LogType.Information, callerPath);
			UserInterface.DrawInputLine();
		}

		public static void LogSuccess(object o, [CallerFilePath] string callerPath = "") {
			Log(o, LogType.Success, callerPath);
			UserInterface.DrawInputLine();
		}

		public static void LogWarning(object o, [CallerFilePath] string callerPath = "") {
			Log(o, LogType.Warning, callerPath);
			UserInterface.DrawInputLine();
		}

		public static void LogRawText(object o) {
			LogRawText(o, GetTypeColor(LogType.RawText));
		}

		public static void LogRawText(object o, ConsoleColor color) {
			Console.ForegroundColor = color;
			Log(o, LogType.RawText, null);
		}

		public static void LogCenter(object o) {
			LogCenter(o, GetTypeColor(LogType.RawText));
		}

		public static void LogCenter(object o, ConsoleColor color) {
			string txt = o == null ? "null" : o.ToString();
			if (txt.Length > Console.WindowWidth - 6)
				txt = txt.Substring(0, Console.WindowWidth - 9) + "...";

			txt = string.Format("[ {0} ]", txt);
			txt = '='.Repeat((int)(Console.WindowWidth / 2d - txt.Length / 2d)) + txt;
			txt = txt + '='.Repeat(Console.WindowWidth - txt.Length);

			Console.ForegroundColor = color;
			Log(txt, LogType.RawText, null);
			Console.CursorTop--;
		}

		private static void Log(object o, LogType type, string callerPath) {
			ClearLine();
			if (type == LogType.RawText) {
				Console.WriteLine(o == null ? "null" : o.ToString());
				AddToLog(o);
			} else if (type == LogType.ExceptionHeader) {
				Console.ForegroundColor = GetTypeColor(type);
				string desc = string.Format("[{0}] {1}", GetTypeTag(type), o == null ? "null" : o.ToString());
				string title = string.Format("[{0}] {1}", GetTypeTag(type), new string('-', 16) + " Stacktrace: " + new string('-', 16));
				AddToLog(desc);
				AddToLog(title);
				Console.WriteLine(desc);
				Console.WriteLine(title);
			} else {
				
				string time = GetTimeStamp();
				string tag = string.Format(" [{0}/{1}]", Path.GetFileNameWithoutExtension(callerPath).Trim('_'), GetTypeTag(type));
				string message = ": " + (o == null ? "null" : o.ToString());
				AddToLog(time + tag + message);

				Console.ForegroundColor = GetTimeColor();
				Console.Write(time);
				Console.ForegroundColor = GetTypeColor(type);
				Console.Write(tag);
				Console.ForegroundColor = GetTypeColor(LogType.RawText);
				Console.WriteLine(message);
			}
		}

		public static void LogMultiline(object o, LogType type, [CallerMemberName] string callerName = "") {
			string value = o == null ? "null" : o.ToString();
			string[] lines = value.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);

			string timestamp = GetTimeStamp();
			string typetag = GetTypeTag(type);

			for (int i=0; i<lines.Length; i++) {
				Log(lines[i], type, callerName);
			}
			UserInterface.DrawInputLine();
		}

		private static void LogExceptionStack(object o) {
			string value = o == null ? "null" : o.ToString();
			string[] lines = value.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);

			Console.ForegroundColor = GetTypeColor(LogType.Error);
			ClearLine();

			for (int i = 0; i < lines.Length; i++) {
				Console.WriteLine(lines[i]);
				AddToLog(lines[i]);
			}
		}

		public static void LogException(object info, Exception err, [CallerFilePath] string callerPath = "") {
			Log(info, LogType.Error, callerPath: callerPath);
			Log(err.Message, LogType.ExceptionHeader, callerPath: callerPath);
			LogExceptionStack(err.StackTrace);

			UserInterface.DrawInputLine();
		}

		public static void ClearLine() {
			int pos = Console.CursorTop;
			Console.SetCursorPosition(0, pos);
			Console.Write(new string(' ', Console.WindowWidth));
			Console.SetCursorPosition(0, pos);
		}

		private static void AddToLog(object o) {
			log.AddRange((o == null ? "null" : o.ToString()).Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None));

			if (log.Count > 1000) log.RemoveRange(999, log.Count - 999);
		}
	}

	public enum LogType {
		/// <summary>
		/// An error event. This indicates a significant problem the user should know about; usually a loss of functionality or data.
		/// </summary>
		Error,

		/// <summary>
		/// A failure event. This indicates a security event that occurs when an audited access attempt fails; for example, a failed attempt to open a file.
		/// </summary>
		Failure,

		/// <summary>
		/// An information event. This indicates a significant, successful operation.
		/// </summary>
		Information,

		/// <summary>
		/// A success event. This indicates a security event that occurs when an audited access attempt is successful; for example, logging on successfully.
		/// </summary>
		Success,

		/// <summary>
		/// A warning event. This indicates a problem that is not immediately significant, but that may signify conditions that could cause future problems.
		/// </summary>
		Warning,

		/// <summary>
		/// The start of an exception. This is used within the LogException
		/// </summary>
		ExceptionHeader,

		/// <summary>
		/// Write out raw text. No formatting.
		/// Equal to Console.WriteLine() but with custom color.
		/// </summary>
		RawText,
	}
}
