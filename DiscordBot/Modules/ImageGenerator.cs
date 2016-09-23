using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using System.Drawing;
using DiscordBot.Utility;

namespace DiscordBot.Modules {
	public sealed class ImageGenerator : Module {
		public override string modulePrefix { get; } = "imgg";

		public override void Init() {
			AddCommand(cmdText);
			AddCommand(cmdToggle);
		}

		public override void Unload() {
			RemoveCommand(cmdText);
			RemoveCommand(cmdToggle);
		}

		private CmdToggle cmdToggle = new CmdToggle();
		public sealed class CmdToggle : Command<ImageGenerator> {
			public override string name { get; } = "toggle";
			public override CommandPerm requires { get; } = CommandPerm.Selfbot;
			public override string description { get { return "This toggles that functionality on/off.\nThis is currently " + (enabled ? "enabled" : "disabled") + "."; } }
			public override string usage { get; } = "";

			private bool enabled = false;
			public async void OnCommandParseFailed(object sender, MessageEventArgs e) {
				if (!bot.isSelfbot || !e.Message.IsAuthor) return;

				await e.Message.SafeDelete();
				await Task.Delay(800);
				using (Image image = DrawText(e.Message.Text, new Font(FontFamily.GenericMonospace, 32), System.Drawing.Color.Aqua, System.Drawing.Color.Transparent))
					await e.Channel.SendImage(image);
			}

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (args.Length > 1) return false;

				var handler = me.bot.modules.Find(x => x is CommandHandler) as CommandHandler;
				try {
					if (handler == null) return false;

					enabled = !enabled;
					if (enabled) {
						handler.CommandParseFailed += OnCommandParseFailed;
						await DynamicSendMessage(e, "**Toggled image-generation!** Will now switch out words for images!");
					} else {
						handler.CommandParseFailed -= OnCommandParseFailed;
						await DynamicSendMessage(e, "**Toggled image-generation!** Will no longer switch out words for images!");
					}

				} catch {
					if (handler != null)
						handler.CommandParseFailed -= OnCommandParseFailed;
					throw;
				}

				return true;
			}
		}

		private CmdText cmdText = new CmdText();
		public sealed class CmdText : Command<ImageGenerator> {
			public override string name { get; } = "text";
			public override CommandPerm requires { get; } = CommandPerm.Selfbot;
			public override string description { get; } = "Renders an image with text, then replaces the original message with a message with the image as an attachment.";
			public override string usage { get; } = "<Text>";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				if (string.IsNullOrWhiteSpace(rest)) return false;

				using (Image image = DrawText(rest, new Font(FontFamily.GenericMonospace, 20), System.Drawing.Color.Cyan, System.Drawing.Color.Transparent)) {
					await e.Channel.SendImage(image);
					await e.Message.SafeDelete();
				}
				return true;
			}
		}


		#region Generation algorithms
		/// <summary>
		/// Generates a graph image of the given history dictionary.
		/// Dictionary float value represents full percent, from 0-100.
		/// </summary>
		/// <param name="title">Header text for the graph</param>
		/// <param name="history">History, marked with timestamps and percent values ranging from 0-100</param>
		/// <param name="span">The timespan of the graph. I.e. the width (in time)</param>
		/// <param name="step">The distance between each vertical line.</param>
		/// <returns></returns>
		public static Image GenerateGraph(string title, Dictionary<DateTime, float> history, TimeSpan span, TimeSpan step) {
			Point offset = new Point(0, 200);
			Size size = new Size(2048, 920);
			Size graph = new Size(1920, 640);

			Image img = new Bitmap(size.Width, size.Height);
			using (Graphics drawing = Graphics.FromImage(img)) {

				DateTime now = DateTime.Now;
				DateTime start = now.AddSeconds(-span.TotalSeconds);

				// Columns
				using (Brush brush = new SolidBrush(System.Drawing.Color.Cyan))
				using (Brush headerBrush = new SolidBrush(System.Drawing.Color.ForestGreen))
				using (Font header = new Font(FontFamily.GenericMonospace, 56))
				using (Font font = new Font(FontFamily.GenericMonospace, 12))
				using (Font lastFont = new Font(FontFamily.GenericMonospace, 24))
				using (StringFormat format = new StringFormat()) {

					// Title
					format.Alignment = StringAlignment.Center;
					format.LineAlignment = StringAlignment.Far;
					
					drawing.DrawString(title, header, headerBrush, size.Width / 2, offset.Y, format);


					format.Alignment = StringAlignment.Center;
					format.LineAlignment = StringAlignment.Near;

					drawing.TranslateTransform(offset.X, offset.Y);

					// Vertical Lines
					for (DateTime time = start.AddSeconds(step.TotalSeconds - start.Second - start.Millisecond * 0.001d); time < now; time = time.AddSeconds(step.TotalSeconds)) {

						double p = 1d-((now - time).TotalSeconds / span.TotalSeconds);
						int x = (int)(p * graph.Width);

						drawing.DrawLine(Pens.DarkSlateGray, x, 0, x, graph.Height);
						if (time.Minute % 15 == 0) {
							drawing.DrawString(time.ToString("mm"), lastFont, brush, x, graph.Height + 40, format);
							drawing.DrawString(time.ToString("HH"), lastFont, brush, x, graph.Height + 12, format);
						} else {
							drawing.DrawString(time.ToString("mm"), (time.Minute % 5 == 0 ? lastFont : font), brush, x, graph.Height + (time.Minute % 5 == 0 ? 8 : 12), format);
						}
					}

					format.Alignment = StringAlignment.Near;
					format.LineAlignment = StringAlignment.Center;

					// Horizontal lines
					for (double per = 0; per <= 1; per += 0.2) {
						int y = (int)(graph.Height - per * graph.Height);

						drawing.DrawLine(Pens.LemonChiffon, 0, y, graph.Width, y);
						drawing.DrawString(string.Format("{0:P0}", per), lastFont, brush, graph.Width + 8, y, format);
					}

					// Calc position of history items
					List<Point> points = new List<Point>();
					int peak = graph.Height;
					long average = 0;
					foreach (KeyValuePair<DateTime, float> pair in history.OrderBy(x => x.Key)) {
						TimeSpan ago = now - pair.Key;
						double per = 1d - ago.TotalSeconds / span.TotalSeconds;
						int x = (int)(per * graph.Width);
						int y = (int)(graph.Height - pair.Value * 0.01d * graph.Height);
						peak = Math.Min(peak, y);
						average += y;

						if (points.Count == 0)
							points.Add(new Point(x, graph.Height));
						points.Add(new Point(x, y));
					}
					points.Add(new Point(graph.Width, points[points.Count - 1].Y));
					average = (long) (average / (float) history.Count);

					// Draw lines
					drawing.DrawLine(Pens.LightGoldenrodYellow, 0, peak, graph.Width, peak);
					drawing.DrawLine(Pens.LightGreen, 0, average, graph.Width, average);
					drawing.DrawLines(Pens.Red, points.ToArray());

					// Border
					drawing.DrawLine(Pens.LightGray, 0, graph.Height, graph.Width, graph.Height);
					drawing.DrawLine(Pens.LightGray, graph.Width, 0, graph.Width, graph.Height);
					drawing.DrawLine(Pens.LightGray, 0, 0, 0, graph.Height);
				}

			}

			return img;
		}

		public static Image DrawText(string text, Font font, System.Drawing.Color textColor, System.Drawing.Color backColor) {
			SizeF textSize;
			Image img;
			Graphics drawing;

			// Create dummy
			using (img = new Bitmap(1, 1))
			using (drawing = Graphics.FromImage(img))

				// Measure string
				textSize = drawing.MeasureString(text, font);

			// Create new image, correct size
			img = new Bitmap((int) textSize.Width, (int) textSize.Height);

			using (drawing = Graphics.FromImage(img)) {
				// Paint the background
				drawing.Clear(backColor);

				// Brush for text
				using (Brush textBrush = new SolidBrush(textColor))
					drawing.DrawString(text, font, textBrush, 0, 0);

				drawing.Save();
			}
			
			return img;
		}

		#endregion
	}
}
