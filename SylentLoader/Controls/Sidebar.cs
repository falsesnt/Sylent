using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using SylentLoader.Services;

namespace SylentLoader.Controls;

public sealed class Sidebar : Control
{
	private sealed record Item(string Id, string Tip, Image? Art, Action<Graphics, float, float, Color>? Glyph, bool Bottom);

	public const int SideW = 72;

	private const int IconSz = 46;

	private const int IconGp = 10;

	private const int IconX0 = 13;

	private const int IconY0 = 20;

	private const float BarMs = 170f;

	public const string IdHome = "HOME";

	public const string IdGta = "GTA V";

	public const string IdRdr = "RDR2";

	public const string IdSettings = "SETTINGS";

	public const string IdSignOut = "SIGNOUT";

	private readonly Item[] _items;

	private string _active = "HOME";

	private int _hover = -1;

	private readonly FramePump _pump;

	private readonly Stopwatch _clock = new Stopwatch();

	private float _barY;

	private float _fromY;

	private float _toY;

	private bool _barInit;

	private readonly ToolTip _tip = new ToolTip
	{
		OwnerDraw = true
	};

	private static readonly Font TipFont = new Font("Segoe UI", 8.5f);

	private int BottomCount => _items.Count((Item x) => x.Bottom);

	public event Action<string>? ItemClicked;

	public Sidebar()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Side;
		Dock = DockStyle.Left;
		Width = 72;
		_items = new Item[4]
		{
			new Item("HOME", "Home", null, DrawHome, Bottom: false),
			new Item("GTA V", "GTA V", ImageLoader.GetGTA5Art(), null, Bottom: false),
			new Item("SETTINGS", "Settings", null, DrawGear, Bottom: true),
			new Item("SIGNOUT", "Sign out", null, DrawExit, Bottom: true)
		};
		_pump = new FramePump(this, () =>
		{
			float num = Math.Min(1f, (float)_clock.ElapsedMilliseconds / 170f);
			float num2 = 1f - (float)Math.Pow(1f - num, 3.0);
			_barY = _fromY + (_toY - _fromY) * num2;
			if (num >= 1f)
			{
				_barY = _toY;
				_pump.Stop();
				_clock.Stop();
			}
			Invalidate(new Rectangle(0, 0, 6, Height));
			Update();
		});
		_tip.Popup += (object? _, PopupEventArgs e) =>
		{
			Size size = TextRenderer.MeasureText(_tip.GetToolTip(e.AssociatedControl) ?? "", TipFont);
			e.ToolTipSize = new Size(size.Width + 16, size.Height + 10);
		};
		_tip.Draw += (object? _, DrawToolTipEventArgs e) =>
		{
			using SolidBrush brush = new SolidBrush(Theme.Surface);
			using Pen pen = new Pen(Theme.BorderHi, 1f);
			e.Graphics.FillRectangle(brush, e.Bounds);
			e.Graphics.DrawRectangle(pen, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
			TextRenderer.DrawText(e.Graphics, e.ToolTipText, TipFont, e.Bounds, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		};
	}

	public void SetActive(string id)
	{
		if (!(_active == id) || !_barInit)
		{
			_active = id;
			RetargetBar();
			Invalidate();
		}
	}

	private int IndexInGroup(int i)
	{
		int num = 0;
		for (int j = 0; j < i; j++)
		{
			if (_items[j].Bottom == _items[i].Bottom)
			{
				num++;
			}
		}
		return num;
	}

	private Rectangle IconRect(int i)
	{
		int num = IndexInGroup(i);
		int y = (_items[i].Bottom ? (Height - 20 - 46 - (BottomCount - 1 - num) * 56) : (20 + num * 56));
		return new Rectangle(13, y, 46, 46);
	}

	private int ItemAt(Point p)
	{
		for (int i = 0; i < _items.Length; i++)
		{
			if (IconRect(i).Contains(p))
			{
				return i;
			}
		}
		return -1;
	}

	private void RetargetBar()
	{
		if (_items == null)
		{
			return;
		}
		int num = Array.FindIndex(_items, (Item x) => x.Id == _active);
		if (num < 0 || Height <= 0)
		{
			return;
		}
		float num2 = IconRect(num).Y;
		if (!_barInit)
		{
			_barY = (_fromY = (_toY = num2));
			_barInit = true;
		}
		else if (!(Math.Abs(num2 - _toY) < 0.5f))
		{
			_fromY = _barY;
			_toY = num2;
			if (!IsHandleCreated)
			{
				_barY = num2;
				return;
			}
			_clock.Restart();
			_pump.Start();
		}
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		_barInit = false;
		RetargetBar();
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		RetargetBar();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_pump.Dispose();
			_tip.Dispose();
		}
		base.Dispose(disposing);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = ItemAt(e.Location);
		if (num != _hover)
		{
			_hover = num;
			Cursor = ((num >= 0) ? Cursors.Hand : Cursors.Default);
			if (num >= 0)
			{
				Rectangle rectangle = IconRect(num);
				_tip.Show(_items[num].Tip, this, 68, rectangle.Y + (rectangle.Height - 24) / 2, 2500);
			}
			else
			{
				_tip.Hide(this);
			}
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		if (_hover != -1)
		{
			_hover = -1;
			Cursor = Cursors.Default;
			_tip.Hide(this);
			Invalidate();
		}
		base.OnMouseLeave(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		int num = ItemAt(e.Location);
		if (num >= 0 && e.Button == MouseButtons.Left)
		{
			_tip.Hide(this);
			ItemClicked?.Invoke(_items[num].Id);
		}
		base.OnMouseClick(e);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		graphics.Clear(Theme.Side);
		using (Pen pen = new Pen(Theme.Border, 1f))
		{
			graphics.DrawLine(pen, Width - 1, 0, Width - 1, Height);
		}
		for (int i = 0; i < _items.Length; i++)
		{
			PaintItem(graphics, i);
		}
		if (!_barInit || !_items.Any((Item x) => x.Id == _active))
		{
			return;
		}
		using SolidBrush brush = new SolidBrush(Theme.Accent);
		using GraphicsPath path = Rounded(new RectangleF(0f, _barY + 12f, 3f, 22f), 1.5f);
		graphics.FillPath(brush, path);
	}

	private void PaintItem(Graphics g, int i)
	{
		Item item = _items[i];
		Rectangle rectangle = IconRect(i);
		bool flag = item.Id == _active;
		bool flag2 = i == _hover;
		bool flag3 = item.Id == "SIGNOUT";
		using GraphicsPath graphicsPath = Rounded(new RectangleF((float)rectangle.X + 0.5f, (float)rectangle.Y + 0.5f, (float)rectangle.Width - 1f, (float)rectangle.Height - 1f), 11f);
		if (item.Art != null)
		{
			Image art = item.Art;
			float num = Math.Min(art.Width, art.Height);
			RectangleF srcRect = new RectangleF(((float)art.Width - num) / 2f, ((float)art.Height - num) / 2f, num, num);
			g.SetClip(graphicsPath);
			g.InterpolationMode = InterpolationMode.HighQualityBicubic;
			g.DrawImage(art, rectangle, srcRect, GraphicsUnit.Pixel);
			if (!flag && !flag2)
			{
				using SolidBrush brush = new SolidBrush(Color.FromArgb(70, 0, 0, 0));
				g.FillRectangle(brush, rectangle);
			}
			g.ResetClip();
		}
		else
		{
			using (SolidBrush brush2 = new SolidBrush(flag2 ? Theme.SurfaceHi : Theme.Surface))
			{
				g.FillPath(brush2, graphicsPath);
			}
			Color arg;
			if (flag3)
			{
				arg = (flag2 ? Theme.Error : Theme.Sub);
			}
			else if (flag)
			{
				arg = Theme.Accent;
			}
			else
			{
				arg = (flag2 ? Theme.Text : Theme.Dim);
			}
			item.Glyph(g, (float)rectangle.X + (float)rectangle.Width / 2f, (float)rectangle.Y + (float)rectangle.Height / 2f, arg);
		}
		Color color;
		if (flag)
		{
			color = Theme.Accent;
		}
		else if (flag2)
		{
			color = (flag3 ? Color.FromArgb(150, Theme.Error) : Theme.BorderHi);
		}
		else
		{
			color = Theme.Border;
		}
		using Pen pen = new Pen(color, flag ? 2f : 1f);
		g.DrawPath(pen, graphicsPath);
	}

	private static void DrawHome(Graphics g, float cx, float cy, Color c)
	{
		using Pen pen = new Pen(c, 1.8f)
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round,
			LineJoin = LineJoin.Round
		};
		g.DrawLines(pen, new PointF[3]
		{
			new PointF(cx - 10f, cy - 1f),
			new PointF(cx, cy - 10f),
			new PointF(cx + 10f, cy - 1f)
		});
		g.DrawLines(pen, new PointF[4]
		{
			new PointF(cx - 7f, cy - 3f),
			new PointF(cx - 7f, cy + 9f),
			new PointF(cx + 7f, cy + 9f),
			new PointF(cx + 7f, cy - 3f)
		});
		g.DrawLines(pen, new PointF[4]
		{
			new PointF(cx - 2.6f, cy + 9f),
			new PointF(cx - 2.6f, cy + 3f),
			new PointF(cx + 2.6f, cy + 3f),
			new PointF(cx + 2.6f, cy + 9f)
		});
	}

	private static void DrawGear(Graphics g, float cx, float cy, Color c)
	{
		List<PointF> list = new List<PointF>();
		for (int i = 0; i < 8; i++)
		{
			double num = (double)(i * 2) * Math.PI / 8.0;
			double num2 = Math.PI / 16.0;
			double num3 = 0.10995574287564278;
			(double, float)[] array = new (double, float)[4]
			{
				(num - num2, 8.2f),
				(num - num3, 11f),
				(num + num3, 11f),
				(num + num2, 8.2f)
			};
			for (int j = 0; j < array.Length; j++)
			{
				var (num4, num5) = array[j];
				list.Add(new PointF(cx + (float)(Math.Cos(num4) * (double)num5), cy + (float)(Math.Sin(num4) * (double)num5)));
			}
		}
		using GraphicsPath graphicsPath = new GraphicsPath(FillMode.Alternate);
		graphicsPath.AddPolygon(list.ToArray());
		graphicsPath.AddEllipse(cx - 3.6f, cy - 3.6f, 7.2f, 7.2f);
		using SolidBrush brush = new SolidBrush(c);
		g.FillPath(brush, graphicsPath);
	}

	private static void DrawExit(Graphics g, float cx, float cy, Color c)
	{
		using Pen pen = new Pen(c, 1.8f)
		{
			StartCap = LineCap.Round,
			EndCap = LineCap.Round,
			LineJoin = LineJoin.Round
		};
		g.DrawLines(pen, new PointF[4]
		{
			new PointF(cx - 1f, cy - 9f),
			new PointF(cx - 9f, cy - 9f),
			new PointF(cx - 9f, cy + 9f),
			new PointF(cx - 1f, cy + 9f)
		});
		g.DrawLine(pen, cx - 3f, cy, cx + 9f, cy);
		g.DrawLines(pen, new PointF[3]
		{
			new PointF(cx + 4f, cy - 5f),
			new PointF(cx + 9f, cy),
			new PointF(cx + 4f, cy + 5f)
		});
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
