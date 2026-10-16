using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;

namespace SylentLoader.Controls;

internal static class BlurredText
{
	public static string Decoy(int seed, int length = 40)
	{
		Random random = new Random(seed);
		StringBuilder stringBuilder = new StringBuilder(length);
		int num = 0;
		while (stringBuilder.Length < length)
		{
			if (num % 5 == 4)
			{
				stringBuilder.Append('-');
			}
			else
			{
				stringBuilder.Append("ABCDEFGHJKLMNPQRSTUVWXYZ23456789"[random.Next("ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Length)]);
			}
			num++;
		}
		return stringBuilder.ToString();
	}

	public static Bitmap Render(string text, Font font, Color tint, int w, int h, int leftPad = 10, int radiusX = 3, int radiusY = 2, int fadeEdge = 16)
	{
		float[] array = new float[w * h];
		using (Bitmap bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb))
		{
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.Clear(Color.Black);
				graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
				using StringFormat format = new StringFormat(StringFormat.GenericTypographic)
				{
					LineAlignment = StringAlignment.Center,
					FormatFlags = (StringFormatFlags.NoWrap | StringFormatFlags.NoClip)
				};
				graphics.DrawString(text, font, Brushes.White, new RectangleF(leftPad, 0f, w * 4, h), format);
			}
			BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			try
			{
				byte[] array2 = new byte[bitmapData.Stride];
				for (int i = 0; i < h; i++)
				{
					Marshal.Copy(bitmapData.Scan0 + i * bitmapData.Stride, array2, 0, bitmapData.Stride);
					for (int j = 0; j < w; j++)
					{
						array[i * w + j] = (float)(int)array2[j * 4 + 1] / 255f;
					}
				}
			}
			finally
			{
				bitmap.UnlockBits(bitmapData);
			}
		}
		float[] array3 = new float[w * h];
		for (int k = 0; k < 3; k++)
		{
			Box(array, array3, w, h, radiusX, horizontal: true);
			Box(array3, array, w, h, radiusY, horizontal: false);
		}
		float num = 0f;
		float[] array4 = array;
		foreach (float num2 in array4)
		{
			if (num2 > num)
			{
				num = num2;
			}
		}
		float num3 = ((num > 0f) ? (0.92f / num) : 0f);
		Bitmap bitmap2 = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
		BitmapData bitmapData2 = bitmap2.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
		try
		{
			byte[] array5 = new byte[bitmapData2.Stride];
			for (int m = 0; m < h; m++)
			{
				for (int n = 0; n < w; n++)
				{
					float num4 = ((fadeEdge <= 0) ? 1f : Math.Min(1f, Math.Min(((float)n + 1f) / (float)fadeEdge, (float)(w - n) / (float)fadeEdge)));
					float num5 = Math.Clamp(array[m * w + n] * num3 * num4, 0f, 1f);
					array5[n * 4] = (byte)Math.Round((float)(int)tint.B * num5);
					array5[n * 4 + 1] = (byte)Math.Round((float)(int)tint.G * num5);
					array5[n * 4 + 2] = (byte)Math.Round((float)(int)tint.R * num5);
					array5[n * 4 + 3] = (byte)Math.Round(255f * num5);
				}
				Marshal.Copy(array5, 0, bitmapData2.Scan0 + m * bitmapData2.Stride, bitmapData2.Stride);
			}
		}
		finally
		{
			bitmap2.UnlockBits(bitmapData2);
		}
		return bitmap2;
	}

	private static void Box(float[] src, float[] dst, int w, int h, int r, bool horizontal)
	{
		int num = (horizontal ? w : h);
		int num2 = (horizontal ? h : w);
		int num3 = (horizontal ? 1 : w);
		float num4 = 1f / (float)(2 * r + 1);
		for (int i = 0; i < num2; i++)
		{
			int num5 = (horizontal ? (i * w) : i);
			float num6 = 0f;
			for (int j = 0; j <= r && j < num; j++)
			{
				num6 += src[num5 + j * num3];
			}
			for (int k = 0; k < num; k++)
			{
				dst[num5 + k * num3] = num6 * num4;
				int num7 = k + r + 1;
				int num8 = k - r;
				if (num7 < num)
				{
					num6 += src[num5 + num7 * num3];
				}
				if (num8 >= 0)
				{
					num6 -= src[num5 + num8 * num3];
				}
			}
		}
	}
}
