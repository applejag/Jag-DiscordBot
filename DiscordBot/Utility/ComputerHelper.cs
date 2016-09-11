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
		private static List<float> cpuHistory;

		private const int HISTORY_SIZE = 3600;
		private const double TIMER_INTERVAL = 1000;
		private static System.Timers.Timer cpuCheck;

		public static void Init() {
			cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
			cpuHistory = new List<float>();
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
			cpuHistory.Add(cpu.NextValue());
			if (cpuHistory.Count > HISTORY_SIZE)
				cpuHistory.RemoveAt(0);
		}

		public static long GetAvailableMemory() {
			Process currentProcess = Process.GetCurrentProcess();
			return currentProcess.NonpagedSystemMemorySize64;
		}

		public static long GetAllocatedMemory() {
			Process currentProcess = Process.GetCurrentProcess();
			return currentProcess.WorkingSet64 + currentProcess.PagedMemorySize64;
		}

		public static float GetCPUUsage(int millisecondTimespan = 1000) {
			return cpuHistory.Last();
		}
	}
}
