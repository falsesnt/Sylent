using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SylentLoader.Models;
using SylentLoader.Services.Auth;

namespace SylentLoader.Services;

public static class AutoInjector
{
	private sealed class Cycle
	{
		public int Pid;

		public long Start;

		public GtaVersion Version;

		public readonly DateTime FirstSeen = DateTime.Now;

		public LoadWatcher Watcher = new LoadWatcher(GtaVersion.Enhanced);

		public bool Done;

		public int Attempts;

		public DateTime RetryAt = DateTime.MinValue;
	}

	private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

	private struct IoCounters
	{
		public ulong ReadOps;

		public ulong WriteOps;

		public ulong OtherOps;

		public ulong ReadBytes;

		public ulong WriteBytes;

		public ulong OtherBytes;
	}

	private static readonly object Gate = new object();

	private static CancellationTokenSource? _cts;

	private const int MaxAttempts = 3;

	private const uint WM_NULL = 0u;

	private const uint SMTO_ABORTIFHUNG = 2u;

	private const int PROCESS_QUERY_LIMITED_INFORMATION = 4096;

	public static AutoStatus Current { get; private set; } = new AutoStatus(AutoStage.Off, "");

	public static event Action<AutoStatus>? StatusChanged;

	public static event Action<bool, string>? Finished;

	public static void Start()
	{
		AppSettings.Changed += Sync;
		Sync();
	}

	private static void Sync()
	{
		lock (Gate)
		{
			if (AppSettings.AutoInject && _cts == null)
			{
				_cts = new CancellationTokenSource();
				CancellationToken ct = _cts.Token;
				Task.Run(() => Run(ct));
			}
			else if (!AppSettings.AutoInject && _cts != null)
			{
				_cts.Cancel();
				_cts = null;
				Publish(CancellationToken.None, AutoStage.Off, "");
			}
		}
	}

	private static void Publish(CancellationToken ct, AutoStage stage, string text)
	{
		if (!ct.IsCancellationRequested)
		{
			AutoStatus autoStatus = new AutoStatus(stage, text);
			if (!(autoStatus == Current))
			{
				Current = autoStatus;
				StatusChanged?.Invoke(autoStatus);
			}
		}
	}

	private static async Task Run(CancellationToken ct)
	{
		Cycle cur = null;
		while (!ct.IsCancellationRequested)
		{
			try
			{
				cur = Step(cur, ct);
			}
			catch (Exception ex)
			{
				Publish(ct, AutoStage.Failed, "Auto inject · " + ex.GetType().Name);
			}
			try
			{
				await Task.Delay(1000, ct);
			}
			catch (OperationCanceledException)
			{
				break;
			}
		}
	}

	private static Cycle? Step(Cycle? cur, CancellationToken ct)
	{
		Process process = FindGame(out var version);
		if (process == null)
		{
			Publish(ct, AutoStage.Waiting, "Auto inject · waiting for GTA V");
			return null;
		}
		using (process)
		{
			long num = Injector.StartTicksOf(process);
			if (cur == null || cur.Pid != process.Id || cur.Start != num)
			{
				cur = new Cycle
				{
					Pid = process.Id,
					Start = num,
					Version = version
				};
				cur.Watcher = new LoadWatcher(version);
			}
			if (cur.Done || DateTime.Now < cur.RetryAt)
			{
				return cur;
			}
			if (Injector.HasInjected(process))
			{
				return Skip(cur, ct);
			}
			if (!Observe(cur, process))
			{
				Publish(ct, AutoStage.Loading, "Auto inject · waiting for the GTA V main menu");
				return cur;
			}
			if (Injector.HasPayloadLoaded(process))
			{
				return Skip(cur, ct);
			}
			Attempt(cur, process.ProcessName, ct);
			return cur;
		}
	}

	private static Cycle Skip(Cycle c, CancellationToken ct)
	{
		c.Done = true;
		Publish(ct, AutoStage.Injected, "Auto inject · already injected");
		return c;
	}

	private static bool Observe(Cycle c, Process proc)
	{
		try
		{
			proc.Refresh();
			DateTime dateTime = ((c.Start != 0L) ? new DateTime(c.Start) : c.FirstSeen);
			TimeSpan uptime = DateTime.Now - dateTime;
			nint num = FindWindow(proc.Id);
			bool windowUp = num != IntPtr.Zero && Responsive(num);
			return c.Watcher.Feed(DateTime.Now, uptime, windowUp, proc.WorkingSet64, ReadBytes(proc.Id));
		}
		catch (Exception ex) when ((ex is InvalidOperationException || ex is Win32Exception) ? true : false)
		{
			return false;
		}
	}

	private static void Attempt(Cycle c, string processName, CancellationToken ct)
	{
		GtaMod? gtaMod = ChooseMod(c.Version);
		if (!gtaMod.HasValue)
		{
			c.Done = true;
			Publish(ct, AutoStage.Failed, "Auto inject · no GTA V key on this PC");
			Finished?.Invoke(arg1: false, "Auto inject skipped — no GTA V key on this PC.");
			return;
		}
		Product p = Entitlements.ForMod(gtaMod.Value);
		string text = Entitlements.DisplayName(p);
		Publish(ct, AutoStage.Injecting, "Auto inject · injecting " + text + "…");
		(bool, string) tuple;
		try
		{
			AuthResult authResult = (Entitlements.IsUnlocked(p) ? new AuthResult(Ok: true, "") : Entitlements.UnlockSaved(p));
			if (ct.IsCancellationRequested)
			{
				return;
			}
			string source = $"auto:{c.Attempts + 1}";
			tuple = (authResult.Ok ? Injector.InjectEmbedded(processName, GameProfile.DllFor(gtaMod.Value, c.Version), source) : (false, (authResult.Message.Length > 0) ? authResult.Message : (text + " isn't licensed.")));
		}
		catch (Exception)
		{
			tuple = (false, "Inject failed — please try again. [I22]");
		}
		if (tuple.Item1)
		{
			c.Done = true;
			Publish(ct, AutoStage.Injected, "Auto inject · " + text + " injected");
			Finished?.Invoke(arg1: true, "Auto-injected " + text + ".");
		}
		else if (++c.Attempts >= 3)
		{
			c.Done = true;
			Publish(ct, AutoStage.Failed, "Auto inject · " + tuple.Item2);
			Finished?.Invoke(arg1: false, "Auto inject failed: " + tuple.Item2);
		}
		else
		{
			c.RetryAt = DateTime.Now.AddSeconds(6.0);
			Publish(ct, AutoStage.Failed, "Auto inject · " + tuple.Item2 + " (retrying)");
		}
	}

	private static GtaMod? ChooseMod(GtaVersion version)
	{
		foreach (GtaMod item in new GtaMod[4]
		{
			AppSettings.LastGtaMod,
			GtaMod.SylentMenu,
			GtaMod.SylentRecovery,
			GtaMod.Aurora
		}.Distinct())
		{
			if (GameProfile.DllFor(item, version).Length != 0)
			{
				Product p = Entitlements.ForMod(item);
				if (Entitlements.IsUnlocked(p) || Entitlements.HasSavedKey(p))
				{
					return item;
				}
			}
		}
		return null;
	}

	private static Process? FindGame(out GtaVersion version)
	{
		version = GtaVersion.Enhanced;
		Process process = null;
		(string, GtaVersion)[] array = new (string, GtaVersion)[2]
		{
			("GTA5_Enhanced", GtaVersion.Enhanced),
			("GTA5", GtaVersion.Legacy)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, GtaVersion) tuple = array[i];
			string item = tuple.Item1;
			GtaVersion item2 = tuple.Item2;
			Process[] processesByName = Process.GetProcessesByName(item);
			foreach (Process process2 in processesByName)
			{
				if (process == null)
				{
					process = process2;
					version = item2;
				}
				else
				{
					process2.Dispose();
				}
			}
			if (process != null)
			{
				break;
			}
		}
		return process;
	}

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc cb, nint lParam);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(nint hwnd);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern nint SendMessageTimeout(nint hwnd, uint msg, nint wParam, nint lParam, uint flags, uint timeoutMs, out nint result);

	private static nint FindWindow(int pid)
	{
		nint found = IntPtr.Zero;
		EnumWindows((nint h, nint _) =>
		{
			GetWindowThreadProcessId(h, out var pid2);
			if (pid2 == (uint)pid && IsWindowVisible(h))
			{
				found = h;
				return false;
			}
			return true;
		}, IntPtr.Zero);
		return found;
	}

	private static bool Responsive(nint hwnd)
	{
		nint result;
		return SendMessageTimeout(hwnd, 0u, IntPtr.Zero, IntPtr.Zero, 2u, 2000u, out result) != IntPtr.Zero;
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint OpenProcess(int access, bool inherit, int pid);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GetProcessIoCounters(nint process, out IoCounters counters);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(nint handle);

	private static long ReadBytes(int pid)
	{
		nint num = OpenProcess(4096, inherit: false, pid);
		if (num == IntPtr.Zero)
		{
			return 0L;
		}
		try
		{
			IoCounters counters;
			return (long)(GetProcessIoCounters(num, out counters) ? counters.ReadBytes : 0);
		}
		finally
		{
			CloseHandle(num);
		}
	}
}
