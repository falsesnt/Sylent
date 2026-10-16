using System;
using System.IO;
using System.Text.Json;
using SylentLoader.Models;

namespace SylentLoader.Services;

public static class AppSettings
{
	private sealed class Data
	{
		public bool AutoSignIn { get; set; } = true;

		public bool RememberKeys { get; set; } = true;

		public bool CloseAfterInject { get; set; }

		public bool AutoInject { get; set; }

		public GtaMod LastGtaMod { get; set; }

		public string? PubLobbyBypassPath { get; set; }

		public string? PubLobbyBypassPathLegacy { get; set; }

		public string? PubLobbyBypassPathEnhanced { get; set; }

		public string? CachedGtaVLegacyPath { get; set; }

		public string? CachedGtaVEnhancedPath { get; set; }

		public string? CachedRdr2Path { get; set; }
	}

	private static Data _d;

	public static bool AutoSignIn
	{
		get
		{
			return _d.AutoSignIn;
		}
		set
		{
			_d.AutoSignIn = value;
			Save();
		}
	}

	public static bool RememberKeys
	{
		get
		{
			return _d.RememberKeys;
		}
		set
		{
			_d.RememberKeys = value;
			Save();
		}
	}

	public static bool CloseAfterInject
	{
		get
		{
			return _d.CloseAfterInject;
		}
		set
		{
			_d.CloseAfterInject = value;
			Save();
		}
	}

	public static bool AutoInject
	{
		get
		{
			return _d.AutoInject;
		}
		set
		{
			_d.AutoInject = value;
			Save();
		}
	}

	public static GtaMod LastGtaMod
	{
		get
		{
			return _d.LastGtaMod;
		}
		set
		{
			_d.LastGtaMod = value;
			Save();
		}
	}

	public static string? PubLobbyBypassPathLegacy
	{
		get
		{
			return _d.PubLobbyBypassPathLegacy;
		}
		set
		{
			_d.PubLobbyBypassPathLegacy = value;
			Save();
		}
	}

	public static string? PubLobbyBypassPathEnhanced
	{
		get
		{
			return _d.PubLobbyBypassPathEnhanced;
		}
		set
		{
			_d.PubLobbyBypassPathEnhanced = value;
			Save();
		}
	}

	public static string? CachedGtaVLegacyPath
	{
		get
		{
			return _d.CachedGtaVLegacyPath;
		}
		set
		{
			_d.CachedGtaVLegacyPath = value;
			Save();
		}
	}

	public static string? CachedGtaVEnhancedPath
	{
		get
		{
			return _d.CachedGtaVEnhancedPath;
		}
		set
		{
			_d.CachedGtaVEnhancedPath = value;
			Save();
		}
	}

	public static string? CachedRdr2Path
	{
		get
		{
			return _d.CachedRdr2Path;
		}
		set
		{
			_d.CachedRdr2Path = value;
			Save();
		}
	}

	private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SylentLoader", "settings.json");

	public static event Action? Changed;

	static AppSettings()
	{
		_d = new Data();
		try
		{
			if (File.Exists(FilePath))
			{
				_d = JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath)) ?? new Data();
			}
		}
		catch
		{
			_d = new Data();
		}
		MigrateLegacyBypassPath();
	}

	private static void MigrateLegacyBypassPath()
	{
		string pubLobbyBypassPath = _d.PubLobbyBypassPath;
		if (string.IsNullOrEmpty(pubLobbyBypassPath))
		{
			return;
		}
		try
		{
			if (File.Exists(Path.Combine(Path.GetDirectoryName(pubLobbyBypassPath) ?? "", "GTA5_Enhanced.exe")))
			{
				if (string.IsNullOrEmpty(_d.PubLobbyBypassPathEnhanced))
				{
					_d.PubLobbyBypassPathEnhanced = pubLobbyBypassPath;
				}
			}
			else if (string.IsNullOrEmpty(_d.PubLobbyBypassPathLegacy))
			{
				_d.PubLobbyBypassPathLegacy = pubLobbyBypassPath;
			}
		}
		catch
		{
		}
		_d.PubLobbyBypassPath = null;
		Save();
	}

	private static void Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
			File.WriteAllText(FilePath, JsonSerializer.Serialize(_d, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
		}
		catch
		{
		}
		Changed?.Invoke();
	}
}
