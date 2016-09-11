using Discord;
using DiscordBot.Utility;
using System;
using System.Collections.Generic;
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

			private static List<ulong> tasks = new List<ulong>();
			public override async Task Callback(MessageEventArgs e, string[] args, string rest) {
				// Only run if the other is not. So not multiple bots try to remove the same message

				// Only run selfbot in private channels
				if (!bot.isSelfbot && e.Channel.IsPrivate) return;
				// Check if non-private channel
				if (bot.isSelfbot && !e.Channel.IsPrivate) {
					// Check if there is any non-selfbots in this channel
					if (Program.bots.Any(b => b != bot && !b.isSelfbot && e.Channel.Users.Any(u => u.Id == b.client.CurrentUser.Id))) {
						return;
					}
				}
				// First-one-to-the-boat for the non-selfbots
				if (!bot.isSelfbot && !e.Channel.IsPrivate) {
					if (!tasks.Contains(e.Message.Id))
						tasks.Add(e.Message.Id);
					else
						return;
				}

				// Start clearin
				int count = 2;

				if (args.Length > 1) {
					if (int.TryParse(args[1], out count))
						count = Math.Max(Math.Min(Math.Abs(count + 1), 200), 2);
					else {
						await DynamicSendMessage(e, "Unable to interperate the clear count. Please specify a valid integer.");
						return;
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
						return;
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
					try {
						await DynamicSendMessage(e, "An error occured while trying to perform this task!\n```" + err.Message + "```");
					} catch { }
				} finally {
					tasks.Remove(e.Message.Id);
				}
			}
		}

		private CmdLog cmdLog = new CmdLog();
		public sealed class CmdLog : Command<Admin> {
			public override string name { get; } = "log";
			public override CommandPerm requires { get; } = CommandPerm.Selfbot;

			public override async Task Callback(MessageEventArgs e, string[] args, string rest) {
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
			}
		}

		private CmdStatus cmdStatus = new CmdStatus();
		public sealed class CmdStatus : Command<Admin> {
			public override string name { get; } = "status";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;

			public override async Task Callback(MessageEventArgs e, string[] args, string rest) {
				Message status = await DynamicSendMessage(e, "Calculating stats...");
				await e.Channel.SendIsTyping();

				float cpuUsage = ComputerHelper.GetCPUUsage();
				long allocatedMemory = ComputerHelper.GetAllocatedMemory();
				long freeMemory = ComputerHelper.GetAvailableMemory();

				await status.SafeEdit("**Status for bot running " + bot.client.CurrentUser.Mention + "**\n```"
					+ "Online since: " + bot.activeSince.ToString("yyyy-MM-dd HH:mm:ss") + "\n"
					+ "Online for: " + StringHelper.FormatTimespan(DateTime.Now - bot.activeSince) + "\n"
					+ string.Format("CPU usage: {0:n2} %", cpuUsage) + "\n"
					+ string.Format("Used Memory: {0} ({1} B)", StringHelper.FormatBytes(allocatedMemory), StringHelper.FormatThousands(allocatedMemory)) + "\n"
					+ string.Format("Unallocated Memory: {0} ({1} B)", StringHelper.FormatBytes(freeMemory), StringHelper.FormatThousands(freeMemory)) + "\n"
					+ "Modules active: " + bot.modules.Count + "\n"
					+ (bot.modules.Count > 0 ? bot.modules.ToString(mod => ", " + mod.GetType().Name).TrimStart(',', ' ') + "\n" : string.Empty)
					+ "Commands active: " + bot.commands.Count + "\n"
					+ (bot.commands.Count > 0 ? bot.commands.ToString(cmd => ", " + cmd.id).TrimStart(',', ' ') + "\n" : string.Empty)
					+ "```");
			}
		}

		private CmdRestart cmdRestart = new CmdRestart();
		public sealed class CmdRestart : Command<Admin> {
			public override string name { get; } = "restart";
			public override CommandPerm requires { get; } = CommandPerm.Selfbot;

			public override async Task Callback(MessageEventArgs e, string[] args, string rest) {
				try {
					Message status = await DynamicSendMessage(e, "Restarting... *(this may take a while)*");
					Program.RestartBots();
					await DynamicEditMessage(status, e.User, "Bot has restarted!");
				} catch {

				} finally {
					Program.ShutdownBots();
				}
			}
		}
		#endregion

	}
}
