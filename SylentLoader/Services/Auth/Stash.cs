using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SylentLoader.Services.Security;

namespace SylentLoader.Services.Auth;

internal static class Stash
{
	private struct Blob
	{
		public int Size;

		public nint Data;
	}

	private const int UiForbidden = 1;

	private static readonly byte[] EntropyPrefix = new byte[3] { 62, 9, 97 };

	[DllImport("crypt32.dll", SetLastError = true)]
	private static extern bool CryptProtectData(ref Blob input, nint desc, ref Blob entropy, nint reserved, nint prompt, int flags, out Blob output);

	[DllImport("crypt32.dll", SetLastError = true)]
	private static extern bool CryptUnprotectData(ref Blob input, nint desc, ref Blob entropy, nint reserved, nint prompt, int flags, out Blob output);

	[DllImport("kernel32.dll")]
	private static extern nint LocalFree(nint mem);

	public static bool Exists(Product p)
	{
		return File.Exists(PathFor(p));
	}

	public static void Save(Product p, string key)
	{
		try
		{
			string path = PathFor(p);
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			byte[] array = Transform(Encoding.UTF8.GetBytes(key), Entropy(p), protect: true);
			if (array != null)
			{
				File.WriteAllBytes(path, array);
			}
		}
		catch
		{
		}
	}

	public static string? Load(Product p)
	{
		try
		{
			string path = PathFor(p);
			if (!File.Exists(path))
			{
				return null;
			}
			byte[] array = Transform(File.ReadAllBytes(path), Entropy(p), protect: false);
			return (array == null) ? null : Encoding.UTF8.GetString(array);
		}
		catch
		{
			return null;
		}
	}

	public static void Delete(Product p)
	{
		try
		{
			File.Delete(PathFor(p));
		}
		catch
		{
		}
	}

	private static string PathFor(Product p)
	{
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SylentLoader");
		string text = Convert.ToHexString(SHA256.HashData(Entropy(p))).Substring(0, 12).ToLowerInvariant();
		return Path.Combine(path, text + ".dat");
	}

	private static byte[] Entropy(Product p)
	{
		string text = Obfs.UnmaskString(EntropyPrefix, 68);
		string value = "kekma";
		return Encoding.UTF8.GetBytes(text + p.ToString() + "/" + value);
	}

	private static byte[]? Transform(byte[] input, byte[] entropy, bool protect)
	{
		Blob input2 = Pin(input);
		Blob entropy2 = Pin(entropy);
		try
		{
			if (!(protect ? CryptProtectData(ref input2, IntPtr.Zero, ref entropy2, IntPtr.Zero, IntPtr.Zero, 1, out var output) : CryptUnprotectData(ref input2, IntPtr.Zero, ref entropy2, IntPtr.Zero, IntPtr.Zero, 1, out output)))
			{
				return null;
			}
			byte[] array = new byte[output.Size];
			Marshal.Copy(output.Data, array, 0, output.Size);
			LocalFree(output.Data);
			return array;
		}
		finally
		{
			Marshal.FreeHGlobal(input2.Data);
			Marshal.FreeHGlobal(entropy2.Data);
		}
	}

	private static Blob Pin(byte[] data)
	{
		nint num = Marshal.AllocHGlobal(data.Length);
		Marshal.Copy(data, 0, num, data.Length);
		return new Blob
		{
			Size = data.Length,
			Data = num
		};
	}
}
