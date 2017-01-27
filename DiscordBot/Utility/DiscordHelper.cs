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
		
		public const int MESSAGE_CHECK_LOOP_DELAY = 250; // 1/4 sec
		public const int MESSAGE_LENGTH_LIMIT = 2000;

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

		/// <summary>
		/// Sends a private message to <paramref name="user"/> and waits until it's no longer queued.
		/// </summary>
		public static async Task<Message> SafeSendMessage(this User user, string text) {
			if (user?.PrivateChannel == null)
				throw new ArgumentNullException("user");
			if (string.IsNullOrWhiteSpace(text))
				throw new ArgumentException("Invalid text");
			if (text.Length > MESSAGE_LENGTH_LIMIT)
				throw new ArgumentException("Message text is too long! Message had " + text.Length + " characters while maximum is " + MESSAGE_LENGTH_LIMIT, "text");

			Message msg = await user.PrivateChannel.SendMessage(text);
			await TaskHelper.WaitUntil(MESSAGE_CHECK_LOOP_DELAY, () => msg.State != MessageState.Queued);
			return msg;
		}

		/// <summary>
		/// Sends a text message to <paramref name="channel"/> and waits until it's no longer queued.
		/// </summary>
		public static async Task<Message> SafeSendMessage(this Channel channel, string text) {
			if (channel == null)
				throw new ArgumentNullException("channel");
			if (string.IsNullOrWhiteSpace(text))
				throw new ArgumentException("Invalid text");
			if (text.Length > MESSAGE_LENGTH_LIMIT)
				throw new ArgumentException("Message text is too long! Message had " + text.Length + " characters while maximum is " + MESSAGE_LENGTH_LIMIT, "text");

			Message msg = await channel.SendMessage(text);
			await TaskHelper.WaitUntil(MESSAGE_CHECK_LOOP_DELAY, () => msg.State != MessageState.Queued);
			return msg;
		}

		/// <summary>
		/// Sends a file from <paramref name="filePath"/> and waits until it's no longer queued.
		/// </summary>
		public static async Task<Message> SafeSendFile(this Channel channel, string filePath) {
			if (channel == null)
				throw new ArgumentNullException("channel");
			if (string.IsNullOrWhiteSpace(filePath))
				throw new ArgumentNullException("filePath");
			if (!File.Exists(filePath))
				throw new FileNotFoundException("Attachment file not found!", filePath);

			Message msg = await channel.SendFile(filePath);
			await TaskHelper.WaitUntil(MESSAGE_CHECK_LOOP_DELAY, () => msg.State != MessageState.Queued);
			return msg;
		}

		/// <summary>
		/// Sometimes the message object you edit dont always update. This makes sure to return the updated message.
		/// </summary>
		public static async Task<Message> SafeEdit(this Message message, string newText) {
			if (message == null)
				throw new ArgumentNullException("message");
			if (string.IsNullOrWhiteSpace(newText))
				throw new ArgumentException("Invalid text");
			if (newText.Length > MESSAGE_LENGTH_LIMIT)
				throw new ArgumentException("Message text is too long! Message had " + newText.Length + " characters while maximum is " + MESSAGE_LENGTH_LIMIT, "text");

			await message.Edit(newText);
			return message.Channel.GetMessage(message.Id);
		}

		/// <summary>
		/// <para>This is here in case the Discord.Net library changes so it doesn't get fully deleted.</para>
		/// <para>It's identical to the original for now.</para>
		/// </summary>
		public static async Task<Message> SafeDelete(this Message message) {
			if (message == null)
				throw new ArgumentNullException("message");

			await message.Delete();
			return message;
		}

		/// <summary>
		/// Dynamically checks if the reply to the <see cref="MessageEventArgs"/> needs to include a mention, based of if it's a selfbot and private or public channel.
		/// </summary>
		public static async Task<Message> DynamicSendMessage(this MessageEventArgs e, string text) {
			if (e?.Message == null || e?.Channel == null)
				throw new ArgumentNullException("e");

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

		/// <summary>
		/// Similar to the <see cref="DynamicSendMessage(MessageEventArgs, string)"/>, where it checks if the message needs to include a mention.
		/// </summary>
		public static async Task<Message> DynamicEditMessage(this Message message, User other, string newText) {
			if (message == null)
				throw new ArgumentNullException("message");

			if (message.User.Id == other.Id) {
				// Dont need to mention myself
				return await message.SafeEdit(newText);
			} else {
				// Mention if it isnt in a private chat
				return await message.SafeEdit(
					message.Channel.IsPrivate
					? newText
					: other.Mention + " " + newText
				);
			}
		}
		
		public static async Task<Message> SendFileFromWeb(this Channel channel, string url, string ext = ".txt") {
			return await SendFileFromWeb(channel, url, Path.GetTempFileName().Replace(".tmp", string.Empty), ext);
		} 

		/// <summary>
		/// Downloads a file to a temporaryly from <paramref name="url"/> and sends it.
		/// </summary>
		/// <param name="channel">The channel the file will be sent to</param>
		/// <param name="url">The url to download the file from</param>
		/// <param name="title">The name of the file to be sent</param>
		/// <param name="ext">If the url does not contain an extension then use this value</param>
		public static async Task<Message> SendFileFromWeb(this Channel channel, string url, string title, string ext = ".txt") {
			if (channel == null)
				throw new ArgumentNullException("channel");
			if (string.IsNullOrWhiteSpace(url))
				throw new ArgumentNullException("url");

			using (var client = new WebClient()) {
				LogHelper.LogInformation("Started downloading file from \"" + url + "\"");

				DateTime start = DateTime.Now;
				// Create directory if needed

				string tmp = Path.GetInvalidFileNameChars().Aggregate(title, (current, c) => current.Replace(c, '_'));
				string filename = tmp + ext;
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
			if (channel == null)
				throw new ArgumentNullException("channel");
			if (image == null)
				throw new ArgumentNullException("image");

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
