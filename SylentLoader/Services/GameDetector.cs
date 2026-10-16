using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SylentLoader.Models;

namespace SylentLoader.Services;

public static class GameDetector
{
	private static readonly string[] EnhancedFallbackPaths = new string[20]
	{
		"Program Files\\Rockstar Games\\Grand Theft Auto V Enhanced", "Program Files\\Rockstar Games\\Grand Theft Auto V", "Rockstar Games\\Grand Theft Auto V Enhanced", "Rockstar Games\\Grand Theft Auto V", "Program Files (x86)\\Steam\\steamapps\\common\\Grand Theft Auto V Enhanced", "Program Files (x86)\\Steam\\steamapps\\common\\Grand Theft Auto V", "Steam\\steamapps\\common\\Grand Theft Auto V Enhanced", "Steam\\steamapps\\common\\Grand Theft Auto V", "SteamLibrary\\steamapps\\common\\Grand Theft Auto V Enhanced", "SteamLibrary\\steamapps\\common\\Grand Theft Auto V",
		"Games\\Steam\\steamapps\\common\\Grand Theft Auto V Enhanced", "Games\\Grand Theft Auto V Enhanced", "Games\\Grand Theft Auto V", "Program Files\\Epic Games\\GTAV", "Epic Games\\GTAV", "Grand Theft Auto V Enhanced", "Grand Theft Auto V", "GTA V Enhanced", "GTAV Enhanced", "GTAV"
	};

	private static readonly string[] LegacyFallbackPaths = new string[15]
	{
		"Program Files\\Rockstar Games\\Grand Theft Auto V", "Program Files\\Rockstar Games\\Grand Theft Auto V Legacy", "Rockstar Games\\Grand Theft Auto V", "Rockstar Games\\Grand Theft Auto V Legacy", "Program Files (x86)\\Steam\\steamapps\\common\\Grand Theft Auto V", "Steam\\steamapps\\common\\Grand Theft Auto V", "SteamLibrary\\steamapps\\common\\Grand Theft Auto V", "Games\\Steam\\steamapps\\common\\Grand Theft Auto V", "Games\\Grand Theft Auto V", "Program Files\\Epic Games\\GTAV",
		"Epic Games\\GTAV", "Grand Theft Auto V", "Grand Theft Auto V Legacy", "GTA V", "GTAV"
	};

	private static readonly string[] Rdr2FallbackPaths = new string[10] { "Program Files\\Rockstar Games\\Red Dead Redemption 2", "Rockstar Games\\Red Dead Redemption 2", "Program Files (x86)\\Steam\\steamapps\\common\\Red Dead Redemption 2", "Steam\\steamapps\\common\\Red Dead Redemption 2", "SteamLibrary\\steamapps\\common\\Red Dead Redemption 2", "Games\\Red Dead Redemption 2", "Program Files\\Epic Games\\Red Dead Redemption 2", "Epic Games\\Red Dead Redemption 2", "Red Dead Redemption 2", "RDR2" };

	public static GameProfile CreateGTAV()
	{
		return new GameProfile
		{
			Id = GameId.GTAV,
			DisplayName = "Grand Theft Auto V",
			ShortName = "GTA V",
			ExecutableName = "GTA5.exe",
			SteamAppId = 271590,
			BannerColorTop = Color.FromArgb(230, 90, 10),
			BannerColorBottom = Color.FromArgb(15, 15, 25),
			InstallPath = DetectGTAV(),
			EnhancedInstallPath = DetectGTAVEnhanced()
		};
	}

	public static GameProfile CreateRDR2()
	{
		return new GameProfile
		{
			Id = GameId.RDR2,
			DisplayName = "Red Dead Redemption 2",
			ShortName = "RDR 2",
			ExecutableName = "RDR2.exe",
			SteamAppId = 1174180,
			BannerColorTop = Color.FromArgb(90, 30, 10),
			BannerColorBottom = Color.FromArgb(12, 8, 5),
			InstallPath = DetectRDR2()
		};
	}

	private static string? DetectGTAVEnhanced()
	{
		if (Valid(AppSettings.CachedGtaVEnhancedPath, "GTA5_Enhanced.exe"))
		{
			return AppSettings.CachedGtaVEnhancedPath;
		}
		string text = ReadReg("SOFTWARE\\WOW6432Node\\Rockstar Games\\Grand Theft Auto V Enhanced", "InstallFolder");
		if (Valid(text, "GTA5_Enhanced.exe"))
		{
			return text;
		}
		text = ReadReg("SOFTWARE\\WOW6432Node\\Rockstar Games\\Grand Theft Auto V", "InstallFolder");
		if (Valid(text, "GTA5_Enhanced.exe"))
		{
			return text;
		}
		text = ReadReg("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App 271590", "InstallLocation");
		if (Valid(text, "GTA5_Enhanced.exe"))
		{
			return text;
		}
		string text2 = FindSteamGame("Grand Theft Auto V Enhanced", "GTA5_Enhanced.exe") ?? FindSteamGame("Grand Theft Auto V", "GTA5_Enhanced.exe");
		if (text2 != null)
		{
			return text2;
		}
		string text3 = FindOnDrives(new string[2] { "XboxGames\\Grand Theft Auto V Enhanced\\Content", "XboxGames\\Grand Theft Auto V\\Content" }, "GTA5_Enhanced.exe");
		if (text3 != null)
		{
			return text3;
		}
		return FindOnDrives(EnhancedFallbackPaths, "GTA5_Enhanced.exe");
	}

	private static string? DetectGTAV()
	{
		if (Valid(AppSettings.CachedGtaVLegacyPath, "GTA5.exe"))
		{
			return AppSettings.CachedGtaVLegacyPath;
		}
		string text = ReadReg("SOFTWARE\\WOW6432Node\\Rockstar Games\\Grand Theft Auto V", "InstallFolder");
		if (Valid(text, "GTA5.exe"))
		{
			return text;
		}
		text = ReadReg("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App 271590", "InstallLocation");
		if (Valid(text, "GTA5.exe"))
		{
			return text;
		}
		string text2 = FindSteamGame("Grand Theft Auto V", "GTA5.exe");
		if (text2 != null)
		{
			return text2;
		}
		string text3 = FindOnDrives(new string[1] { "XboxGames\\Grand Theft Auto V\\Content" }, "GTA5.exe");
		if (text3 != null)
		{
			return text3;
		}
		return FindOnDrives(LegacyFallbackPaths, "GTA5.exe");
	}

	private static string? DetectRDR2()
	{
		if (Valid(AppSettings.CachedRdr2Path, "RDR2.exe"))
		{
			return AppSettings.CachedRdr2Path;
		}
		string text = ReadReg("SOFTWARE\\WOW6432Node\\Rockstar Games\\Red Dead Redemption 2", "InstallFolder");
		if (Valid(text, "RDR2.exe"))
		{
			return text;
		}
		text = ReadReg("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App 1174180", "InstallLocation");
		if (Valid(text, "RDR2.exe"))
		{
			return text;
		}
		string text2 = FindSteamGame("Red Dead Redemption 2", "RDR2.exe");
		if (text2 != null)
		{
			return text2;
		}
		string text3 = FindOnDrives(new string[1] { "XboxGames\\Red Dead Redemption 2\\Content" }, "RDR2.exe");
		if (text3 != null)
		{
			return text3;
		}
		return FindOnDrives(Rdr2FallbackPaths, "RDR2.exe");
	}

	private static string? FindOnDrives(string[] subpaths, string exe)
	{
		try
		{
			DriveInfo[] drives = DriveInfo.GetDrives();
			foreach (DriveInfo driveInfo in drives)
			{
				if (driveInfo.DriveType != DriveType.Fixed)
				{
					continue;
				}
				string fullName = driveInfo.RootDirectory.FullName;
				foreach (string path in subpaths)
				{
					string text = Path.Combine(fullName, path);
					if (Valid(text, exe))
					{
						return text;
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public static string? DeepFind(string exeName, int maxDepth = 6, int budgetMs = 20000)
	{
		DateTime deadline = DateTime.UtcNow.AddMilliseconds(budgetMs);
		HashSet<string> skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"Windows", "WindowsApps", "ProgramData", "$Recycle.Bin", "System Volume Information", "Recovery", "PerfLogs", "node_modules", ".git", "AppData",
			"OneDriveTemp"
		};
		try
		{
			DriveInfo[] drives = DriveInfo.GetDrives();
			foreach (DriveInfo driveInfo in drives)
			{
				if (driveInfo.DriveType == DriveType.Fixed)
				{
					string text = Walk(driveInfo.RootDirectory.FullName, exeName, 0, maxDepth, skip, deadline);
					if (text != null)
					{
						return text;
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static string? Walk(string dir, string exe, int depth, int maxDepth, HashSet<string> skip, DateTime deadline)
	{
		if (DateTime.UtcNow > deadline || depth > maxDepth)
		{
			return null;
		}
		try
		{
			if (File.Exists(Path.Combine(dir, exe)))
			{
				return dir;
			}
			foreach (string item in Directory.EnumerateDirectories(dir))
			{
				string fileName = Path.GetFileName(item);
				if (fileName.Length == 0 || fileName[0] == '.' || skip.Contains(fileName))
				{
					continue;
				}
				FileAttributes attributes;
				try
				{
					attributes = new DirectoryInfo(item).Attributes;
				}
				catch
				{
					continue;
				}
				if ((attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint)) == 0)
				{
					string text = Walk(item, exe, depth + 1, maxDepth, skip, deadline);
					if (text != null)
					{
						return text;
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static string? FindSteamGame(string folderName, string exe)
	{
		string text = ReadReg("SOFTWARE\\WOW6432Node\\Valve\\Steam", "InstallPath") ?? ReadReg("SOFTWARE\\Valve\\Steam", "InstallPath") ?? ReadRegHkcu("SOFTWARE\\Valve\\Steam", "SteamPath");
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		string path = Path.Combine(text, "steamapps", "libraryfolders.vdf");
		if (!File.Exists(path))
		{
			return null;
		}
		try
		{
			foreach (Match item in Regex.Matches(File.ReadAllText(path), "\"path\"\\s+\"([^\"]+)\""))
			{
				string text2 = Path.Combine(item.Groups[1].Value.Replace("\\\\", "\\"), "steamapps", "common", folderName);
				if (Valid(text2, exe))
				{
					return text2;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public static string? ReadReg(string key, string value)
	{
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(key);
			return registryKey?.GetValue(value) as string;
		}
		catch
		{
			return null;
		}
	}

	public static string? ReadRegHkcu(string key, string value)
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(key);
			return registryKey?.GetValue(value) as string;
		}
		catch
		{
			return null;
		}
	}

	private static bool Valid(string? path, string exe)
	{
		if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
		{
			return File.Exists(Path.Combine(path, exe));
		}
		return false;
	}
}
