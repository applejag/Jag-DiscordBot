using DiscordBot.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot {
	public static class UserInterface {
		static string inputLine = "";

		static Dictionary<string, Action<string[]>> commands = new Dictionary<string, Action<string[]>> {
			{ "help", (args) => {
				LogHelper.LogRawText("[!] Available commands:", ConsoleColor.Yellow);
				foreach (var cmd in commands) {
					LogHelper.ClearLine();
					Console.ForegroundColor = ConsoleColor.Gray;
					Console.Write("- ");
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine(cmd.Key);
				}
			} },
			{ "stop", (args) => {
				Program.ShutdownBots();
			} },
			{ "restart", async (args) => {
				Program.ShutdownBots();
				await Task.Delay(1500);
				Program.StartupBots();
			} }
		};

		public static string[] AskForTokens() {
			bool running = true;
			string err = null;
			List<string> tokens;

			if (SaveData.singleton.Bot_tokens != null && SaveData.singleton.Bot_tokens.Length > 0)
				tokens = new List<string>(SaveData.singleton.Bot_tokens);
			else
				tokens = new List<string>();

			do {
				Console.Clear();

				// Error message
				if (err != null) {
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("[ " + err + " ]");
					err = null;
				}

				// List tokens list (what?)
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine("Current chosen tokens:");

				if (tokens.Count > 0) {
					for (int i = 0; i < tokens.Count; i++) {
						Console.ForegroundColor = ConsoleColor.Green;
						Console.Write(i + ". ");
						Console.ForegroundColor = ConsoleColor.Cyan;
						Console.Write("\"");
						Console.ForegroundColor = ConsoleColor.Gray;
						Console.Write(tokens[i]);
						Console.ForegroundColor = ConsoleColor.Cyan;
						Console.WriteLine("\"");
					}
				} else {
					Console.ForegroundColor = ConsoleColor.Gray;
					Console.WriteLine("< no tokens >");
				}

				// List commands
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine("\nAvailable commands:");
				Console.ForegroundColor = ConsoleColor.Green;
				Console.WriteLine("add <token>");
				Console.WriteLine("remove <token id>");
				if (tokens.Count > 0)
					Console.WriteLine("done");
				
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.Write("\n> ");
				Console.ForegroundColor = ConsoleColor.White;

				// Read & interpret input
				string[] input = Console.ReadLine().Split(' ');
				
				if (input.Length == 0) {
					err = "Please enter a command!";
				} else {
					switch (input[0].ToLower()) {
						case "add":
							if (input.Length != 2 && input.Length != 3)
								err = "Please enter a valid command!";
							else {
								string t = string.Join(" ", input.SubArray(1));
								string pattern = "^[a-zA-Z0-9\\.\\-]*$";
								if (t.Length == 88 && Regex.IsMatch(t, pattern))
									tokens.Add(t);
								else if (t.Length == 63 && t.StartsWith("Bot ") && Regex.IsMatch(t.Substring(4), pattern))
									tokens.Add(t);
								else if (t.Length == 59 && Regex.IsMatch(t, pattern))
									tokens.Add("Bot " + t);
								else
									err = "Please enter a valid token!";
							}
							break;

						case "remove":
							if (input.Length != 2)
								err = "Please enter a valid command!";
							else {
								int id = -1;
								if (int.TryParse(input[1], out id)) {
									if (id >= 0 && id < tokens.Count)
										tokens.RemoveAt(id);
									else
										err = "Please enter a valid integer ID!";
								}
								else
									err = "Please enter a valid integer ID!";
							}
							break;

						case "done":
							if (tokens.Count == 0 || input.Length > 1)
								err = "Please enter a valid command!";
							else
								running = false;
							break;

						default:
							err = "Please enter a valid command!";
							break;
					}
				}

			} while (running);

			Console.Clear();

			SaveData.singleton.Bot_tokens = tokens.ToArray();
			return SaveData.singleton.Bot_tokens;
		}

		public static void MainLoop() {
			while (Program.bots != null && Program.bots.All(bot => bot != null && bot.valid)) {
				DrawInputLine();

				ConsoleKeyInfo info = Console.ReadKey(true);
				if (Program.bots == null || Program.bots.Any(bot => bot == null || !bot.valid)) break;

				if (char.IsControl(info.KeyChar)) {
					switch (info.Key) {
						case ConsoleKey.Backspace:
							if (inputLine.Length > 0)
								inputLine = inputLine.Substring(0, inputLine.Length - 1);
							break;

						case ConsoleKey.Enter:
							HandleInput();
							inputLine = "";
							break;
					}
				} else {
					inputLine += info.KeyChar;
				}
			}
		}

		public static void DrawInputLine() {
			if (Program.bots == null || Program.bots.Any(bot => bot == null || !bot.valid)) return;

			int ypos = Console.CursorTop;
			int xpos = 0;

			Console.BackgroundColor = ConsoleColor.Black;
			Console.SetCursorPosition(xpos, ypos);

			Console.ForegroundColor = ConsoleColor.DarkCyan;
			Console.Write("[Command] > ");
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.Write(inputLine.Substring(Math.Max(0, inputLine.Length - Console.WindowWidth + Console.CursorLeft + 1)));

			// Fill out rest of the line
			xpos = Console.CursorLeft;
			Console.Write(new string(' ', Console.WindowWidth - Console.CursorLeft));
			Console.SetCursorPosition(xpos, ypos);

			Console.ResetColor();
		}

		public static void HandleInput() {
			// Parse command
			string[] args = inputLine.Split();
			if (args.Length == 0) return;
			
			// Execute
			if (commands.ContainsKey(args[0])) {
				commands[args[0]](args);
			} else
				LogHelper.LogRawText("<!> Unknown command! (" + args[0] + "). Try 'help' to list all commands!", ConsoleColor.Red);
		}

		public static void PauseBeforeExit() {
			LogHelper.LogRawText("Press any button to exit");
			Console.ReadKey(false);
		}
	}
}
