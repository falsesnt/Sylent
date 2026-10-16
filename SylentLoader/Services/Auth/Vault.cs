using System;
using System.Collections.Generic;
using System.Text;

namespace SylentLoader.Services.Auth;

internal static class Vault
{
	private static readonly byte[] Ep = new byte[28]
	{
		60, 74, 225, 111, 165, 58, 56, 206, 51, 167,
		224, 194, 175, 240, 115, 75, 43, 47, 243, 8,
		191, 120, 118, 198, 81, 228, 146, 132
	};

	private static readonly byte[] Pk = new byte[64]
	{
		251, 49, 183, 83, 50, 242, 115, 68, 228, 177,
		240, 222, 53, 126, 119, 159, 238, 59, 242, 89,
		109, 248, 42, 78, 238, 243, 255, 200, 106, 51,
		123, 215, 237, 45, 174, 65, 80, 176, 24, 6,
		219, 250, 199, 202, 85, 57, 64, 218, 209, 120,
		150, 30, 94, 191, 24, 90, 210, 148, 152, 56,
		88, 80, 31, 161
	};

	private static readonly byte[] DeliverPk = new byte[64]
	{
		247, 98, 233, 92, 54, 165, 34, 70, 180, 233,
		167, 221, 53, 46, 37, 159, 178, 110, 245, 15,
		108, 175, 123, 23, 226, 161, 169, 194, 56, 54,
		120, 129, 239, 39, 175, 19, 83, 176, 67, 83,
		131, 169, 148, 159, 2, 57, 23, 140, 223, 36,
		144, 75, 11, 188, 16, 6, 142, 146, 153, 54,
		88, 86, 73, 162
	};

	private static readonly byte[] MenuN = new byte[10] { 161, 249, 255, 132, 58, 54, 53, 196, 162, 101 };

	private static readonly byte[] MenuO = new byte[10] { 186, 203, 179, 169, 198, 178, 201, 89, 224, 67 };

	private static readonly byte[] MenuV = new byte[3] { 62, 205, 160 };

	private static readonly byte[] RecN = new byte[13]
	{
		88, 77, 136, 230, 218, 143, 31, 50, 14, 216,
		128, 98, 220
	};

	private static readonly byte[] RecO = new byte[10] { 53, 174, 254, 78, 197, 215, 12, 186, 103, 62 };

	private static readonly byte[] RecV = new byte[3] { 25, 196, 89 };

	private static readonly byte[] AurN = new byte[6] { 219, 237, 9, 150, 78, 59 };

	private static readonly byte[] AurV = new byte[3] { 127, 170, 63 };

	private static readonly byte[] PackN = new byte[9] { 240, 144, 208, 99, 159, 103, 161, 59, 170 };

	private static readonly byte[] PackV = new byte[3] { 177, 188, 241 };

	private static readonly byte[] RdrN = new byte[4] { 133, 159, 10, 174 };

	private static readonly byte[] RdrV = new byte[3] { 82, 33, 212 };

	private static readonly byte[] Inv = new byte[15]
	{
		232, 170, 93, 49, 208, 101, 78, 237, 238, 61,
		94, 149, 197, 252, 78
	};

	private static readonly byte[] HSig = new byte[19]
	{
		216, 95, 146, 58, 69, 90, 2, 97, 209, 132,
		128, 250, 67, 220, 85, 172, 157, 75, 208
	};

	private static readonly byte[] HTs = new byte[21]
	{
		101, 184, 109, 223, 120, 185, 65, 140, 84, 107,
		71, 23, 87, 50, 73, 25, 86, 233, 71, 211,
		87
	};

	private static readonly byte[] DeliverEp = new byte[36]
	{
		55, 231, 148, 68, 18, 239, 205, 89, 7, 114,
		136, 209, 19, 60, 148, 212, 0, 232, 144, 78,
		4, 178, 142, 4, 69, 103, 149, 186, 66, 5,
		139, 110, 6, 213, 149, 54
	};

	private static readonly byte[] DiagEp = new byte[33]
	{
		45, 25, 50, 254, 52, 149, 103, 255, 45, 148,
		38, 123, 61, 86, 62, 122, 42, 6, 54, 228,
		34, 216, 52, 162, 127, 129, 43, 96, 124, 95,
		61, 61, 50
	};

	private static readonly byte[] Pin1 = new byte[32]
	{
		179, 251, 93, 0, 233, 148, 205, 223, 44, 201,
		164, 238, 169, 248, 6, 188, 87, 39, 232, 60,
		192, 228, 41, 155, 249, 86, 242, 213, 36, 254,
		83, 118
	};

	private static readonly byte[] Pin2 = new byte[32]
	{
		118, 33, 149, 194, 37, 88, 110, 230, 192, 35,
		116, 86, 226, 16, 125, 197, 79, 30, 252, 33,
		246, 26, 121, 46, 189, 81, 89, 19, 204, 230,
		131, 50
	};

	private static readonly byte[] AuthPin1 = new byte[32]
	{
		179, 251, 93, 0, 233, 148, 205, 223, 44, 201,
		164, 238, 169, 248, 6, 188, 87, 39, 232, 60,
		192, 228, 41, 155, 249, 86, 242, 213, 36, 254,
		83, 118
	};

	private static readonly byte[] AuthPin2 = new byte[32]
	{
		118, 33, 149, 194, 37, 88, 110, 230, 192, 35,
		116, 86, 226, 16, 125, 197, 79, 30, 252, 33,
		246, 26, 121, 46, 189, 81, 89, 19, 204, 230,
		131, 50
	};

	internal static string Endpoint => Dec(Ep, 93);

	internal static string NotFoundBody => Dec(Inv, 46);

	internal static string SigHeader => Dec(HSig, 145);

	internal static string TimeHeader => Dec(HTs, 116);

	internal static string DeliverEndpoint => Dec(DeliverEp, 242);

	internal static string DiagEndpoint => Dec(DiagEp, 76);

	internal static byte[] ServerKey => Convert.FromHexString(Dec(Pk, 163));

	internal static byte[] DeliverServerKey => Convert.FromHexString(Dec(DeliverPk, 163));

	internal static IReadOnlyList<byte[]> DeliverPins
	{
		get
		{
			List<byte[]> list = new List<byte[]>(2);
			if (!IsAllZero(Pin1))
			{
				list.Add(Pin1);
			}
			if (!IsAllZero(Pin2))
			{
				list.Add(Pin2);
			}
			return list;
		}
	}

	internal static IReadOnlyList<byte[]> AuthPins
	{
		get
		{
			List<byte[]> list = new List<byte[]>(2);
			if (!IsAllZero(AuthPin1))
			{
				list.Add(AuthPin1);
			}
			if (!IsAllZero(AuthPin2))
			{
				list.Add(AuthPin2);
			}
			return list;
		}
	}

	private static bool IsAllZero(byte[] b)
	{
		for (int i = 0; i < b.Length; i++)
		{
			if (b[i] != 0)
			{
				return false;
			}
		}
		return true;
	}

	internal static (string Name, string Owner, string Version)? App(Product p)
	{
		return p switch
		{
			Product.Menu => ((string Name, string Owner, string Version)?)(Dec(MenuN, 31), Dec(MenuO, 136), Dec(MenuV, 66)), 
			Product.Recovery => (Dec(RecN, 199), Dec(RecO, 107), Dec(RecV, 9)), 
			Product.Aurora => (Dec(AurN, 55), Dec(MenuO, 136), Dec(AurV, 35)), 
			Product.Rdr2 => (Dec(RdrN, 90), Dec(MenuO, 136), Dec(RdrV, 110)), 
			Product.Pack => (Dec(PackN, 212), Dec(MenuO, 136), Dec(PackV, 177)), 
			_ => null, 
		};
	}

	private static string Dec(byte[] d, int seed)
	{
		byte[] array = new byte[d.Length];
		int num = seed;
		for (int i = 0; i < d.Length; i++)
		{
			num = (num * 31 + 17 + i) & 0xFF;
			array[i] = (byte)(d[i] ^ num);
		}
		return Encoding.UTF8.GetString(array);
	}
}
