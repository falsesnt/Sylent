using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using SylentLoader.Services;
using SylentLoader.Services.Auth;

namespace SylentLoader.Controls;

public sealed class UnlockForm : Form
{
	private sealed class RoundedInput : Panel
	{
		public readonly TextBox Inner;

		private bool _focused;

		public RoundedInput()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			BackColor = Theme.Black;
			Inner = new TextBox
			{
				BorderStyle = BorderStyle.None,
				BackColor = Theme.Black,
				ForeColor = Theme.Text,
				Font = new Font("Consolas", 11f)
			};
			Inner.GotFocus += (object? _, EventArgs _) =>
			{
				_focused = true;
				Invalidate();
			};
			Inner.LostFocus += (object? _, EventArgs _) =>
			{
				_focused = false;
				Invalidate();
			};
			Controls.Add(Inner);
		}

		protected override void OnSizeChanged(EventArgs e)
		{
			base.OnSizeChanged(e);
			int num = 13;
			int preferredHeight = Inner.PreferredHeight;
			Inner.SetBounds(num, (Height - preferredHeight) / 2, Width - num * 2, preferredHeight);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			Inner.Focus();
			base.OnMouseDown(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			using (SolidBrush brush = new SolidBrush(Parent?.BackColor ?? Theme.Surface))
			{
				graphics.FillRectangle(brush, ClientRectangle);
			}
			using GraphicsPath path = Rounded(new RectangleF(0.5f, 0.5f, (float)Width - 1f, (float)Height - 1f), 8f);
			using (SolidBrush brush2 = new SolidBrush(Theme.Black))
			{
				graphics.FillPath(brush2, path);
			}
			using Pen pen = new Pen(_focused ? Theme.Accent : Theme.Border, _focused ? 1.4f : 1f);
			graphics.DrawPath(pen, path);
		}
	}

	private sealed class CheckLine : Control
	{
		private bool _checked;

		private bool _hover;

		public bool Checked
		{
			get
			{
				return _checked;
			}
			set
			{
				_checked = value;
				Invalidate();
			}
		}

		public CheckLine()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
			Cursor = Cursors.Hand;
			Font = new Font("Segoe UI", 8.5f);
			ForeColor = Theme.Dim;
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

		protected override void OnClick(EventArgs e)
		{
			_checked = !_checked;
			Invalidate();
			base.OnClick(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			using (SolidBrush brush = new SolidBrush(Parent?.BackColor ?? Theme.Surface))
			{
				graphics.FillRectangle(brush, ClientRectangle);
			}
			int num = (Height - 16) / 2;
			using GraphicsPath path = Rounded(new RectangleF(0.5f, (float)num + 0.5f, 15f, 15f), 4f);
			if (_checked)
			{
				using SolidBrush brush2 = new SolidBrush(Theme.Accent);
				graphics.FillPath(brush2, path);
				using Pen pen = new Pen(Color.White, 1.9f)
				{
					StartCap = LineCap.Round,
					EndCap = LineCap.Round,
					LineJoin = LineJoin.Round
				};
				graphics.DrawLines(pen, new PointF[3]
				{
					new PointF(4f, (float)num + 8f),
					new PointF(7f, (float)num + 11f),
					new PointF(12f, (float)num + 5f)
				});
			}
			else
			{
				using Pen pen2 = new Pen(_hover ? Theme.BorderHi : Theme.Border, 1f);
				graphics.DrawPath(pen2, path);
			}
			using SolidBrush brush3 = new SolidBrush(_hover ? Theme.Text : ForeColor);
			graphics.DrawString(Text, Font, brush3, new PointF(25f, (float)(Height - Font.Height) / 2f));
		}
	}

	private static readonly Color Bg = Theme.Surface;

	private static readonly Color Accent = Theme.Accent;

	private static readonly Color SubCol = Theme.Sub;

	private static readonly Color TextCol = Theme.Text;

	private static readonly Color ErrCol = Theme.Error;

	private const int Gutter = 24;

	private readonly Product _product;

	private readonly RoundedInput _key;

	private readonly CheckLine _remember;

	private readonly Label _status;

	private readonly RoundedButton _unlock;

	private readonly RoundedButton _cancel;

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

	public UnlockForm(Product product, string? note = null)
	{
		_product = product;
		FormBorderStyle = FormBorderStyle.None;
		StartPosition = FormStartPosition.CenterParent;
		BackColor = Bg;
		Size = new Size(400, 300);
		DoubleBuffered = true;
		KeyPreview = true;
		int width = Width - 48;
		_key = new RoundedInput
		{
			Location = new Point(24, 92),
			Size = new Size(width, 42)
		};
		_key.Inner.Font = new Font("Consolas", 11f);
		_key.Inner.MaxLength = 128;
		_key.Inner.PlaceholderText = "License key (enter anything lmao ez)";
		_remember = new CheckLine
		{
			Text = "Remember on this PC",
			Location = new Point(24, 150),
			Size = new Size(220, 20),
			Checked = AppSettings.RememberKeys
		};
		_status = new Label
		{
			Location = new Point(24, 180),
			Size = new Size(width, 44),
			ForeColor = (string.IsNullOrEmpty(note) ? SubCol : ErrCol),
			BackColor = Bg,
			Font = new Font("Segoe UI", 8.5f),
			Text = (note ?? "lmao ez SnT love you very much")
		};
		_unlock = new RoundedButton
		{
			Text = "Crack ts <3",
			Size = new Size(110, 34),
			Location = new Point(Width - 24 - 110, 242),
			BackColor = Theme.AccentTop,
			GradientEnd = Theme.AccentBot,
			HoverBackColor = Theme.AccentHover,
			HoverGradientEnd = Theme.AccentHoverBot,
			BorderColor = Theme.AccentBorder,
			ForeColor = Color.White,
			Font = new Font("Segoe UI", 9f, FontStyle.Bold),
			CornerRadius = 8,
			Cursor = Cursors.Hand
		};
		_unlock.Click += OnUnlock;
		_cancel = new RoundedButton
		{
			Text = "Cancel",
			Size = new Size(80, 34),
			Location = new Point(Width - 24 - 110 - 8 - 80, 242),
			BackColor = Theme.Black,
			HoverBackColor = Theme.SurfaceHi,
			ForeColor = Theme.Dim,
			BorderColor = Theme.BorderHi,
			Font = new Font("Segoe UI", 8.5f),
			CornerRadius = 8,
			Cursor = Cursors.Hand
		};
		_cancel.Click += (object? _, EventArgs _) =>
		{
			DialogResult = DialogResult.Cancel;
			Close();
		};
		Controls.AddRange(new Control[5] { _key, _remember, _status, _cancel, _unlock });
		AcceptButton = _unlock;
		CancelButton = _cancel;
		Shown += (object? _, EventArgs _) =>
		{
			_key.Inner.Focus();
		};
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		int value = 2;
		DwmSetWindowAttribute(Handle, 33, ref value, 4);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		using (Pen pen = new Pen(Theme.Border, 1f))
		{
			graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
		}
		using (Font font = new Font("Segoe UI", 13.5f, FontStyle.Bold))
		{
			using SolidBrush brush = new SolidBrush(TextCol);
			graphics.DrawString("Unlock " + Entitlements.DisplayName(_product), font, brush, new PointF(22f, 24f));
		}
		using (Font font2 = new Font("Segoe UI", 8.5f))
		{
			using SolidBrush brush2 = new SolidBrush(Theme.Dim);
			string s = "Enter anything to unlock this shit LMAO";
			graphics.DrawString(s, font2, brush2, new PointF(23f, 54f));
		}
		using Pen pen2 = new Pen(Theme.Border, 1f);
		graphics.DrawLine(pen2, 24, 226, Width - 24, 226);
	}

	private async void OnUnlock(object? sender, EventArgs e)
	{
		string key = _key.Inner.Text.Trim();
		if (key.Length == 0)
		{
			Say("enter literally anything bro even \"a\" works", ErrCol);
			return;
		}
		SetBusy(busy: true);
		Say("Checking license…", SubCol);
		bool remember = _remember.Checked;
		Product product = _product;
		AuthResult authResult;
		try
		{
			authResult = await Task.Run(() => Entitlements.Unlock(product, key, remember));
		}
		catch (Exception ex)
		{
			authResult = new AuthResult(Ok: false, "Unexpected error: " + ex.GetType().Name);
		}
		if (!IsDisposed)
		{
			SetBusy(busy: false);
			if (authResult.Ok)
			{
				DialogResult = DialogResult.OK;
				Close();
			}
			else
			{
				Say(authResult.Message, ErrCol);
				_key.Inner.SelectAll();
				_key.Inner.Focus();
			}
		}
	}

	private void SetBusy(bool busy)
	{
		_unlock.Enabled = !busy;
		_key.Enabled = !busy;
		_remember.Enabled = !busy;
	}

	private void Say(string text, Color col)
	{
		_status.ForeColor = col;
		_status.Text = text;
	}

	internal static GraphicsPath Rounded(RectangleF r, float radius)
	{
		float num = radius * 2f;
		GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddArc(r.X, r.Y, num, num, 180f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Y, num, num, 270f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
		graphicsPath.AddArc(r.X, r.Bottom - num, num, num, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}
}
