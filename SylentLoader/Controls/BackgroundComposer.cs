using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SylentLoader.Controls;

public static class BackgroundComposer
{
	public static Bitmap Compose(Image src, Size size)
	{
		size = new Size(Math.Max(1, size.Width), Math.Max(1, size.Height));
		Bitmap bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.SmoothingMode = SmoothingMode.HighQuality;
		graphics.Clear(Color.Black);
		Rectangle r = new Rectangle(Point.Empty, size);
		double num = (double)src.Width / (double)src.Height;
		bool flag = num >= 1.3;
		int alpha = (flag ? 178 : 232);
		int alphaTop = (flag ? 60 : 140);
		int alphaBottom = (flag ? 150 : 200);
		if (flag)
		{
			DrawCover(graphics, src, size, 0.62f);
		}
		else
		{
			using (Bitmap img = Soften(src))
			{
				DrawCover(graphics, img, size, 0.5f);
			}
			Tint(graphics, r, 150);
			int height = size.Height;
			int num2 = (int)Math.Round((double)height * num);
			int num3 = size.Width - num2;
			graphics.DrawImage(src, new Rectangle(num3, 0, num2, height));
			Rectangle rect = new Rectangle(num3 - 1, 0, Math.Min(150, num2), height);
			using LinearGradientBrush brush = new LinearGradientBrush(rect, Color.FromArgb(240, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), LinearGradientMode.Horizontal);
			graphics.FillRectangle(brush, rect);
		}
		Fade(graphics, new Rectangle(0, 0, size.Width, (int)((double)size.Height * 0.28)), alphaTop, 0);
		int num4 = (int)((double)size.Height * (flag ? 0.62 : 0.5));
		Fade(graphics, new Rectangle(0, num4, size.Width, size.Height - num4), 0, alphaBottom);
		Rectangle rect2 = new Rectangle(0, 0, (int)((double)size.Width * (flag ? 0.6 : 0.66)), size.Height);
		if (rect2.Width > 0)
		{
			using LinearGradientBrush brush2 = new LinearGradientBrush(rect2, Color.FromArgb(alpha, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), LinearGradientMode.Horizontal);
			graphics.FillRectangle(brush2, rect2);
		}
		return bitmap;
	}

	private static void DrawCover(Graphics g, Image img, Size size, float anchorX)
	{
		float num = Math.Max((float)size.Width / (float)img.Width, (float)size.Height / (float)img.Height);
		float num2 = (float)img.Width * num;
		float num3 = (float)img.Height * num;
		float x = (0f - (num2 - (float)size.Width)) * anchorX;
		float y = (0f - (num3 - (float)size.Height)) * 0.5f;
		g.DrawImage(img, x, y, num2, num3);
	}

	private static void Tint(Graphics g, Rectangle r, int alpha)
	{
		using SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
		g.FillRectangle(brush, r);
	}

	private static void Fade(Graphics g, Rectangle r, int alphaTop, int alphaBottom)
	{
		if (r.Height <= 0 || r.Width <= 0)
		{
			return;
		}
		using LinearGradientBrush brush = new LinearGradientBrush(r, Color.FromArgb(alphaTop, 0, 0, 0), Color.FromArgb(alphaBottom, 0, 0, 0), LinearGradientMode.Vertical);
		g.FillRectangle(brush, r);
	}

	private static Bitmap Soften(Image src)
	{
		int width = Math.Max(8, src.Width / 22);
		int height = Math.Max(8, src.Height / 22);
		Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
		using Graphics graphics = Graphics.FromImage(bitmap);
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.DrawImage(src, new Rectangle(0, 0, width, height));
		return bitmap;
	}
}
