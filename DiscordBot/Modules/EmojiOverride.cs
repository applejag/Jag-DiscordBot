using Discord;
using DiscordBot.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Modules {
	public sealed class EmojiOverride : Module {
		public override string modulePrefix { get; } = "emoji";
		public override string description { get; } = "Replaces custom ASCII emojis in chat messages.\nFor example, writing /shrug will result in ¯\\_(ツ)_/¯";

		private CommandHandler handler;
		public bool registered = false;

		private Dictionary<string, string> replacements = new Dictionary<string, string> {
			// EMOJIS
			{ "/shrug", "¯\\_(ツ)_/¯" },
			{ "/lenny", "( ͡° ͜ʖ ͡°)" },
			{ "/lennys", "( ͡°( ͡° ͜ʖ( ͡° ͜ʖ ͡°)ʖ ͡°) ͡°)" },
			{ "/lod", "ಠ_ಠ" },
			{ "/zoidberg", "(V) (°,,,°) (V)" },
			{ "/pony", "/)(^3^)(\\" },
			// ARROWS
			{ "-->", "→" },
			{ "<--", "←" },
			{ "==>", "⇒" },
			{ "<==", "⇐" },
			// MATH
			{ "/inf", "∞" },
			{ "/deg", "°" },
			{ "/pi", "π" },
			{ "/tau", "τ" },
			{ "/theta", "θ" },
			{ "+/-", "±" },
			{ "-/+", "∓" },
			// SUPERSCRIPT
			{ "/sup0", "⁰" },
			{ "/sup1", "¹" },
			{ "/sup2", "²" },
			{ "/sup3", "³" },
			{ "/sup4", "⁴" },
			{ "/sup5", "⁵" },
			{ "/sup6", "⁶" },
			{ "/sup7", "⁷" },
			{ "/sup8", "⁸" },
			{ "/sup9", "⁹" },
			// SUBSCRIPT
			{ "/sub0", "₀" },
			{ "/sub1", "₁" },
			{ "/sub2", "₂" },
			{ "/sub3", "₃" },
			{ "/sub4", "₄" },
			{ "/sub5", "₅" },
			{ "/sub6", "₆" },
			{ "/sub7", "₇" },
			{ "/sub8", "₈" },
			{ "/sub9", "₉" },
		};

		public override void Init() {
			handler = bot.modules.Find(x => x is CommandHandler) as CommandHandler;

			AddCommand(cmdActivate);
			AddCommand(cmdDeactivate);
			AddCommand(cmdList);

			registered = SaveData.singleton.Emoji_replace;
			if (registered)
				handler.CommandParseFailed += OnCommandParseFailed;
		}
		
		public override void Unload() {
			SaveData.singleton.Emoji_replace = registered;

			RemoveCommand(cmdActivate);
			RemoveCommand(cmdDeactivate);
			RemoveCommand(cmdList);

			if (registered) {
				handler.CommandParseFailed -= OnCommandParseFailed;
				registered = false;
			}
		}

		private CmdActivate cmdActivate = new CmdActivate();
		public sealed class CmdActivate : Command<EmojiOverride> {
			public override string description { get; } = "Activate emoji replacement.";
			public override string name { get; } = "activate";
			public override CommandPerm requires { get; } = CommandPerm.Selfbot;
			public override string usage { get; } = "";
			public override string[] alias { get; internal set; } = { "enable", "on", "start" };

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length > 1) return false;

				if (!me.registered) {
					me.handler.CommandParseFailed += me.OnCommandParseFailed;
					me.registered = true;
					await DynamicSendMessage(e, "¯\\_(ツ)_/¯ **Emojis has been activated!**");
				} else {
					await DynamicSendMessage(e, "¯\\_(ツ)_/¯ **Emojis is already active!**");
				}
				return true;
			}
		}

		private CmdDeactivate cmdDeactivate = new CmdDeactivate();
		public sealed class CmdDeactivate : Command<EmojiOverride> {
			public override string description { get; } = "Deactivate emoji replacement.";
			public override string name { get; } = "deactivate";
			public override CommandPerm requires { get; } = CommandPerm.Selfbot;
			public override string usage { get; } = "";
			public override string[] alias { get; internal set; } = { "disable", "off", "stop" };

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length > 1) return false;
				if (me.registered) {
					me.handler.CommandParseFailed -= me.OnCommandParseFailed;
					me.registered = false;
					await DynamicSendMessage(e, "¯\\_(ツ)_/¯ **Emojis has been disabled!**");
				} else {
					await DynamicSendMessage(e, "¯\\_(ツ)_/¯ **Emojis is not even running!**");
				}
				return true;
			}
		}

		private CmdList cmdList = new CmdList();
		public sealed class CmdList : Command<EmojiOverride> {
			public override string description { get; } = "List all available replacements.";
			public override string name { get; } = "list";
			public override CommandPerm requires { get; } = CommandPerm.Selfbot;
			public override string usage { get; } = "";
			public override string[] alias { get; internal set; } = { "?", "help" };

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length > 1) return false;

				int maxKeyWidth = me.replacements.Keys.Max(k=>k.Length);
				//int maxValueWidth = me.replacements.Values.Max(v=>v.Length);

				string msg = "¯\\_(ツ)_/¯ **Currently available emojis:**\n"
					+	"```\n"
					+   me.replacements.Sum(kv => "'" + (kv.Key + "'").PadRight(maxKeyWidth+1) + " ⇒ '" + kv.Value + "'\n")
					+	"```";

				string[] messages = StringHelper.SplitMessage(msg.Split('\n'), 1994);
				List<Message> sent = new List<Message>();

				for (int i = 0; i < messages.Length; i++) {
					msg = messages[i];

					// Add to start of part
					if (i != 0) msg = "```" + msg;
					// Add to end of part
					if (i != messages.Length-1) msg += "```";

					messages[i] = msg;
					sent.Add(await e.Channel.SafeSendMessage("__**TEMPORARY**__"));
				}

				for (int i=0; i<messages.Length; i++) {
					await sent[i].SafeEdit(messages[i]);
				}

				return true;
			}
		}

		private async void OnCommandParseFailed(object sender, MessageEventArgs e) {
			// ONLY ACTIVE ON SELF
			if (e.User?.Id != client.CurrentUser.Id) return;

			string message = e.Message.RawText;

			foreach (var pair in replacements) {
				message = message.Replace(pair.Key, pair.Value);
			}

			if (message != e.Message.RawText)
				await e.Message.SafeEdit(message);
		}
	}
}
