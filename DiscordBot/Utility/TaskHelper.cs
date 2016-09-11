using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Utility {
	public static class TaskHelper {

		public static async Task WaitUntil(int checkDelay, Func<bool> check) {
			while (!check()) {
				await Task.Delay(checkDelay);
			}
		}
		public static async Task WaitUntil(int checkDelay, int timeout, Func<bool> check) {
			CancellationTokenSource source = new CancellationTokenSource(timeout);

			while (!check()) {
				try {
					await Task.Delay(checkDelay, source.Token);
				} catch {
					throw new TimeoutException("Timeout while waiting for task");
				}
			}
		}

	}
}
