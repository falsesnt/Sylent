using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SylentLoader.Controls;

public sealed class ToggleSwitch : Control
{
	private static readonly Color OffTrack = Color.FromArgb(22, 22, 27);

	private static readonly Color OffKnob = Color.FromArgb(120, 120, 134);

	private const float SlideMs = 150f;

	private readonly FramePump _pump;

	private readonly Stopwatch _clock = new Stopwatch();

	private bool _checked;

	private float _pos;

	private float _from;

	private float _to;

	private bool _hover;

	public bool Checked
	{
		get
		{
			return _checked;
		}
		set
		{
			SetChecked(value, IsHandleCreated && Visible, raise: false);
		}
	}

	public event EventHandler? CheckedChanged;

	public ToggleSwitch()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
		Size = new Size(44, 24);
		Cursor = Cursors.Hand;
		TabStop = false;
		_pump = new FramePump(this, () =>
		{
			float num = Math.Min(1f, (float)_clock.ElapsedMilliseconds / 150f);
			float num2 = 1f - (float)Math.Pow(1f - num, 3.0);
			_pos = _from + (_to - _from) * num2;
			Invalidate();
			Update();
			if (num >= 1f)
			{
				_pump.Stop();
				_clock.Stop();
			}
		});
	}

	private void SetChecked(bool value, bool animate, bool raise)
	{
		if (_checked == value)
		{
			if (!animate)
			{
				_pos = (value ? 1f : 0f);
				Invalidate();
			}
			return;
		}
		_checked = value;
		if (animate)
		{
			_from = _pos;
			_to = (value ? 1f : 0f);
			_clock.Restart();
			_pump.Start();
		}
		else
		{
			_pos = (value ? 1f : 0f);
			Invalidate();
		}
		if (raise)
		{
			CheckedChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left && Enabled)
		{
			SetChecked(!_checked, animate: true, raise: true);
		}
		base.OnMouseClick(e);
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

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_pump.Dispose();
		}
		base.Dispose(disposing);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		Color b = Color.FromArgb((Theme.AccentTop.R + Theme.AccentBot.R) / 2, (Theme.AccentTop.G + Theme.AccentBot.G) / 2, (Theme.AccentTop.B + Theme.AccentBot.B) / 2);
		Color a = (_hover ? Theme.BorderHi : Theme.Border);
		using (GraphicsPath path = Pill(new RectangleF(0.5f, 0.5f, (float)Width - 1f, (float)Height - 1f)))
		{
			using (SolidBrush brush = new SolidBrush(Lerp(OffTrack, b, _pos)))
			{
				graphics.FillPath(brush, path);
			}
			using Pen pen = new Pen(Lerp(a, Theme.AccentBorder, _pos), 1f);
			graphics.DrawPath(pen, path);
		}
		float num = (float)Height - 8f;
		float x = 4f + ((float)Width - 8f - num) * _pos;
		using SolidBrush brush2 = new SolidBrush((_pos > 0.5f) ? Color.White : OffKnob);
		graphics.FillEllipse(brush2, x, 4f, num, num);
	}

	private static Color Lerp(Color a, Color b, float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		return Color.FromArgb((int)((float)(int)a.R + (float)(b.R - a.R) * t), (int)((float)(int)a.G + (float)(b.G - a.G) * t), (int)((float)(int)a.B + (float)(b.B - a.B) * t));
	}

	private static GraphicsPath Pill(RectangleF r)
	{
		float height = r.Height;
		GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddArc(r.X, r.Y, height, height, 90f, 180f);
		graphicsPath.AddArc(r.Right - height, r.Y, height, height, 270f, 180f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}
}
