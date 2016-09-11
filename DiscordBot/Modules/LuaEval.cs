﻿using System;
using System.Threading.Tasks;
using Discord;
using DiscordBot.Utility;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using System.Collections;
using System.Threading;
using Nito.AsyncEx.Synchronous;

namespace DiscordBot.Modules {
	public class LuaEval : Module {
		public Script script;
		internal CancellationTokenSource token = null;

		public override void Init() {
			script = new Script(CoreModules.Preset_SoftSandbox);
			UserData.RegisterProxyType<LuaInterface.MessageProxy, Message>(m => new LuaInterface.MessageProxy(m));
			UserData.RegisterProxyType<LuaInterface.UserProxy, User>(u => new LuaInterface.UserProxy(u));
			UserData.RegisterProxyType<LuaInterface.ChannelProxy, Channel>(c => new LuaInterface.ChannelProxy(c));
			UserData.RegisterProxyType<LuaInterface.ServerProxy, Server>(s => new LuaInterface.ServerProxy(s));
			UserData.RegisterProxyType<LuaInterface, MessageEventArgs>(e => new LuaInterface(this, e));

			AddCommand(cmdLua);
		}

		public override void Unload() {
			RemoveCommand(cmdLua);
		}

		private CmdLua cmdLua = new CmdLua();
		public sealed class CmdLua : Command<LuaEval> {
			public override string name { get; } = "lua";
			public override CommandPerm requires { get; } = CommandPerm.Selfbot;

			public override async Task Callback(MessageEventArgs e, string[] args, string rest) {
				Message status = null;
				try {
					status = await e.Channel.SafeSendMessage("**Executing code...**");

					if (me.token != null) {
						me.token.Cancel();
						LogHelper.LogWarning("Sent a cancel to previous Thread.");
					}
					me.token = new CancellationTokenSource();

					using (new LuaInterface(me, e)) {
						DynValue response = await (await me.script.LoadStringAsync(rest)).Function.CallAsync();
						
						if (response == null || response.IsVoid())
							await DynamicEditMessage(status, e.User, "**Done!**");
						else
							await DynamicEditMessage(status, e.User, "**Done!** Output:\n```fix\n" + response.ToString() + "```");
					}
 				} catch (Exception err) {
					LogHelper.LogException("Error while executing Lua!", err);
					if (status != null)
						await status.SafeEdit("**Error!**\n```\n" + err.Message + "```");
					else
						await e.Channel.SafeSendMessage("**Error!**\n```\n" + err.Message + "```");
				}
				me.token = null;
			}
		}
		
		public class LuaInterface : IDisposable {

			private Script script;
			private LuaEval me;
			private MessageEventArgs e;
			private CancellationToken token;

			private List<string> stack;

			public User User { get { return e.User; } }
			public Message Message { get { return e.Message; } }
			public Channel Channel { get { return e.Channel; } }
			public Server Server { get { return e.Server; } }

			[MoonSharpHidden]
			public LuaInterface(LuaEval me, MessageEventArgs e) {
				this.me = me;
				this.e = e;
				this.script = me.script;
				this.token = me.token.Token;
				stack = new List<string>();

				script.Globals["print"] = new Action<DynValue>(print);
				script.Globals["sleep"] = new Action<DynValue>(sleep);
				script.Globals["e"] = this;
			}

			public void print(DynValue text) {
				stack.Add(text.ToString());
			}

			public void sleep(DynValue time) {
				if (time.Type == DataType.Number)
					Task.Delay((int) (time.Number * 1000), token).Wait();
				else
					throw new ArgumentException("Invalid argument! (sleep)");
			}

			[MoonSharpHidden]
			public void Dispose() {
				string[] messages = StringHelper.SplitMessage(stack.ToArray(), 1984);
				for (int i=0; i<messages.Length; i++) {
					e.Channel.SafeSendMessage(messages[i]).Wait();
				}
			}

			public class ServerProxy {
				private Server target;

				public ulong Id { get { return target.Id; } }
				public string Name { get { return target.Name; } }
				public User Owner { get { return target.Owner; } }
				public bool IsOwner { get { return target.IsOwner; } }

				public int UserCount { get { return target.UserCount; } }
				public List<User> Users { get { return new List<User>(target.Users); } }

				public int ChannelCount { get { return target.ChannelCount; } }
				public List<Channel> AllChannels { get { return new List<Channel>(target.AllChannels); } }
				public List<Channel> TextChannels { get { return new List<Channel>(target.TextChannels); } }
				public List<Channel> VoiceChannels { get { return new List<Channel>(target.VoiceChannels); } }
				public Channel AFKChannel { get { return target.AFKChannel; } }
				public int AFKTimeout { get { return target.AFKTimeout; } }
				public Channel DefaultChannel { get { return target.DefaultChannel; } }

				public User CurrentUser { get { return target.CurrentUser; } }
				public List<string> Features { get { return new List<string>(target.Features); } }

				[MoonSharpHidden]
				public ServerProxy(Server p) {
					target = p;
				}

				public User FindUser(DynValue needle) {
					if (needle.Type == DataType.Number)
						return target.GetUser((ulong) needle.Number);
					else if (needle.Type == DataType.String) {
						if (needle.String.Contains('#')) {
							string name = needle.String.Substring(0, needle.String.IndexOf('#'));
							ushort num = ushort.Parse(needle.String.Substring(needle.String.IndexOf('#') + 1));
							return target.Users.First(u => u.Name == name && u.Discriminator == num);
						} else
							return target.FindUsers(needle.String).First();
					} else
						throw new ArgumentException("Invalid argument! (Channel.FindUser)");
				}
			}

			public class ChannelProxy {
				private Channel target;

				public ulong Id { get { return target.Id; } }
				public string Name { get { return target.Name; } }
				public string Mention { get { return target.Mention; } }
				public bool IsPrivate { get { return target.IsPrivate; } }
				public string Topic { get { return target.Topic; } }

				public List<Message> Messages { get { return new List<Message>(target.Messages); } }
				public List<User> Users { get { return new List<User>(target.Users); } }
				public Server Server { get { return target.Server; } }

				[MoonSharpHidden]
				public ChannelProxy(Channel p) {
					target = p;
				}

				public Message SendMessage(string text) {
					return target.SafeSendMessage(text).WaitAndUnwrapException();
				}

				public List<UserProxy> GetUsers() {
					return target.Users.Select(x => new UserProxy(x)).ToList();
				}

				public List<MessageProxy> GetMessages() {
					return target.Messages.Select(x => new MessageProxy(x)).ToList();
				}

				public User FindUser(DynValue needle) {
					if (needle.Type == DataType.Number)
						return target.GetUser((ulong) needle.Number);
					else if (needle.Type == DataType.String) {
						if (needle.String.Contains('#')) {
							string name = needle.String.Substring(0, needle.String.IndexOf('#'));
							ushort num = ushort.Parse(needle.String.Substring(needle.String.IndexOf('#') + 1));
							return target.Users.First(u => u.Name == name && u.Discriminator == num);
						} else
							return target.FindUsers(needle.String).First();
					} else
						throw new ArgumentException("Invalid argument! (Channel.FindUser)");
				}
			}

			public class UserProxy {
				private User target;

				public ulong Id { get { return target.Id; } }
				public string Name { get { return target.Name; } }
				public string Mention { get { return target.Mention; } }
				public string Nickname { get { return target.Nickname; } }
				public string NicknameMention { get { return target.NicknameMention; } }
				public Channel PrivateChannel { get { return target.PrivateChannel; } }
				public Channel VoiceChannel { get { return target.VoiceChannel; } }
				public Server Server { get { return target.Server; } }


				[MoonSharpHidden]
				public UserProxy(User p) {
					target = p;
				}

				public Message SendMessage(string text) {
					if (target.Client.CurrentUser.Id != target.Id)
						return target.SafeSendMessage(text).WaitAndUnwrapException();
					else
						throw new Exception("Invalid request! (User.SendMessage)");
				}
			}

			public class MessageProxy {
				private Message target;

				public ulong Id { get { return target.Id; } }
				public string Text { get { return target.Text; } }
				public string RawText { get { return target.RawText; } }
				public User User { get { return target.User; } }
				public Channel Channel { get { return target.Channel; } }
				public Server Server { get { return target.Server; } }

				[MoonSharpHidden]
				public MessageProxy(Message p) {
					target = p;
				}

				public void Edit(string text) {
					if (target.IsAuthor)
						target.SafeEdit(text).Wait();
					else
						throw new Exception("Permission denied! (Message.Edit)");
				}

				public void Delete() {
					target.SafeDelete().Wait();
				}
			}

		}
	}

}
