using Discord;
using DiscordBot.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Discord.Message;

namespace DiscordBot.Modules {
	public sealed class ImageStorage : Module {
		static readonly string IMAGE_FOLDER_PATH = Path.Combine(Environment.CurrentDirectory, "images"); // Relative
		Folder image_folder;
		static Random random = new Random();

		public override string modulePrefix { get; } = "img";

		public override void Init() {
			image_folder = new Folder(IMAGE_FOLDER_PATH);

			AddCommand(cmdCache);
			AddCommand(cmdSave);
			AddCommand(cmdSend);
			AddCommand(cmdRefresh);
			AddCommand(cmdList);
		}

		public override void Unload() {
			RemoveCommand(cmdCache);
			RemoveCommand(cmdSave);
			RemoveCommand(cmdSend);
			RemoveCommand(cmdRefresh);
			RemoveCommand(cmdList);
		}

		#region Command definitions
		private CmdCache cmdCache = new CmdCache();
		public sealed class CmdCache : Command<ImageStorage> {
			public override string name { get; } = "cache";
			public override CommandPerm requires { get; } = CommandPerm.Selfbot;
			public override string description { get; } = "Since the bot does not download /everything/ some manual caching is sometimes needed. All this does is giving the bot a heads up that it may have missed some messages. Espesually useful for the `save` command who doesn't cache automatically.";
			public override string usage { get; } = "";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				int num = 100;
				int tmp;

				if (args.Length > 1) {
					if (int.TryParse(args[1], out tmp) && tmp > 1) {
						num = Math.Abs(tmp);
						if (num > 1000) {
							await DynamicSendMessage(e, "Cache requests are limited to 1000 messages. Changing to 1000 messages.");
							num = 1000;
						} else if (num < 10) {
							await DynamicSendMessage(e, "You may wish to select a higher cache limit. Changing to 10 messages.");
						}
					} else {
						await DynamicSendMessage(e, "Unable to interperate the cache limit value. Changing to default of 100 messages.");
					}
				}

				LogHelper.LogInformation("Started caching the latest " + num + " messages");
				Message status = await DynamicSendMessage(e, "Started caching...");

				DateTime start = DateTime.Now;
				try {
					await e.Channel.DownloadMessages(limit: num);
				} catch {
					num = e.Channel.Messages.Count();
				}
				double span = (DateTime.Now - start).TotalSeconds;

				Thread.Sleep(300);
				await DynamicEditMessage(status, e.User, string.Format("Caching of {0} messages is complete! *(took {1:0.00} {2})*", num, span, span == 1d ? "second" : "seconds"));
				LogHelper.LogSuccess(string.Format("Caching of {0} messages is complete! (took {1:0.00} {2})", num, span, span == 1d ? "second" : "seconds"));
				return true;
			}
		}

		private CmdSave cmdSave = new CmdSave();
		public sealed class CmdSave : Command<ImageStorage> {
			public override string name { get; } = "save";
			public override CommandPerm requires { get; } = CommandPerm.Selfbot;
			public override string description { get; } = "Save the most recent attachment from the channel at the bot's main storage. You may specify the filename and folder to store the attachment. Spaces in filenames are forbidden, as well as other odd symbols such as emojis :bowtie:.\nNote: This command does not cache images automatically, so a run of the cache may be required.";
			public override string usage { get; } = "[filename]";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				// Save latest image in channel

				bool any = false;

				Message status = await DynamicSendMessage(e, "Seaching for attachment...");

				try {
					foreach (var msg in e.Channel.Messages) {
						if (msg.Attachments == null || msg.Attachments.Length == 0) continue;
						Attachment a = msg.Attachments[0];
						any = true;

						string filename = Path.GetFileNameWithoutExtension(a.Filename);
						string ext = Path.GetExtension(a.Filename);
						if (args.Length > 1) {
							// Use custom filename
							filename = args[1].Trim('\\').Trim();

							if (Path.HasExtension(filename)) {
								ext = Path.GetExtension(filename);
								filename = filename.Substring(filename.Length - ext.Length);
							}

							foreach (char invalid in Path.GetInvalidFileNameChars())
								if (invalid != '\\' && invalid != '/')
									filename = filename.Replace(invalid.ToString(), "");
							foreach (char invalid in Path.GetInvalidPathChars())
								filename = filename.Replace(invalid.ToString(), "");

							filename = filename.Replace(".", "");
							filename = filename.Replace('/', '\\');
						}

						string original = filename;
						int num = 0;
						// Make sure it's not overriding
						while (System.IO.File.Exists(Path.Combine(IMAGE_FOLDER_PATH, filename + ext)) || Directory.Exists(Path.Combine(IMAGE_FOLDER_PATH, filename + ext))) {
							num++;
							filename = original + num.ToString().PadLeft(3);
						}

						using (var client = new WebClient()) {
							LogHelper.LogInformation("Started downloading attachment \"" + a.Filename + "\"...");
							await DynamicEditMessage(status, e.User, "Downloading attachment...");

							DateTime start = DateTime.Now;
							// Create directory if needed
							new FileInfo(Path.Combine(IMAGE_FOLDER_PATH, filename + ext)).Directory.Create();
							// Start downling
							await client.DownloadFileTaskAsync(a.Url, Path.Combine(IMAGE_FOLDER_PATH, filename + ext));
							me.image_folder = null;
							double span = (DateTime.Now - start).TotalSeconds;

							Thread.Sleep(300);
							await DynamicEditMessage(status, e.User, string.Format("Download complete! File has been saved as `{0}` _(took {1:0.00} {2})_", filename + ext, span, span == 1d ? "second" : "seconds"));
							LogHelper.LogSuccess(string.Format("Download complete! File has been saved at \"{0}\" (took {1:0.00} {2})", Path.Combine(IMAGE_FOLDER_PATH, filename + ext), span, span == 1d ? "second" : "seconds"));
						}

						break;
					}

					if (!any) {
						await e.Channel.SendIsTyping();
						Thread.Sleep(800);
						await DynamicEditMessage(status, e.User, "No attachments founds! Try `cache` to load in some messages and then try again!");
					}
				} catch (Exception err) {
					LogHelper.LogException("Unexpected error while performing save of attachment.", err);
					await DynamicSendMessage(e, "Unexpected error... \n```" + err.Message + "```");
					throw;
				}
				return true;
			}
		}

		private CmdSend cmdSend = new CmdSend();
		public sealed class CmdSend : Command<ImageStorage> {
			public override string name { get; } = "send";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description { get; } = "Sends an image from the bots local storage.\nNotes:\n- The filter/needles used may only be a partial match, but it will pick one random of all the matches it finds.\n- The algorithm checks if ALL needles match, not just if one matched.\n- If filtered only with an asterisk ( * ) it will randomize between all images stored.";
			public override string usage { get; } = "<needle> [needle 2] [needle 3] etc.";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {// Error message
				if (args.Length < 2) {
					await DynamicSendMessage(e, "Please specify the file name!\n*Protip: Use the command `list` to get a list of available images.*");
					return false;
				}

				// Get file path
				string file;
				try {
					me.image_folder = me.image_folder ?? new Folder(IMAGE_FOLDER_PATH);
					string[] results = me.image_folder.Search(args.SubArray(1));

					if (results.Length == 0) {
						LogHelper.LogFailure("Unable to find user requested file \"" + args[1] + "\"");
						await DynamicSendMessage(e, "Unable to find a matching file to `" + args[1] + "`\n*Protip: Use the command `list` to get a list of available images.*");
						return false;
					}
					file = results[random.Next(results.Length)];

				} catch (Exception err) {
					LogHelper.LogException("Unexpected error when searching for file.", err);
					await DynamicSendMessage(e, "Unexpected error while searching for file...\n```" + err.Message + "```");
					throw;
				}

				// Send file
				try {
					// Try delete the original message, if I own the rights to
					await e.Message.SafeDelete();

					LogHelper.LogInformation("Sending file \"" + file + "\"...");
					await e.Channel.SafeSendFile(file);

					LogHelper.LogSuccess("Successfully sent file!");
				} catch (Exception err) {
					LogHelper.LogException("Unexpected error when sending a file.", err);
					await DynamicSendMessage(e, "Unable to send file `" + Path.GetFileName(args[1]) + "`\n```" + err.Message + "```");
					throw;
				}
				return true;
			}
		}

		private CmdRefresh cmdRefresh = new CmdRefresh();
		public sealed class CmdRefresh : Command<ImageStorage> {
			public override string name { get; } = "refresh";
			public override CommandPerm requires { get; } = CommandPerm.Selfbot;
			public override string description { get; } = "Plain and simple, just refreshes the internal list of images. This is useful if an image was added by 3rd part during runtime.\nNote that the save command refreshes the list automatically.";
			public override string usage { get; } = "";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				me.image_folder = new Folder(IMAGE_FOLDER_PATH);
				await e.Channel.SendIsTyping();
				Thread.Sleep(800);
				await DynamicSendMessage(e, "Image database has been manually refreshed!");
				return true;
			}
		}


		private CmdList cmdList = new CmdList();
		public sealed class CmdList : Command<ImageStorage> {
			public override string name { get; } = "list";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description { get; } = "This sends a list of available images in the bot's local storage to you (in private chat, if possible).";
			public override string usage { get; } = "";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length > 1) return false;
				try {
					me.image_folder = me.image_folder ?? new Folder(IMAGE_FOLDER_PATH);
					string header = "**These images are available**";
					string footer = "**Sending is complete**";
					string[] messages = StringHelper.SplitMessage(me.image_folder.ToString().Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None), 1984);

					if (bot.isSelfbot) {
						await DynamicSendMessage(e, header);
						for (int i = 0; i < messages.Length; i++) {
							await e.Channel.SafeSendMessage("```" + messages[i] + "```");
						}
						await e.Channel.SafeSendMessage(footer);
					} else {
						if (e.Channel.Id == e.User.PrivateChannel.Id) {
							// Is in users private channel
							await DynamicSendMessage(e, header);
							for (int i = 0; i < messages.Length; i++) {
								await e.Channel.SafeSendMessage("```" + messages[i] + "```");
							}
							await e.Channel.SafeSendMessage(footer);
						} else {
							// Is somewhere public
							await DynamicSendMessage(e, "A list of available images is being sent to you in private.");
							await e.User.PrivateChannel.SafeSendMessage(header);
							for (int i = 0; i < messages.Length; i++) {
								await e.User.PrivateChannel.SafeSendMessage("```" + messages[i] + "```");
							}
							await e.User.PrivateChannel.SafeSendMessage(footer);
						}
					}
				} catch (Exception err) {
					LogHelper.LogException("Unexpected error when sending list.", err);
					await DynamicSendMessage(e, "Unexpected error occurred when sending list.\n```" + err.Message + "```");
					throw;
				}
				return true;
			}
		}
		#endregion

		class Folder {
			public readonly string path;
			public readonly Folder[] folders;
			public readonly string[] files;
			private readonly string str;

			public bool empty {
				get { return (folders == null || folders.Length == 0) && (files == null || files.Length == 0); }
			}

			public Folder(string path) {
				string[] entries = Directory.GetFileSystemEntries(path);

				List<string> files = new List<string>();
				List<Folder> folders = new List<Folder>();

				for (int i = 0; i < entries.Length; i++) {
					if (System.IO.File.Exists(entries[i]))
						// It's a file
						files.Add(entries[i]);
					else if (Directory.Exists(entries[i]))
						// It's a directory
						folders.Add(new Folder(entries[i] + "\\"));
				}

				files.Sort();
				folders.Sort((a, b) => a.path.CompareTo(b.path));

				this.path = path;
				this.files = files.ToArray();
				this.folders = folders.ToArray();
				this.str = ListEntries();
			}

			private string ListEntries(string sofar = "", int indent = 1) {
				// Some formatting stuff
				string prefix = "";
				int count = 0;
				// Start the prefix after the X'th last backslash
				for (int i = path.Length - 1; i >= 0; i--) {
					if (path[i] == '\\')
						count++;
					if (count >= indent) {
						prefix = path.Substring(i).Trim('\\').Replace('\\','/');
						break;
					}
				}

				sofar += "\n[ images" + (prefix.Length > 0 ? "/" : string.Empty) + prefix + " ]" + (files.Length>0 ? " (" + files.Length + " files)\n" : "\n");

				// Check the folders content
				if (empty)
					sofar += "\t<no files here>\n";
				else {
					List<object> entries = new List<object>();
					entries.AddRange(files);
					entries.AddRange(folders);

					for (int i=0; i<entries.Count; i++) {
						if (entries[i] is Folder)
							// Folder
							sofar = (entries[i] as Folder).ListEntries(sofar, indent + 1) + "\n";
						else
							// File
							sofar += "- " + prefix + (prefix.Length > 0 ? "/" : string.Empty) + Path.GetFileName(entries[i] as string) + "\n";
					}
				}

				return sofar;
			}

			public override string ToString() {
				return str;
			}

			public string[] Search(params string[] needles) {
				List<string> results = new List<string>();

				// Main search algorithm
				if (needles.Length == 1 && needles[0][0] == '*') {
					// Add everything
					results.AddRange(files);
				} else {
					// Filter out matches
					results.AddRange(files.Where(file =>
						needles.All(needle => Path.GetFileName(file).ToLower().IndexOf(needle.ToLower()) != -1
							|| Path.GetFileName(path.Trim('\\')).ToLower().IndexOf(needle.ToLower()) != -1)
					));
				}

				foreach (Folder folder in folders) {
					results.AddRange(folder.Search(needles));
				}

				return results.ToArray();
			}
		}
		
	}
}
