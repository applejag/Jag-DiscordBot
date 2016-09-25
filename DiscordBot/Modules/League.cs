using Discord;
using DiscordBot.Utility;
using Newtonsoft.Json;
using Nito.AsyncEx.Synchronous;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static DiscordBot.Modules.Command;

namespace DiscordBot.Modules {
	public sealed class League : Module {
		public override string modulePrefix { get; } = "lol";
		private const string API_KEY = "RGAPI-E58B870E-8943-402B-8CB6-9E49E8AB4204";
		private const int TIMER_DELAY_SECONDS = 60*5;

		private CancellationTokenSource timerTokenSource = new CancellationTokenSource();
		private CancellationToken timerToken;

		Dictionary<string, Player> players { get; set; }

		public override void Init() {
			players = new Dictionary<string, Player>();
			if (SaveData.singleton.League_players != null && SaveData.singleton.League_players.Length > 0) {
				LoadPlayers().WaitAndUnwrapException();
			}

			// Starts timer
			timerToken = timerTokenSource.Token;
			Timer();

			// Create commands
			AddCommand(cmdAdd);
			AddCommand(cmdDel);
			AddCommand(cmdRefresh);
			AddCommand(cmdList);
		}

		public override void Unload() {
			timerTokenSource.Cancel();
			SaveData.singleton.League_players = players.Values.ToArray();
			players.Clear();
			players = null;

			// Remove commands
			RemoveCommand(cmdAdd);
			RemoveCommand(cmdDel);
			RemoveCommand(cmdRefresh);
			RemoveCommand(cmdList);
		}

		#region Command definitions
		private CmdAdd cmdAdd = new CmdAdd();
		public sealed class CmdAdd : Command<League> {
			public override string name { get; } = "add";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description { get; } = "Add a player to the list of notifications! Once added the bot will every now and then check their rank to see if they have ranked up, and if so give them a congratulations message.\n**Note:** This binds a discord user to a League of Legends account, but also the channel the command is executed in, for it's that channel that the bot will send the message in.";
			public override string usage { get; } = "<Discord mention> <LoL IGN>";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				// Only command
				if (args.Length == 1) {
					return false;
				}
				// Too few arguments, requires at least 2
				if (args.Length == 2) {
					return false;
				}

				ulong user_id;
				string dis = e.Server.Users.GetFirstMentionInString(rest, out user_id);
				string lol = rest.Substring(dis.Length).Trim();
				User user = e.Server.GetUser(user_id);

				if (rest.IndexOf(dis) != 0
				|| string.IsNullOrWhiteSpace(lol)) {
					// Wrong syntax. Discord user FIRST
					return false;
				}

				if (me.players.Values.Any(p => p.discord_user == user_id)) {
					Player taken = me.players.Values.First(p => p.discord_user == user_id);
					await DynamicSendMessage(e, "User `" + taken.name + "` is already registered! Please unregister user " + e.Server.GetUser(taken.discord_user).Mention + " first, then try again.");
					return false;
				}

				LogHelper.LogInformation("Adding player '" + lol + "' to account @" + user.Name + "#" + user.Discriminator + ", validating using Riots API...");
				Message status = await DynamicSendMessage(e, "Checking with Riot Games API for an `" + lol + "`...");

				Player player = null;
				try {

					player = await Player.FetchFromName(lol);
					player.discord_channel = e.Channel.Id;
					player.discord_user = user_id;
					player.discord_server = e.Server.Id;
					await player.CheckRank(me);

					me.players.Add(player.name, player);
					LogHelper.LogSuccess("Player '" + player.name + "' (summoner:" + player.id + ") has been added!");
					await DynamicEditMessage(status, e.User, "Player `" + player.name + "` successfully added!");

				} catch (Exception err) {
					// Cleanup
					if (player != null)
						me.players.Remove(player.name);

					LogHelper.LogException("Unexpected error when fetching player!", err);
					await DynamicEditMessage(status, e.User, "Unexpected error when adding player `" + lol + "`\n```" + err.Message + "```");
					throw;
				}
				return true;
			}
		}

		private CmdDel cmdDel = new CmdDel();
		public sealed class CmdDel : Command<League> {
			public override string name { get; } = "delete";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description { get; } = "Opposite of add. Removes a player from the watch list.";
			public override string usage { get; } = "<Discord mention>";
			public override string[] alias { get; internal set; } = { "del", "remove" };

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length == 1) {
					return false;
				}

				rest = rest.Trim();
				LogHelper.LogInformation("Removing player '" + rest + "'...");
				ulong user;
				string dis = e.Server.Users.GetFirstMentionInString(rest, out user);
				if (string.IsNullOrWhiteSpace(dis) || rest.IndexOf(dis) != 0) {
					return false;
				}

				try {
					Player player = me.players.Values.First(p => p.discord_user == user);
					me.players.Remove(player.name);
					player.dead = true;
					LogHelper.LogSuccess("Player '" + player.name + "' (summoner:" + player.id + ") has been unregistered!");
					await DynamicSendMessage(e, "Player `" + player.name + "` has successfully been unregistered from " + dis + "!");

				} catch (Exception err) {
					LogHelper.LogException("Unexpected error when unregistering player!", err);
					await DynamicSendMessage(e, "Unexpected error when unregistering user " + dis + "\n```" + err.Message + "```");
					throw;
				}
				return true;
			}
		}

		private CmdRefresh cmdRefresh = new CmdRefresh();
		public sealed class CmdRefresh : Command<League> {
			public override string name { get; } = "refresh";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description { get; } = "Does a manual check for all added users.";
			public override string usage { get; } = "";
			public override string[] alias { get; internal set; } = { "update", "check" };

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				Message status = await DynamicSendMessage(e, "Refreshing all users...");

				LogHelper.LogInformation("Refreshing league player data...");
				string bugs = string.Empty;
				int count = 0;

				foreach (var p in me.players.Values) {
					try {
						// Fetch rank
						await p.CheckRank(me);

						LogHelper.LogSuccess("'" + p.name + "' has been refreshed.");
						count++;

						await Task.Delay(1000);
					} catch (Exception err) {
						bugs += "\nError while loading stats for `" + p.name + "` (" + e.Server.GetUser(p.discord_user) + ")\n```" + err.Message + "```\n";
						LogHelper.LogException("Unexpected exception while fetching LoL player data!", err);
						throw;
					}
				}

				if (count == me.players.Count) {
					await DynamicEditMessage(status, e.User, string.Format("Reloaded {0}/{0} profiles successfully.", me.players.Count, me.players.Count));
				} else {
					await DynamicEditMessage(status, e.User, string.Format("Reloaded {0}/{1} profiles successfully.\n", count, me.players.Count) + bugs);
				}
				return true;
			}
		}

		private CmdList cmdList = new CmdList();
		public sealed class CmdList : Command<League> {
			public override string name { get; } = "list";
			public override CommandPerm requires { get; } = CommandPerm.Whitelist;
			public override string description { get; } = "Sends a list of added players. Shows their IGN, Discord mention, and their LoL rank.";
			public override string usage { get; } = "";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (me.players.Count == 0) {
					await DynamicSendMessage(e, "**No users are added.**");
				} else {
					await DynamicSendMessage(e, "**Users that's logged** *(" + me.players.Count + " players)*\n"
						+ me.players.Values.Sum(p => string.Format("- **{0}** *(tagged to {1})*  League {2}{3}\n",
							  p.name,
							  bot.client.GetServer(p.discord_server)?.GetChannel(p.discord_channel)?.GetUser(p.discord_user)?.Mention ?? "null",
							  p.rank.ToString(),
							  p.rank.name == null ? string.Empty : (" in *" + p.rank.name + "*")
						  )).TrimEnd('\n')
						);
				}
				return true;
			}
		}
		#endregion

		private async Task LoadPlayers() {
			LogHelper.LogInformation("Started fetching player data...");
			// Reload from 'net
			foreach (var p in SaveData.singleton.League_players) {
				try {
					// Fetch rank
					await p.CheckRank(this);
					await Task.Delay(1000);

					LogHelper.LogSuccess("Downloaded rank for player '" + p.name + "'");
					players.Add(p.name, p);
				} catch (Exception err) {
					LogHelper.LogException("Unexpected exception while fetching LoL player data!", err);
				}
			}
			LogHelper.LogSuccess("Done downloading player data!");
		}

		async void Timer() {
			try {
				Stack<Player> stack = new Stack<Player>();

				while (true) {
					await Task.Delay(TIMER_DELAY_SECONDS * 1000, timerToken);
					if (timerToken.IsCancellationRequested) break;

					// Iterate stack
					if (stack.Count == 0) {
						foreach (var p in players.Values) {
							stack.Push(p);
						}
					}
					while (stack.Count != 0) {
						Player player = stack.Pop();
						if (player.dead) continue;

						try {
							// Fetch rank
							bool levelup = await player.CheckRank(this);

							if (levelup)
								LogHelper.LogSuccess("'" + player.name + "' gained a rank! (via automatic update).");
						} catch (WebRequestHelper.MyHttpWebException) {
							//bugs += "\nError while loading stats for `" + p.name + "` (" + e.Server.GetUser(p.discord_user) + ")\n```" + err.Message + "```\n";
							LogHelper.LogWarning(string.Format("Error while trying to automatically check player rank for '{0}'. Will retry in {1} minutes",
								player?.name == null ? "null" : player.name,
								players.Count * 5));
						} catch (Exception err) {
							LogHelper.LogException("Unexpected exception while fetching LoL player data!", err);
						}
						break;
					}
				}
			} catch (TaskCanceledException) {
				LogHelper.LogInformation("League timer has been canceled!");
			} catch (Exception err) {
				LogHelper.LogException("League timer has crashed!", err);
			}
		}

		public static string URLGetSummonerByName(string name) {
			return string.Format("https://euw.api.pvp.net/api/lol/euw/v1.4/summoner/by-name/{0}?api_key={1}", name, API_KEY);
		}

		public static string URLGetSummonerById(long id) {
			return string.Format("https://euw.api.pvp.net/api/lol/euw/v1.4/summoner/{0}?api_key={1}", id, API_KEY);
		}

		public static string URLGetLeagueEntriesBySummonerId(long id) {
			return string.Format("https://euw.api.pvp.net/api/lol/euw/v2.5/league/by-summoner/{0}/entry?api_key={1}", id, API_KEY);
		}

		public static string URLGetProfilePicture(int picture_id) {
			return string.Format("http://ddragon.leagueoflegends.com/cdn/6.18.1/img/profileicon/{0}.png", picture_id);
		}

		[Serializable]
		public class Player {
			public bool dead;

			public long id;
			public string name;
			public long summonerLevel;
			public int profileIconId;

			public ulong discord_user;
			public ulong discord_channel;
			public ulong discord_server;

			public Rank rank;

			private async Task Celebrate(Module module, Rank old) {
				Server server = module.client.GetServer(discord_server);
				Channel channel = server.GetChannel(discord_channel);
				User user = server.GetUser(discord_user);

				await channel.SafeSendMessage(":dancer:**Congratulations " + user.Mention + " !!** :confetti_ball::tada:\n\n:yellow_heart::purple_heart: You just promoted yourself from *" + old + "* to **" + rank + "** :metal::boom:");
				await channel.SendFileFromWeb(URLGetProfilePicture(profileIconId));
			}

			public async Task<bool> CheckRank(Module module) {
				if (string.IsNullOrWhiteSpace(rank.division) || string.IsNullOrWhiteSpace(rank.tier)) {
					// Haven't been checked even onceuu
					rank = await Rank.FetchFromID(id);
					name = rank.entries[0].playerOrTeamName ?? name;
				} else {
					Rank old = rank;
					rank = await Rank.FetchFromID(id);
					name = rank.entries[0].playerOrTeamName ?? name;

					if (Rank.HigherRank(rank, old)) {
						// Our new rank is higher!
						await Celebrate(module, old);
						return true;
					}
				}
				return false;
			}

			public static async Task<Player> FetchFromID(long id) {
				int tries = 3;
			Retry:
				try {
					HttpWebRequest request = WebRequest.CreateHttp(URLGetSummonerById(id));
					return JsonConvert.DeserializeObject<Dictionary<string, Player>>(await request.GetHTMLContent()).First().Value;
				} catch (WebRequestHelper.MyHttpWebException err) {
					if (err.StatusCode == HttpStatusCode.InternalServerError && tries > 0) {
						tries--;
						LogHelper.LogWarning("Internal server error on fetching player from ID! Retrying...");
						await Task.Delay(1000);
						goto Retry;
					}
					throw;
				}
			}

			public static async Task<Player> FetchFromName(string name) {
				int tries = 3;
			Retry:
				try {
					HttpWebRequest request = WebRequest.CreateHttp(URLGetSummonerByName(name));
					return JsonConvert.DeserializeObject<Dictionary<string, Player>>(await request.GetHTMLContent()).First().Value;
				} catch (WebRequestHelper.MyHttpWebException err) {
					if (err.StatusCode == HttpStatusCode.InternalServerError && tries > 0) {
						tries--;
						LogHelper.LogWarning("Internal server error on fetching player from name! Retrying...");
						await Task.Delay(1000);
						goto Retry;
					}
					throw;
				}
			}
		}

		[Serializable]
		public class Rank {

			public string division { get {
					return entries != null && entries.Length > 0 ? entries[0].division : string.Empty;
				} set {
					if (entries != null && entries.Length > 0) entries[0].division = value;
				} }
			public string tier;
			public Entries[] entries;
			public string queue;
			public string name;
			public string participantId;

			[Serializable]
			public struct Entries {
				public string division;
				public bool isFreshBlood;
				public bool isHotStreak;
				public bool isInactive;
				public bool isVeteran;
				public int leaguePoints;
				public int losses;
				public string playerOrTeamId;
				public string playerOrTeamName;
				public string playStyle;
				public int wins;
				public Promos miniSeries;

				[Serializable]
				public struct Promos {
					public string progress; // Example : "WNN"
					public int target;		// nr of wins for promotion
					public int losses;		// nr of current losses
					public int wins;		// nr of current wins
				}
			}

			public static async Task<Rank> FetchFromID(long id) {
				int tries = 3;
			Retry:
				try {
					HttpWebRequest request = WebRequest.CreateHttp(URLGetLeagueEntriesBySummonerId(id));
					return JsonConvert.DeserializeObject<Dictionary<string, List<Rank>>>(await request.GetHTMLContent()).Values?.First().First(r => r?.queue == "RANKED_SOLO_5x5");
				} catch (WebRequestHelper.MyHttpWebException err) {
					if (err.StatusCode == HttpStatusCode.InternalServerError && tries > 0) {
						tries--;
						LogHelper.LogWarning("Internal server error on fetching rank from ID! Retrying...");
						await Task.Delay(1000);
						goto Retry;
					}
					if (err.StatusCode == HttpStatusCode.NotFound)
						return new Rank { tier = "UNRANKED", entries = new Entries[] { new Entries { division = "UNRANKED" } } };
					else throw;
				}
			}

			public override string ToString() {
				if (tier == "UNRANKED") return tier;
				else return tier + " " + division;
			}

			// is a > b ?
			public static bool HigherRank(Rank a, Rank b) {
				if (a.tier == b.tier) return HigherDivision(a, b);
				else return HigherTier(a, b);
			}

			// is a > b ?
			public static bool HigherTier(Rank a, Rank b) {
				return TierFromString(a.tier) > TierFromString(b.tier);
			}

			public static int TierFromString(string tier) {
				switch (tier) {
					case "UNRANKED": return -1;
					case "BRONZE": return 0;
					case "SILVER": return 1;
					case "GOLD": return 2;
					case "PLATINUM": return 3;
					case "DIAMOND": return 4;
					case "MASTER": return 5;
					case "CHALLENGER": return 6;

					default:
						throw new ArgumentException("Invalid tier! '" + tier + "' is not a valid tier", "tier");
				}
			}

			// is a > b ?
			public static bool HigherDivision(Rank a, Rank b) {
				return DivisionFromString(a.division) > DivisionFromString(b.division);
			}

			public static int DivisionFromString(string division) {
				switch (division) {
					case "UNRANKED": return -1;
					case "V": return 0;
					case "IV": return 1;
					case "III": return 2;
					case "II": return 3;
					case "I": return 4;

					default:
						throw new ArgumentException("Invalid division! '" + division + "' is not a valid division", "division");
				}
			}
		}
	}
}
