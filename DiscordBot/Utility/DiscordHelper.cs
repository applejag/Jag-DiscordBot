using Discord;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Utility {
	public static class DiscordHelper {

		public const int MESSAGE_TIMEOUT = 5000; // 5 sec
		public const int MESSAGE_CHECK_DELAY = 250; // 1/4 sec

		/// <summary>
		/// Finds the first valid mention of someone in a list of users within a string.
		/// </summary>
		/// <param name="users">The list of users in question</param>
		/// <param name="message">The text string to search</param>
		/// <param name="user">The user found</param>
		/// <returns>The mention that was matched</returns>
		public static string GetFirstMentionInString(this IEnumerable<User> users, string message, out User user) {
			user = null;
			if (string.IsNullOrWhiteSpace(message)) return null;

			foreach (User u in users) {
				if (message.IndexOf(u.Mention) != -1) {
					user = u;
					return u.Mention;
				}
				if (message.IndexOf(u.NicknameMention) != -1) {
					user = u;
					return u.NicknameMention;
				}
				var nameMention = string.Format("@{0}", u.Name);
				if (message.IndexOf(nameMention) != -1) {
					user = u;
					return nameMention;
				}

				nameMention = string.Format("@{0}#{1}", u.Name, u.Discriminator);
				if (message.IndexOf(nameMention) != -1) {
					user = u;
					return nameMention;
				}
			}

			throw new Exception("Mention not found");
		}

		/// <summary>
		/// Finds the first valid mention of someone in a list of users within a string.
		/// </summary>
		/// <param name="users">The list of users in question</param>
		/// <param name="message">The text string to search</param>
		/// <param name="id">The id of the user found</param>
		/// <returns>The mention that was matched</returns>
		public static string GetFirstMentionInString(this IEnumerable<User> users, string message, out ulong id) {
			User user = null;
			string mention = GetFirstMentionInString(users, message, out user);
			id = user.Id;
			return mention;
		}

		public static async Task<Message> SafeSendMessage(this User user, string text) {
			Message msg = await user.PrivateChannel.SendMessage(text);
			try {
				await TaskHelper.WaitUntil(MESSAGE_CHECK_DELAY, MESSAGE_TIMEOUT, () => msg.State != MessageState.Queued);
			} catch {
				throw new TimeoutException("Timeout while waiting for message to be sent.");
			}
			return msg;
		}

		public static async Task<Message> SafeSendMessage(this Channel channel, string text) {
			Message msg = await channel.SendMessage(text);
			try {
				await TaskHelper.WaitUntil(MESSAGE_CHECK_DELAY, MESSAGE_TIMEOUT, () => msg.State != MessageState.Queued);
			} catch {
				throw new TimeoutException("Timeout while waiting for message to be sent.");
			}
			return msg;
		}

		public static async Task<Message> SafeSendFile(this Channel channel, string filePath) {
			Message msg = await channel.SendFile(filePath);
			try {
				await TaskHelper.WaitUntil(MESSAGE_CHECK_DELAY, MESSAGE_TIMEOUT, () => msg.State != MessageState.Queued);
			} catch {
				throw new TimeoutException("Timeout while waiting for file to be sent.");
			}
			return msg;
		}

		public static async Task<Message> SafeEdit(this Message message, string newText) {
			await message.Edit(newText);
			return message.Channel.GetMessage(message.Id);
		}

		public static async Task<Message> SafeDelete(this Message message) {
			await message.Delete();
			return message;
		}

		public static async Task<Message> DynamicSendMessage(MessageEventArgs e, string text) {
			if (e.Message.IsAuthor) {
				// Dont need to mention myself
				return await e.Message.SafeEdit(text);
			} else {
				// Mention if it isnt in a private chat
				Message msg = await e.Channel.SafeSendMessage(
					e.Channel.IsPrivate
					?   text
					:   e.User.Mention + " " + text
				);
				return msg;
			}
		}

		public static async Task<Message> DynamicEditMessage(Message message, string newText, bool isMe) {
			if (isMe) {
				// Dont need to mention myself
				return await message.SafeEdit(newText);
			} else {
				// Mention if it isnt in a private chat
				return await message.SafeEdit(
					message.Channel.IsPrivate
					? newText
					: message.User.Mention + " " + newText
				);
			}
		}
		
		/// <summary>
		/// Downloads a file to a temporaryly from <paramref name="url"/> and sends it.
		/// </summary>
		/// <param name="channel">The channel the file will be sent to</param>
		/// <param name="url">The url to download the file from</param>
		public static async Task<Message> SendFileFromWeb(this Channel channel, string url) {
			using (var client = new WebClient()) {
				LogHelper.LogInformation("Started downloading file from \"" + url + "\"");

				DateTime start = DateTime.Now;
				// Create directory if needed

				string tmp = Path.GetTempFileName();
				string filename = Path.ChangeExtension(tmp, new FileInfo(new Uri(url).AbsolutePath).Extension ?? ".txt");
				new FileInfo(filename).Directory.Create();
				// Start downling
				await client.DownloadFileTaskAsync(url, filename);
				double span = (DateTime.Now - start).TotalSeconds;

				await Task.Delay(300);
				
				LogHelper.LogSuccess(string.Format("Download complete! File has been saved at \"{0}\" (took {1:0.00} {2})", filename, span, span == 1d ? "second" : "seconds"));

				// Time to send it
				Message message = await channel.SafeSendFile(filename);
				LogHelper.LogSuccess("File sent into channel '" + channel.Name + "'");

				// Now we can remove the file
				File.Delete(filename);
				LogHelper.LogInformation("Temporary file has been removed.");

				return message;
			}
		}


		/// <summary>
		/// Saves <paramref name="image"/> to a temporary location, sends it, then deletes it.
		/// </summary>
		/// <param name="channel">The channel the file will be sent to</param>
		/// <param name="image">The image to be sent</param>
		public static async Task<Message> SendImage(this Channel channel, Image image) {
			using (var client = new WebClient()) {
				LogHelper.LogInformation("Sending raw image...");

				DateTime start = DateTime.Now;
				// Create directory if needed

				string tmp = Path.GetTempFileName();
				string filename = Path.ChangeExtension(tmp, ".png");
				new FileInfo(filename).Directory.Create();

				image.Save(filename, ImageFormat.Png);
								
				// Time to send it
				Message message = await channel.SafeSendFile(filename);

				double span = (DateTime.Now - start).TotalSeconds;
				LogHelper.LogSuccess(string.Format("Image sent to channel '{0}' (took {1:0.00} {2})", channel.Name, span, span == 1d ? "second" : "seconds"));

				// Now we can remove the file
				File.Delete(filename);
				LogHelper.LogInformation("Temporary file has been removed.");

				return message;
			}
		}
	}
}
