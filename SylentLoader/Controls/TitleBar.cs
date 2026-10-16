using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SylentLoader.Controls;

public sealed class TitleBar : Panel
{
	private sealed class WindowButton : Button
	{
		protected override bool ShowFocusCues => false;

		public WindowButton()
		{
			SetStyle(ControlStyles.Selectable, value: false);
			TabStop = false;
		}
	}

	private static readonly Color CloseHov = Color.FromArgb(200, 40, 55);

	private static readonly Color BtnHov = Theme.SurfaceHi;

	private const string Idle = "SYLENT | CRACKED WITH LOVE BY SnT 💞";

	private static readonly Font SlashFont = new Font("Segoe UI", 10f);

	private static readonly Font SectionFont = new Font("Segoe UI", 10f, FontStyle.Bold);

	private static readonly StringFormat Tight = StringFormat.GenericTypographic;

	private readonly Label _titleLbl;

	private readonly Button _closeBtn;

	private readonly Button _minBtn;

	private Image? _heroImage;

	private string? _section;

	private float _slashW = -1f;

	private Point _dragOrigin;

	private bool _dragging;

	public TitleBar(Form owner)
	{
		TitleBar titleBar = this;
		Dock = DockStyle.Top;
		Height = 42;
		BackColor = Theme.TitleBar;
		_titleLbl = new Label
		{
			Text = "SYLENT | CRACKED WITH LOVE BY SnT 💞",
			Font = new Font("Segoe UI", 10f, FontStyle.Bold),
			ForeColor = Theme.Text,
			AutoSize = true,
			Location = new Point(14, 12),
			BackColor = Color.Transparent,
			Cursor = Cursors.SizeAll
		};
		_minBtn = WinBtn("—");
		_minBtn.Click += (object? _, EventArgs _) =>
		{
			owner.WindowState = FormWindowState.Minimized;
		};
		_closeBtn = WinBtn("✕");
		_closeBtn.Click += (object? _, EventArgs _) =>
		{
			Application.Exit();
		};
		_closeBtn.MouseEnter += (object? _, EventArgs _) =>
		{
			titleBar._closeBtn.BackColor = CloseHov;
		};
		_closeBtn.MouseLeave += (object? _, EventArgs _) =>
		{
			titleBar._closeBtn.BackColor = Color.Transparent;
		};
		Controls.AddRange(new Control[3] { _titleLbl, _minBtn, _closeBtn });
		Control[] array = new Control[2] { this, _titleLbl };
		foreach (Control obj in array)
		{
			obj.MouseDown += StartDrag;
			obj.MouseMove += DoDrag;
			obj.MouseUp += StopDrag;
			void DoDrag(object? s, MouseEventArgs e)
			{
				if (titleBar._dragging)
				{
					owner.Location = new Point(owner.Left + e.X - titleBar._dragOrigin.X, owner.Top + e.Y - titleBar._dragOrigin.Y);
				}
			}
			void StartDrag(object? s, MouseEventArgs e)
			{
				if (e.Button == MouseButtons.Left)
				{
					titleBar._dragging = true;
					titleBar._dragOrigin = e.Location;
				}
			}
			void StopDrag(object? s, MouseEventArgs e)
			{
				titleBar._dragging = false;
			}
		}
		Paint += OnPaint;
		Resize += (object? _, EventArgs _) =>
		{
			titleBar.PositionBtns();
		};
		PositionBtns();
	}

	public void SetHeroImage(Image? img)
	{
		_heroImage = img;
		Invalidate();
	}

	public void SetSection(string? section)
	{
		_section = (string.IsNullOrWhiteSpace(section) ? null : section.Trim());
		_titleLbl.Text = "SYLENT | CRACKED WITH LOVE BY SnT 💞";
		_slashW = -1f;
		Invalidate();
	}

	private void PositionBtns()
	{
		_closeBtn.Location = new Point(Width - 42, 0);
		_minBtn.Location = new Point(Width - 84, 0);
	}

	private void OnPaint(object? sender, PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		if (_heroImage != null)
		{
			float num = (float)Width / (float)_heroImage.Width;
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			graphics.DrawImage(_heroImage, 0, 0, Width, (int)((float)_heroImage.Height * num));
			using LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, Width, Height), Color.FromArgb(160, 0, 0, 0), Color.FromArgb(80, 0, 0, 0), LinearGradientMode.Vertical);
			graphics.FillRectangle(brush, ClientRectangle);
		}
		else
		{
			graphics.Clear(Theme.TitleBar);
			using Pen pen = new Pen(Theme.Border, 1f);
			graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
		}
		if (_section == null)
		{
			return;
		}
		if (_slashW < 0f)
		{
			_slashW = graphics.MeasureString("/", SlashFont, PointF.Empty, Tight).Width;
		}
		float num2 = _titleLbl.Right + 3;
		using SolidBrush brush2 = new SolidBrush(Theme.Faint);
		using SolidBrush brush3 = new SolidBrush(Theme.Accent);
		graphics.DrawString("/", SlashFont, brush2, new PointF(num2, 12f), Tight);
		graphics.DrawString(_section, SectionFont, brush3, new PointF(num2 + _slashW + 8f, 12f), Tight);
	}

	private static Button WinBtn(string glyph)
	{
		WindowButton windowButton = new WindowButton();
		windowButton.Text = glyph;
		windowButton.FlatStyle = FlatStyle.Flat;
		windowButton.BackColor = Color.Transparent;
		windowButton.ForeColor = Theme.Dim;
		windowButton.Font = new Font("Segoe UI", 10f);
		windowButton.Size = new Size(42, 42);
		windowButton.Cursor = Cursors.Hand;
		windowButton.FlatAppearance.BorderSize = 0;
		windowButton.FlatAppearance.MouseOverBackColor = BtnHov;
		return windowButton;
	}
}
