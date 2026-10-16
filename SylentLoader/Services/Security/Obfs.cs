using System.Text;

namespace SylentLoader.Services.Security;

internal static class Obfs
{
	public static byte[] Unmask(byte[] masked, int seed)
	{
		byte[] array = new byte[masked.Length];
		int num = seed;
		for (int i = 0; i < masked.Length; i++)
		{
			num = (num * 31 + 17 + i) & 0xFF;
			array[i] = (byte)(masked[i] ^ num);
		}
		return array;
	}

	public static byte[] Mask(byte[] plain, int seed)
	{
		return Unmask(plain, seed);
	}

	public static string UnmaskString(byte[] masked, int seed)
	{
		return Encoding.UTF8.GetString(Unmask(masked, seed));
	}
}
