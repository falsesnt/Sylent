using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SylentLoader.Controls;

public sealed class ModPickerForm : Form
{
	private sealed class OptionButton : Control
	{
		private readonly string _name;

		private readonly string _sub;

		private readonly Color _accent;

		private bool _hover;

		public OptionButton(string name, string sub, Color accent)
		{
			_name = name;
			_sub = sub;
			_accent = accent;
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
			Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			using GraphicsPath path = RoundedPath(new RectangleF(0.5f, 0.5f, (float)Width - 1f, (float)Height - 1f), 8);
			using (SolidBrush brush = new SolidBrush(_hover ? Theme.SurfaceHi : CardBg))
			{
				graphics.FillPath(brush, path);
			}
			using (Pen pen = new Pen(_hover ? _accent : Border, _hover ? 1.5f : 1f))
			{
				graphics.DrawPath(pen, path);
			}
			using (SolidBrush brush2 = new SolidBrush(_accent))
			{
				graphics.FillEllipse(brush2, 16f, (float)(Height - 8) / 2f, 8f, 8f);
			}
			using Font font = new Font("Segoe UI", 11f, FontStyle.Bold);
			using Font font2 = new Font("Segoe UI", 8.5f);
			float height = graphics.MeasureString(_name, font).Height;
			float height2 = graphics.MeasureString(_sub, font2).Height;
			float num = ((float)Height - (height + height2 - 2f)) / 2f;
			using (SolidBrush brush3 = new SolidBrush(Theme.Text))
			{
				graphics.DrawString(_name, font, brush3, new PointF(37f, num));
			}
			using (SolidBrush brush4 = new SolidBrush(_hover ? Theme.Text : Theme.Dim))
			{
				graphics.DrawString(_sub, font2, brush4, new PointF(38f, num + height - 2f));
			}
			if (!_hover)
			{
				return;
			}
			using Font font3 = new Font("Segoe UI", 11f, FontStyle.Bold);
			using SolidBrush brush5 = new SolidBrush(_accent);
			float height3 = graphics.MeasureString("→", font3).Height;
			graphics.DrawString("→", font3, brush5, new PointF(Width - 32, ((float)Height - height3) / 2f));
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

	private static readonly Color Bg = Theme.Surface;

	private static readonly Color CardBg = Theme.Black;

	private static readonly Color Border = Theme.Border;

	private static readonly Color Accent = Theme.Accent;

	private static readonly Color SubCol = Theme.Sub;

	private static readonly Color TextCol = Theme.Text;

	private static readonly (string Name, string Sub, Color Accent)[] Options = new (string, string, Color)[1]
	{
		("Sylent Menu", "Shitty paste", Color.FromArgb(65, 190, 140)),
	};

	public string? SelectedMod { get; private set; }

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		int value = 2;
		DwmSetWindowAttribute(Handle, 33, ref value, 4);
	}

	public ModPickerForm()
	{
		FormBorderStyle = FormBorderStyle.None;
		StartPosition = FormStartPosition.CenterParent;
		BackColor = Bg;
		Size = new Size(340, 344);
		DoubleBuffered = true;
		int num = 290;
		int num2 = 62;
		int num3 = 10;
		int num4 = 68;
		int x = (Width - num) / 2;
		for (int i = 0; i < Options.Length; i++)
		{
			(string Name, string Sub, Color Accent) tuple = Options[i];
			string item = tuple.Name;
			string item2 = tuple.Sub;
			Color item3 = tuple.Accent;
			string captured = item;
			int y = num4 + i * (num2 + num3);
			OptionButton optionButton = new OptionButton(item, item2, item3)
			{
				Location = new Point(x, y),
				Size = new Size(num, num2),
				Cursor = Cursors.Hand
			};
			optionButton.Click += (object? _, EventArgs _) =>
			{
				SelectedMod = captured;
				DialogResult = DialogResult.OK;
				Close();
			};
			Controls.Add(optionButton);
		}
		RoundedButton roundedButton = new RoundedButton
		{
			Text = "Cancel",
			Size = new Size(96, 30),
			BackColor = Theme.Black,
			HoverBackColor = Theme.SurfaceHi,
			ForeColor = Theme.Dim,
			BorderColor = Theme.BorderHi,
			Font = new Font("Segoe UI", 8.5f),
			CornerRadius = 8,
			Cursor = Cursors.Hand
		};
		roundedButton.Location = new Point((Width - roundedButton.Width) / 2, num4 + Options.Length * (num2 + num3) - num3 + 18);
		roundedButton.Click += (object? _, EventArgs _) =>
		{
			DialogResult = DialogResult.Cancel;
			Close();
		};
		Controls.Add(roundedButton);
		KeyPreview = true;
		KeyDown += (object? _, KeyEventArgs e) =>
		{
			if (e.KeyCode == Keys.Escape)
			{
				DialogResult = DialogResult.Cancel;
				Close();
			}
		};
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		using Pen pen = new Pen(Border, 1f);
		graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
		using Font font = new Font("Segoe UI", 13f, FontStyle.Bold);
		using SolidBrush brush = new SolidBrush(TextCol);
		graphics.DrawString("Select Mod", font, brush, new PointF(20f, 18f));
		using Font font2 = new Font("Segoe UI", 8.5f);
		using SolidBrush brush2 = new SolidBrush(Theme.Dim);
		graphics.DrawString("Choose which mod to load into GTA V", font2, brush2, new PointF(21f, 44f));
	}
}
