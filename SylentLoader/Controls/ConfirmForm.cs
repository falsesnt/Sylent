using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SylentLoader.Controls;

public sealed class ConfirmForm : Form
{
	private readonly string _title;

	private readonly string _message;

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		int value = 2;
		DwmSetWindowAttribute(Handle, 33, ref value, 4);
	}

	public static bool Ask(IWin32Window? owner, string title, string message, string yes = "Yes", string no = "Cancel")
	{
		using ConfirmForm confirmForm = new ConfirmForm(title, message, yes, no);
		return confirmForm.ShowDialog(owner) == DialogResult.OK;
	}

	private ConfirmForm(string title, string message, string yes, string no)
	{
		_title = title;
		_message = message;
		FormBorderStyle = FormBorderStyle.None;
		StartPosition = FormStartPosition.CenterParent;
		ShowInTaskbar = false;
		BackColor = Theme.Surface;
		Size = new Size(360, 176);
		DoubleBuffered = true;
		RoundedButton roundedButton = new RoundedButton
		{
			Text = yes,
			Size = new Size(110, 32),
			Location = new Point(230, 126),
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
		roundedButton.Click += (object? _, EventArgs _) =>
		{
			DialogResult = DialogResult.OK;
			Close();
		};
		RoundedButton roundedButton2 = new RoundedButton
		{
			Text = no,
			Size = new Size(80, 32),
			Location = new Point(140, 126),
			BackColor = Theme.Black,
			HoverBackColor = Theme.SurfaceHi,
			ForeColor = Theme.Dim,
			BorderColor = Theme.Border,
			Font = new Font("Segoe UI", 8.5f),
			CornerRadius = 8,
			Cursor = Cursors.Hand
		};
		roundedButton2.Click += (object? _, EventArgs _) =>
		{
			DialogResult = DialogResult.Cancel;
			Close();
		};
		Controls.AddRange(new Control[2] { roundedButton2, roundedButton });
		AcceptButton = roundedButton;
		CancelButton = roundedButton2;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		using Pen pen = new Pen(Theme.Border, 1f);
		graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
		using Font font = new Font("Segoe UI", 13f, FontStyle.Bold);
		using SolidBrush brush = new SolidBrush(Theme.Text);
		graphics.DrawString(_title, font, brush, new PointF(20f, 18f));
		using Font font2 = new Font("Segoe UI", 8.5f);
		using SolidBrush brush2 = new SolidBrush(Theme.Dim);
		graphics.DrawString(_message, font2, brush2, new RectangleF(20f, 56f, Width - 40, 60f));
	}
}
