using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using SylentLoader.Services.Auth;
using SylentLoader.Services.Security;

namespace SylentLoader.Services;

public static class Injector
{
	private const int PROCESS_ALL_ACCESS = 2035711;

	private const uint MEM_COMMIT = 4096u;

	private const uint MEM_RESERVE = 8192u;

	private const uint PAGE_READWRITE = 4u;

	private static readonly HashSet<(int Pid, long Start)> Injected = new HashSet<(int, long)>();

	private static readonly HashSet<string> InjectedModuleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly object InjectLock = new object();

	private const int LegacyInjectWindowSeconds = 600;

	private const uint MOVEFILE_DELAY_UNTIL_REBOOT = 4u;

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint OpenProcess(int access, bool inherit, int pid);

	[DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
	private static extern nint GetProcAddress(nint hMod, string name);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern uint WaitForSingleObject(nint handle, uint milliseconds);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GetExitCodeThread(nint thread, out uint exitCode);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern nint GetModuleHandle(string name);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint VirtualAllocEx(nint hProc, nint addr, uint size, uint type, uint protect);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool WriteProcessMemory(nint hProc, nint addr, byte[] buf, uint size, out nuint written);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint CreateRemoteThread(nint hProc, nint attr, uint stackSize, nint func, nint param, uint flags, nint id);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(nint handle);

	public static long StartTicksOf(Process p)
	{
		try
		{
			return p.StartTime.Ticks;
		}
		catch
		{
			return 0L;
		}
	}

	public static bool HasInjected(Process p)
	{
		lock (Injected)
		{
			return Injected.Contains((p.Id, StartTicksOf(p)));
		}
	}

	public static bool HasPayloadLoaded(Process p)
	{
		try
		{
			lock (InjectedModuleNames)
			{
				if (InjectedModuleNames.Count == 0)
				{
					return false;
				}
				foreach (ProcessModule module in p.Modules)
				{
					if (InjectedModuleNames.Contains(module.ModuleName))
					{
						return true;
					}
				}
			}
		}
		catch
		{
		}
		return false;
	}

	public static (bool ok, string msg) InjectDll(string processName, string dllPath, Dictionary<string, string>? diag = null)
	{
		if (diag != null)
		{
			DiagReporter.Note(diag, "proc_target", processName);
		}
		if (!File.Exists(dllPath))
		{
			if (diag != null)
			{
				DiagReporter.Note(diag, "stage", "file_missing");
			}
			return (ok: false, msg: "Payload not staged — please try again. [I1]");
		}
		if (diag != null)
		{
			DiagReporter.Note(diag, "temp_state", DiagReporter.TempFileState(dllPath));
		}
		Process[] processesByName = Process.GetProcessesByName(processName);
		if (diag != null)
		{
			DiagReporter.Note(diag, "proc_count", processesByName.Length.ToString());
		}
		if (processesByName.Length == 0)
		{
			if (diag != null)
			{
				DiagReporter.Note(diag, "stage", "no_process");
			}
			return (ok: false, msg: "Game process not found — launch the game first. [I2]");
		}
		Process process = processesByName[0];
		DateTime dateTime = DateTime.MinValue;
		try
		{
			dateTime = process.StartTime;
		}
		catch
		{
		}
		for (int i = 1; i < processesByName.Length; i++)
		{
			try
			{
				DateTime startTime = processesByName[i].StartTime;
				if (startTime > dateTime)
				{
					process = processesByName[i];
					dateTime = startTime;
				}
			}
			catch
			{
			}
		}
		if (diag != null)
		{
			DiagReporter.Note(diag, "proc_pid", process.Id.ToString());
			try
			{
				DiagReporter.Note(diag, "proc_started_ago_s", (DateTime.Now - process.StartTime).TotalSeconds.ToString("F0"));
			}
			catch
			{
			}
			DiagReporter.Note(diag, "proc_modules", DiagReporter.ProcessModules(process));
		}
		if (processName == "GTA5" && diag != null)
		{
			DiagReporter.Note(diag, "game_file", DiagReporter.GameFileInfo(process));
			DiagReporter.Note(diag, "game_parent", DiagReporter.ParentInfo(process));
			DiagReporter.Note(diag, "loader_up_s", DiagReporter.LoaderUptimeSeconds());
			try
			{
				DiagReporter.Note(diag, "proc_started_ago_ms", (DateTime.Now - process.StartTime).TotalMilliseconds.ToString("F0"));
			}
			catch
			{
			}
		}
		if (processName == "GTA5")
		{
			try
			{
				if ((DateTime.Now - process.StartTime).TotalSeconds > 600.0)
				{
					if (diag != null)
					{
						DiagReporter.Note(diag, "stage", "too_late");
					}
					return (ok: false, msg: "Injected too late — GTA5 has already locked itself. Restart the game and inject the moment the window appears. [I3]");
				}
			}
			catch
			{
			}
		}
		string fullPath = Path.GetFullPath(dllPath);
		byte[] bytes = Encoding.Unicode.GetBytes(fullPath + "\0");
		nint num = OpenProcess(2035711, inherit: false, process.Id);
		if (num == IntPtr.Zero)
		{
			int lastWin32Error = Marshal.GetLastWin32Error();
			if (diag != null)
			{
				DiagReporter.Note(diag, "stage", "open_process");
				DiagReporter.Note(diag, "err_open", lastWin32Error.ToString());
			}
			return (ok: false, msg: "Cannot open process. Try running as Administrator. [I4]");
		}
		try
		{
			nint num2 = VirtualAllocEx(num, IntPtr.Zero, (uint)bytes.Length, 12288u, 4u);
			if (num2 == IntPtr.Zero)
			{
				int lastWin32Error2 = Marshal.GetLastWin32Error();
				if (diag != null)
				{
					DiagReporter.Note(diag, "stage", "valloc");
					DiagReporter.Note(diag, "err_valloc", lastWin32Error2.ToString());
				}
				return (ok: false, msg: "Memory allocation failed. [I5]");
			}
			if (!WriteProcessMemory(num, num2, bytes, (uint)bytes.Length, out var _))
			{
				int lastWin32Error3 = Marshal.GetLastWin32Error();
				if (diag != null)
				{
					DiagReporter.Note(diag, "stage", "wpm");
					DiagReporter.Note(diag, "err_wpm", lastWin32Error3.ToString());
				}
				return (ok: false, msg: "Could not write to the game process. [I6]");
			}
			nint procAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
			if (procAddress == IntPtr.Zero)
			{
				if (diag != null)
				{
					DiagReporter.Note(diag, "stage", "loadlib_addr");
				}
				return (ok: false, msg: "Could not locate LoadLibraryW (nothing was injected). [I7]");
			}
			nint num3 = CreateRemoteThread(num, IntPtr.Zero, 0u, procAddress, num2, 0u, IntPtr.Zero);
			if (num3 == IntPtr.Zero)
			{
				int lastWin32Error4 = Marshal.GetLastWin32Error();
				if (diag != null)
				{
					DiagReporter.Note(diag, "stage", "create_thread");
					DiagReporter.Note(diag, "err_ct", lastWin32Error4.ToString());
				}
				return (ok: false, msg: "CreateRemoteThread failed. [I8]");
			}
			try
			{
				uint num4 = WaitForSingleObject(num3, 30000u);
				if (diag != null)
				{
					DiagReporter.Note(diag, "wait", num4.ToString());
				}
				if (num4 != 0)
				{
					if (diag != null)
					{
						DiagReporter.Note(diag, "stage", (num4 == 258) ? "wait_timeout" : "wait_failed");
						DiagReporter.Note(diag, "proc_modules_after", DiagReporter.ProcessModules(process));
					}
					return (ok: false, msg: (num4 == 258) ? "Payload took too long to load — the game may be frozen. Restart the game and try again. [I9]" : "Waiting for the payload to load failed. [I10]");
				}
				if (GetExitCodeThread(num3, out var exitCode) && exitCode == 0)
				{
					string text = DiagReporter.RemoteThreadLastError(num, num3);
					string text2 = DiagReporter.LocalLoadProbe(fullPath);
					string text3 = DiagReporter.ImportProbe(fullPath);
					string text4 = DiagReporter.ProcessModules(process);
					string text5 = DiagReporter.TempFileState(fullPath);
					if (diag != null)
					{
						DiagReporter.Note(diag, "stage", "loadlibrary_null");
						DiagReporter.Note(diag, "remote_err", text);
						DiagReporter.Note(diag, "local_probe", text2);
						DiagReporter.Note(diag, "import_probe", text3);
						DiagReporter.Note(diag, "proc_modules_after", text4);
						DiagReporter.Note(diag, "temp_state_after", text5);
					}
					return (ok: false, msg: MessageForLoadLibraryFailure(text2, text4, text5, text, text3));
				}
			}
			finally
			{
				CloseHandle(num3);
			}
			lock (Injected)
			{
				Injected.Add((process.Id, StartTicksOf(process)));
			}
			if (diag != null)
			{
				DiagReporter.Note(diag, "stage", "ok");
			}
			return (ok: true, msg: "✓ Injected successfully");
		}
		finally
		{
			CloseHandle(num);
		}
	}

	private static string MessageForLoadLibraryFailure(string probe, string modules, string tempState, string remoteErr, string importProbe)
	{
		if (tempState.StartsWith("missing", StringComparison.OrdinalIgnoreCase))
		{
			return "Your antivirus removed the payload before the game could load it. Add %TEMP% to exclusions and try again. [I12]";
		}
		if (modules.Contains("BEClient", StringComparison.OrdinalIgnoreCase) || modules.Contains("BEDaisy", StringComparison.OrdinalIgnoreCase))
		{
			return "BattlEye is running — join Story Mode (not Online) and try again. [I13]";
		}
		if (int.TryParse(remoteErr, out var result))
		{
			switch (result)
			{
			case 126:
				return NameMissingImport(importProbe, "[I14]");
			case 193:
				return "Payload architecture doesn't match the game. Please try again, and contact support if this keeps happening. [I15]";
			case 5:
				return "Your antivirus blocked the payload. Add %TEMP% to exclusions (or turn off real-time protection briefly) and try again. [I16]";
			case 2:
				return "The payload file disappeared before the game could load it — antivirus most likely. Add %TEMP% to exclusions and try again. [I17]";
			case 1114:
				return "The mod's startup check failed on this PC. Contact support with code [I23].";
			default:
				return $"The game refused to load the payload (Windows error {result}). [I18]";
			case 0:
				break;
			}
		}
		if (probe.StartsWith("err=", StringComparison.Ordinal) && int.TryParse(probe.AsSpan(4), out var result2))
		{
			return result2 switch
			{
				126 => NameMissingImport(importProbe, "[I14]"), 
				193 => "Payload architecture doesn't match the game. Please try again, and contact support if this keeps happening. [I15]", 
				5 => "Your antivirus blocked the payload. Add %TEMP% to exclusions (or turn off real-time protection briefly) and try again. [I16]", 
				2 => "The payload file disappeared before the game could load it — antivirus most likely. Add %TEMP% to exclusions and try again. [I17]", 
				_ => $"The game refused to load the payload (Windows error {result2}). [I18]", 
			};
		}
		if (probe == "pe_ok")
		{
			return "The game refused to load the payload (no Windows error returned). Contact support with code [I11].";
		}
		return "The game refused to load the payload (LoadLibrary failed). [I19]";
	}

	private static string NameMissingImport(string importProbe, string codeTag)
	{
		if (!string.IsNullOrEmpty(importProbe) && importProbe != "ok" && importProbe != "all_ok" && !importProbe.StartsWith("no_") && !importProbe.StartsWith("bad_") && !importProbe.StartsWith("throw") && !importProbe.StartsWith("trunc") && !importProbe.StartsWith("imp_"))
		{
			int num = importProbe.IndexOf('=');
			int num2 = importProbe.IndexOf(',');
			string text = importProbe;
			int num3;
			if (num > 0)
			{
				num3 = num;
			}
			else
			{
				num3 = ((num2 > 0) ? num2 : importProbe.Length);
			}
			string text2 = text[..num3];
			return "Payload needs " + text2 + " — install the Microsoft Visual C++ 2015–2022 x64 runtime (vc_redist.x64.exe) and try again. " + codeTag;
		}
		return "Payload needs the Microsoft Visual C++ 2015–2022 x64 runtime — install vc_redist.x64.exe and try again. " + codeTag;
	}

	private static byte[]? ReadDecrypted(string resourceName, out string error)
	{
	
		string text = TokenOf(resourceName);
	
		
		byte[] array = TryReadEmbedded(resourceName);

		error = null;
		
        return array;
    }

	private static string TokenOf(string resourceName)
	{
		string[] array = resourceName.Split('.');
		if (array.Length < 2)
		{
			return resourceName;
		}
		return array[^2];
	}

	private static byte[]? TryReadEmbedded(string resourceName)
	{
		try
		{
			using Stream stream = typeof(Injector).Assembly.GetManifestResourceStream(resourceName);
			if (stream == null)
			{
				return null;
			}
			using MemoryStream memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			return memoryStream.ToArray();
		}
		catch
		{
			return null;
		}
	}

	private static bool LooksLikePE(byte[] b)
	{
		if (b == null || b.Length < 70)
		{
			return false;
		}
		if (b[0] != 77 || b[1] != 90)
		{
			return false;
		}
		int num = BitConverter.ToInt32(b, 60);
		if (num < 0 || num > b.Length - 6)
		{
			return false;
		}
		if (b[num] != 80 || b[num + 1] != 69 || b[num + 2] != 0 || b[num + 3] != 0)
		{
			return false;
		}
		return BitConverter.ToUInt16(b, num + 4) == 34404;
	}

	private static string BuildStealthOutputName()
	{
		return PayloadPath.NextStealthPath();
	}

	public static (bool ok, string msg) ExtractToDirectory(string resourceName, string outputFileName, string directory)
	{
		AuthResult authResult = Entitlements.Authorize(Entitlements.Classify(resourceName));
		if (!authResult.Ok)
		{
			return (ok: false, msg: authResult.Message);
		}
		byte[] array = ReadDecrypted(resourceName, out string error);
		if (array == null)
		{
			return (ok: false, msg: error);
		}
		string path = Path.Combine(directory, outputFileName);
		try
		{
			File.WriteAllBytes(path, array);
			return (ok: true, msg: "Copied " + outputFileName + " to folder.");
		}
		catch (UnauthorizedAccessException)
		{
			return TryElevatedCopy(array, outputFileName, directory);
		}
		catch (IOException)
		{
			return (ok: false, msg: "Couldn't write " + outputFileName + " — is GTA V running? Close it and try again.");
		}
		catch (Exception)
		{
			return (ok: false, msg: "Copy failed — check the destination folder and try again.");
		}
	}

	private static (bool ok, string msg) TryElevatedCopy(byte[] plain, string outputFileName, string directory)
	{
		string text = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}_{outputFileName}");
		try
		{
			File.WriteAllBytes(text, plain);
		}
		catch
		{
			return (ok: false, msg: "Couldn't stage the file in TEMP.");
		}
		string text2 = Path.Combine(directory, outputFileName);
		try
		{
			using Process process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c copy /Y \"{text}\" \"{text2}\"")
			{
				UseShellExecute = true,
				Verb = "runas",
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			});
			process?.WaitForExit(15000);
			try
			{
				File.Delete(text);
			}
			catch
			{
			}
			return File.Exists(text2) ? (ok: true, msg: "Copied " + outputFileName + " (elevated).") : (ok: false, msg: "Elevated copy didn't complete — try again.");
		}
		catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
		{
			try
			{
				File.Delete(text);
			}
			catch
			{
			}
			return (ok: false, msg: "UAC was declined — Pub Lobby needs admin to write into Program Files.");
		}
		catch (Exception)
		{
			try
			{
				File.Delete(text);
			}
			catch
			{
			}
			return (ok: false, msg: "Couldn't elevate — right-click the loader and Run as administrator.");
		}
	}

	public static (bool ok, string msg) InjectEmbedded(string processName, string resourceName, string source = "manual")
	{
		Dictionary<string, string> dictionary = DiagReporter.New(TokenOf(resourceName), "");
		DiagReporter.Note(dictionary, "source", source);
		AuthResult authResult = Entitlements.Authorize(Entitlements.Classify(resourceName));
		if (!authResult.Ok)
		{
			DiagReporter.Note(dictionary, "stage", "auth");
			DiagReporter.Note(dictionary, "result", authResult.Message);
			DiagReporter.SendAsync(dictionary);
			return (ok: false, msg: authResult.Message);
		}
		byte[] array = ReadDecrypted(resourceName, out string error);
		if (array == null)
		{
			DiagReporter.Note(dictionary, "stage", "fetch");
			DiagReporter.Note(dictionary, "result", error);
			DiagReporter.SendAsync(dictionary);
			return (ok: false, msg: error);
		}
		DiagReporter.Note(dictionary, "blob", DiagReporter.BlobFingerprint(array));
		if (!LooksLikePE(array))
		{
			Array.Clear(array);
			DiagReporter.Note(dictionary, "stage", "not_pe");
			DiagReporter.SendAsync(dictionary);
			return (ok: false, msg: "Payload is corrupted or was tampered with. [I20]");
		}
		string text = PayloadPath.NextStealthPath();
		DiagReporter.Note(dictionary, "temp_ext", Path.GetExtension(text));
		lock (InjectLock)
		{
			(bool, string) result;
			try
			{
				File.WriteAllBytes(text, array);
				result = InjectDll(processName, text, dictionary);
			}
			catch (Exception ex)
			{
				DiagReporter.Note(dictionary, "stage", "write_or_inject_throw");
				DiagReporter.Note(dictionary, "throw", ex.GetType().Name + ": " + ex.Message);
				result = (false, "Inject failed — please try again. [I21]");
			}
			if (result.Item1)
			{
				lock (InjectedModuleNames)
				{
					InjectedModuleNames.Add(Path.GetFileName(text));
				}
			}
			DiagReporter.Note(dictionary, "result", result.Item2);
			DiagReporter.Note(dictionary, "ok", result.Item1 ? "1" : "0");
			DiagReporter.SendAsync(dictionary);
			// Array.Clear(array);
			// CleanupTempFile(text, !result.Item1);
			return result;
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool MoveFileEx(string lpExistingFileName, string? lpNewFileName, uint dwFlags);

	private static void CleanupTempFile(string path, bool shred = true)
	{
		if (shred)
		{
			PayloadPath.Shred(path);
		}
		for (int i = 0; i < 4; i++)
		{
			try
			{
				File.Delete(path);
				return;
			}
			catch (IOException)
			{
				Thread.Sleep(150);
			}
			catch (UnauthorizedAccessException)
			{
				Thread.Sleep(150);
			}
		}
		try
		{
			MoveFileEx(path, null, 4u);
		}
		catch
		{
		}
	}

	public static bool IsRunning(string procName)
	{
		Process[] processesByName = Process.GetProcessesByName(procName);
		try
		{
			return processesByName.Length != 0;
		}
		finally
		{
			Process[] array = processesByName;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Dispose();
			}
		}
	}
}
