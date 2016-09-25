using Discord;
using DiscordBot.Utility;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DiscordBot.Modules {
	public sealed class CommandHandler : Module {
		public const string CMD_PREFIX = "|";

		public override void Init() {
			client.MessageReceived += Client_MessageReceived;
		}

		public override void Unload() {
			client.MessageReceived -= Client_MessageReceived;
		}

		public event EventHandler<MessageEventArgs> CommandParseFailed;
		private void OnCommandParseFailed(object sender, MessageEventArgs e) {
			CommandParseFailed?.Invoke(sender, e);
		}

		async void Client_MessageReceived(object sender, MessageEventArgs e) {
			if (e.User.IsBot) return;
			if (!bot.initialized) return;
			if (string.IsNullOrWhiteSpace(e.Message.Text)) return;
			
			bool isWhitelisted = bot.CheckIsWhitelisted(e.User);

			CommandPerm permissions = e.User.Id == bot.client.CurrentUser.Id
											? CommandPerm.Selfbot
											: (bot.CheckIsWhitelisted(e.User)
												? CommandPerm.Whitelist
												: CommandPerm.None);

			foreach (var m in bot.modules) {
				string[] args; string rest;
				Command cmd = TryParseCommand(m, e.Message.Text, permissions, out args, out rest);
				if (cmd != null) {
					string trimmed = rest.Trim();
					if (trimmed == "?" || trimmed == "-h" || trimmed == "--help") {
						await DiscordHelper.DynamicSendMessage(e, "`" + e.Message.RawText + "`\n" + cmd.GetInformation());
						return;
					}

					try {
						LogHelper.LogInformation("Command \"" + cmd.name + "\" ran by " + e.User.Name + "#" + e.User.Discriminator);
						if (!await cmd.Callback(e, args, rest))
							await DiscordHelper.DynamicSendMessage(e, "`" + e.Message.RawText + "`\n" + cmd.GetInformation());
					} catch (Exception err) {
						LogHelper.LogException("Error executing command \"" + cmd.name + "\"", err);
						await e.Channel.SafeSendMessage("Error executing command!\n```" + err.Message + "```");
					}
					return;
				}
			}

			OnCommandParseFailed(sender, e);
		}

		public static Command TryParseCommand(Module module, string input, CommandPerm permissions, out string[] args, out string rest) {
			rest = string.Empty;
			args = input.Split(' ');
			int argindex = 0;

			if (args.Length == 0) return null;
			if (string.IsNullOrWhiteSpace(args[0])) return null;

			// Check if mentioned
			if (args[0][0] == '@' && args[0].Length > 1 && (
					args[0].Substring(1) == module.bot.client.CurrentUser.Name
				|| args[0].Substring(1) == module.bot.client.CurrentUser.Name + "#" + module.bot.client.CurrentUser.Discriminator
				|| args[0] == module.bot.client.CurrentUser.NicknameMention
			)) {
				argindex++;
			}

			// Got enough arguments?
			if (argindex >= args.Length) return null;

			// Check if prefix
			if (args[argindex].StartsWith(CMD_PREFIX)) {
				args[argindex] = args[argindex].Substring(CMD_PREFIX.Length);
			}

			// Check for command
			Command[] commands = module.GetCommands();
			for (int i=0; i<commands.Length; i++) {
				int j = argindex;
				Command cmd = commands[i];
				
				// Check if module prefix
				if (!string.IsNullOrWhiteSpace(module.modulePrefix) && cmd.useModulePrefix) {
					if (module.modulePrefix.ToLower() == args[argindex].ToLower()) {
						j++;
					} else {
						continue;
					}
				}

				// Check command
				string cmdName = args[j].ToLower();
				if (cmdName != cmd.name
					&& !cmd.alias.Contains(cmdName)) continue;

				// Check permissions
				if (cmd.requires == CommandPerm.Whitelist && permissions == CommandPerm.None) continue;
				if (cmd.requires == CommandPerm.Selfbot && permissions != CommandPerm.Selfbot) continue;
				
				args = args.SubArray(j);
				rest = string.Join(" ", args.SubArray(1));

				return cmd;
			}

			return null;
		}
		
	}
}
