using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SylentLoader.Models;
using SylentLoader.Services;
using SylentLoader.Services.Auth;
using SylentLoader.Services.Security;

namespace SylentLoader.Controls;

public sealed class MainPanel : Panel
{
	private readonly record struct Column(float TagY, float TitleY, float TitleH, float ProductY, float RuleY, float RowsY);

	private const int PadX = 40;

	private const int ColMax = 400;

	private const int RowH = 36;

	private const int LabelW = 118;

	private static readonly Color AccentCol = Theme.Accent;

	private static readonly Color SuccessCol = Theme.Success;

	private static readonly Color SubCol = Theme.Sub;

	private static readonly Color TextCol = Theme.Text;

	private static readonly Color DimCol = Theme.Dim;

	private readonly List<GameProfile> _profiles;

	private GameProfile? _selected;

	private readonly RoundedButton _injectBtn;

	private readonly RoundedButton _pubLobbyBtn;

	private readonly SegmentedToggle _versionToggle;

	private readonly RoundedButton _switchBtn;

	private readonly HomeView _home;

	private readonly SettingsView _settings;

	private bool _settingsOpen;

	private readonly Timer _statusTimer = new Timer
	{
		Interval = 2000
	};

	private string _feedbackText = "";

	private Color _feedbackCol = Theme.Sub;

	private bool _running;

	private static readonly Dictionary<GtaMod, string> ModDesc = new Dictionary<GtaMod, string>
	{
		[GtaMod.SylentMenu] = "shitty paste for gta numero cinco",
	};

	private bool _picking;

	private const int FadeMs = 200;

	private readonly Stopwatch _fadeClock = new Stopwatch();

	private FramePump? _fadePump;

	private Bitmap? _fadeOld;

	private Bitmap? _fadeNew;

	private int _fadeDir;

	private bool _transitioning;

	private bool _hidHome;

	private bool _hidInject;

	private bool _hidPub;

	private bool _hidToggle;

	private bool _hidSwitch;

	private bool _hidSettings;

	private bool _checkingRunning;

	private string? _otherRunning;

	private readonly object _bgLock = new object();

	private readonly Dictionary<GameId, Bitmap> _bgCache = new Dictionary<GameId, Bitmap>();

	private Size _bgSize;

	private int PageIndex
	{
		get
		{
			if (!_settingsOpen)
			{
				if (_selected != null)
				{
					if (_selected.Id != GameId.GTAV)
					{
						return 2;
					}
					return 1;
				}
				return 0;
			}
			return 3;
		}
	}

	private int ColW => Math.Min(400, Width - 80);

	public MainPanel(IReadOnlyList<GameProfile> profiles)
	{
		_profiles = profiles.ToList();
		GameProfile gameProfile = _profiles.FirstOrDefault((GameProfile x) => x.Id == GameId.GTAV);
		if (gameProfile != null)
		{
			gameProfile.SelectedMod = AppSettings.LastGtaMod;
			if (gameProfile.SelectedMod == GtaMod.Aurora)
			{
				gameProfile.GtaVersion = GtaVersion.Enhanced;
			}
		}
		BackColor = Theme.Black;
		Dock = DockStyle.Fill;
		DoubleBuffered = true;
		Cursor = Cursors.Default;
		_injectBtn = new RoundedButton
		{
			Text = "Inject",
			Icon = ButtonIcon.Play,
			BackColor = Theme.AccentTop,
			GradientEnd = Theme.AccentBot,
			HoverBackColor = Theme.AccentHover,
			HoverGradientEnd = Theme.AccentHoverBot,
			BorderColor = Theme.AccentBorder,
			ForeColor = Color.White,
			Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
			TransparentCorners = true,
			CornerRadius = 10,
			Cursor = Cursors.Hand,
			Visible = false
		};
		_injectBtn.Click += (object? _, EventArgs _) =>
		{
			DoInject();
		};
		_pubLobbyBtn = new RoundedButton
		{
			Text = "Pub Lobby",
			BackColor = Color.FromArgb(230, 8, 8, 10),
			HoverBackColor = Theme.SurfaceHi,
			BorderColor = Theme.BorderHi,
			ForeColor = DimCol,
			Font = new Font("Segoe UI", 9.5f),
			TransparentCorners = true,
			CornerRadius = 10,
			Cursor = Cursors.Hand,
			Visible = false
		};
		_pubLobbyBtn.Click += (object? _, EventArgs _) =>
		{
			DoPubLobby();
		};
		_versionToggle = new SegmentedToggle
		{
			Items = new string[2] { "Legacy", "Enhanced" },
			Visible = false
		};
		_versionToggle.SelectedChanged += (int i) =>
		{
			SetVersion((i != 0) ? GtaVersion.Enhanced : GtaVersion.Legacy);
		};
		_switchBtn = new RoundedButton
		{
			Text = "Change",
			Icon = ButtonIcon.Swap,
			TransparentCorners = true,
			BackColor = Color.FromArgb(215, 8, 8, 10),
			HoverBackColor = Color.FromArgb(235, 22, 24, 34),
			BorderColor = Color.FromArgb(150, 90, 90, 104),
			ForeColor = Theme.Dim,
			Font = new Font("Segoe UI", 8.5f),
			CornerRadius = 13,
			Cursor = Cursors.Hand,
			Visible = false
		};
		_switchBtn.Click += (object? _, EventArgs _) =>
		{
			if (_selected != null)
			{
				PickGame(_selected);
			}
		};
		Controls.AddRange(new Control[4] { _injectBtn, _pubLobbyBtn, _versionToggle, _switchBtn });
		_home = new HomeView
		{
			Visible = false
		};
		_home.ProductRequested += OpenProduct;
		Controls.Add(_home);
		_settings = new SettingsView
		{
			Visible = false
		};
		_settings.AccountsChanged += () =>
		{
			_home.RefreshState();
		};
		_settings.SignOutRequested += () =>
		{
			if (FindForm() is MainForm mainForm)
			{
				mainForm.RequestSignOut();
			}
		};
		Controls.Add(_settings);
		_statusTimer.Tick += (object? _, EventArgs _) =>
		{
			RefreshRunning();
		};
		_statusTimer.Start();
		Resize += (object? _, EventArgs _) =>
		{
			DoLayout();
		};
		VisibleChanged += (object? _, EventArgs _) =>
		{
			if (Visible)
			{
				DoLayout();
			}
		};
	}

	public void ResetSelection()
	{
		if (_selected == null && !_settingsOpen)
		{
			_home.RefreshState();
			return;
		}
		Transition(() =>
		{
			_selected = null;
			_settingsOpen = false;
			_feedbackText = "";
			Announce(null, "HOME");
			DoLayout();
		});
	}

	public void OpenSettings()
	{
		if (!_settingsOpen)
		{
			Transition(() =>
			{
				_selected = null;
				_settingsOpen = true;
				_feedbackText = "";
				Announce(null, "SETTINGS");
				DoLayout();
			});
		}
	}

	public void PlayIntro()
	{
		if (_selected == null && !_settingsOpen)
		{
			_home.Replay();
		}
	}

	public void RefreshAccounts()
	{
		_home.RefreshState();
		if (_settingsOpen)
		{
			_settings.RefreshState();
		}
	}

	public void OpenGame(GameId id)
	{
		GameProfile? selected = _selected;
		if (selected != null && selected.Id == id && !_settingsOpen)
		{
			return;
		}
		GameProfile gameProfile = _profiles.FirstOrDefault((GameProfile x) => x.Id == id);
		if (gameProfile == null)
		{
			return;
		}
		if (id == GameId.GTAV)
		{
			foreach (GtaMod item in new GtaMod[2]
			{
				gameProfile.SelectedMod,
				GtaMod.SylentMenu
			}.Distinct())
			{
				Product product = Entitlements.ForMod(item);
				if (Entitlements.IsUnlocked(product) || Entitlements.HasSavedKey(product))
				{
					OpenProduct(product);
					return;
				}
			}
		}
		PickGame(gameProfile);
	}

	public void WarmLicenses()
	{
		Product[] products = new Product[1]
		{
			Product.Menu
		}.Where((Product p) => !Entitlements.IsUnlocked(p) && Entitlements.HasSavedKey(p)).ToArray();
		if (products.Length == 0)
		{
			return;
		}
		Task.Run(() =>
		{
			try
			{
				Parallel.ForEach((IEnumerable<Product>)products, (Action<Product>)((Product p) =>
				{
					Entitlements.UnlockSaved(p);
				}));
			}
			catch
			{
			}
		}).ContinueWith((Task _) =>
		{
			if (IsHandleCreated && !IsDisposed)
			{
				try
				{
					BeginInvoke(() =>
					{
						_home.RefreshState();
						Invalidate();
					});
				}
				catch
				{
				}
			}
		}, TaskScheduler.Default);
	}

	private void Announce(string? section, string tab)
	{
		if (FindForm() is MainForm mainForm)
		{
			mainForm.SetSection(section);
			mainForm.SetTab(tab);
		}
	}

	private async void PickGame(GameProfile p)
	{
		if (_picking)
		{
			return;
		}
		_picking = true;
		try
		{
			GtaMod? mod = null;
			if (p.Id == GameId.GTAV)
			{
				using ModPickerForm modPickerForm = new ModPickerForm();
				if (modPickerForm.ShowDialog(FindForm()) != DialogResult.OK)
				{
					return;
				}
				string selectedMod = modPickerForm.SelectedMod;
				GtaMod value = ((selectedMod == "Sylent Recovery") ? GtaMod.SylentRecovery : ((selectedMod == "Aurora") ? GtaMod.Aurora : GtaMod.SylentMenu));
				mod = value;
			}
			await ActivateAsync(p, mod);
		}
		finally
		{
			_picking = false;
			_home.RefreshState();
		}
	}

	private async void OpenProduct(Product product)
	{
		if (_picking)
		{
			return;
		}
		_picking = true;
		try
		{
			GameProfile p = _profiles.First((GameProfile x) => x.Id == GameId.GTAV);
			GameProfile p2 = _profiles.First((GameProfile x) => x.Id == GameId.RDR2);
			switch (product)
			{
			case Product.Menu:
				await ActivateAsync(p, GtaMod.SylentMenu);
				break;
			case Product.Recovery:
				await ActivateAsync(p, GtaMod.SylentRecovery);
				break;
			case Product.Aurora:
				await ActivateAsync(p, GtaMod.Aurora);
				break;
			case Product.Rdr2:
				await ActivateAsync(p2, null);
				break;
			}
		}
		finally
		{
			_picking = false;
			_home.RefreshState();
		}
	}

	private async Task ActivateAsync(GameProfile p, GtaMod? mod)
	{
		Product product = (mod.HasValue ? Entitlements.ForMod(mod.Value) : ((p.Id == GameId.RDR2) ? Product.Rdr2 : Product.None));
		if (!(await ModGate.EnsureAsync(FindForm(), product)))
		{
			return;
		}
		Transition(() =>
		{
			if (mod.HasValue)
			{
				p.SelectedMod = mod.Value;
				if (mod == GtaMod.Aurora)
				{
					p.GtaVersion = GtaVersion.Enhanced;
				}
				if (p.Id == GameId.GTAV && AppSettings.LastGtaMod != mod.Value)
				{
					AppSettings.LastGtaMod = mod.Value;
				}
			}
			_selected = p;
			_settingsOpen = false;
			_feedbackText = "";
			_running = false;
			Announce(SectionFor(p), (p.Id == GameId.GTAV) ? "GTA V" : "RDR2");
			DoLayout();
		});
		RefreshRunning();
	}

	private void OnFadeFrame()
	{
		Invalidate();
		Update();
		if (_fadeClock.ElapsedMilliseconds >= 200)
		{
			EndTransition();
		}
	}

	private void StartFrames()
	{
		if (_fadePump == null)
		{
			_fadePump = new FramePump(this, OnFadeFrame);
		}
		_fadePump.Start();
	}

	private void StopFrames()
	{
		_fadePump?.Stop();
	}

	private Bitmap Snapshot()
	{
		Bitmap bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
		DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
		return bitmap;
	}

	private void Transition(Action change)
	{
		if (_transitioning)
		{
			EndTransition();
		}
		if (!IsHandleCreated || !Visible || Width == 0 || Height == 0)
		{
			change();
			Invalidate();
			return;
		}
		int pageIndex = PageIndex;
		Bitmap fadeOld = Snapshot();
		change();
		Bitmap fadeNew = Snapshot();
		_fadeOld = fadeOld;
		_fadeNew = fadeNew;
		_fadeDir = Math.Sign(PageIndex - pageIndex);
		_hidHome = _home.Visible;
		_hidInject = _injectBtn.Visible;
		_hidPub = _pubLobbyBtn.Visible;
		_hidToggle = _versionToggle.Visible;
		_hidSwitch = _switchBtn.Visible;
		_hidSettings = _settings.Visible;
		HomeView home = _home;
		RoundedButton injectBtn = _injectBtn;
		RoundedButton pubLobbyBtn = _pubLobbyBtn;
		SegmentedToggle versionToggle = _versionToggle;
		bool flag = (_switchBtn.Visible = false);
		bool flag3 = (versionToggle.Visible = flag);
		bool flag5 = (pubLobbyBtn.Visible = flag3);
		bool visible = (injectBtn.Visible = flag5);
		home.Visible = visible;
		_settings.Visible = false;
		_transitioning = true;
		_fadeClock.Restart();
		StartFrames();
		Invalidate();
	}

	private void EndTransition()
	{
		StopFrames();
		_transitioning = false;
		_fadeOld?.Dispose();
		_fadeOld = null;
		_fadeNew?.Dispose();
		_fadeNew = null;
		_injectBtn.Visible = _hidInject;
		_pubLobbyBtn.Visible = _hidPub;
		_versionToggle.Visible = _hidToggle;
		_switchBtn.Visible = _hidSwitch;
		if (_hidSettings)
		{
			_settings.Visible = true;
		}
		if (_hidHome)
		{
			_home.Visible = true;
			_home.Replay();
		}
		DoLayout();
		Invalidate();
	}

	private void PaintFade(Graphics g)
	{
		if (_fadeOld != null && _fadeNew != null)
		{
			float num = Math.Min(1f, (float)_fadeClock.ElapsedMilliseconds / 200f);
			float alpha = 1f - Smooth(num / 0.45f);
			float alpha2 = Smooth((num - 0.12f) / 0.88f);
			float num2 = 1f - (float)Math.Pow(1f - Math.Clamp(num, 0f, 1f), 3.0);
			g.InterpolationMode = InterpolationMode.NearestNeighbor;
			g.PixelOffsetMode = PixelOffsetMode.Half;
			DrawFaded(g, _fadeOld, alpha, 0);
			DrawFaded(g, _fadeNew, alpha2, (int)Math.Round((float)(_fadeDir * 14) * (1f - num2)));
		}
	}

	private static float Smooth(float x)
	{
		x = Math.Clamp(x, 0f, 1f);
		return x * x * (3f - 2f * x);
	}

	private void DrawFaded(Graphics g, Bitmap bmp, float alpha, int dx)
	{
		if (alpha <= 0.003f)
		{
			return;
		}
		ColorMatrix newColorMatrix = new ColorMatrix
		{
			Matrix33 = Math.Clamp(alpha, 0f, 1f)
		};
		using ImageAttributes imageAttributes = new ImageAttributes();
		imageAttributes.SetColorMatrix(newColorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
		g.DrawImage(bmp, new Rectangle(dx, 0, Width, Height), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, imageAttributes);
	}

	private static string SectionFor(GameProfile p)
	{
		if (p.Id == GameId.RDR2)
		{
			return "RDR2";
		}
		return p.SelectedMod switch
		{
			GtaMod.SylentMenu => "GTA Menu", 
			_ => "GTA", 
		};
	}

	private static Product ProductOf(GameProfile p)
	{
		if (p.Id != GameId.RDR2)
		{
			return Entitlements.ForMod(p.SelectedMod);
		}
		return Product.Rdr2;
	}

	private static string LicenseNote(Product product)
	{
		DateTimeOffset? dateTimeOffset = Entitlements.ExpiresAt(product);
		if (!dateTimeOffset.HasValue)
		{
			if (!Entitlements.IsUnlocked(product))
			{
				return "Not licensed";
			}
			return "Licensed";
		}
		string text = (Entitlements.ViaPack(product) ? "Full Access" : "Licensed");
		if (!Entitlements.IsLifetime(dateTimeOffset.Value))
		{
			return $"{text} · until {dateTimeOffset.Value:yyyy-MM-dd}";
		}
		return text + " · lifetime";
	}

	private static string? OtherBuildProcess(GameProfile p)
	{
		if (p.Id != GameId.GTAV)
		{
			return null;
		}
		if (p.GtaVersion == GtaVersion.Enhanced)
		{
			return "GTA5";
		}
		if (p.SelectedMod != GtaMod.Aurora)
		{
			return "GTA5_Enhanced";
		}
		return null;
	}

	private async void RefreshRunning()
	{
		if (_selected == null || !Visible || _checkingRunning)
		{
			return;
		}
		_checkingRunning = true;
		try
		{
			string proc = _selected.ProcessName;
			string other = OtherBuildProcess(_selected);
			var (flag, flag2) = await Task.Run(() => (Injector.IsRunning(proc), other != null && Injector.IsRunning(other)));
			if (_selected != null && _selected.ProcessName == proc)
			{
				string text = ((!flag & flag2) ? other : null);
				if (flag != _running || text != _otherRunning)
				{
					_running = flag;
					_otherRunning = text;
					Invalidate();
				}
			}
		}
		finally
		{
			_checkingRunning = false;
		}
	}

	private Column MeasureColumn(Graphics g)
	{
		using Font font = new Font("Segoe UI", 30f, FontStyle.Bold);
		float height = g.MeasureString(_selected.DisplayName, font, ColW).Height;
		float num = 38f + 22f;
		float num2 = num + height + 6f;
		float num3 = num2 + 52f;
		float rowsY = num3 + 14f;
		return new Column(38f, num, height, num2, num3, rowsY);
	}

	private void DoLayout()
	{
		if (Width == 0 || _transitioning)
		{
			return;
		}
		if (_selected == null)
		{
			RoundedButton injectBtn = _injectBtn;
			RoundedButton pubLobbyBtn = _pubLobbyBtn;
			SegmentedToggle versionToggle = _versionToggle;
			bool flag = (_switchBtn.Visible = false);
			bool flag3 = (versionToggle.Visible = flag);
			bool visible = (pubLobbyBtn.Visible = flag3);
			injectBtn.Visible = visible;
			if (_settingsOpen)
			{
				_home.Visible = false;
				_settings.Bounds = new Rectangle(0, 0, Width, Height);
				if (!_settings.Visible)
				{
					_settings.Visible = true;
				}
				return;
			}
			_settings.Visible = false;
			_home.Bounds = new Rectangle(0, 0, Width, Height);
			if (!_home.Visible)
			{
				_home.Visible = true;
				_home.Replay();
			}
			_home.RefreshState();
			return;
		}
		_home.Visible = false;
		_settings.Visible = false;
		bool flag6 = _selected.Id == GameId.GTAV;
		int y = Height - 88;
		_injectBtn.SetBounds(40, y, 220, 46);
		_injectBtn.Visible = true;
		_pubLobbyBtn.SetBounds(270, y, 130, 46);
		bool flag7 = flag6 && (_selected.SelectedMod == GtaMod.SylentMenu || _selected.SelectedMod == GtaMod.Aurora);
		_pubLobbyBtn.Visible = flag7;
		if (flag7)
		{
			RefreshPubLobbyButton();
		}
		using (Graphics g = CreateGraphics())
		{
			Column column = MeasureColumn(g);
			_versionToggle.SetBounds(40, (int)(column.TagY - 8f), 188, 28);
			_switchBtn.SetBounds(40 + ColW - 88, (int)(column.ProductY + 11f - 13f), 88, 26);
		}
		_versionToggle.Visible = flag6;
		_switchBtn.Visible = flag6;
		RefreshInjectState();
		if (flag6)
		{
			RefreshVersionToggle();
		}
		Invalidate();
	}

	private Bitmap GetBackground(GameId id, Size size)
	{
		lock (_bgLock)
		{
			if (_bgSize != size)
			{
				foreach (Bitmap value2 in _bgCache.Values)
				{
					value2.Dispose();
				}
				_bgCache.Clear();
				_bgSize = size;
			}
			if (!_bgCache.TryGetValue(id, out Bitmap value))
			{
				Image src = ((id == GameId.GTAV) ? ImageLoader.GetGtaBackground() : ImageLoader.GetRdr2Background());
				value = (_bgCache[id] = BackgroundComposer.Compose(src, size));
			}
			return value;
		}
	}

	public void WarmBackgrounds()
	{
		Size size = new Size(Width, Height);
		if (size.Width < 100 || size.Height < 100)
		{
			return;
		}
		Task.Run(() =>
		{
			try
			{
				GameId[] array = new GameId[2]
				{
					GameId.GTAV,
					GameId.RDR2
				};
				foreach (GameId id in array)
				{
					GetBackground(id, size);
				}
			}
			catch
			{
			}
		});
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_fadePump?.Dispose();
			_statusTimer.Dispose();
			_fadeOld?.Dispose();
			_fadeNew?.Dispose();
			lock (_bgLock)
			{
				foreach (Bitmap value in _bgCache.Values)
				{
					value.Dispose();
				}
				_bgCache.Clear();
			}
		}
		base.Dispose(disposing);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		if (Width == 0 || Height == 0)
		{
			base.OnPaintBackground(e);
		}
		else if (_transitioning)
		{
			graphics.Clear(Theme.Black);
		}
		else if (_selected != null)
		{
			graphics.DrawImageUnscaled(GetBackground(_selected.Id, new Size(Width, Height)), 0, 0);
		}
		else
		{
			graphics.Clear(Theme.Black);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		if (Width != 0)
		{
			if (_transitioning)
			{
				PaintFade(graphics);
			}
			else if (_selected != null)
			{
				PaintDetail(graphics);
			}
		}
	}

	private void PaintDetail(Graphics g)
	{
		GameProfile selected = _selected;
		bool flag = selected.Id == GameId.GTAV;
		Product product = ProductOf(selected);
		Column column = MeasureColumn(g);
		Color color = ProductAccent(product);
		using (Font font = new Font("Segoe UI", 30f, FontStyle.Bold))
		{
			RectangleF layoutRectangle = new RectangleF(38f, column.TitleY, ColW, column.TitleH + 8f);
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
			{
				g.DrawString(selected.DisplayName, font, brush, new RectangleF(layoutRectangle.X + 1f, layoutRectangle.Y + 2f, layoutRectangle.Width, layoutRectangle.Height));
			}
			using SolidBrush brush2 = new SolidBrush(TextCol);
			g.DrawString(selected.DisplayName, font, brush2, layoutRectangle);
		}
		string s = (flag ? ModLabelFull(selected.SelectedMod) : "RDR2 Mod");
		string s2 = ((flag && ModDesc.TryGetValue(selected.SelectedMod, out string value)) ? value : "Mod injection for Red Dead Redemption 2");
		using (SolidBrush brush3 = new SolidBrush(color))
		{
			g.FillEllipse(brush3, 40f, column.ProductY + 6f, 9f, 9f);
		}
		using (Font font2 = new Font("Segoe UI", 12f, FontStyle.Bold))
		{
			using SolidBrush brush4 = new SolidBrush(TextCol);
			g.DrawString(s, font2, brush4, new PointF(58f, column.ProductY));
		}
		using (Font font3 = new Font("Segoe UI", 9f))
		{
			using SolidBrush brush5 = new SolidBrush(DimCol);
			g.DrawString(s2, font3, brush5, new PointF(59f, column.ProductY + 24f));
		}
		using (Pen pen = new Pen(Color.FromArgb(90, 120, 120, 135), 1f))
		{
			g.DrawLine(pen, 40f, column.RuleY, 40 + ColW, column.RuleY);
		}
		PaintRow(g, column.RowsY, "LICENSE", LicenseNote(product), Entitlements.IsUnlocked(product) ? SuccessCol : Theme.Error, Entitlements.IsUnlocked(product));
		string text = selected.ProcessName + ".exe";
		string text2;
		if (_running)
		{
			text2 = text + " · running";
		}
		else
		{
			text2 = ((_otherRunning != null) ? (text + " · not running — " + _otherRunning + ".exe is (switch Build)") : (text + " · not running"));
		}
		MainPanel mainPanel = this;
		Graphics g2 = g;
		float y = column.RowsY + 36f;
		string value2 = text2;
		Color valueCol;
		if (_running)
		{
			valueCol = SuccessCol;
		}
		else
		{
			valueCol = ((_otherRunning != null) ? Color.FromArgb(232, 184, 92) : DimCol);
		}
		mainPanel.PaintRow(g2, y, "GAME", value2, valueCol, _running);
		if (string.IsNullOrEmpty(_feedbackText))
		{
			return;
		}
		using Font font4 = new Font("Segoe UI", 8.5f);
		using SolidBrush brush6 = new SolidBrush(_feedbackCol);
		using StringFormat format = new StringFormat(StringFormat.GenericTypographic)
		{
			LineAlignment = StringAlignment.Center,
			Trimming = StringTrimming.EllipsisCharacter,
			FormatFlags = StringFormatFlags.NoWrap
		};
		using (SolidBrush brush7 = new SolidBrush(_feedbackCol))
		{
			g.FillEllipse(brush7, 41, Height - 27, 6, 6);
		}
		g.DrawString(_feedbackText, font4, brush6, new RectangleF(54f, Height - 32, ColW + 60, 16f), format);
	}

	private void PaintRow(Graphics g, float y, string label, string? value, Color valueCol, bool ok)
	{
		DrawMark(g, 40f, y + 9f, ok);
		using (Font f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
		{
			using SolidBrush b = new SolidBrush(SubCol);
			DrawSpacedLeft(g, label, f, b, 68f, y + 11f, 2.2f);
		}
		if (value == null)
		{
			return;
		}
		using Font font = new Font("Segoe UI", 9.5f);
		using SolidBrush brush = new SolidBrush(valueCol);
		g.DrawString(value, font, brush, new PointF(158f, y + 8f));
	}

	private static void DrawMark(Graphics g, float x, float y, bool ok)
	{
		RectangleF rect = new RectangleF(x, y, 16f, 16f);
		if (ok)
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(38, SuccessCol)))
			{
				g.FillEllipse(brush, rect);
			}
			using (Pen pen = new Pen(SuccessCol, 1.3f))
			{
				g.DrawEllipse(pen, rect);
			}
			using Pen pen2 = new Pen(SuccessCol, 1.8f)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round,
				LineJoin = LineJoin.Round
			};
			g.DrawLines(pen2, new PointF[3]
			{
				new PointF(x + 4.4f, y + 8.4f),
				new PointF(x + 7f, y + 10.9f),
				new PointF(x + 11.7f, y + 5.5f)
			});
			return;
		}
		using (Pen pen3 = new Pen(Theme.BorderHi, 1.3f))
		{
			g.DrawEllipse(pen3, rect);
		}
		using SolidBrush brush2 = new SolidBrush(Theme.Faint);
		g.FillEllipse(brush2, x + 5.5f, y + 5.5f, 5f, 5f);
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

	private static Color ProductAccent(Product p)
	{
		return p switch
		{
			Product.Menu => Color.FromArgb(65, 190, 140), 
			Product.Recovery => Theme.Accent, 
			Product.Aurora => Color.FromArgb(214, 124, 72), 
			Product.Rdr2 => Color.FromArgb(214, 84, 76), 
			_ => Theme.Accent, 
		};
	}

	private void SetVersion(GtaVersion ver)
	{
		if (_selected != null && (ver != GtaVersion.Legacy || _selected.SelectedMod != GtaMod.Aurora))
		{
			_selected.GtaVersion = ver;
			_running = false;
			_otherRunning = null;
			RefreshRunning();
			RefreshVersionToggle();
			RefreshInjectState();
			RefreshPubLobbyButton();
			Invalidate();
		}
	}

	private void RefreshVersionToggle()
	{
		if (_selected != null)
		{
			_versionToggle.SelectedIndex = ((_selected.GtaVersion != GtaVersion.Legacy) ? 1 : 0);
			_versionToggle.Disabled.Clear();
			if (_selected.SelectedMod == GtaMod.Aurora)
			{
				_versionToggle.Disabled.Add(0);
			}
			_versionToggle.Invalidate();
		}
	}

	private static bool TryDeepFindAndCache(GameProfile p)
	{
		string text = GameDetector.DeepFind(p.ActiveExecutable);
		if (text == null)
		{
			return false;
		}
		if (p.Id == GameId.RDR2)
		{
			AppSettings.CachedRdr2Path = text;
			p.InstallPath = text;
		}
		else if (p.GtaVersion == GtaVersion.Enhanced)
		{
			AppSettings.CachedGtaVEnhancedPath = text;
			p.EnhancedInstallPath = text;
		}
		else
		{
			AppSettings.CachedGtaVLegacyPath = text;
			p.InstallPath = text;
		}
		return true;
	}

	private void RefreshInjectState()
	{
		if (_selected != null)
		{
			bool flag = !string.IsNullOrEmpty(_selected.EmbeddedDll1Name);
			_injectBtn.Enabled = flag;
			_injectBtn.Text = (flag ? "Inject" : "DLL not configured");
			_injectBtn.Icon = (flag ? ButtonIcon.Play : ButtonIcon.None);
			_injectBtn.BackColor = (flag ? Theme.AccentTop : Theme.SurfaceHi);
			_injectBtn.GradientEnd = (flag ? Theme.AccentBot : Color.Empty);
			_injectBtn.HoverBackColor = (flag ? Theme.AccentHover : Theme.SurfaceHi);
			_injectBtn.HoverGradientEnd = (flag ? Theme.AccentHoverBot : Color.Empty);
			_injectBtn.BorderColor = (flag ? Theme.AccentBorder : Theme.Border);
			_injectBtn.ForeColor = (flag ? Color.White : SubCol);
		}
	}

	private async void DoInject()
	{
		if (_selected == null)
		{
			return;
		}
		string res = _selected.EmbeddedDll1Name;
		if (string.IsNullOrEmpty(res))
		{
			Feedback("DLL not configured.", SubCol);
			return;
		}
		string proc = _selected.ProcessName;
		if (!(await Task.Run(() => Injector.IsRunning(proc))))
		{
			Feedback("Start " + proc + ".exe first, then hit Inject at the main menu.", SubCol);
			return;
		}
		_injectBtn.Enabled = false;
		Feedback("Verifying license…", Theme.Dim);
		(bool, string) tuple;
		try
		{
			tuple = await Task.Run(() => Injector.InjectEmbedded(proc, res));
		}
		catch (Exception)
		{
			tuple = (false, "Inject failed — please try again.");
		}
		RefreshInjectState();
		RefreshRunning();
		Feedback(tuple.Item2, tuple.Item1 ? SuccessCol : Theme.Error);
		if (tuple.Item1)
		{
			await CloseIfWantedAsync();
		}
	}

	public async void OnAutoInjectFinished(bool ok, string message)
	{
		Feedback(message, ok ? SuccessCol : Theme.Error);
		RefreshRunning();
		RefreshInjectState();
		if (ok)
		{
			await CloseIfWantedAsync();
		}
	}

	private static async Task CloseIfWantedAsync()
	{
		if (AppSettings.CloseAfterInject)
		{
			await Task.Delay(1400);
			Application.Exit();
		}
	}

	private static string? StoredBypassFor(GameProfile p)
	{
		if (p.GtaVersion != GtaVersion.Enhanced)
		{
			return AppSettings.PubLobbyBypassPathLegacy;
		}
		return AppSettings.PubLobbyBypassPathEnhanced;
	}

	private static void SetStoredBypassFor(GameProfile p, string? path)
	{
		if (p.GtaVersion == GtaVersion.Enhanced)
		{
			AppSettings.PubLobbyBypassPathEnhanced = path;
		}
		else
		{
			AppSettings.PubLobbyBypassPathLegacy = path;
		}
	}

	private bool BypassInstalled(out string path)
	{
		path = "";
		if (_selected == null || _selected.Id != GameId.GTAV)
		{
			return false;
		}
		string text = StoredBypassFor(_selected);
		if (!string.IsNullOrEmpty(text) && File.Exists(text))
		{
			path = text;
			return true;
		}
		string currentInstallPath = _selected.CurrentInstallPath;
		if (!string.IsNullOrEmpty(currentInstallPath))
		{
			string text2 = Path.Combine(currentInstallPath, "WINMM.dll");
			if (File.Exists(text2))
			{
				path = text2;
				return true;
			}
		}
		return false;
	}

	private void RefreshPubLobbyButton()
	{
		if (BypassInstalled(out string _))
		{
			_pubLobbyBtn.Text = "Delete Bypass";
			_pubLobbyBtn.ForeColor = Theme.Error;
		}
		else
		{
			_pubLobbyBtn.Text = "Pub Lobby";
			_pubLobbyBtn.ForeColor = Theme.Text;
		}
	}

	private async void DoPubLobby()
	{
		if (_selected == null)
		{
			return;
		}
		if (BypassInstalled(out string path))
		{
			try
			{
				File.Delete(path);
				SetStoredBypassFor(_selected, null);
				RefreshPubLobbyButton();
				Feedback("Bypass tool removed.", SuccessCol);
				return;
			}
			catch (Exception)
			{
				Feedback("Could not delete — close GTA V and try again, or run as administrator.", Theme.Error);
				return;
			}
		}
		string exeName = ((_selected.GtaVersion == GtaVersion.Enhanced) ? "GTA5_Enhanced.exe" : "GTA5.exe");
		if (!_selected.IsCurrentVersionInstalled)
		{
			_pubLobbyBtn.Enabled = false;
			Feedback("Searching all drives for " + exeName + " (up to 20 s)…", Theme.Dim);
			bool flag = await Task.Run(() => TryDeepFindAndCache(_selected));
			_pubLobbyBtn.Enabled = true;
			if (!flag)
			{
				using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
				{
					Description = "Couldn't find " + exeName + " — pick your GTA V folder",
					UseDescriptionForTitle = true,
					ShowNewFolderButton = false
				};
				if (folderBrowserDialog.ShowDialog() != DialogResult.OK)
				{
					Feedback("Cancelled — no folder picked.", SubCol);
					return;
				}
				if (_selected.GtaVersion == GtaVersion.Enhanced)
				{
					_selected.EnhancedInstallPath = folderBrowserDialog.SelectedPath;
				}
				else
				{
					_selected.InstallPath = folderBrowserDialog.SelectedPath;
				}
			}
		}
		string currentInstallPath = _selected.CurrentInstallPath;
		var (flag2, msg) = Injector.ExtractToDirectory(PayloadTokens.ResourceName("a2d40f63"), "WINMM.dll", currentInstallPath);
		if (flag2)
		{
			SetStoredBypassFor(_selected, Path.Combine(currentInstallPath, "WINMM.dll"));
			WriteFslSettings();
			RefreshPubLobbyButton();
			Feedback("Bypass installed next to " + exeName + ".", SuccessCol);
		}
		else
		{
			Feedback(msg, Theme.Error);
		}
	}

	private static void WriteFslSettings()
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FSL");
			Directory.CreateDirectory(text);
			File.WriteAllText(Path.Combine(text, "settings.ini"), "[Global]\r\nBlockTelemetry=true\r\n\r\n[GTA]\r\nUseLocalSaves=false\r\nEnableMoneyCheatCodes=true\r\nUnlockGTAPlus=true\r\nBypassBattlEye=true\r\nUnlockCESP=true\r\nEnableBackups=true\r\n");
			string path = Path.Combine(text, "settings.txt");
			if (File.Exists(path))
			{
				try
				{
					File.Delete(path);
					return;
				}
				catch
				{
					return;
				}
			}
		}
		catch
		{
		}
	}

	private void Feedback(string msg, Color col)
	{
		_feedbackText = msg;
		_feedbackCol = col;
		Invalidate();
	}

	private static string ModLabelFull(GtaMod m)
	{
		return m switch
		{
			GtaMod.SylentMenu => "Sylent Menu", 
			GtaMod.SylentRecovery => "Sylent Recovery", 
			GtaMod.Aurora => "Aurora", 
			_ => "", 
		};
	}
}
