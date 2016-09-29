using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using System.Threading;
using DiscordBot.Utility;
using System.IO;
using System.Diagnostics.Contracts;
using NAudio.Wave;
using Nito.AsyncEx.Synchronous;
using Google.Apis.Auth.OAuth2;
using Google.Apis.YouTube.v3;
using Google.Apis.Util.Store;
using Google.Apis.Services;

namespace DiscordBot.Modules {
	public sealed class MusicBot : Module {

		public override string modulePrefix { get; } = "music";
		public override string description => "Music player! Play songs via youtube to the entire voice chat!\nFor starters, use the\n\t" + CommandHandler.CMD_PREFIX + modulePrefix + " queue 4F0LKs4sYFo\ncommand to queue a song!";

		public static readonly string QUEUE_TEMP_FOLDER = Path.Combine(Environment.CurrentDirectory, "music");

		public List<Song> songHistory = new List<Song>();
		public Queue<Song> songQueue = new Queue<Song>();
		public Queue<Song> songPlaylist = new Queue<Song>();
		public Song? songCurrent = null;
		public IAudioClient audio;

		public const float VOLUME_MULTIPLIER = 0.5f;
		public float volume = 1f;
		public bool musicPaused = false;
		public bool musicStopped = false;
		public bool musicSkip = false;
		public bool idlePlayer { get; private set; } = true;

		public override void Init() {
			volume = SaveData.singleton.Music_volume;

			CleanupTempFolder();

			AddCommand(cmdVolume);
			AddCommand(cmdQueue);
			AddCommand(cmdPause);
			AddCommand(cmdResume);
			AddCommand(cmdStop);
			AddCommand(cmdSkip);
			AddCommand(cmdPlaylist);
			AddCommand(cmdList);
			AddCommand(cmdStart);
		}

		public override void Unload() {
			SaveData.singleton.Music_volume = volume;

			songQueue.Clear();
			musicStopped = true;
			TaskHelper.WaitUntil(100, () => idlePlayer).Wait();

			CleanupTempFolder();

			RemoveCommand(cmdVolume);
			RemoveCommand(cmdQueue);
			RemoveCommand(cmdPause);
			RemoveCommand(cmdResume);
			RemoveCommand(cmdStop);
			RemoveCommand(cmdSkip);
			RemoveCommand(cmdPlaylist);
			RemoveCommand(cmdList);
			RemoveCommand(cmdStart);
		}

		public static void CleanupTempFolder() {
			try {
				string[] entries = Directory.GetFileSystemEntries(QUEUE_TEMP_FOLDER);
				for (int i = 0; i < entries.Length; i++) {
					File.Delete(entries[i]);
				}
			} catch (Exception err) {
				LogHelper.LogException("Unexpected exception while clearing temporary sound folder!", err);
			}
		}

		#region Command definitions
		private CmdDisconnect cmdDisconnect = new CmdDisconnect();
		public sealed class CmdDisconnect : Command<MusicBot> {
			public override string name { get; } = "disconnect";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description => "Stops the current track and disconnects from the voice channel.\nTo stop use\n" + me.cmdStop.fullUsage;
			public override string usage { get; } = "";
			public override string[] alias { get; internal set; } = { "dc" };

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length != 1) return false;

				if (me.idlePlayer) {
					await DynamicSendMessage(e, ":musical_note: *Music player isn't even running..*");
					return true;
				}

				me.musicStopped = true;
				await DynamicSendMessage(e, ":musical_note: **Music has been stopped!**");
				return true;
			}
		}

		private CmdStart cmdStart = new CmdStart();
		public sealed class CmdStart : Command<MusicBot> {
			public override string name { get; } = "start";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description => "Starts up the music player.\nYou must be in a voice channel when calling this command.\nTo stop use\n" + me.cmdStop.fullUsage;
			public override string usage { get; } = "";
			public override string[] alias { get; internal set; } = { "boot" };

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length != 1) return false;

				if (!me.idlePlayer) {
					await DynamicSendMessage(e, ":musical_note: *Music player is already running..*");
					return true;
				}

				if (e.User?.VoiceChannel != null) { 
					me.StartMusicPlayer(e);
					await DynamicSendMessage(e, ":musical_note: **Music has been started!**");
				} else {
					return false;
				}
				return true;
			}
		}

		private CmdList cmdList = new CmdList();
		public sealed class CmdList : Command<MusicBot> {
			public override string name { get; } = "list";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description { get; } = "Lists the current queue.";
			public override string usage { get; } = "";

			private static string SafeString(string str) {
				return str.Replace('`', '\'').Replace('*', '∗').Replace('_', '＿').Replace('~', '∼');
			}

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length != 1) return false;

				Song[] queue = me.songQueue.ToArray();
				Song[] coqueue = me.songPlaylist.ToArray();
				int count = me.songCurrent.HasValue ? queue.Length + 1 : queue.Length;
				int cocount = coqueue.Length;

				if (count == 0 && cocount == 0) {
					await DynamicSendMessage(e, ":musical_note: **The queue is empty!**");
					return true;
				}

				string list = string.Empty;
				// Current song
				if (me.songCurrent.HasValue) {
					list += (me.musicPaused ? ":pause_button:" : ":arrow_forward:") + " `CURRENT` " + SafeString(me.songCurrent.Value.name) + "\n";
				}
				// Queued songs
				for (int i=0; i<queue.Length; i++) {
					list += ":small_blue_diamond: `" + (i+1) + ".` " + SafeString(queue[i].name) + "\n";
				}
				// Playlist-queue songs (not yet queued, but will be)
				for (int i=0; i<Math.Min(cocount, 10); i++) {
					list += ":small_orange_diamond: `" + (i + queue.Length + 1) + ".` _" + coqueue[i].name + "_\n";
				}

				string[] messages = StringHelper.SplitMessage(list.Split('\n'), 1900);

				await DynamicSendMessage(e, ":musical_note: **Current song queue:** _(" + count + (count == 1 ? " song" : " songs") + ")_");
				for (int i=0; i<messages.Length; i++) {
					await e.Channel.SafeSendMessage(messages[i]);
				}

				return true;
			}
		}

		private CmdPlaylist cmdPlaylist = new CmdPlaylist();
		public sealed class CmdPlaylist : Command<MusicBot> {
			public override string name { get; } = "playlist";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description { get; } = "Sets a playlist onto the queue.\nWhen a playlist is set the bot will load the next video in the playlist beforehand, but not the entire playlist at the same time.\nIf supplied with the shuffle argument it will randomize the playlist before queueing it.";
			public override string usage { get; } = "[--shuffle | -s] <playlist link | playlist id>";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length > 3) return false;
				if (args.Length < 2) return false;

				bool shuffle = false;
				if (args[1].ToLower() == "--shuffle" || args[1].ToLower() == "-s")
					shuffle = true;
				else if (args.Length == 3)
					// Too many arguments
					return false;

				string playlistID = string.Empty;
				Message status = null;

				// Check what input we got. Youtube url? Youtu.be url? Youtube video id? Youtube playlist id?
				Uri uri;
				if (Uri.TryCreate(args[shuffle ? 2 : 1], UriKind.Absolute, out uri)) {
					if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) {
						await DynamicSendMessage(e, ":musical_note: **Invalid youtube url!** _(Unacceptable uri scheme)_");
						return true;
					}

					// valid link, youtube.com?
					if (uri.Host == "youtube.com" || uri.Host == "www.youtube.com") {
						// Get v parameter (format: youtube.com?v=video_id)
						var p = uri.ParseQuery();
						if (p.ContainsKey("list"))
							playlistID = p["list"];
						else {
							await DynamicSendMessage(e, ":musical_note: **Invalid youtube url!** _(Missing playlist id)_");
							return true;
						}
					} else {
						// Unknown host
						await DynamicSendMessage(e, ":musical_note: **Invalid youtube url!** _(Unaccaptable uri host)_");
					}
				} else {
					// Not an url, assume it's a video id
					status = await DynamicSendMessage(e, ":musical_note: *Checking if playlist exists...*");
					var statusCode = await WebRequestHelper.TestUrlAsync("https://www.youtube.com/oembed?format=json&url=http://www.youtube.com/playlist?list=" + Uri.EscapeDataString(args[shuffle ? 2 : 1]));
					if (statusCode == System.Net.HttpStatusCode.OK) {
						playlistID = args[shuffle ? 2 : 1];
					} else {
						await DynamicEditMessage(status, e.User, ":musical_note: **Invalid youtube playlist ID!** _(Playlist not found)_");
						return true;
					}
				}

				string url = "http://www.youtube.com/playlist?list=" + playlistID;

				if (status == null) status = await DynamicSendMessage(e, ":musical_note: *Fetching playlist videos...*");
				else status = await DynamicEditMessage(status, e.User, ":musical_note: *Fetching playlist videos...*");
				LogHelper.LogInformation("Fetching playlist items...");

				me.songPlaylist.Clear();
				List<Song> items = await me.FetchPlaylistItems(playlistID);

				if (shuffle) items.Shuffle();

				foreach (var item in items)
					me.songPlaylist.Enqueue(item);

				// Döne
				string x = me.songPlaylist.Count + (me.songPlaylist.Count == 1 ? " song" : " songs");
				LogHelper.LogSuccess("Added " + x + " to the music queue!");
				await DynamicEditMessage(status, e.User, ":musical_note: **Fetching complete! " + x + " has been queued!**");

				me.QueuePlaylistedSong(e);

				return true;
			}
		}

		private CmdSkip cmdSkip = new CmdSkip();
		public sealed class CmdSkip : Command<MusicBot> {
			public override string name { get; } = "skip";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description { get; } = "Stops the current track and starts playing the next one.";
			public override string usage { get; } = "";
			public override string[] alias { get; internal set; } = { "next" };

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length != 1) return false;

				if (me.idlePlayer) {
					await DynamicSendMessage(e, ":musical_note: *Music player isn't even running..*");
					return true;
				}

				me.musicSkip = true;
				await DynamicSendMessage(e, ":musical_note: **Skipping to next song...**");
				return true;
			}
		}

		private CmdStop cmdStop = new CmdStop();
		public sealed class CmdStop : Command<MusicBot> {
			public override string name { get; } = "stop";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description { get; } = "Stops the current track and clears the queue.";
			public override string usage { get; } = "";
			public override string[] alias { get; internal set; } = { "clear", "disconnect", "hammertime" };

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length != 1) return false;

				if (me.idlePlayer) {
					await DynamicSendMessage(e, ":musical_note: *Music player isn't even running..*");
					return true;
				}

				if (!me.musicStopped) {
					me.musicStopped = true;
					me.songQueue.Clear();
					me.songPlaylist.Clear();
					await DynamicSendMessage(e, ":musical_note: **Music has been stopped!**");
				} else {
					await DynamicSendMessage(e, ":musical_note: *Music isn't even playing!*");
				}
				return true;
			}
		}

		private CmdResume cmdResume = new CmdResume();
		public sealed class CmdResume : Command<MusicBot> {
			public override string name { get; } = "resume";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description => "Resumes the current playback if paused. To pause, use\n\t" + me.cmdPause.fullUsage;
			public override string usage { get; } = "";
			public override string[] alias { get; internal set; } = { "play", "continue" };

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length != 1) return false;

				if (me.idlePlayer) {
					await DynamicSendMessage(e, ":musical_note: *Music player isn't even running.. (Pssst, use `" + me.cmdStart.fullUsage + "` to make it start running)*");
					return true;
				}

				if (me.musicPaused) {
					me.musicPaused = false;
					me.musicStopped = false;
					await DynamicSendMessage(e, ":musical_note: **Music has been resumed!**");
				} else {
					await DynamicSendMessage(e, ":musical_note: *Music is already playing!*");
				}
				return true;
			}
		}

		private CmdPause cmdPause = new CmdPause();
		public sealed class CmdPause : Command<MusicBot> {
			public override string name { get; } = "pause";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description => "Pauses the current playback. To resume, use\n\t" + me.cmdResume.fullUsage;
			public override string usage { get; } = "";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length != 1) return false;

				if (me.idlePlayer) {
					await DynamicSendMessage(e, ":musical_note: *Music player isn't even running!*");
					return true;
				}

				if (!me.musicPaused) {
					me.musicPaused = true;
					await DynamicSendMessage(e, ":musical_note: **Music has been paused!**");
				} else {
					switch (RandomHelper.random.Next(5)) {
						case 0: await DynamicSendMessage(e, ":musical_note: *Music isn't even playing you dumnut*"); break;
						case 1: await DynamicSendMessage(e, ":musical_note: *Can't you hear? It isn't even playing*"); break;
						case 2: await DynamicSendMessage(e, ":musical_note: *\"Please don't stop the mu-\" ...oh it's already stopped*"); break;
						case 3: await DynamicSendMessage(e, ":musical_note: *According to statistics, around 0.2 % of the population has hearing loss. Are you that special snowflake? Because the music isn't even playing.*"); break;
						case 4: await DynamicSendMessage(e, ":musical_note: *If you're hearing music, it wasen't me. I can promise you that.*"); break;
					}
				}
				return true;
			}
		}

		private CmdVolume cmdVolume = new CmdVolume();
		public sealed class CmdVolume : Command<MusicBot> {
			public override string name { get; } = "volume";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description { get; } = "Get or change the music player volume. Value of 100 represents 100%, 50 represents 50%, etc.\nMaximum value is 200%, minimum is 0%.\nOnly integer values are allowed";
			public override string usage { get; } = "[value]";
			public override string[] alias { get; internal set; } = { "setvolume", "v" };

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length > 2) return false;

				// Tell user the volume
				if (args.Length == 1) {
					await DynamicSendMessage(e, ":musical_note: **Current volume is at " + me.volume.ToString("P0") + "**");
					return true;
				}

				int volume;
				if (int.TryParse(args[1], out volume)) {
					float old = me.volume;

					volume = Math.Min(Math.Max(volume, 0), 200);
					me.volume = volume / 100f;

					string t = string.Empty;
					if (me.volume - old > 0.5)
						t = " up";
					else if (me.volume - old < -0.5)
						t = " down";

					await DynamicSendMessage(e, ":musical_note: **Volume has been changed from " + old.ToString("P0") + t + " to " + me.volume.ToString("P0") + "**");
					return true;
				} else {
					return false;
				}
			}
		}

		private CmdQueue cmdQueue = new CmdQueue();
		public sealed class CmdQueue : Command<MusicBot> {
			public override string name { get; } = "queue";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description { get; } = "Queue a YouTube song!\nYouTube link and video ID is acceptable as input.";
			public override string usage { get; } = "<video url or id>";
			public override string[] alias { get; internal set; } = { "add", "q" };
			public bool queueing = false;

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length != 2) return false;

				string videoID = string.Empty;
				Message status = null;
				queueing = true;

				// Check what input we got. Youtube url? Youtu.be url? Youtube video id? Youtube playlist id?
				Uri uri;
				if (Uri.TryCreate(args[1], UriKind.Absolute, out uri)) {
					if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) {
						await DynamicSendMessage(e, ":musical_note: **Invalid youtube url!** _(Unacceptable uri scheme)_");
						queueing = false;
						return true;
					}

					// valid link, youtube.com or youtu.be?
					if (uri.Host == "youtube.com" || uri.Host == "www.youtube.com") {
						// Get v parameter (format: youtube.com?v=video_id)
						var p = uri.ParseQuery();
						if (p.ContainsKey("v"))
							videoID = p["v"];
						else {
							await DynamicSendMessage(e, ":musical_note: **Invalid youtube url!** _(Missing video id)_");
							queueing = false;
							return true;
						}

					} else if (uri.Host == "youtu.be") {
						// Get file name (format: youtu.be/video_id)
						var id = uri.AbsolutePath;
						if (id.Length > 1 && (id = id.Substring(1)).IndexOf('/') == -1) {
							videoID = id;
						} else {
							await DynamicSendMessage(e, ":musical_note: **Invalid youtube url!** _(Missing video id)_");
							queueing = false;
							return true;
						}
					} else {
						await DynamicSendMessage(e, ":musical_note: **Invalid youtube url!** _(Unacceptable uri host)_");
						queueing = false;
						return true;
					}
				} else {
					// Not an url, assume it's a video id
					status = await DynamicSendMessage(e, ":musical_note: *Checking if video exists...*");
					var statusCode = await WebRequestHelper.TestUrlAsync("https://www.youtube.com/oembed?format=json&url=http://www.youtube.com/watch?v=" + Uri.EscapeDataString(args[1]));
					if (statusCode == System.Net.HttpStatusCode.OK) {
						videoID = args[1];
					} else {
						await DynamicEditMessage(status, e.User, ":musical_note: **Invalid youtube video ID!** _(Video not found)_");
						queueing = false;
						return true;
					}
				}

				string filename = Path.Combine(QUEUE_TEMP_FOLDER, videoID) + ".mp3";
				string url = "http://www.youtube.com/watch?v=" + videoID;

				int index;
				if ((index = me.songHistory.FindIndex(s => s.filename == filename)) != -1) {
					// Song has been played previously, just queue that one
					Song song = me.songHistory[index];

					me.songQueue.Enqueue(song);
					if (status == null) status = await DynamicSendMessage(e, ":musical_note: **Song queued!**\n" + song.name + "\n" + song.url);
					await DynamicEditMessage(status, e.User, ":musical_note: **Song queued!**\n" + song.name + "\n" + song.url);
				} else {
					// Make a new cache entry of it

					if (File.Exists(filename))
						File.Delete(filename);

					// Start downloading
					if (status == null) status = await DynamicSendMessage(e, ":musical_note: *Downloading video...*");
					else status = await DynamicEditMessage(status, e.User, ":musical_note: *Downloading video...*");
					LogHelper.LogInformation("Downloading youtube video " + Convert.ToString(url));
					AudioHelper.DownloadedVideo vid = await AudioHelper.DownloadYoutubeVideo(videoID);

					// Start converting
					await DynamicEditMessage(status, e.User, ":musical_note: *Download complete! Converting to mp3...*");
					LogHelper.LogInformation("Download complete, converting to mp3...");

					try {
						await AudioHelper.ConvertToMp3(vid.videoFilename, filename);
					} finally {
						File.Delete(vid.videoFilename);
					}
					LogHelper.LogSuccess("Task complete!");

					// Queue song
					var song = new Song {
						filename = filename,
						name = vid.videoTitle,
						id = videoID,
					};
					me.songQueue.Enqueue(song);
					me.songHistory.Add(song);
					await DynamicEditMessage(status, e.User, ":musical_note: **Song queued!**\n" + vid.videoTitle + "\n" + vid.videoUri);
				}

				me.StartMusicPlayer(e);

				// Start queueing another song in another thread
				if (me.songQueue.Count < 2 && me.songPlaylist.Count > 0)
					me.QueuePlaylistedSong(e);
				else
					queueing = false;

				return true;

			}
		}
		#endregion

		async void StartMusicPlayer(MessageEventArgs e) {
			// Join channel
			if ((audio?.Channel == null || audio.State == ConnectionState.Disconnected) && e.User.VoiceChannel != null)
				audio = await AudioExtensions.JoinAudio(e.User.VoiceChannel);

			// Start music player
			if (idlePlayer && audio?.State == ConnectionState.Connected) {
				musicPaused = false;
				musicStopped = false;

				(new Thread(() => {
					MusicPlayer();
				}) {
					Name = "DiscordBot.MusicBot",
					Priority = ThreadPriority.AboveNormal
				}).Start();
			}
		}

		void MusicPlayer() {
			try {
				idlePlayer = false;
				LogHelper.LogInformation("Started music player!");
				while (songQueue.Count > 0 && !musicStopped) {
					musicSkip = false;

					// Queue upcoming songs
					if (!cmdQueue.queueing)
						QueuePlaylistedSong();

					// Queue next song
					songCurrent = songQueue.Dequeue();
					var song = songCurrent.Value;
					if (!File.Exists(song.filename)) continue;

					// Play it
					LogHelper.LogInformation("Starting song \"" + song.name + "\"");
					client.SetGame(song.name, GameType.Default, song.url);
					SendMp3(audio, song.filename).Wait();

					// Wait for it to finish playing
					try {
						Task.Run(() => audio.Wait()).WaitAndUnwrapException();
					} catch (OperationCanceledException) {}
					songCurrent = null;

					// Wait some before quitting
					if (songQueue.Count == 0 && !musicStopped) {
						if (!TaskHelper.WaitUntil(350, 15000, () => songQueue.Count > 0 || musicStopped).WaitAndUnwrapException())
							musicStopped = true;
					}
				}
				LogHelper.LogInformation("Stopped music player");
				client.SetGame(null);
				audio.Disconnect().Wait();
				audio = null;
				idlePlayer = true;
			} catch (Exception err) {
				LogHelper.LogException("Unexpected exception in the music player!", err);
			}
		}

		public async Task SendMp3(IAudioClient client, string filename) {
			try {
				int channelCount = client.Server.Client.GetService<AudioService>().Config.Channels;
				var outFormat = new WaveFormat(48000, 16, channelCount); // Format supported by discord.

				using (var MP3Reader = new Mp3FileReader(filename))
				using (var resampler = new MediaFoundationResampler(MP3Reader, outFormat)) {
					resampler.ResamplerQuality = 60; // Highest quality ^^
					int blockSize = outFormat.AverageBytesPerSecond / 50;
					byte[] buffer = new byte[blockSize];
					int byteCount;

					// Read audio into our buffer, and keep a loop open while data is present
					while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0) {
						// Pause and stop
						if (musicPaused) await TaskHelper.WaitUntil(1000, () => !musicPaused || musicStopped);
						if (musicStopped) break;
						if (musicSkip) break;

						// Read frame
						if (byteCount < blockSize) {
							// Incomplete frame
							for (int i = byteCount; i < blockSize; i++)
								buffer[i] = 0;
						}

						// Change volume
						buffer = AudioHelper.ScaleVolumeSafeNoAlloc(buffer, volume * VOLUME_MULTIPLIER);

						// Add to client out stream queue
						client.Send(buffer, 0, blockSize);
					}
				}
			} catch (Exception err) {
				LogHelper.LogException("Unexpected exception while sending MP3 data...", err);
			}
		}

		private void QueuePlaylistedSong(MessageEventArgs e = null) {
			if (songPlaylist.Count == 0) return;

			(new Thread(() => {
			Song q = songPlaylist.Dequeue();
			string filename = Path.Combine(QUEUE_TEMP_FOLDER, q.id) + ".mp3";
			string url = "http://www.youtube.com/watch?v=" + q.id;

			int index;
			if ((index = songHistory.FindIndex(s => s.filename == filename)) != -1) {
				// Song has been played previously, just queue that one
				Song song = songHistory[index];
				songQueue.Enqueue(song);
			} else {
				// Make a new cache entry of it

				if (File.Exists(filename))
					File.Delete(filename);

				// Start downloading
				LogHelper.LogInformation("Downloading youtube video " + Convert.ToString(url));
				AudioHelper.DownloadedVideo vid = AudioHelper.DownloadYoutubeVideo(q.id).Result;

				// Start converting
				LogHelper.LogInformation("Download complete, converting to mp3...");

				try {
					AudioHelper.ConvertToMp3(vid.videoFilename, filename).Wait();
				} finally {
					File.Delete(vid.videoFilename);
				}
				LogHelper.LogSuccess("Task complete!");

				// Queue song
				var song = new Song {
					filename = filename,
					name = vid.videoTitle,
					id = vid.videoID,
				};
				songQueue.Enqueue(song);
				songHistory.Add(song);
			}

			if (e != null) StartMusicPlayer(e);

			// Start queueing another song in another thread
			if (songQueue.Count < 2 && songPlaylist.Count > 0)
				QueuePlaylistedSong(e);

			}) { Name = "DiscordBot.PlaylistQueuer", IsBackground = true }).Start();
		}

		public async Task<List<Song>> FetchPlaylistItems(string playlistID) {
			var youtubeService = new YouTubeService(new BaseClientService.Initializer()
			 {
				ApiKey = SaveData.singleton.Youtube_Key,
				ApplicationName = this.GetType().ToString()
			});

			List<Song> songList = new List<Song>();
			

			var nextPageToken = "";
			while (nextPageToken != null) {
				var playlistItemsListRequest = youtubeService.PlaylistItems.List("snippet");
				playlistItemsListRequest.MaxResults = 50;
				playlistItemsListRequest.PageToken = nextPageToken;
				playlistItemsListRequest.PlaylistId = playlistID;

				// Retrieve the list of videos uploaded to the authenticated user's channel.
				var playlistItemsListResponse = await playlistItemsListRequest.ExecuteAsync();

				foreach (var playlistItem in playlistItemsListResponse.Items) {
					//Console.WriteLine("{0} ({1})", playlistItem.Snippet.Title, playlistItem.Snippet.ResourceId.VideoId);
					songList.Add(new Song { id = playlistItem.Snippet.ResourceId.VideoId, name = playlistItem.Snippet.Title });
				}

				nextPageToken = playlistItemsListResponse.NextPageToken;
			}

			return songList;
		}

		public struct Song {
			public string id;
			public string name;
			public string filename;
			public string url => "http://www.youtube.com/watch?v=" + id;
		}

	}
}
