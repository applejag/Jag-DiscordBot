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

		public static void Shuffle<T>(this IList<T> list) {
			int n = list.Count;
			while (n > 1) {
				n--;
				int k = RandomHelper.random.Next(n + 1);
				T value = list[k];
				list[k] = list[n];
				list[n] = value;
			}
		}

		public static T Pop<T>(this IList<T> list) {
			T item = list[list.Count - 1];
			list.RemoveAt(list.Count - 1);
			return item;
		}

		public static void Push<T>(this IList<T> list, T item) {
			list.Add(item);
		}

		public static T Dequeue<T>(this IList<T> list) {
			T item = list[0];
			list.RemoveAt(0);
			return item;
		}

		public static void Enqueue<T>(this IList<T> list, T item) {
			list.Add(item);
		}


	}
}
