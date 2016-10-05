using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using WolframAlpha;
using WolframAlpha.Api.v2;
using WolframAlpha.Api.v2.Requests;
using WolframAlpha.Api.v2.Components;
using DiscordBot.Utility;

namespace DiscordBot.Modules {
	public sealed class Wolfram : Module {
		public override string modulePrefix { get; } = "wolfram";

		public override void Init() {
			AddCommand(cmdWolfram);
		}

		public override void Unload() {
			RemoveCommand(cmdWolfram);
		}

		#region Command definitions
		private CmdWolfram cmdWolfram = new CmdWolfram();
		public sealed class CmdWolfram : Command<Wolfram> {
			public override string name { get; } = "wolfram";
			public override CommandPerm requires { get; } = CommandPerm.None;
			public override string description { get; } = "Fetch information from wolfram";
			public override string usage { get; } = "<query>";
			public override bool useModulePrefix { get; } = false;
			public override string[] alias { get; internal set; } = { "wolf", "wolframalpha" };

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (string.IsNullOrWhiteSpace(rest))
					return false;

				Message status = await DynamicSendMessage(e, ":clock10: *Making request to wolfram, please hold...*");

				DateTime start = DateTime.Now;

				// Create query
				QueryBuilder b = new QueryBuilder();
				b.AppId = SaveData.singleton.Wolfram_Key;
				b.Input = rest;

				// Create request
				QueryRequest r = new QueryRequest();
				QueryResult result = await r.ExecuteAsync(b.QueryUri);

				TimeSpan took = DateTime.Now - start;
				await status.DynamicEditMessage(e.User, string.Format(":ballot_box_with_check: **Request complete!** _(took {0:0.00} seconds)_", took.TotalSeconds));

				// Send result

				// Error
				if (result?.Error == "true")
					await e.Channel.SafeSendMessage(":x: **ERROR**\n```\n" + result.ErrorData.Msg + "```");

				// Assumptions
				for (int i = 0; i < (result?.Assumptions?.Assumptions != null ? result.Assumptions.Assumptions.Length : 0); i++) {
					string tmp = string.Empty;
					var assumption = result.Assumptions.Assumptions[i];

					for (int j = 0; j < assumption.Values.Length; j++) {
						var value = assumption.Values[j];
						tmp += "*Input: `" + value.Name + "`* " + value.Desc + "\n";
					}
					await e.Channel.SafeSendMessage(tmp);
				}

				// Pods
				string output = string.Empty;
				for (int i = 0; i < (result?.Pods != null ? result.Pods.Length : 0); i++) {
					var pod = result.Pods[i];
					
					output += ":large_orange_diamond: **" + pod.Title + "**";
					for (int j = 0; j < pod.SubPods.Length; j++) {
						var subpod = pod.SubPods[j];
						bool plain = !string.IsNullOrEmpty(subpod.PlainText);
						bool title = !string.IsNullOrWhiteSpace(subpod.Title);
						bool multiline = subpod.PlainText.Contains('\n');
						string formatted = 
							plain // there is plain text?
								? (multiline // is it multiline?
									? (subpod.Img != null // image available?
										? string.Empty
										: "```\n" + subpod.PlainText + "```\n")
									: "    `" + subpod.PlainText + "`\n")
								: string.Empty;

						if (!output.EndsWith("\n") && !(plain && !title)) output += "\n";

						if (!plain && title)
							// Only title
							output += ":small_blue_diamond: **" + subpod.Title + "**\n";
						else if (plain && !title)
							// Only plain text
							output += formatted;
						else if (plain && title)
							// Both
							output += ":small_blue_diamond: **" + subpod.Title + "**" + formatted;

						if (subpod?.Img != null && (!plain || multiline)) {
							// Send output stack
							string[] tmp = StringHelper.SplitMessage(output.Split('\n'), 2000);
							for (int m = 0; m < tmp.Length; m++)
								await e.Channel.SafeSendMessage(tmp[m]);
							output = string.Empty;

							// Send image
							await e.Channel.SendFileFromWeb(subpod.Img.Src, subpod.Img.Title.Length > 120 ? subpod.Img.Title.Substring(0,120) : subpod.Img.Title, ".png");
						}
					}

				}

				// Send output stack
				if (!string.IsNullOrWhiteSpace(output)) {
					string[] messages = StringHelper.SplitMessage(output.Split('\n'), 2000);
					for (int m = 0; m < messages.Length; m++)
						await e.Channel.SafeSendMessage(messages[m]);
				}

				return true;
			}
		}
		#endregion
	}
}
