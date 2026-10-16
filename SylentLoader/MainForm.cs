using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using SylentLoader.Controls;
using SylentLoader.Models;
using SylentLoader.Services;
using SylentLoader.Services.Auth;

namespace SylentLoader;

public class MainForm : Form
{
	private static readonly Color BgDark = Theme.Black;

	private static readonly Color FooterBg = Theme.Footer;

	private static readonly Color FooterSep = Theme.Border;

	private static readonly Color FooterTxt = Theme.Faint;

	private readonly TitleBar _titleBar;

	private readonly Sidebar _sidebar;

	private readonly MainPanel _mainPanel;

	private FramePump? _introPump;

	private readonly Stopwatch _introClock = new Stopwatch();

	private Panel _footer;

	private AutoStatus _auto = new AutoStatus(AutoStage.Off, "");

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

	public MainForm()
	{
		FormBorderStyle = FormBorderStyle.None;
		StartPosition = FormStartPosition.CenterScreen;
		BackColor = BgDark;
		DoubleBuffered = true;
		Size = new Size(850, 550);
		Opacity = 0.0;
		List<GameProfile> list = new List<GameProfile>
		{
			GameDetector.CreateGTAV(),
			GameDetector.CreateRDR2()
		};
		foreach (GameProfile item in list)
		{
			ModManager.DetectMods(item);
		}
		_titleBar = new TitleBar(this);
		_sidebar = new Sidebar();
		_mainPanel = new MainPanel(list);
		_footer = BuildFooter();
		Panel footer = _footer;
		AutoInjector.StatusChanged += (AutoStatus s) =>
		{
			OnUi(() =>
			{
				_auto = s;
				_footer.Invalidate();
			});
		};
		AutoInjector.Finished += (bool ok, string msg) =>
		{
			OnUi(() =>
			{
				_mainPanel.OnAutoInjectFinished(ok, msg);
			});
		};
		_sidebar.ItemClicked += (string id) =>
		{
			switch (id)
			{
			case "HOME":
				_mainPanel.ResetSelection();
				break;
			case "GTA V":
				_mainPanel.OpenGame(GameId.GTAV);
				break;
			case "RDR2":
				_mainPanel.OpenGame(GameId.RDR2);
				break;
			case "SETTINGS":
				_mainPanel.OpenSettings();
				break;
			case "SIGNOUT":
				OnSignOut();
				break;
			}
		};
		Controls.Add(_mainPanel);
		Controls.Add(_sidebar);
		Controls.Add(footer);
		Controls.Add(_titleBar);
		Shown += (object? _, EventArgs _) =>
		{
			PlayOpeningAnimation();
			_mainPanel.WarmBackgrounds();
			if (AppSettings.AutoSignIn)
			{
				_mainPanel.WarmLicenses();
			}
			AutoInjector.Start();
		};
	}

	private void OnUi(Action a)
	{
		if (!IsHandleCreated || IsDisposed)
		{
			return;
		}
		try
		{
			BeginInvoke(a);
		}
		catch (InvalidOperationException)
		{
		}
	}

	private void PlayOpeningAnimation()
	{
		int restTop = Top;
		Top = restTop + 18;
		_introClock.Restart();
		_introPump = new FramePump(this, () =>
		{
			float num = Math.Min(1f, (float)_introClock.ElapsedMilliseconds / 420f);
			float num2 = 1f - (float)Math.Pow(1f - num, 3.0);
			Opacity = num2;
			Top = restTop + (int)Math.Round((1f - num2) * 18f);
			if (num >= 1f)
			{
				Opacity = 1.0;
				Top = restTop;
				_introPump.Stop();
			}
		});
		_introPump.Start();
		_mainPanel.PlayIntro();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_introPump?.Dispose();
		}
		base.Dispose(disposing);
	}

	public void SetSection(string? section)
	{
		_titleBar.SetSection(section);
	}

	public void SetTab(string tab)
	{
		_sidebar.SetActive(tab);
	}

	public void RequestSignOut()
	{
		OnSignOut();
	}

	private async void OnSignOut()
	{
		if (ConfirmForm.Ask(this, "Sign out", "Forget every saved license key on this PC?\nYou'll need to enter your keys again.", "Sign out"))
		{
			await Task.Run((Action)Entitlements.SignOut);
			_mainPanel.ResetSelection();
			_mainPanel.RefreshAccounts();
		}
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		int value = 2;
		DwmSetWindowAttribute(Handle, 33, ref value, 4);
	}

	private void PaintAutoStatus(Graphics g, int width)
	{
		if (_auto.Stage == AutoStage.Off || _auto.Text.Length == 0)
		{
			return;
		}
		Color color = _auto.Stage switch
		{
			AutoStage.Injected => Theme.Success, 
			AutoStage.Failed => Theme.Error, 
			AutoStage.Injecting => Theme.Accent, 
			AutoStage.Loading => Color.FromArgb(232, 184, 92), 
			_ => FooterTxt, 
		};
		AutoStage stage = _auto.Stage;
		bool flag = (uint)(stage - 4) <= 1u;
		Color color2 = (flag ? color : FooterTxt);
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		using Font font = new Font("Segoe UI", 7f);
		float width2 = g.MeasureString(_auto.Text, font).Width;
		float num = (float)(width - 12) - width2;
		using (SolidBrush brush = new SolidBrush(color))
		{
			g.FillEllipse(brush, num - 10f, 8.5f, 5.5f, 5.5f);
		}
		using SolidBrush brush2 = new SolidBrush(color2);
		g.DrawString(_auto.Text, font, brush2, num, 5f);
	}

	private Panel BuildFooter()
	{
		Panel ftr = new Panel
		{
			Dock = DockStyle.Bottom,
			Height = 22,
			BackColor = FooterBg
		};
		ftr.Paint += (object? s, PaintEventArgs e) =>
		{
			using Pen pen = new Pen(FooterSep, 1f);
			e.Graphics.DrawLine(pen, 0, 0, ftr.Width, 0);
			PaintAutoStatus(e.Graphics, ftr.Width);
		};
		ftr.Resize += (object? _, EventArgs _) =>
		{
			ftr.Invalidate();
		};
		ftr.Controls.Add(new Label
		{
			Text = "Sylent Loader v1.0",
			ForeColor = FooterTxt,
			Font = new Font("Segoe UI", 7f),
			AutoSize = true,
			Location = new Point(5, 0),
			BackColor = Color.Transparent
		});
		return ftr;
	}
}
