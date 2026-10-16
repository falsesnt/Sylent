using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using SylentLoader.Services.Security;

namespace SylentLoader.Models;

public class GameProfile
{
	public GameId Id { get; set; }

	public string DisplayName { get; set; } = "";

	public string ShortName { get; set; } = "";

	public string? InstallPath { get; set; }

	public string ExecutableName { get; set; } = "";

	public int SteamAppId { get; set; }

	public Color BannerColorTop { get; set; }

	public Color BannerColorBottom { get; set; }

	public List<ModFramework> ModFrameworks { get; set; } = new List<ModFramework>();

	public GtaVersion GtaVersion { get; set; }

	public GtaMod SelectedMod { get; set; }

	public string? EnhancedInstallPath { get; set; }

	public bool HasEnhancedVersion
	{
		get
		{
			if (Id == GameId.GTAV && !string.IsNullOrEmpty(InstallPath))
			{
				return File.Exists(Path.Combine(InstallPath, "GTA5_Enhanced.exe"));
			}
			return false;
		}
	}

	public string? CurrentInstallPath
	{
		get
		{
			if (Id != GameId.GTAV)
			{
				return InstallPath;
			}
			if (GtaVersion != GtaVersion.Enhanced)
			{
				return InstallPath;
			}
			return EnhancedInstallPath;
		}
	}

	public bool IsCurrentVersionInstalled
	{
		get
		{
			if (!string.IsNullOrEmpty(CurrentInstallPath))
			{
				return Directory.Exists(CurrentInstallPath);
			}
			return false;
		}
	}

	public string ActiveExecutable
	{
		get
		{
			if (Id != GameId.GTAV || GtaVersion != GtaVersion.Enhanced || !HasEnhancedVersion)
			{
				return ExecutableName;
			}
			return "GTA5_Enhanced.exe";
		}
	}

	public string ProcessName
	{
		get
		{
			if (Id != GameId.GTAV)
			{
				return Path.GetFileNameWithoutExtension(ExecutableName);
			}
			if (GtaVersion != GtaVersion.Enhanced)
			{
				return "GTA5";
			}
			return "GTA5_Enhanced";
		}
	}

	public string EmbeddedDll1Name => Id switch
	{
		GameId.GTAV => DllFor(SelectedMod, GtaVersion), 
		GameId.RDR2 => PayloadTokens.ResourceName("5e9b7710"), 
		_ => "", 
	};

	public string EmbeddedDll2Name
	{
		get
		{
			if (Id == GameId.RDR2)
			{
				return PayloadTokens.ResourceName("a2d40f63");
			}
			return "";
		}
	}

	public bool IsInstalled
	{
		get
		{
			if (!string.IsNullOrEmpty(InstallPath) && Directory.Exists(InstallPath))
			{
				return File.Exists(Path.Combine(InstallPath, ExecutableName));
			}
			return false;
		}
	}

	public bool HasAnyMods => ModFrameworks.Any((ModFramework m) => m.Detected);

	public static string DllFor(GtaMod mod, GtaVersion version)
	{
		return mod switch
		{
			GtaMod.SylentMenu => PayloadTokens.ResourceName((version == GtaVersion.Enhanced) ? "7be02a44" : "d43a9f1c"), 
			GtaMod.SylentRecovery => PayloadTokens.ResourceName((version == GtaVersion.Enhanced) ? "88a13de9" : "2f91cc08"), 
			GtaMod.Aurora => (version == GtaVersion.Enhanced) ? PayloadTokens.ResourceName("c015f2a7") : "", 
			_ => "", 
		};
	}
}
