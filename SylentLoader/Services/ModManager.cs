using System.Diagnostics;
using System.IO;
using SylentLoader.Models;

namespace SylentLoader.Services;

public static class ModManager
{
	public static void DetectMods(GameProfile profile)
	{
		profile.ModFrameworks.Clear();
		if (profile.IsInstalled)
		{
			string installPath = profile.InstallPath;
			if (profile.Id == GameId.GTAV)
			{
				DetectGTAV(installPath, profile);
			}
			else if (profile.Id == GameId.RDR2)
			{
				DetectRDR2(installPath, profile);
			}
		}
	}

	private static void DetectGTAV(string dir, GameProfile profile)
	{
		AddFileMod(profile, "ASI Loader", dir, "dinput8.dll");
		AddFileMod(profile, "ScriptHookV", dir, "ScriptHookV.dll");
		AddFileMod(profile, "ScriptHookVDotNet", dir, "ScriptHookVDotNet3.dll", "ScriptHookVDotNet.dll");
		AddFolderMod(profile, "Mods Folder (OpenIV)", Path.Combine(dir, "mods"));
	}

	private static void DetectRDR2(string dir, GameProfile profile)
	{
		AddFileMod(profile, "ASI Loader", dir, "version.dll");
		AddFileMod(profile, "ScriptHookRDR2", dir, "ScriptHookRDR2.dll");
		AddFolderMod(profile, "Lenny's Mod Loader (lml)", Path.Combine(dir, "lml"));
	}

	private static void AddFileMod(GameProfile profile, string name, string dir, params string[] candidates)
	{
		foreach (string path in candidates)
		{
			string text = Path.Combine(dir, path);
			string path2 = text + ".bak";
			if (File.Exists(text) || File.Exists(path2))
			{
				profile.ModFrameworks.Add(new ModFramework
				{
					Name = name,
					Detected = true,
					Enabled = File.Exists(text),
					Version = (File.Exists(text) ? GetFileVersion(text) : null)
				});
				return;
			}
		}
		profile.ModFrameworks.Add(new ModFramework
		{
			Name = name,
			Detected = false
		});
	}

	private static void AddFolderMod(GameProfile profile, string name, string folderPath)
	{
		profile.ModFrameworks.Add(new ModFramework
		{
			Name = name,
			Detected = Directory.Exists(folderPath),
			Enabled = Directory.Exists(folderPath)
		});
	}

	public static bool ToggleMods(GameProfile profile, bool enable)
	{
		if (!profile.IsInstalled)
		{
			return false;
		}
		string installPath = profile.InstallPath;
		string path = ((profile.Id == GameId.GTAV) ? "dinput8.dll" : "version.dll");
		try
		{
			string text = Path.Combine(installPath, path);
			string text2 = text + ".bak";
			if (enable && File.Exists(text2) && !File.Exists(text))
			{
				File.Move(text2, text);
			}
			else if (!enable && File.Exists(text) && !File.Exists(text2))
			{
				File.Move(text, text2);
			}
			DetectMods(profile);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string? GetFileVersion(string path)
	{
		try
		{
			return FileVersionInfo.GetVersionInfo(path).FileVersion;
		}
		catch
		{
			return null;
		}
	}
}
