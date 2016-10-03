using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.Modules;
using System.IO;
using NAudio;
using NAudio.Wave;
using DiscordBot.Utility;

namespace DiscordBot.Modules {
	public sealed class DuckHorn : Module {

		public override void Init() {
			bot.client.UsingAudio(x => x.Mode = AudioMode.Outgoing);

			AddCommand(cmdDuckHorn);
		}

		public override void Unload() {
			if (cmdDuckHorn?.client != null)
				cmdDuckHorn.client.Disconnect();

			RemoveCommand(cmdDuckHorn);
		}

		#region Command definitions
		private CmdDuckHorn cmdDuckHorn = new CmdDuckHorn();
		public sealed class CmdDuckHorn : Command<DuckHorn> {
			public IAudioClient client;

			public override string name { get; } = "quack";
			public override CommandPerm requires { get; } = CommandPerm.None;
			public override string description { get; } = "QUACK QUACK QUACK\nQUACK QUACK QUACK QUACK\nQUACK QUACK\n<Use while in a voice channel>";
			public override string usage { get; } = string.Empty;
			public override bool useModulePrefix { get; } = false;
			public override string[] alias { get; internal set; } = { "🦆", ":duck:" };

			public static readonly string QUACK_FOLDER = Path.Combine(Environment.CurrentDirectory, "sounds"); // Relative

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {

				if (e.User.VoiceChannel == null)
					return false;

				// See if musicbot is playing
				foreach (var mod in bot.modules) {
					if (mod is MusicBot && !(mod as MusicBot).idlePlayer) {
						await DynamicSendMessage(e, "🦆 **Music is playing, can't quack at this moment...**");
						return true;
					}
				}

				if (client != null) {
					await DynamicSendMessage(e, "🦆 **Already quacking, please wait your turn...**");
					return true;
				}

				var quack = RandomQuackFile();
				if (quack == null) {
					await DynamicSendMessage(e, "🦆 **No quacks found!**");
					return true;
				}
				
				client = await AudioExtensions.JoinAudio(e.User.VoiceChannel);

				client.SendMp3(quack);
				// Wait 'til it's sent
				try {
					await Task.Run(() => client.Wait());
				} catch { }

				await Task.Delay(500);
				await client.Disconnect();
				client = null;

				// All went good
				return true;
			}

			public static string RandomQuackFile() {
				string[] entries = Directory.GetFileSystemEntries(QUACK_FOLDER);

				List<string> quacks = new List<string>();

				for (int i = 0; i < entries.Length; i++) {
					if (File.Exists(entries[i]) && Path.GetExtension(entries[i])?.ToLower() == ".mp3")
						// It's a file
						quacks.Add(entries[i]);
				}

				return quacks.Count > 0 ? quacks[RandomHelper.random.Next(quacks.Count)] : null;
			}
		}
		#endregion
		
	}
}
