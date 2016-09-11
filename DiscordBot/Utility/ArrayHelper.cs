using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Utility {
	public static class ArrayHelper {
		public static T[] SubArray<T>(this T[] data, int index, int length) {
			T[] result = new T[length];
			Array.Copy(data, index, result, 0, length);
			return result;
		}

		public static T[] SubArray<T>(this T[] data, int index) {
			T[] result = new T[data.Length-index];
			Array.Copy(data, index, result, 0, result.Length);
			return result;
		}

		public static string ToString<T>(this IEnumerable<T> list, Func<T,string> each) {
			string txt = string.Empty;
			foreach (T item in list) {
				txt += each(item);
			}
			return txt;
		}
	}
}
