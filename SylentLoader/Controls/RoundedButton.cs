using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SylentLoader.Controls;

public sealed class RoundedButton : Button
{
	private bool _hover;

	private bool _down;

	public int CornerRadius { get; set; } = 8;

	public Color BorderColor { get; set; } = Theme.BorderHi;

	public Color HoverBackColor { get; set; } = Color.Empty;

	public Color GradientEnd { get; set; } = Color.Empty;

	public Color HoverGradientEnd { get; set; } = Color.Empty;

	public ButtonIcon Icon { get; set; }

	public Color CornerFill { get; set; } = Color.Empty;

	public bool TransparentCorners { get; set; }

	public RoundedButton()
	{
		FlatStyle = FlatStyle.Flat;
		FlatAppearance.BorderSize = 0;
		SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		_hover = true;
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hover = false;
		_down = false;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		_down = true;
		Invalidate();
		base.OnMouseDown(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		_down = false;
		Invalidate();
		base.OnMouseUp(e);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		if (TransparentCorners && Parent != null)
		{
			GraphicsState gstate = graphics.Save();
			graphics.TranslateTransform(-Left, -Top);
			PaintEventArgs e2 = new PaintEventArgs(graphics, new Rectangle(Left, Top, Width, Height));
			InvokePaintBackground(Parent, e2);
			InvokePaint(Parent, e2);
			graphics.Restore(gstate);
		}
		else
		{
			Color color;
			if (CornerFill != Color.Empty)
			{
				color = CornerFill;
			}
			else
			{
				color = ((Parent != null && Parent.BackColor.A == byte.MaxValue) ? Parent.BackColor : Color.Black);
			}
			using SolidBrush brush = new SolidBrush(color);
			graphics.FillRectangle(brush, ClientRectangle);
		}
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		using GraphicsPath path = RoundedPath(new RectangleF(0.5f, 0.5f, (float)Width - 1f, (float)Height - 1f), CornerRadius);
		bool flag = _hover && Enabled;
		Color color2 = ((flag && HoverBackColor != Color.Empty) ? HoverBackColor : BackColor);
		Color color3;
		if (GradientEnd == Color.Empty)
		{
			color3 = color2;
		}
		else
		{
			color3 = ((flag && HoverGradientEnd != Color.Empty) ? HoverGradientEnd : GradientEnd);
		}
		if (_down && Enabled)
		{
			color2 = Darken(color2);
			color3 = Darken(color3);
		}
		if (!Enabled)
		{
			color2 = Color.FromArgb(60, color2);
			color3 = Color.FromArgb(60, color3);
		}
		if (color2 == color3)
		{
			using SolidBrush brush2 = new SolidBrush(color2);
			graphics.FillPath(brush2, path);
		}
		else
		{
			using LinearGradientBrush brush3 = new LinearGradientBrush(new RectangleF(0f, 0f, Width, Height), color2, color3, LinearGradientMode.Vertical);
			graphics.FillPath(brush3, path);
		}
		using (Pen pen = new Pen(Enabled ? BorderColor : Color.FromArgb(60, BorderColor), 1f))
		{
			graphics.DrawPath(pen, path);
		}
		Color color4 = (Enabled ? ForeColor : Color.FromArgb(100, ForeColor));
		StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic)
		{
			Alignment = StringAlignment.Near,
			LineAlignment = StringAlignment.Center,
			FormatFlags = StringFormatFlags.NoWrap
		};
		float num = (string.IsNullOrEmpty(Text) ? 0f : graphics.MeasureString(Text, Font, PointF.Empty, stringFormat).Width);
		float num2 = Icon switch
		{
			ButtonIcon.Play => 10f, 
			ButtonIcon.Swap => 12f, 
			_ => 0f, 
		};
		float num3 = ((num2 > 0f && num > 0f) ? 8f : 0f);
		float num4 = ((float)Width - (num2 + num3 + num)) / 2f;
		float num5 = (float)Height / 2f;
		using SolidBrush brush4 = new SolidBrush(color4);
		if (Icon == ButtonIcon.Play)
		{
			PointF[] points = new PointF[3]
			{
				new PointF(num4, num5 - 5.5f),
				new PointF(num4, num5 + 5.5f),
				new PointF(num4 + num2, num5)
			};
			graphics.FillPolygon(brush4, points);
		}
		else if (Icon == ButtonIcon.Swap)
		{
			using Pen pen2 = new Pen(color4, 1.4f)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round,
				LineJoin = LineJoin.Round
			};
			float num6 = num5 - 3.2f;
			float num7 = num5 + 3.2f;
			graphics.DrawLine(pen2, num4, num6, num4 + num2, num6);
			graphics.DrawLines(pen2, new PointF[3]
			{
				new PointF(num4 + num2 - 3f, num6 - 2.6f),
				new PointF(num4 + num2, num6),
				new PointF(num4 + num2 - 3f, num6 + 2.6f)
			});
			graphics.DrawLine(pen2, num4, num7, num4 + num2, num7);
			graphics.DrawLines(pen2, new PointF[3]
			{
				new PointF(num4 + 3f, num7 - 2.6f),
				new PointF(num4, num7),
				new PointF(num4 + 3f, num7 + 2.6f)
			});
		}
		if (num > 0f)
		{
			graphics.DrawString(Text, Font, brush4, new RectangleF(num4 + num2 + num3, 0f, num + 2f, Height), stringFormat);
		}
	}

	private static Color Darken(Color c)
	{
		return Color.FromArgb(c.A, (int)((double)(int)c.R * 0.86), (int)((double)(int)c.G * 0.86), (int)((double)(int)c.B * 0.86));
	}

	private static GraphicsPath RoundedPath(RectangleF r, int radius)
	{
		float num = (float)radius * 2f;
		GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddArc(r.X, r.Y, num, num, 180f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Y, num, num, 270f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
		graphicsPath.AddArc(r.X, r.Bottom - num, num, num, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}
}
