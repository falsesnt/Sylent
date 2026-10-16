using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using SylentLoader.Services.Auth;

namespace SylentLoader.Controls;

public sealed class HomeView : Control
{
	private sealed record Card(Product Product, string Title, string Desc, Color Accent);

	private static readonly Card[] Cards = new Card[1]
	{
		new Card(Product.Menu, "Soylent Menu", "shitty paste for gta numero cinco", Color.FromArgb(65, 190, 140))
	};

	private const int PadX = 40;

	private const int Gap = 14;

	private const int CardH = 92;

	private const int CardsTop = 178;

	private const float TotalSeconds = 1f;

	private readonly FramePump _pump;

	private readonly Stopwatch _clock = new Stopwatch();

	private int _hover = -1;

	private List<Card> _visible = new List<Card>();

	private float Elapsed
	{
		get
		{
			if (!_clock.IsRunning)
			{
				return 99f;
			}
			return (float)_clock.Elapsed.TotalSeconds;
		}
	}

	public event Action<Product>? ProductRequested;

	public HomeView()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Black;
		DoubleBuffered = true;
		RefreshState();
		_pump = new FramePump(this, () =>
		{
			Invalidate();
			Update();
			if (_clock.Elapsed.TotalSeconds > 1.0)
			{
				_pump.Stop();
				_clock.Stop();
				Invalidate();
			}
		});
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_pump.Dispose();
		}
		base.Dispose(disposing);
	}

	public void Replay()
	{
		_clock.Restart();
		_pump.Start();
		Invalidate();
	}

	public void RefreshState()
	{
		_visible = Cards.Where((Card c) => Entitlements.IsUnlocked(c.Product) || Entitlements.HasSavedKey(c.Product)).ToList();
		_hover = -1;
		Invalidate();
	}

	private int CardAt(Point p)
	{
		for (int i = 0; i < _visible.Count; i++)
		{
			if (CardRect(i).Contains(p))
			{
				return i;
			}
		}
		return -1;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = CardAt(e.Location);
		if (num != _hover)
		{
			_hover = num;
			Cursor = ((num >= 0) ? Cursors.Hand : Cursors.Default);
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
			Invalidate();
		}
		base.OnMouseLeave(e);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		int num = CardAt(e.Location);
		if (num >= 0 && e.Button == MouseButtons.Left)
		{
			ProductRequested?.Invoke(_visible[num].Product);
		}
		base.OnMouseClick(e);
	}

	private Rectangle CardRect(int i)
	{
		int num = (Width - 80 - 14) / 2;
		return new Rectangle(40 + i % 2 * (num + 14), 178 + i / 2 * 106, num, 92);
	}

	private float Progress(float delay, float len = 0.45f)
	{
		float num = Math.Clamp((Elapsed - delay) / len, 0f, 1f);
		return 1f - (float)Math.Pow(1f - num, 3.0);
	}

	private static Color Fade(Color c, float p)
	{
		return Color.FromArgb((int)((float)(int)c.A * p), c);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		graphics.Clear(Theme.Black);
		if (Width < 200)
		{
			return;
		}
		PaintGlow(graphics);
		PaintHeader(graphics);
		if (_visible.Count == 0)
		{
			PaintNoLicences(graphics);
			return;
		}
		for (int i = 0; i < _visible.Count; i++)
		{
			PaintCard(graphics, i);
		}
	}

	private void PaintNoLicences(Graphics g)
	{
		float num = Progress(0.24f);
		Rectangle rectangle = new Rectangle(40, 178, Width - 80, 100);
		rectangle.Offset(0, (int)((1f - num) * 16f));
		using GraphicsPath path = Rounded(new RectangleF((float)rectangle.X + 0.5f, (float)rectangle.Y + 0.5f, (float)rectangle.Width - 1f, (float)rectangle.Height - 1f), 12f);
		using (SolidBrush brush = new SolidBrush(Fade(Theme.Surface, num)))
		{
			g.FillPath(brush, path);
		}
		using (Pen pen = new Pen(Fade(Theme.BorderHi, num), 1f)
		{
			DashStyle = DashStyle.Dash
		})
		{
			g.DrawPath(pen, path);
		}
		using (SolidBrush brush2 = new SolidBrush(Fade(Theme.Sub, num)))
		{
			using GraphicsPath path2 = Rounded(new RectangleF(rectangle.X + 14, rectangle.Y + 22, 3f, rectangle.Height - 44), 1.5f);
			g.FillPath(brush2, path2);
		}
		using (Font font = new Font("Segoe UI", 11.5f, FontStyle.Bold))
		{
			using SolidBrush brush3 = new SolidBrush(Fade(Theme.Text, num));
			g.DrawString("No licenses on this PC yet", font, brush3, new PointF(rectangle.X + 30, rectangle.Y + 18));
		}
		using Font font2 = new Font("Segoe UI", 9f);
		using SolidBrush brush4 = new SolidBrush(Fade(Theme.Dim, num));
		g.DrawString("Choose GTA V in the sidebar and enter your key. Once it's saved, your products show up here for one-click access.", font2, brush4, new RectangleF(rectangle.X + 31, rectangle.Y + 44, rectangle.Width - 62, rectangle.Height - 50));
	}

	private void PaintGlow(Graphics g)
	{
		float num = Progress(0f, 0.9f);
		using GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddEllipse(-120, -150, 560, 340);
		PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
		pathGradientBrush.CenterColor = Color.FromArgb((int)(40f * num), Theme.Accent);
		pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, Theme.Accent) };
		using PathGradientBrush brush = pathGradientBrush;
		g.FillPath(brush, graphicsPath);
	}

	private void PaintHeader(Graphics g)
	{
		float num = Progress(0f);
		using (Font f = new Font("Segoe UI", 8.5f, FontStyle.Bold))
		{
			using SolidBrush b = new SolidBrush(Fade(Theme.Accent, num));
			DrawSpacedLeft(g, GreetingText().ToUpperInvariant(), f, b, 41f, 40f + (1f - num) * 12f, 3.2f);
		}
		float num2 = Progress(0.08f);
		using (Font font = new Font("Segoe UI", 28f, FontStyle.Bold))
		{
			using SolidBrush brush = new SolidBrush(Fade(Theme.Text, num2));
			g.DrawString("Welcome to Sylent", font, brush, new PointF(38f, 52f + (1f - num2) * 12f));
		}
		float num3 = Progress(0.16f);
		string s = ((_visible.Count > 0) ? "Jump straight into one of your products below, or pick a game from the sidebar." : "Pick a game from the sidebar and enter your key to get started.");
		using (Font font2 = new Font("Segoe UI", 10f))
		{
			using SolidBrush brush2 = new SolidBrush(Fade(Theme.Dim, num3));
			g.DrawString(s, font2, brush2, new PointF(40f, 118f + (1f - num3) * 12f));
		}
		float num4 = Progress(0.12f);
		using SolidBrush brush3 = new SolidBrush(Fade(Theme.Accent, num4));
		g.FillRectangle(brush3, 40f, 150f, 36f * num4, 2f);
	}

	private void PaintCard(Graphics g, int i)
	{
		Card card = _visible[i];
		float num = Progress(0.24f + (float)i * 0.08f);
		Rectangle rectangle = CardRect(i);
		rectangle.Offset(0, (int)((1f - num) * 16f));
		bool flag = i == _hover && num >= 1f;
		using (GraphicsPath path = Rounded(new RectangleF((float)rectangle.X + 0.5f, (float)rectangle.Y + 0.5f, (float)rectangle.Width - 1f, (float)rectangle.Height - 1f), 12f))
		{
			using (SolidBrush brush = new SolidBrush(Fade(flag ? Theme.SurfaceHi : Theme.Surface, num)))
			{
				g.FillPath(brush, path);
			}
			using Pen pen = new Pen(Fade(flag ? Color.FromArgb(170, card.Accent) : Theme.Border, num), 1f);
			g.DrawPath(pen, path);
		}
		using (SolidBrush brush2 = new SolidBrush(Fade(card.Accent, num)))
		{
			using GraphicsPath path2 = Rounded(new RectangleF(rectangle.X + 14, rectangle.Y + 20, 3f, rectangle.Height - 40), 1.5f);
			g.FillPath(brush2, path2);
		}
		using (Font font = new Font("Segoe UI", 11.5f, FontStyle.Bold))
		{
			using SolidBrush brush3 = new SolidBrush(Fade(Theme.Text, num));
			g.DrawString(card.Title, font, brush3, new PointF(rectangle.X + 30, rectangle.Y + 15));
		}
		using (Font font2 = new Font("Segoe UI", 8.5f))
		{
			using SolidBrush brush4 = new SolidBrush(Fade(flag ? Theme.Text : Theme.Dim, num));
			g.DrawString(card.Desc, font2, brush4, new PointF(rectangle.X + 31, rectangle.Y + 38));
		}
		var (s, c) = Standing(card.Product);
		using (Font font3 = new Font("Segoe UI", 8f, FontStyle.Bold))
		{
			using SolidBrush brush5 = new SolidBrush(Fade(c, num));
			g.DrawString(s, font3, brush5, new PointF(rectangle.X + 31, rectangle.Bottom - 28));
		}
		if (!flag)
		{
			return;
		}
		using Font font4 = new Font("Segoe UI", 12f, FontStyle.Bold);
		using SolidBrush brush6 = new SolidBrush(card.Accent);
		g.DrawString("→", font4, brush6, new PointF(rectangle.Right - 34, (float)rectangle.Y + (float)(rectangle.Height - 22) / 2f));
	}

	private static string GreetingText()
	{
		switch (DateTime.Now.Hour)
		{
		case 5:
		case 6:
		case 7:
		case 8:
		case 9:
		case 10:
		case 11:
			return "Good morning";
		case 12:
		case 13:
		case 14:
		case 15:
		case 16:
		case 17:
			return "Good afternoon";
		case 18:
		case 19:
		case 20:
		case 21:
			return "Good evening";
		default:
			return "Good night";
		}
	}

	private static (string Text, Color Color) Standing(Product p)
	{
		if (Entitlements.IsUnlocked(p))
		{
			DateTimeOffset? dateTimeOffset = Entitlements.ExpiresAt(p);
			string text = (Entitlements.ViaPack(p) ? "Full Access" : "Licensed");
			string text2;
			if (dateTimeOffset.HasValue)
			{
				text2 = (Entitlements.IsLifetime(dateTimeOffset.Value) ? " · lifetime" : $" · until {dateTimeOffset.Value:yyyy-MM-dd}");
			}
			else
			{
				text2 = "";
			}
			return (Text: "●  " + text + text2, Color: Theme.Success);
		}
		if (Entitlements.HasSavedKey(p))
		{
			return (Text: "●  Key saved · opens instantly", Color: Theme.Accent);
		}
		return (Text: "○  Locked · enter a key to unlock", Color: Theme.Sub);
	}

	private static void DrawSpacedLeft(Graphics g, string text, Font f, Brush b, float x, float y, float spacing)
	{
		StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic)
		{
			FormatFlags = StringFormatFlags.MeasureTrailingSpaces
		};
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			float num = ((c == ' ') ? (f.Size * 0.5f) : g.MeasureString(c.ToString(), f, PointF.Empty, stringFormat).Width);
			if (c != ' ')
			{
				g.DrawString(c.ToString(), f, b, new PointF(x, y), stringFormat);
			}
			x += num + spacing;
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
