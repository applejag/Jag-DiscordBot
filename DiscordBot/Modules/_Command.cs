using Discord;
using DiscordBot.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules {
	public abstract class Command {
		public abstract string id { get; }
		public abstract string name { get; }
		public abstract CommandPerm requires { get; }
		public abstract Task Callback(MessageEventArgs e, string[] args, string rest);
		public abstract Bot bot { get; internal set; }
		public abstract Module module { get; internal set; }
		public virtual bool useModulePrefix { get; } = true;

		#region Safe Message Wrappers
		public static async Task<Message> SafeSendMessage(Channel channel, string text) {
			return await channel.SafeSendMessage(text);
		}

		public static async Task<Message> DynamicSendMessage(MessageEventArgs e, string text) {
			return await DiscordHelper.DynamicSendMessage(e, text);
		}

		public async Task<Message> DynamicEditMessage(Message message, User target, string text) {
			return await DiscordHelper.DynamicEditMessage(message, text, target.Id == bot.client.CurrentUser.Id);
		}
		#endregion
	}


	public abstract class Command<T> : Command where T : Module {
		public override string id { get {
				return name == null ? null : (string.IsNullOrWhiteSpace(me.modulePrefix) ? "" : me.modulePrefix + ".") + name;
		} }

		public override Bot bot { get; internal set; }
		public override Module module { get { return me; } internal set { me = module as T; } }
		public T me { get; internal set; }
	}

	public enum CommandPerm {
		None,
		Whitelist,
		Selfbot,
	}
}
