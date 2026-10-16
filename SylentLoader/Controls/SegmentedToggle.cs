using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SylentLoader.Controls;

public sealed class SegmentedToggle : Control
{
	private static readonly Color Track = Color.FromArgb(215, 0, 0, 0);

	private static readonly Color TrackBorder = Theme.BorderHi;

	private static readonly Color SegTop = Theme.AccentTop;

	private static readonly Color SegBottom = Theme.AccentBot;

	private static readonly Color Idle = Theme.Sub;

	private static readonly Color IdleHover = Theme.Text;

	private string[] _items = Array.Empty<string>();

	private int _selected;

	private int _hover = -1;

	public HashSet<int> Disabled { get; } = new HashSet<int>();

	public string[] Items
	{
		get
		{
			return _items;
		}
		set
		{
			_items = value;
			Invalidate();
		}
	}

	public int SelectedIndex
	{
		get
		{
			return _selected;
		}
		set
		{
			if (_selected != value)
			{
				_selected = value;
				Invalidate();
			}
		}
	}

	public event Action<int>? SelectedChanged;

	public SegmentedToggle()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
	}

	private int SegmentAt(int x)
	{
		if (_items.Length != 0)
		{
			return Math.Clamp(x * _items.Length / Math.Max(1, Width), 0, _items.Length - 1);
		}
		return -1;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = SegmentAt(e.X);
		if (num != _hover)
		{
			_hover = num;
			Cursor = (Disabled.Contains(num) ? Cursors.Default : Cursors.Hand);
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hover = -1;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		int num = SegmentAt(e.X);
		if (num >= 0 && !Disabled.Contains(num) && num != _selected)
		{
			_selected = num;
			Invalidate();
			SelectedChanged?.Invoke(num);
		}
		base.OnMouseClick(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		if (_items.Length == 0)
		{
			return;
		}
		using (GraphicsPath path = Rounded(new RectangleF(0.5f, 0.5f, (float)Width - 1f, (float)Height - 1f), (float)Height / 2f))
		{
			using (SolidBrush brush = new SolidBrush(Track))
			{
				graphics.FillPath(brush, path);
			}
			using Pen pen = new Pen(TrackBorder, 1f);
			graphics.DrawPath(pen, path);
		}
		float num = ((float)Width - 6f) / (float)_items.Length;
		StringFormat format = new StringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		};
		for (int i = 0; i < _items.Length; i++)
		{
			RectangleF rectangleF = new RectangleF(3f + (float)i * num, 3f, num, (float)Height - 6f);
			bool flag = i == _selected;
			bool flag2 = Disabled.Contains(i);
			if (flag)
			{
				using GraphicsPath path2 = Rounded(rectangleF, rectangleF.Height / 2f);
				using LinearGradientBrush brush2 = new LinearGradientBrush(rectangleF, SegTop, SegBottom, LinearGradientMode.Vertical);
				graphics.FillPath(brush2, path2);
			}
			Color color;
			if (flag)
			{
				color = Color.White;
			}
			else if (flag2)
			{
				color = Color.FromArgb(70, Idle);
			}
			else
			{
				color = ((i == _hover) ? IdleHover : Idle);
			}
			using SolidBrush brush3 = new SolidBrush(color);
			graphics.DrawString(_items[i], Font, brush3, rectangleF, format);
		}
	}

	private static GraphicsPath Rounded(RectangleF r, float radius)
	{
		float num = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
		GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddArc(r.X, r.Y, num, num, 180f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Y, num, num, 270f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
		graphicsPath.AddArc(r.X, r.Bottom - num, num, num, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}
}
