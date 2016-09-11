using Discord;
using DiscordBot.Modules;
using DiscordBot.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Modules {
	public abstract class Module {
		public Bot bot { get; internal set; }
		public DiscordClient client { get { return bot == null ? null : bot.client; } }
		public virtual string modulePrefix { get { return null; } }

		public abstract void Init();
		public abstract void Unload();

		#region Command methods
		internal void AddCommand<T>(Command<T> cmd) where T : Module {
			if (cmd.requires == CommandPerm.Selfbot && !bot.isSelfbot) return;

			cmd.bot = bot;
			cmd.me = (T)this;
			
			if (bot.commands.Any(c=>c.id == cmd.id || c.module.modulePrefix == cmd.id))
				LogHelper.LogFailure("Command \"" + cmd.id + "\" is conflicting with another command! Skipping...");
			else {
				bot.commands.Add(cmd);
				LogHelper.LogSuccess("Registered command \"" + cmd.id + "\"");
			}
		}

		internal bool RemoveCommand<T>(Command<T> cmd) where T : Module {
			if (bot.commands.Contains(cmd))
				return bot.commands.Remove(cmd);
			return false;
		}

		internal bool RemoveCommand(string id) {
			return bot.commands.RemoveAll((cmd) => cmd.id == id) > 0;
		}

		public Command[] GetCommands() {
			return bot.commands.Where(cmd => cmd.module == this).ToArray();
		}
		#endregion

		// Temporary print function
		internal static void print(object o) {
			LogHelper.LogRawText(o);
		}
	}

}
