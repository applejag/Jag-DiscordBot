using DiscordBot.Modules;
using DiscordBot.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot {
	[Serializable]
	public sealed class SaveData {
		[NonSerialized]
		public static SaveData singleton = new SaveData();
		[NonSerialized]
		public static readonly string SAVE_FILE = Path.Combine(Environment.CurrentDirectory, "save.bin");

		#region SERIALIZABLE DATA
		public League.Player[] League_players;

		public string[] Bot_tokens;
		#endregion

		public static void Save() {
			if (singleton == null) return;

			bool success = false;

			using (Stream stream = new FileStream(SAVE_FILE, FileMode.Create, FileAccess.Write, FileShare.None)) {
				IFormatter formatter = new BinaryFormatter();
				formatter.Serialize(stream, singleton);
				success = true;
			}

			if (success) {
				LogHelper.LogSuccess("Saved serialized data.");
			} else {
				LogHelper.LogFailure("Failed to save serialize data.");
			}
		}

		public static void Load() {
			if (!File.Exists(SAVE_FILE)) return;

			bool success = false;
			using (Stream stream = new FileStream(SAVE_FILE, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				IFormatter formatter = new BinaryFormatter();
				singleton = formatter.Deserialize(stream) as SaveData;
				success = true;
			}

			if (success) {
				LogHelper.LogSuccess("Loaded serialized data.");
			} else {
				LogHelper.LogFailure("Failed to load serialize data.");
			}
		}
	}
}
