using DiscordBot.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic.Devices;
using Discord;

namespace DiscordBot.Modules {
	public sealed class BotHandler : Module {

		public BotHandler(string prefix) {
			this.prefix = prefix;
		}

	}
}
