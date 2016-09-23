using Discord;
using NCalc;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using NodaTime;
using System.Collections.Generic;
using DiscordBot.Utility;

namespace DiscordBot.Modules {
	public sealed class Eval : Module {

		static Random random = new Random();
		Dictionary<ulong, ulong> evaluvated;

		private CmdEval cmdEval = new CmdEval();
		public sealed class CmdEval : Command<Eval> {
			public override string name { get; } = "eval";
			public override CommandPerm requires { get; } = CommandPerm.None;
			public override string description { get; } = "Uses NCalc to evaluate a mathematical expression. Supports functions, ex: sqrt() but also dates in the format #2014-09-25# plus additional methods for handleing dates, ex: days(#2014-09-25#) gives the differance in days to said date.";
			public override string usage { get; } = "<Math>";

			public override async Task<bool> Callback(MessageEventArgs e, string[] args, string rest) {
				await e.Channel.SendIsTyping();

				string reply;
				object value;
				Exception err;

				if (TryEvaluate(rest, out value, out err)) {
					reply = "Output: `" + Convert.ToString(value) + "`";
				} else {
					reply = "Error while evaluating!\n```" + err.Message + "```";
				}

				reply = e.User.Mention + " " + reply;
				Thread.Sleep(300);

				Message replyMessage = await e.Channel.SafeSendMessage(reply);
				me.evaluvated.Add(e.Message.Id, replyMessage.Id);
				return true;
			}
		}

		public override void Init() {
			evaluvated = new Dictionary<ulong, ulong>();

			AddCommand(cmdEval);

			client.MessageUpdated += Client_MessageUpdated;
		}

		private async void Client_MessageUpdated(object sender, MessageUpdatedEventArgs e) {
			if (e.User == null || e.User.IsBot) return;
			if (!evaluvated.ContainsKey(e.After.Id)) return;

			try {
				Message replyMessage = e.Channel.GetMessage(evaluvated[e.After.Id]);
				if (replyMessage.User != null && replyMessage.State == MessageState.Normal) {

					// Try manually parse it
					string[] args; string rest;
					Command cmd = CommandHandler.TryParseCommand(this, e.After.Text, CommandPerm.None, out args, out rest);
					if (cmd == null || cmd.name != "eval") return;

					await e.Channel.SendIsTyping();

					string reply;
					object value;
					Exception err;

					if (TryEvaluate(rest, out value, out err)) {
						reply = "Output: `" + Convert.ToString(value) + "`";
					} else {
						reply = "Error while evaluating!\n```" + err.Message + "```";
					}

					reply = e.User.Mention + " " + reply;

					Thread.Sleep(300);

					await replyMessage.SafeEdit(reply);
				}
			} catch (Exception err) {
				LogHelper.LogException("Unknown error while updating evaluated response message!", err);
			}
		}

		public static bool TryEvaluate(string expression, out object value, out Exception err) {
			Expression exp = new Expression(expression);

			exp.EvaluateFunction += Exp_EvaluateFunction;
			exp.EvaluateParameter += Exp_EvaluateParameter;

			try {
				value = exp.Evaluate();
				err = null;
				return true;
			} catch (Exception e) {
				value = null;
				err = e;
				return false;
			}
		}
		private static void Exp_EvaluateParameter(string name, ParameterArgs args) {
			if (name == "seconds") args.Result = DateTime.Now.Second;
			else if (name == "minute") args.Result = DateTime.Now.Minute;
			else if (name == "hour") args.Result = DateTime.Now.Hour;
			else if (name == "month") args.Result = DateTime.Now.Month;
			else if (name == "millisecond") args.Result = DateTime.Now.Millisecond;
			else if (name == "year") args.Result = DateTime.Now.Year;
			else if (name == "day" || name == "dayofmonth") args.Result = DateTime.Now.Day;
			else if (name == "weekday" || name == "dayofweek") args.Result = (int) DateTime.Now.DayOfWeek;
			else if (name == "yearday" || name == "dayofyear") args.Result = DateTime.Now.DayOfYear;
			else if (name == "today" || name == "now" || name == "date") args.Result = DateTime.Now;

			else if (name == "inf" || name == "infinity") args.Result = double.PositiveInfinity;
			else if (name == "max") args.Result = double.MaxValue;
			else if (name == "min") args.Result = double.MinValue;
			else if (name == "nan") args.Result = double.NaN;

			else if (name == "pi" || name == "π") args.Result = Math.PI;
			else if (name == "tau" || name == "τ") args.Result = Math.PI * 2;
			else if (name == "euler" || name == "e") args.Result = Math.E;
			else if (name == "gamma" || name == "γ") args.Result = 0.57721566490153286060;
			else if (name == "epsilon") args.Result = double.Epsilon;
			else if (name == "ratio" || name == "goldenratio" || name == "φ") args.Result = 1.6180339887498948482;

			else if (name == "random" || name == "rand") args.Result = random.NextDouble();
			else if (name == "irandom" || name == "irand") args.Result = random.Next();
		}
		private static void Exp_EvaluateFunction(string name, FunctionArgs args) {

			name = name.ToLower();
			var par = args.EvaluateParameters();

			if (par.Length == 0) {
				if (name == "random" || name == "rand") args.Result = random.NextDouble();
			}
			if (par.Length == 1) {
				double a;
				try { a = Convert.ToDouble(par[0]); } catch { a = 0; }
				if (name == "abs") args.Result = Math.Abs(a);
				else if (name == "log") args.Result = Math.Log(a);
				else if (name == "log10") args.Result = Math.Log10(a);
				else if (name == "round") args.Result = Math.Round(a);
				else if (name == "truncate") args.Result = Math.Truncate(a);
				else if (name == "floor") args.Result = Math.Floor(a);
				else if (name == "ceil" || name == "ceiling") args.Result = Math.Ceiling(a);
				else if (name == "exp") args.Result = Math.Exp(a);
				else if (name == "sin") args.Result = Math.Sin(a);
				else if (name == "asin") args.Result = Math.Asin(a);
				else if (name == "sinh") args.Result = Math.Sinh(a);
				else if (name == "cos") args.Result = Math.Cos(a);
				else if (name == "acos") args.Result = Math.Acos(a);
				else if (name == "cosh") args.Result = Math.Cosh(a);
				else if (name == "tan") args.Result = Math.Tan(a);
				else if (name == "atan") args.Result = Math.Atan(a);
				else if (name == "tanh") args.Result = Math.Tanh(a);
				else if (name == "sign") args.Result = Math.Sign(a);
				else if (name == "sqrt") args.Result = Math.Sqrt(a);

				else if (name == "random" || name == "rand") args.Result = random.NextDouble() * a;
				else if (name == "irandom" || name == "irand") args.Result = random.Next((int) a);

				else if (name == "daydiff" || name == "days") args.Result = (DateTime.Now - (DateTime) par[0]).TotalDays;
				else if (name == "hourdiff" || name == "hours") args.Result = (DateTime.Now - (DateTime) par[0]).TotalHours;
				else if (name == "milliseconddiff" || name == "milliseconds") args.Result = (DateTime.Now - (DateTime) par[0]).TotalMilliseconds;
				else if (name == "minutediff" || name == "minutes") args.Result = (DateTime.Now - (DateTime) par[0]).TotalMinutes;
				else if (name == "seconddiff" || name == "seconds") args.Result = (DateTime.Now - (DateTime) par[0]).TotalSeconds;
				else if (name == "yeardiff" || name == "years") {
					DateTime dtstart = (DateTime)par[0];
					DateTime dtend = DateTime.Now;

					LocalDate start = new LocalDate(dtstart.Year, dtstart.Month, dtstart.Day);
					LocalDate end = new LocalDate(dtend.Year, dtend.Month, dtend.Day);

					Period period = Period.Between(start, end, PeriodUnits.Years);
					args.Result = period.Years;
				} else if (name == "monthdiff" || name == "months") {
					DateTime dtstart = (DateTime)par[0];
					DateTime dtend = DateTime.Now;

					LocalDate start = new LocalDate(dtstart.Year, dtstart.Month, dtstart.Day);
					LocalDate end = new LocalDate(dtend.Year, dtend.Month, dtend.Day);

					Period period = Period.Between(start, end, PeriodUnits.Months);
					args.Result = period.Months;
				} else if (name == "weekdiff" || name == "weeks") {
					DateTime dtstart = (DateTime)par[0];
					DateTime dtend = DateTime.Now;

					LocalDate start = new LocalDate(dtstart.Year, dtstart.Month, dtstart.Day);
					LocalDate end = new LocalDate(dtend.Year, dtend.Month, dtend.Day);

					Period period = Period.Between(start, end, PeriodUnits.Weeks);
					args.Result = period.Weeks;
				}
			} else if (par.Length == 2) {
				double a,b;
				try { a = Convert.ToDouble(par[0]); } catch { a = 0; }
				try { b = Convert.ToDouble(par[1]); } catch { b = 0; }

				if (name == "log") args.Result = Math.Log(a, b);
				else if (name == "min") args.Result = Math.Min(a, b);
				else if (name == "max") args.Result = Math.Max(a, b);
				else if (name == "round") args.Result = Math.Round(a, (int) b);
				else if (name == "pow") args.Result = Math.Pow(a, b);
				else if (name == "atan2") args.Result = Math.Atan2(a, b);
				else if (name == "ieeeremainder") args.Result = Math.IEEERemainder(a, b);

				else if (name == "random" || name == "rand") args.Result = random.NextDouble() * (b - a) + a;
				else if (name == "irandom" || name == "irand") args.Result = random.Next((int) a, (int) b);

				else if (name == "daydiff" || name == "days") args.Result = ((DateTime) par[1] - (DateTime) par[0]).TotalDays;
				else if (name == "hourdiff" || name == "hours") args.Result = ((DateTime) par[1] - (DateTime) par[0]).TotalHours;
				else if (name == "milliseconddiff" || name == "milliseconds") args.Result = ((DateTime) par[1] - (DateTime) par[0]).TotalMilliseconds;
				else if (name == "minutediff" || name == "minutes") args.Result = ((DateTime) par[1] - (DateTime) par[0]).TotalMinutes;
				else if (name == "seconddiff" || name == "seconds") args.Result = ((DateTime) par[1] - (DateTime) par[0]).TotalSeconds;
				else if (name == "yeardiff" || name == "years") {
					DateTime dtstart = (DateTime)par[0];
					DateTime dtend = (DateTime)par[1];

					LocalDate start = new LocalDate(dtstart.Year, dtstart.Month, dtstart.Day);
					LocalDate end = new LocalDate(dtend.Year, dtend.Month, dtend.Day);

					Period period = Period.Between(start, end, PeriodUnits.Years);
					args.Result = period.Years;
				} else if (name == "monthdiff" || name == "months") {
					DateTime dtstart = (DateTime)par[0];
					DateTime dtend = (DateTime)par[1];

					LocalDate start = new LocalDate(dtstart.Year, dtstart.Month, dtstart.Day);
					LocalDate end = new LocalDate(dtend.Year, dtend.Month, dtend.Day);

					Period period = Period.Between(start, end, PeriodUnits.Months);
					args.Result = period.Months;
				} else if (name == "weekdiff" || name == "weeks") {
					DateTime dtstart = (DateTime)par[0];
					DateTime dtend = (DateTime)par[1];

					LocalDate start = new LocalDate(dtstart.Year, dtstart.Month, dtstart.Day);
					LocalDate end = new LocalDate(dtend.Year, dtend.Month, dtend.Day);

					Period period = Period.Between(start, end, PeriodUnits.Weeks);
					args.Result = period.Weeks;
				}
			}
			if (name == "in" && par.Length > 0) {
				bool @in = false;
				for (int i = 1; i < par.Length; i++)
					if (par[i] == par[0])
						@in = true;
				args.Result = @in;
			}
			if (name == "if" && par.Length == 3) {
				if (par[0] == null || (bool) par[0] == false)
					args.Result = par[1];
				else args.Result = par[2];
			}
		}

		public override void Unload() {
			client.MessageUpdated -= Client_MessageUpdated;
			RemoveCommand(cmdEval);

			evaluvated.Clear();
			evaluvated = null;
		}
	}
}
