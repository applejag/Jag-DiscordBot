using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Utility {
	public static class ComputerHelper {

		private static PerformanceCounter cpu;
		private static Dictionary<DateTime, float> cpuHistory;

		internal const int HISTORY_MAX_AGE_SECONDS = 3600;
		internal const int HISTORY_MAX_AGE_MINUTES = 60;
		private const double TIMER_INTERVAL = 1000;
		private static System.Timers.Timer cpuCheck;

		public static void Init() {
			cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
			cpuHistory = new Dictionary<DateTime, float>();
			cpuCheck = new System.Timers.Timer(TIMER_INTERVAL);
			cpuCheck.Elapsed += HandleTimer;
			cpuCheck.Start();
		}

		public static void Dispose() {
			cpuCheck.Elapsed -= HandleTimer;
			cpuCheck.Dispose();
			cpuCheck = null;
			cpu.Dispose();
			cpu = null;
			cpuHistory.Clear();
			cpuHistory = null;
		}

		public static void HandleTimer(object sender, EventArgs args) {
			var now = DateTime.Now;
			cpuHistory.Add(now, cpu.NextValue());
			cpuHistory.RemoveAll(x => (now - x.Key).TotalSeconds > HISTORY_MAX_AGE_SECONDS);
		}

		public static long GetAvailableMemory() {
			Process currentProcess = Process.GetCurrentProcess();
			return currentProcess.NonpagedSystemMemorySize64;
		}

		public static long GetAllocatedMemory() {
			Process currentProcess = Process.GetCurrentProcess();
			return currentProcess.WorkingSet64 + currentProcess.PagedMemorySize64;
		}

		public static float GetCPUUsage() {
			return cpuHistory[cpuHistory.Keys.Max()];
		}

		public static Dictionary<DateTime, float> GetCPUHistory() {
			return cpuHistory;
		}
	}
}
