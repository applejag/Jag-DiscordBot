using Discord;
using DiscordBot.Utility;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DiscordBot.Modules {
	public sealed class CommandHandler : Module {
		public const string CMD_PREFIX = "!";

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
												: CommandPerm.Mention);

			foreach (var m in bot.modules) {
				string[] args; string rest;
				dynamic parsed = TryParseCommand(m, e.Message.Text, permissions, out args, out rest);
				if (parsed == null) continue;
				if (parsed is Command) {
					Command cmd = parsed as Command;
					string trimmed = rest.Trim();
					if (trimmed == "?" || trimmed == "-h" || trimmed == "--help") {
						await DiscordHelper.DynamicSendMessage(e, "`" + e.Message.Text + "`\n" + cmd.GetInformation());
						return;
					}

					try {
						LogHelper.LogInformation("Command \"" + cmd.name + "\" ran by " + e.User.Name + "#" + e.User.Discriminator);
						if (!await cmd.Callback(e, args, rest))
							await DiscordHelper.DynamicSendMessage(e, "`" + e.Message.Text + "`\n" + cmd.GetInformation());
					} catch (Exception err) {
						LogHelper.LogException("Error executing command \"" + cmd.name + "\"", err);
						await e.Channel.SafeSendMessage("Error executing command!\n```" + err.Message + "```");
					}
					return;
				} else if (parsed is Module) {
					Module mod = parsed as Module;
					try {
						string info = mod.GetInformation();

						if (string.IsNullOrWhiteSpace(info)) return;

						LogHelper.LogInformation("Module info for \"" + mod.modulePrefix + "\" ran by " + e.User.Name + "#" + e.User.Discriminator);
						await DiscordHelper.DynamicSendMessage(e, "`" + e.Message.Text + "`\n" + info);
					} catch (Exception err) {
						LogHelper.LogException("Error sending module info \"" + mod.GetType().Name + "\"", err);
						await e.Channel.SafeSendMessage("Error sending module info!\n```" + err.Message + "```");
					}
				}
			}

			OnCommandParseFailed(sender, e);
		}

		public static dynamic TryParseCommand(Module module, string input, CommandPerm permissions, out string[] args, out string rest) {
			rest = string.Empty;
			args = input.Trim().Split(' ');
			int argindex = 0;

			if (args.Length == 0) return null;
			if (string.IsNullOrWhiteSpace(args[0])) return null;

			// Check if mentioned
			if (args[0][0] == '@' && args[0].Length > 1 && (
					args[0].Substring(1) == module.bot.client.CurrentUser.Name
				|| args[0].Substring(1) == module.bot.client.CurrentUser.Name + "#" + module.bot.client.CurrentUser.Discriminator
				|| args[0] == module.bot.client.CurrentUser.Mention
				|| args[0] == module.bot.client.CurrentUser.NicknameMention
			)) {
				argindex++;
			}

			// Got enough arguments?
			if (argindex >= args.Length) return null;

			// Check if prefix
			if (args[argindex].StartsWith(CMD_PREFIX)) {
				// Has prefix
				args[argindex] = args[argindex].Substring(CMD_PREFIX.Length);
			} else if (argindex == 0) {
				// No prefix and no mention
				return null;
			}

			// Check for command
			Command[] commands = module.GetCommands();
			for (int i = 0; i < commands.Length; i++) {
				int j = argindex;
				Command cmd = commands[i];

				// Check if module prefix
				if (!string.IsNullOrWhiteSpace(module.modulePrefix) && cmd.useModulePrefix) {
					if (module.modulePrefix.ToLower() == args[argindex].ToLower()) {
						j++;
						if (j >= args.Length) continue;
					} else {
						continue;
					}
				}

				// Check command
				string cmdName = args[j].ToLower();
				if (cmdName != cmd.name
					&& !cmd.alias.Contains(cmdName)) continue;

				// Check permissions
				if (cmd.requires == CommandPerm.Mention
					&& permissions != CommandPerm.Selfbot
					&& permissions != CommandPerm.Whitelist
					&& permissions != CommandPerm.Mention) continue;

				if (cmd.requires == CommandPerm.Whitelist
					&& permissions != CommandPerm.Selfbot
					&& permissions != CommandPerm.Whitelist) continue;

				if (cmd.requires == CommandPerm.Selfbot
					&& permissions != CommandPerm.Selfbot) continue;

				args = args.SubArray(j);
				rest = string.Join(" ", args.SubArray(1));

				return cmd;
			}

			if (args[argindex].ToLower() == module.modulePrefix) {
				// Skip if mention is required, and user hasent mentioned
				if (argindex == 0 && permissions == CommandPerm.Mention)
					return null;

				rest = (argindex+1<args.Length) ? string.Join(" ", args.SubArray(argindex + 1)) : string.Empty;
				string trimmed = rest.Trim();
				if (trimmed == "?" || trimmed == "-h" || trimmed == "--help" || string.IsNullOrWhiteSpace(trimmed)) {
					return module;
				}

			}
			return null;
		}
		
	}
}
