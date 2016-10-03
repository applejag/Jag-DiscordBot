using Discord;
using DiscordBot.Modules;
using DiscordBot.Utility;
using Nito.AsyncEx.Synchronous;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot {
	public sealed class Bot : IDisposable {

		internal readonly static string[] whitelist = {
			"applejag#6330",
			"Love Yaa#9105",
			//"Slamakans#7788",
			"Ralev#6393",
			"Kalleballe#9565"
		};

		private readonly string token;
		public readonly bool isSelfbot;
		public readonly DateTime activeSince;

		public DiscordClient client { get; private set; }
		public bool valid { get; private set; }
		public readonly bool initialized;
		public List<Command> commands { get; private set; }
		public List<Module> modules { get; private set; }

		public Bot(string token) {
			this.token = token;
			isSelfbot = token.Length > 70;
			activeSince = DateTime.Now;

			try {
				DateTime start = DateTime.Now;

				LogHelper.LogCenter("Starting up bot...", ConsoleColor.Green);
				LogHelper.LogInformation("Starting initialization");

				Task task = InitClientAsync();
				task.WaitAndUnwrapException();

				if (valid) {
					InitModules();

					double totalSeconds = (DateTime.Now - start).TotalSeconds;
					initialized = true;
					LogHelper.LogSuccess(string.Format("Initialization complete! (took {0:0.00} {1})", totalSeconds, totalSeconds == 1d ? "second" : "seconds"));
				} else {
					LogHelper.LogFailure("Initialization failed...");
					try {
						Dispose();
					} catch (Exception err) {
						LogHelper.LogException("Error while cleaning up!", err);
					}
				}
			} catch (Exception err) {
				LogHelper.LogException("Error during initialization!", err);
			} finally {
				LogHelper.LogCenter("Bot initialization complete!", ConsoleColor.Green);
			}
		}

		public void InitModules() {
			commands = new List<Command>();

			LogHelper.LogInformation("Initializing modules...");

			modules = new List<Module>();

			#region <Modules> All bots
			// Modules for both selfbots and bot accounts
			modules.Add(new CommandHandler());
			#endregion

			#region <Modules> Selfbot only
			if (isSelfbot) {
				// Selfbot-only modules
				modules.Add(new ImageStorage());
				modules.Add(new ImageGenerator());
				modules.Add(new Eval());
				modules.Add(new Admin("self"));
				modules.Add(new LuaEval());
			}
			#endregion

			#region <Modules> Non-selfbot only
			if (!isSelfbot) {
				// Bot acc-only modules
				modules.Add(new League());
				modules.Add(new Admin("bot"));
				modules.Add(new DuckHorn());
				modules.Add(new MusicBot());
			}
			#endregion

			modules.ForEach((m) => {
				try {
					LogHelper.LogInformation("Initializing module \"" + m.GetType().Name + "\"...");
					m.bot = this;
					m.Init();
					LogHelper.LogSuccess("Successfully initialized module \"" + m.GetType().Name + "\".");
				} catch (Exception err) {
					LogHelper.LogException("Error initializing module \"" + m.GetType().Name + "\".", err);
				}
			});

			LogHelper.LogSuccess("Modules has been initialized!");
		}

		private async Task InitClientAsync() {
			try {
				LogHelper.LogInformation("Connecting to discord...");
				client = new DiscordClient();
				await client.Connect(token, isSelfbot ? TokenType.User : TokenType.Bot);

				LogHelper.LogSuccess("Connection established!");
				valid = true;
			} catch (Exception err) {
				valid = false;
				LogHelper.LogException("Error initializing bot client!", err);
			}
		}
		
		public void Dispose() {
			LogHelper.LogCenter("Shutting down bot...", ConsoleColor.Green);
			valid = false;
			LogHelper.LogInformation("Unloading modules...");
			UnloadModules();

			if (client != null) {
				LogHelper.LogInformation("Disconnecting client...");

				Task task = client.Disconnect();
				task.WaitWithoutException();

				client.Dispose();
				client = null;
				LogHelper.LogSuccess("Disconnected.");
			}
			LogHelper.LogCenter("Bot has been shut down", ConsoleColor.Green);
		}

		public void UnloadModules() {
			if (modules != null) {
				modules.ForEach((m) => {
					try {
						m.Unload();
						LogHelper.LogSuccess("Unloaded module \"" + m.GetType().Name + "\".");
					} catch (Exception err) {
						LogHelper.LogException("Error unloading module \"" + m.GetType().Name + "\".", err);
					}
				});

				modules.Clear();
			}

			if (commands != null) {
				var count = commands.Count;
				if (count > 0) 
					LogHelper.LogWarning(count + (count == 1 ? " command" : " commands") + " did not get properly removed...");

				commands.Clear();
			}
		}

		internal bool CheckIsWhitelisted(User user) {
			return whitelist.Contains(user.Name + "#" + user.Discriminator);
		}
	}
}
