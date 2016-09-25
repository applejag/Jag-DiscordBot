using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Utility {
	public static class ArrayHelper {
		public static List<T> SubArray<T>(this List<T> data, int index, int length) {
			T[] result = new T[length];
			data.CopyTo(index, result, 0, length);
			return result.ToList();
		}

		public static List<T> SubArray<T>(this List<T> data, int index) {
			T[] result = new T[data.Count-index];
			data.CopyTo(index, result, 0, data.Count-index);
			return result.ToList();
		}

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

		public static string Sum<T>(this IEnumerable<T> list, Func<T,string> selector) {
			string txt = string.Empty;
			foreach (T item in list)
				txt += selector(item);
			return txt;
		}

		public static int RemoveAll<K,V>(this Dictionary<K,V> dict, Predicate<KeyValuePair<K,V>> predicate) {
			K[] kill = (dict.Where(kv => predicate(kv)) as Dictionary<K,V>)?.Keys.ToArray();
			if (kill != null) {
				foreach (K key in kill)
					dict.Remove(key);
				return kill.Length;
			} else return 0;
		}
		
	}
}
