using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace SylentLoader.Services;

public static class ImageLoader
{
	private static Image? _gta5;

	private static Image? _rdr2;

	private static Image? _gtaCity;

	private static Image? _gtaBg;

	private static Image? _rdr2Bg;

	private static string? _steamPath;

	public static Image GetGTA5Art()
	{
		return _gta5 ?? (_gta5 = LoadEmbedded("gta5.png"));
	}

	public static Image GetRDR2Art()
	{
		return _rdr2 ?? (_rdr2 = LoadEmbedded("rdr2.jpg"));
	}

	public static Image GetGTACityArt()
	{
		return _gtaCity ?? (_gtaCity = LoadEmbedded("gta_city.jpg"));
	}

	public static Image GetGtaBackground()
	{
		object obj = _gtaBg;
		if (obj == null)
		{
			obj = TryLoadEmbedded("gta_bg.jpg", "gta_bg.png") ?? GetGTACityArt();
			_gtaBg = (Image?)obj;
		}
		return (Image)obj;
	}

	public static Image GetRdr2Background()
	{
		object obj = _rdr2Bg;
		if (obj == null)
		{
			obj = TryLoadEmbedded("rdr2_bg.jpg", "rdr2_bg.png") ?? GetRDR2Art();
			_rdr2Bg = (Image?)obj;
		}
		return (Image)obj;
	}

	private static Image? TryLoadEmbedded(params string[] fileNames)
	{
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		foreach (string text in fileNames)
		{
			using Stream stream = executingAssembly.GetManifestResourceStream("SylentLoader.Resources." + text);
			if (stream == null)
			{
				continue;
			}
			using Image original = Image.FromStream(stream);
			return new Bitmap(original);
		}
		return null;
	}

	private static Image LoadEmbedded(string fileName)
	{
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		string text = "SylentLoader.Resources." + fileName;
		using Stream stream = executingAssembly.GetManifestResourceStream(text) ?? throw new InvalidOperationException("Embedded resource not found: " + text);
		using Image original = Image.FromStream(stream);
		return new Bitmap(original);
	}

	public static Bitmap CreateGradientBanner(int width, int height, Color top, Color bottom, string label)
	{
		Bitmap bitmap = new Bitmap(width, height);
		using Graphics graphics = Graphics.FromImage(bitmap);
		using LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, width, height), top, bottom, LinearGradientMode.Vertical);
		graphics.FillRectangle(brush, 0, 0, width, height);
		using Pen pen = new Pen(Color.FromArgb(18, 255, 255, 255), 1f);
		for (int i = 0; i < width; i += 40)
		{
			graphics.DrawLine(pen, i, 0, i, height);
		}
		for (int j = 0; j < height; j += 40)
		{
			graphics.DrawLine(pen, 0, j, width, j);
		}
		using Font font = new Font("Segoe UI", 28f, FontStyle.Bold);
		using SolidBrush brush2 = new SolidBrush(Color.FromArgb(160, 255, 255, 255));
		SizeF sizeF = graphics.MeasureString(label, font);
		graphics.DrawString(label, font, brush2, ((float)width - sizeF.Width) / 2f, ((float)height - sizeF.Height) / 2f);
		return bitmap;
	}

	public static Image? TryLoadSteamBanner(int appId)
	{
		string steamPath = GetSteamPath();
		if (steamPath == null)
		{
			return null;
		}
		string path = Path.Combine(steamPath, "appcache", "librarycache");
		string[] array = new string[2] { "_header.jpg", "_library_hero.jpg" };
		foreach (string value in array)
		{
			string path2 = Path.Combine(path, $"{appId}{value}");
			if (!File.Exists(path2))
			{
				continue;
			}
			try
			{
				using FileStream fileStream = File.OpenRead(path2);
				using MemoryStream memoryStream = new MemoryStream();
				fileStream.CopyTo(memoryStream);
				memoryStream.Seek(0L, SeekOrigin.Begin);
				return Image.FromStream(memoryStream);
			}
			catch
			{
			}
		}
		return null;
	}

	private static string? GetSteamPath()
	{
		if (_steamPath != null)
		{
			return _steamPath;
		}
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Valve\\Steam");
			_steamPath = registryKey?.GetValue("InstallPath") as string;
		}
		catch
		{
		}
		return _steamPath;
	}
}
