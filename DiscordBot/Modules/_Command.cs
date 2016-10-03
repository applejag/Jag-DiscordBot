using Discord;
using DiscordBot.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules {
	public abstract class Command {
		public string id { get; internal set; }
		public abstract string name { get; }
		public abstract CommandPerm requires { get; }
		public abstract Task<bool> Callback(MessageEventArgs e, string[] args, string rest);
		public abstract Bot bot { get; internal set; }
		public abstract Module module { get; internal set; }
		public virtual bool useModulePrefix { get; } = true;
		public virtual string[] alias { get; internal set; } = {};

		public abstract string usage { get; }
		public string fullUsage => CommandHandler.CMD_PREFIX + (string.IsNullOrWhiteSpace(module.modulePrefix) || !useModulePrefix ? name : module.modulePrefix + " " + name) + " " + usage;
		public abstract string description { get; }
		public string GetInformation() =>
			string.Format("**{0}**\n{1}**Required permissions:** `{2}`\n**Description:** ```\n{3}```\n**Usage:** `{4}`",
			module.GetType().Name + " / " + name,
			alias.Length > 0 ? "**Alias:** _" + alias.Sum(s => s + ", ").TrimEnd(',', ' ') + "_\n" : string.Empty,
			requires.ToString(),
			description,
			fullUsage).Trim();

		#region Safe Message Wrappers
		public static async Task<Message> DynamicSendMessage(MessageEventArgs e, string text) {
			return await DiscordHelper.DynamicSendMessage(e, text);
		}

		public async Task<Message> DynamicEditMessage(Message message, User target, string text) {
			return await DiscordHelper.DynamicEditMessage(message, target, text);
		}
		#endregion
	}


	public abstract class Command<T> : Command where T : Module {
		public override Bot bot { get; internal set; }
		public override Module module { get { return me; } internal set { me = value as T; } }
		public T me { get; private set; }
	}

	public enum CommandPerm {
		/// <summary>
		/// Requires nothing but the command.
		/// </summary>
		None,
		/// <summary>
		/// Requires user to mention the bot, as well as the applicable command.
		/// </summary>
		Mention,
		/// <summary>
		/// Requires user to be whitelisted, as well as the applicable command.
		/// Mention is optional.
		/// </summary>
		Whitelist,
		/// <summary>
		/// Requires user to be the same user as the bot, ie owner of the selfbot.
		/// Mention is optional.
		/// </summary>
		Selfbot,
	}
}
