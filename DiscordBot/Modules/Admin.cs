using Discord;
using DiscordBot.Utility;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Modules {
	public sealed class Admin : Module {
		public override string modulePrefix { get { return prefix; } }
		private readonly string prefix;

		public Admin(string prefix) {
			this.prefix = prefix;
		}

		public override void Init() {
			AddCommand(cmdClear);
			AddCommand(cmdLog);
			AddCommand(cmdStatus);
			AddCommand(cmdRestart);
		}

		public override void Unload() {
			RemoveCommand(cmdClear);
			RemoveCommand(cmdLog);
			RemoveCommand(cmdStatus);
			RemoveCommand(cmdRestart);
		}

		#region Command definitions
		private CmdClear cmdClear = new CmdClear();
		public sealed class CmdClear : Command<Admin> {
			public override string name { get; } = "clear";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override bool useModulePrefix { get; } = false;
			public override string description { get; } = "Removes the most recent message, or the most X recent messages, where X is the argument you supply. The count is excluding the command message itself.";
			public override string usage { get; } = "[count]";
			public override string[] alias { get; internal set; } = { "c" };

			private static List<ulong> tasks = new List<ulong>();
			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length > 2) return false;
				// Only run if the other is not. So not multiple bots try to remove the same message

				// Check if it's already being handled
				if (tasks.Contains(e.Message.Id)) return true;

				// Check if non-private channel
				if (bot.isSelfbot && !e.Channel.IsPrivate) {
					// Check if there is any non-selfbots in this channel
					if (Program.bots.Any(b => b != bot && !b.isSelfbot && e.Channel.Users.Any(u => u.Id == b.client.CurrentUser.Id))) {
						// Return true 'cause we dont want the usage to show
						return true;
					}
				}
				// Only run selfbot in private channels
				if (!bot.isSelfbot && e.Channel.IsPrivate) {
					// Check if there is any selfbots in this channel
					if (Program.bots.Any(b => b != bot && b.isSelfbot && e.Channel.Users.Any(u => u.Id == b.client.CurrentUser.Id))) {
						// Return true 'cause we dont want the usage to show
						return true;
					} else
						return false;
				}
				// First-one-to-the-boat for the non-selfbots
				if (!bot.isSelfbot && !e.Channel.IsPrivate) {
					tasks.Add(e.Message.Id);
				}

				// Start clearin
				int count = 2;

				if (args.Length > 1) {
					if (int.TryParse(args[1], out count))
						count = Math.Max(Math.Min(Math.Abs(count + 1), 200), 2);
					else {
						await DynamicSendMessage(e, "Unable to interperate the clear count. Please specify a valid integer.");
						return false;
					}
				}

				try {
					LogHelper.LogInformation("Started clearing chat...");
					Thread.Sleep(300);

					Message[] messages;
					try {
						messages = await e.Channel.DownloadMessages(limit: count);
					} catch (Exception err) {
						await DynamicSendMessage(e, "An error occurred while fetching messages. *Maybe you tried deleteting too many?*\n```" + err.Message + "```");
						throw;
					}

					List<Message> list = new List<Message>(messages);
					list.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

					if (bot.isSelfbot) {
						for (int i = 0; i < Math.Min(count, list.Count); i++) {
							await list[i].SafeDelete();
						}
					} else {
						await e.Channel.DeleteMessages(list.GetRange(0, count).ToArray());
					}


					LogHelper.LogSuccess("Clearing complete.");
				} catch (Exception err) {
					LogHelper.LogException("Error while executing command 'clear'", err);
					await DynamicSendMessage(e, "An error occured while trying to perform this task!\n```" + err.Message + "```");
					throw;
				} finally {
					tasks.Remove(e.Message.Id);
				}
				return true;
			}
		}

		private CmdLog cmdLog = new CmdLog();
		public sealed class CmdLog : Command<Admin> {
			public override string name { get; } = "log";
			public override CommandPerm requires { get; } = CommandPerm.Selfbot;
			public override string description { get; } = "Sends the console log for the bot in the current channel.\nWarning! As there probably is quite the log, this message may be counted as spam and consequenses from the servers moderators may apply. Be warned.";
			public override string usage { get; } = "";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length > 1) return false;
				DateTime start = DateTime.Now;

				await e.Message.SafeDelete();

				string[] messages = StringHelper.SplitMessage(LogHelper.log.ToArray(), 1984);

				// Start sending the messages
				LogHelper.LogWarning("Starting to send log to user. This may take a while...");
				await e.Channel.SafeSendMessage("**Bots console log** *(approx " + LogHelper.log.Count + " lines)*");
				for (int i = 0; i < messages.Length; i++) {
					await e.Channel.SafeSendMessage("```" + messages[i] + "```");
				}
				TimeSpan time = DateTime.Now - start;
				LogHelper.LogSuccess(string.Format("Finished sending log (took {0:0.00} seconds)", time.TotalSeconds));
				await e.Channel.SafeSendMessage(string.Format("**Submission of log is now complete.** _(took {0:0.00} seconds)_", time.TotalSeconds));
				return true;
			}
		}

		private CmdStatus cmdStatus = new CmdStatus();
		public sealed class CmdStatus : Command<Admin> {
			public override string name { get; } = "status";
			public override CommandPerm requires { get; } = CommandPerm.Selfbot;
			public override string description { get; } = "Gives you the current status of the process. Including RAM and CPU usage, as well as active modules and current runtime.";
			public override string usage { get; } = "";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length > 1) return false;

				Message status = await DynamicSendMessage(e, "`" + e.Message.RawText + "`\n*Calculating stats...*");
				await e.Channel.SendIsTyping();

				float cpuUsage = ComputerHelper.GetCPUUsage();
				long allocatedMemory = ComputerHelper.GetAllocatedMemory();
				long freeMemory = ComputerHelper.GetAvailableMemory();

				await status.SafeEdit("`" + e.Message.RawText + "`\n**Status for bot running " + bot.client.CurrentUser.Mention + "**\n```\n"
					+ "Online since: " + bot.activeSince.ToString("yyyy-MM-dd HH:mm:ss") + "\n"
					+ "Online for: " + StringHelper.FormatTimespan(DateTime.Now - bot.activeSince) + "\n"
					+ string.Format("CPU usage: {0:n2} %", cpuUsage) + "\n"
					+ string.Format("Used Memory: {0} ({1} B)", StringHelper.FormatBytes(allocatedMemory), StringHelper.FormatThousands(allocatedMemory)) + "\n"
					+ string.Format("Unallocated Memory: {0} ({1} B)", StringHelper.FormatBytes(freeMemory), StringHelper.FormatThousands(freeMemory)) + "\n"
					+ Program.bots.Sum(b =>
						"\n[Bot] " + b.client.CurrentUser.Name + "\n"
						+ "- Modules active: " + b.modules.Count + "\n"
						+ (b.modules.Count > 0 ? b.modules.Sum(mod => ", " + mod.GetType().Name).TrimStart(',', ' ') + "\n" : string.Empty)
						+ "- Commands active: " + b.commands.Count + "\n"
						+ (b.commands.Count > 0 ? b.commands.Sum(cmd => ", " + cmd.id).TrimStart(',', ' ') + "\n" : string.Empty)
						+ "- Servers active: " + b.client.Servers.Count() + "\n"
						+ "- Users: " + b.client.Servers.Sum(s => s.Users.Count(u => u.Status == UserStatus.Online))
						+ " online out of " + b.client.Servers.Sum(s => s.Users.Count()) + " users\n"
						)
					+ "```" );

				await Task.Delay(300);

				using (Image image = ImageGenerator.GenerateGraph("CPU usage % during last 60 minutes", ComputerHelper.GetCPUHistory(), TimeSpan.FromSeconds(ComputerHelper.HISTORY_MAX_AGE_SECONDS), TimeSpan.FromMinutes(1)))
					await e.Channel.SendImage(image);
				return true;
			}
		}

		private CmdRestart cmdRestart = new CmdRestart();
		public sealed class CmdRestart : Command<Admin> {
			public override string name { get; } = "restart";
			public override CommandPerm requires { get; } = CommandPerm.Selfbot;
			public override string description { get; } = "Restarts the bots running on this process thread and reloads modules. Takes around 20-30 seconds.\nDangerous! Make sure to have a moderator or similar that can do a proper reboot in case it crashes.";
			public override string usage { get; } = "";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length > 1) return false;
				try {
					Message status = await DynamicSendMessage(e, "Restarting... *(this may take a while)*");
					Program.RestartBots();
					await DynamicEditMessage(status, e.User, "Bot has restarted!");
				} finally {
					Program.ShutdownBots();
				}
				return true;
			}
		}
		#endregion

	}
}
