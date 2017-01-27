using Discord;
using DiscordBot.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules {
	public sealed class Logger : Module {

		public override string modulePrefix { get; } = "log";
		public override string description { get; } = "This module allows you to log a lot from a server. From messages to user updates.";

		public List<LoggedChannel> logs;

		public override void Init() {
			if (SaveData.singleton.Logged_channels == null)
				logs = new List<LoggedChannel>();
			else
				logs = new List<LoggedChannel>(SaveData.singleton.Logged_channels);

			AddCommand(cmdAdd);
			AddCommand(cmdList);
			AddCommand(cmdDel);

			client.MessageReceived += Client_MessageReceived;
			client.MessageUpdated += Client_MessageUpdated;
			client.MessageDeleted += Client_MessageDeleted;
			client.UserJoined += Client_UserJoined;
			client.UserLeft += Client_UserLeft;
			client.UserBanned += Client_UserBanned;
			client.UserUnbanned += Client_UserUnbanned;
			client.UserUpdated += Client_UserUpdated;
		}

		public override void Unload() {
			SaveData.singleton.Logged_channels = logs.ToArray();

			RemoveCommand(cmdAdd);
			RemoveCommand(cmdList);
			RemoveCommand(cmdDel);

			client.MessageReceived -= Client_MessageReceived;
			client.MessageUpdated -= Client_MessageUpdated;
			client.MessageDeleted -= Client_MessageDeleted;
			client.UserJoined -= Client_UserJoined;
			client.UserLeft -= Client_UserLeft;
			client.UserBanned -= Client_UserBanned;
			client.UserUnbanned -= Client_UserUnbanned;
			client.UserUpdated -= Client_UserUpdated;
		}

		#region Event listeners
		private async void TellChannels(string message, Server server, What level) {
			if (server == null) return;
			for (int i=0; i<logs.Count; i++) {
				var log = logs[i];
				// Skip if it's not in the required level
				if ((log.level & (int) level) == 0) continue;
				// Skip if server or channel wasn't found, or if it's the wrong server
				Server s = client.GetServer(log.server);
				Channel c = s?.GetChannel(log.channel);
				if (s == null || c == null || server.Id != s.Id) continue;
				// Send message
				await c.SafeSendMessage(":notepad_spiral: `" + LogHelper.GetTimeStamp() + "` " + message);
			}
		}

		private void Client_UserUpdated(object sender, UserUpdatedEventArgs e) {
			try {
				if (e.After.Nickname != e.Before.Nickname) {
					// Nickname changed
					TellChannels(string.Format("User **{0}** changed nickname from \"_{1}_\" to \"**{2}**\"!", e.After.Name, string.IsNullOrEmpty(e.Before.Nickname) ? "null" : e.Before.Nickname, e.After.Nickname), e.Server, What.user_nickname_change);
				}
			} catch (Exception err) {
				LogHelper.LogException("Error on logging user changed!", err);
			}
		}

		private void Client_UserUnbanned(object sender, UserEventArgs e) {
			try { 
				TellChannels(string.Format("User **{0}**{1} just got **unbanned**!", e.User.Name, string.IsNullOrEmpty(e.User.Nickname) ? string.Empty : " *(" + e.User.Nickname + ")*"), e.Server, What.user_unbanned);
			} catch (Exception err) {
				LogHelper.LogException("Error on logging user unbanned!", err);
			}
		}

		private void Client_UserBanned(object sender, UserEventArgs e) {
			try { 
				TellChannels(string.Format("User **{0}**{1} just got **banned**!", e.User.Name, string.IsNullOrEmpty(e.User.Nickname) ? string.Empty : " *(" + e.User.Nickname + ")*"), e.Server, What.user_banned);
			} catch (Exception err) {
				LogHelper.LogException("Error on logging user banned!", err);
			}
		}

		private void Client_UserLeft(object sender, UserEventArgs e) {
			try {
				TellChannels(string.Format("User **{0}**{1} just **left the server**!", e.User.Name, string.IsNullOrEmpty(e.User.Nickname) ? string.Empty : " *(" + e.User.Nickname + ")*"), e.Server, What.user_left_server);
			} catch (Exception err) {
				LogHelper.LogException("Error on logging user left!", err);
			}
		}

		private void Client_UserJoined(object sender, UserEventArgs e) {
			try {
				TellChannels(string.Format("User **{0}**{1} just **joined the server**!", e.User.Name, string.IsNullOrEmpty(e.User.Nickname) ? string.Empty : " *(" + e.User.Nickname + ")*"), e.Server, What.user_joined_server);
			} catch (Exception err) {
				LogHelper.LogException("Error on logging user joined!", err);
			}
		}

		private void Client_MessageDeleted(object sender, MessageEventArgs e) {
			try {
				// Ignore messages from same channel
				if (ChannelExists(e.Channel)) return;

				string msg = string.Format("Message from **{0}**{1} just got **deleted**",
					e.Message?.User?.Name ?? "N/A",
					string.IsNullOrEmpty(e.Message?.User?.Nickname) ? string.Empty : " _(" + e.Message.User.Nickname + ")_"
				);

				if (!string.IsNullOrEmpty(e.Message.Text)) {
					if (e.Message.Text.Length < 1000)
						msg += "\nMessage:```\n" + e.Message.Text + "\n```";
					else
						msg += "\nMessage:```\n" + e.Message.Text.Substring(0, 999) + "\n... (cropped)\n```";
				}

				for (int i = 0; i < e.Message.Embeds.Length; i++) {
					var em = e.Message.Embeds[i];
					msg += "\nEmbed nr" + i + ": `" + em.Title + "`";
				}

				TellChannels(msg, e.Server, What.message_delete);
			} catch (Exception err) {
				LogHelper.LogException("Error on logging message deleted!", err);
			}
		}

		private void Client_MessageUpdated(object sender, MessageUpdatedEventArgs e) {
			try {
				// Ignore messages from same channel
				if (ChannelExists(e.Channel)) return;

				string msg = string.Format("Message from **{0}**{1} just got **edited**.",
					e.User?.Name ?? "N/A",
					string.IsNullOrEmpty(e.User?.Nickname) ? string.Empty : " *(" + e.User.Nickname + ")*"
				);

				if (!string.IsNullOrEmpty(e.Before.Text)) {
					if (e.Before.Text.Length < 1000)
						msg += "\nMessage before change:```\n" + e.Before.Text + "\n```";
					else
						msg += "\nMessage before change:```\n" + e.Before.Text.Substring(0, 999) + "\n... (cropped)\n```";
				}

				if (!string.IsNullOrEmpty(e.After.Text)) {
					if (e.After.Text.Length < 1000)
						msg += "\nMessage after change:```\n" + e.After.Text + "\n```";
					else
						msg += "\nMessage after change:```\n" + e.After.Text.Substring(0, 999) + "\n... (cropped)\n```";
				}

				TellChannels(msg, e.Server, What.message_changed);
			} catch (Exception err) {
				LogHelper.LogException("Error on logging message changed!", err);
			}
		}

		private void Client_MessageReceived(object sender, MessageEventArgs e) {
			try { 
				// Ignore messages from same channel
				if (ChannelExists(e.Channel)) return;

				string msg = string.Format("Message from **{0}**{1} just got **sent**",
					e.User.Name,
					string.IsNullOrEmpty(e.User.Nickname) ? string.Empty : " *(" + e.User.Nickname + ")*"
				);

				if (!string.IsNullOrEmpty(e.Message.Text)) {
					if (e.Message.Text.Length < 1000)
						msg += "\nMessage:```\n" + e.Message.Text + "\n```";
					else
						msg += "\nMessage:```\n" + e.Message.Text.Substring(0, 999) + "\n... (cropped)\n```";
				}

				for (int i = 0; i < e.Message.Embeds.Length; i++) {
					var em = e.Message.Embeds[i];
					msg += "\nEmbed nr" + i + ": `" + em.Title + "`";
				}

				TellChannels(msg, e.Server, What.message_sent);
			} catch (Exception err) {
				LogHelper.LogException("Error on logging message sent!", err);
			}
		}
		#endregion

		#region Command definitions
		private CmdAdd cmdAdd = new CmdAdd();
		public sealed class CmdAdd : Command<Logger> {
			public override string name { get; } = "add";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string usage { get; } = "<level 1> [level 2] [level 3] [level n]";
			public override string description => "Adds a channel to be logged."
				+ "\nThe channel to use is the one where this command is executed, and the level(s) chosen dictates what will be logged."
				+ "\nNote: You may combine multiple logs!"
				+ "\nLevels to choose from:\n"
				+ Enum.GetNames(typeof(What)).Sum(n=>n=="none"?string.Empty:"- "+n+"\n")
				+ "\nExample:\n"
				+ module.modulePrefix + " " + name + " message_changed";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length == 1) return false;

				What what = What.none;
				string[] levels = Enum.GetNames(typeof(What));
				for (int i=1; i<args.Length; i++) {
					args[i] = args[i].ToLower();

					if (!levels.Contains(args[i]) && args[i] != "none") {
						await DynamicSendMessage(e, ":notepad_spiral: **Invalid level type!** _(\"" + args[i] + "\")_");
						return true;
					}

					what |= (What)Enum.Parse(typeof(What), args[i]);
				}

				if (me.ChannelExists(e.Channel)) {
					me.RemoveChannel(e.Channel);
					me.AddChannel(e.Channel, what);
					await DynamicSendMessage(e, ":notepad_spiral: **Replaced earlier existing channel settings.**");
					LogHelper.LogInformation("Updated " + e.Channel.Name + " with new levels!");
				} else {
					me.AddChannel(e.Channel, what);
					await DynamicSendMessage(e, ":notepad_spiral: **Added channel settings.**");
					LogHelper.LogInformation("Added " + e.Channel.Name + " to logged channels list!");
				}

				me.logs.Sort();
				me.SaveLogs();

				return true;
			}
		}


		private CmdList cmdList = new CmdList();
		public sealed class CmdList : Command<Logger> {
			public override string name { get; } = "list";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string usage { get; } = "[--global]";
			public override string description { get; } = "Lists all current logged channels."
				+ "\nIf supplied with '--global' as an argument it will list for all servers.";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length > 2) return false;
				if (args.Length == 2 && args[1].ToLower() != "--global") {
					await DynamicSendMessage(e, ":notepad_spiral: **Unknown flag!** _(\"" + args[1] + "\")_");
					return true;
				}

				bool global = args.Length == 2;
				me.logs.Sort();

				string output = string.Empty;
				string lastServer = null;
				int zeros = me.logs.Count.ToString().Length;

				for (int i = 0; i < me.logs.Count; i++) {
					var c = me.logs[i];
					string serverName = bot.client.GetServer(c.server)?.Name ?? ("UNKNOWN _(" + c.server + ")_");
					string channelName = bot.client.GetChannel(c.channel)?.Name ?? ("UNKNOWN _(" + c.channel + ")_");

					if (lastServer != serverName) {
						output += "**Server: " + serverName + "**\n";
					}

					output += i.ToString("D" + zeros) + ". *" + channelName + "* `0b" + Convert.ToString(c.level, 2).PadLeft(8,'0') + "`\n";

					lastServer = serverName;
				}

				string[] messages = StringHelper.SplitMessage(output.Split('\n'), DiscordHelper.MESSAGE_LENGTH_LIMIT);
				for (int i = 0; i < messages.Length; i++) {
					await e.Channel.SafeSendMessage(messages[i]);
				}

				await e.Channel.SafeSendMessage(":notepad_spiral: **Sending complete!** Total: " + me.logs.Count + " channels");

				return true;
			}
		}

		private CmdDel cmdDel = new CmdDel();
		public sealed class CmdDel : Command<Logger> {
			public override string name { get; } = "del";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string usage { get; } = "<index>";
			public override string description => "Removes a channel from being logged."
				+ "\nThe index argument can be found by doing the list command, ex:"
				+ me.cmdList.fullUsage;
			public override string[] alias { get; internal set; } = { "remove", "rem", "delete" };

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length != 2)
					return false;

				int index;
				if (!int.TryParse(args[1], out index)) {
					await DynamicSendMessage(e, ":notepad_spiral: **Invalid index value!** _(Unable to parse to int32)_");
					return true;
				}
				
				if (me.RemoveChannel(index)) {
					await DynamicSendMessage(e, ":notepad_spiral: **Channel has been removed from logging!**"
						+ (index != me.logs.Count ? "\n_Note: This changes the index of all logged channels after this one."
						+ "\nMake sure to recheck the `" + me.cmdList.fullUsage + "` command for an updated list!_" : string.Empty));
					LogHelper.LogInformation("Removed a channel from the list of channels!");
				} else {
					await DynamicSendMessage(e, ":notepad_spiral: **Invalid index value!** _(There's no logged channel with that index)_");
				}

				return true;
			}
		}

		#endregion

		public int AddChannel(Channel channel, What level) {
			logs.Add(new LoggedChannel {
				server = channel.Server.Id,
				channel = channel.Id,
				level = (int) level,
			});
			return logs.Count - 1;
		}

		public bool ChannelExists(Channel channel) {
			return logs.Any(c => c.channel == channel.Id && c.server == channel.Server.Id);
		}

		public bool RemoveChannel(Channel channel) {
			return logs.RemoveAll(c => c.channel == channel.Id && c.server == channel.Server.Id) > 0;
		}

		public bool RemoveChannel(int index) {
			if (index >= 0 && index < logs.Count) {
				logs.RemoveAt(index);
				return true;
			} else return false;
		}

		public void SaveLogs() {
			SaveData.singleton.Logged_channels = logs.ToArray();
			SaveData.Save();
			LogHelper.LogInformation("Saved changes on channel logs list.");
		}

		[Serializable]
		public struct LoggedChannel : IComparable<LoggedChannel> {
			public ulong server;
			public ulong channel;
			public int level;

			public int CompareTo(LoggedChannel other) {
				int result = server.CompareTo(other.server);
				if (result == 0) result = channel.CompareTo(other.channel);
				return result;
			}
		}

		/// <summary>
		/// What to log.
		/// </summary>
		[Flags]
		public enum What {
			none					= 0,
			message_delete			= 1,
			message_changed			= 2,
			message_sent			= 4,
			user_joined_server		= 8,
			user_left_server		= 16,
			user_banned				= 32,
			user_unbanned			= 64,
			user_nickname_change	= 128,

			all = message_delete | message_changed | message_sent | user_joined_server | user_left_server | user_banned | user_unbanned | user_nickname_change,
			all_message = message_delete | message_changed | message_sent,
			all_user = user_joined_server | user_left_server | user_banned | user_unbanned | user_nickname_change,
		}
	}
}
