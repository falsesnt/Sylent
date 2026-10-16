using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SylentLoader.Services.Auth;

namespace SylentLoader.Services;

internal static class DiagReporter
{
	private struct ThreadBasicInformation
	{
		public nint ExitStatus;

		public nint TebBaseAddress;

		public nint ClientId_UniqueProcess;

		public nint ClientId_UniqueThread;

		public nint AffinityMask;

		public int Priority;

		public int BasePriority;
	}

	private struct ProcessBasicInformation
	{
		public nint ExitStatus;

		public nint PebBaseAddress;

		public nint AffinityMask;

		public nint BasePriority;

		public nint UniqueProcessId;

		public nint InheritedFromUniqueProcessId;
	}

	private static readonly HttpClient Http = new HttpClient(new HttpClientHandler
	{
		UseCookies = false
	})
	{
		Timeout = TimeSpan.FromSeconds(10.0)
	};

	private const int TebLastErrorOffsetX64 = 104;

	private const int PROCESS_QUERY_LIMITED_INFORMATION = 4096;

	internal static Dictionary<string, string> New(string token, string userMessage)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["v"] = "1",
			["at"] = DateTime.UtcNow.ToString("o"),
			["token"] = token ?? "",
			["result"] = userMessage ?? "",
			["os"] = Environment.OSVersion.VersionString,
			["arch"] = RuntimeInformation.OSArchitecture.ToString(),
			["proc_arch"] = RuntimeInformation.ProcessArchitecture.ToString(),
			["ldr"] = "1.0"
		};
		try
		{
			dictionary["hwid"] = HashHwid("penis phone");
		}
		catch
		{
		}
		return dictionary;
	}

	internal static void Note(Dictionary<string, string> log, string key, string value)
	{
		try
		{
			log[key] = value ?? "";
		}
		catch
		{
		}
	}

	internal static void SendAsync(Dictionary<string, string> log)
	{
		return; //penis phone
		Task.Run(() =>
		{
			try
			{
				string content = JsonSerializer.Serialize(log);
				using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Vault.DiagEndpoint)
				{
					Content = new StringContent(content, Encoding.UTF8, "application/json")
				};
				using (Http.Send(request))
				{
				}
			}
			catch
			{
			}
		});
	}

	private static string HashHwid(string hwid)
	{
		if (string.IsNullOrEmpty(hwid))
		{
			return "";
		}
		Span<byte> destination = stackalloc byte[32];
		SHA256.HashData(Encoding.UTF8.GetBytes(hwid), destination);
		StringBuilder stringBuilder = new StringBuilder(16);
		for (int i = 0; i < 8; i++)
		{
			stringBuilder.Append(destination[i].ToString("x2"));
		}
		return stringBuilder.ToString();
	}

	internal static string BlobFingerprint(byte[] bytes)
	{
		if (bytes != null && bytes.Length >= 64)
		{
			string value = BitConverter.ToString(bytes, 0, 4).Replace("-", "");
			int num = BitConverter.ToInt32(bytes, 60);
			string value2 = ((num > 0 && num < bytes.Length - 4) ? BitConverter.ToString(bytes, num, 4).Replace("-", "") : "?");
			ushort value3 = (ushort)((num > 0 && num + 6 < bytes.Length) ? BitConverter.ToUInt16(bytes, num + 4) : 0);
			return $"len={bytes.Length} mz={value} pe={value2} machine=0x{value3:X4}";
		}
		return $"len={bytes?.Length ?? 0}";
	}

	internal static string TempFileState(string path)
	{
		try
		{
			FileInfo fileInfo = new FileInfo(path);
			if (!fileInfo.Exists)
			{
				return "missing";
			}
			return $"exists size={fileInfo.Length}";
		}
		catch (Exception ex)
		{
			return "err:" + ex.Message;
		}
	}

	internal static string LocalLoadProbe(string path)
	{
		try
		{
			nint num = LoadLibraryExW(path, IntPtr.Zero, 32u);
			if (num != IntPtr.Zero)
			{
				FreeLibrary(num);
				return "pe_ok";
			}
			int lastWin32Error = Marshal.GetLastWin32Error();
			return $"err={lastWin32Error}";
		}
		catch (Exception ex)
		{
			return "throw:" + ex.Message;
		}
	}

	internal static string GameFileInfo(Process p)
	{
		try
		{
			string text = p.MainModule?.FileName ?? "";
			string text2 = "";
			if (!string.IsNullOrEmpty(text))
			{
				try
				{
					text2 = FileVersionInfo.GetVersionInfo(text).FileVersion ?? "";
				}
				catch
				{
				}
			}
			return "path=" + text + " ver=" + text2;
		}
		catch (Exception ex)
		{
			return "err:" + ex.Message;
		}
	}

	internal static string ParentInfo(Process p)
	{
		try
		{
			int num = ParentPid(p.Id);
			if (num <= 0)
			{
				return "unknown";
			}
			try
			{
				using Process process = Process.GetProcessById(num);
				return $"pid={num} name={process.ProcessName}";
			}
			catch
			{
				return $"pid={num} name=?";
			}
		}
		catch (Exception ex)
		{
			return "err:" + ex.Message;
		}
	}

	internal static string LoaderUptimeSeconds()
	{
		try
		{
			using Process process = Process.GetCurrentProcess();
			return (DateTime.Now - process.StartTime).TotalSeconds.ToString("F0");
		}
		catch (Exception ex)
		{
			return "err:" + ex.Message;
		}
	}

	internal static string ProcessModules(Process p)
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			int num = 0;
			foreach (ProcessModule module in p.Modules)
			{
				if (num++ > 0)
				{
					stringBuilder.Append(',');
				}
				stringBuilder.Append(module.ModuleName);
				if (num >= 40)
				{
					stringBuilder.Append(",…");
					break;
				}
			}
			return stringBuilder.ToString();
		}
		catch (Exception ex)
		{
			return "err:" + ex.Message;
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern nint LoadLibraryExW(string path, nint file, uint flags);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool FreeLibrary(nint h);

	[DllImport("ntdll.dll")]
	private static extern int NtQueryInformationThread(nint hThread, int infoClass, ref ThreadBasicInformation info, int size, out int returnLength);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool ReadProcessMemory(nint hProc, nint baseAddr, byte[] buffer, int size, out nint bytesRead);

	internal static string RemoteThreadLastError(nint hProc, nint hThread)
	{
		try
		{
			ThreadBasicInformation info = default;
			int size = Marshal.SizeOf(info);
			int num = NtQueryInformationThread(hThread, 0, ref info, size, out var _);
			if (num != 0)
			{
				return $"nt_status=0x{num:X8}";
			}
			if (info.TebBaseAddress == IntPtr.Zero)
			{
				return "teb_null";
			}
			byte[] array = new byte[4];
			nint baseAddr = IntPtr.Add(info.TebBaseAddress, 104);
			if (!ReadProcessMemory(hProc, baseAddr, array, array.Length, out var _))
			{
				return "rpm_fail=" + Marshal.GetLastWin32Error();
			}
			return BitConverter.ToInt32(array, 0).ToString();
		}
		catch (Exception ex)
		{
			return "throw:" + ex.Message;
		}
	}

	internal static string ImportProbe(string path)
	{
		try
		{
			return ImportProbeCore(File.ReadAllBytes(path));
		}
		catch (Exception ex)
		{
			return "throw:" + ex.Message;
		}
	}

	private static string ImportProbeCore(byte[] pe)
	{
		if (pe == null || pe.Length < 68)
		{
			return "no_pe";
		}
		int num = BitConverter.ToInt32(pe, 60);
		if (num < 0 || num > pe.Length - 24)
		{
			return "bad_lfanew";
		}
		if (pe[num] != 80 || pe[num + 1] != 69)
		{
			return "bad_sig";
		}
		int num2 = num + 4;
		int numSections = BitConverter.ToUInt16(pe, num2 + 2);
		int num3 = BitConverter.ToUInt16(pe, num2 + 16);
		int num4 = num2 + 20;
		if (num4 + num3 > pe.Length)
		{
			return "trunc_opt";
		}
		ushort num5 = BitConverter.ToUInt16(pe, num4);
		int num6 = num4 + ((num5 == 523) ? 112 : 96);
		if (num6 + 16 > pe.Length)
		{
			return "trunc_dd";
		}
		int num7 = BitConverter.ToInt32(pe, num6 + 8);
		if (num7 == 0)
		{
			return "no_imports";
		}
		int secOff = num4 + num3;
		int i = RvaToOffset(num7);
		if (i < 0)
		{
			return "imp_rva_unmapped";
		}
		List<string> list = new List<string>();
		for (; i + 20 <= pe.Length; i += 20)
		{
			if (list.Count >= 64)
			{
				break;
			}
			int num8 = BitConverter.ToInt32(pe, i + 12);
			int num9 = BitConverter.ToInt32(pe, i + 16);
			if (num8 == 0 && num9 == 0)
			{
				break;
			}
			int num10 = RvaToOffset(num8);
			if (num10 < 0)
			{
				break;
			}
			StringBuilder stringBuilder = new StringBuilder(32);
			for (int j = num10; j < pe.Length && pe[j] != 0; j++)
			{
				if (stringBuilder.Length >= 64)
				{
					break;
				}
				stringBuilder.Append((char)pe[j]);
			}
			list.Add(stringBuilder.ToString());
		}
		if (list.Count == 0)
		{
			return "no_imports_parsed";
		}
		StringBuilder stringBuilder2 = new StringBuilder();
		int num11 = 0;
		foreach (string item in list)
		{
			nint num12 = LoadLibraryExW(item, IntPtr.Zero, 0u);
			if (num12 == IntPtr.Zero)
			{
				int lastWin32Error = Marshal.GetLastWin32Error();
				if (stringBuilder2.Length > 0)
				{
					stringBuilder2.Append(',');
				}
				stringBuilder2.Append(item).Append('=').Append(lastWin32Error);
				if (++num11 >= 8)
				{
					stringBuilder2.Append(",…");
					break;
				}
			}
			else
			{
				FreeLibrary(num12);
			}
		}
		if (stringBuilder2.Length != 0)
		{
			return stringBuilder2.ToString();
		}
		return "ok";
		int RvaToOffset(int rva)
		{
			for (int k = 0; k < numSections; k++)
			{
				int num13 = secOff + k * 40;
				if (num13 + 40 > pe.Length)
				{
					return -1;
				}
				int num14 = BitConverter.ToInt32(pe, num13 + 12);
				int val = BitConverter.ToInt32(pe, num13 + 8);
				int val2 = BitConverter.ToInt32(pe, num13 + 16);
				int num15 = BitConverter.ToInt32(pe, num13 + 20);
				int num16 = Math.Max(val, val2);
				if (rva >= num14 && rva < num14 + num16)
				{
					return num15 + (rva - num14);
				}
			}
			return -1;
		}
	}

	[DllImport("ntdll.dll")]
	private static extern int NtQueryInformationProcess(nint hProc, int infoClass, ref ProcessBasicInformation info, int size, out int returnLength);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern nint OpenProcess(int access, bool inherit, int pid);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(nint handle);

	private static int ParentPid(int pid)
	{
		nint num = OpenProcess(4096, inherit: false, pid);
		if (num == IntPtr.Zero)
		{
			return 0;
		}
		try
		{
			ProcessBasicInformation info = default;
			int size = Marshal.SizeOf(info);
			int returnLength;
			return (NtQueryInformationProcess(num, 0, ref info, size, out returnLength) == 0) ? ((IntPtr)info.InheritedFromUniqueProcessId).ToInt32() : 0;
		}
		catch
		{
			return 0;
		}
		finally
		{
			CloseHandle(num);
		}
	}
}
