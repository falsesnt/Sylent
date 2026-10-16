using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SylentLoader.Services;
using SylentLoader.Services.Auth;

namespace SylentLoader.Controls;

public sealed class SettingsView : Control
{
	private sealed record Pref(string Title, string Desc, Func<bool> Get, Action<bool> Set);

	private readonly record struct Geo(int NameY, int StatusY, int PillY, int PillH, int BtnY, int BtnH);

	private const int PadX = 40;

	private const int RowH = 44;

	private const int RowsTop = 152;

	private const int ColGap = 32;

	private readonly Pref[] _prefs;

	private readonly ToggleSwitch[] _toggles;

	private static readonly (Product Product, string Name)[] Accounts = new (Product, string)[5]
	{
		(Product.Menu, "Sylent Menu"),
		(Product.Recovery, "Sylent Recovery"),
		(Product.Aurora, "Aurora"),
		(Product.Rdr2, "Red Dead 2"),
		(Product.Pack, "Full Access")
	};

	private int[] _rows = new int[4] { 0, 1, 2, 3 };

	private const int KeyRowMin = 62;

	private const int KeyRowMax = 110;

	private const int PlainRowH = 36;

	private const int CopyW = 48;

	private const int ForgetW = 54;

	private readonly bool[] _hasKey = new bool[Accounts.Length];

	private int _keyH = 62;

	private static readonly Color CardBack = Color.FromArgb(9, 9, 12);

	private readonly RoundedButton[] _forget;

	private readonly RoundedButton[] _copy;

	private readonly RoundedButton _signOutAll;

	private readonly Bitmap?[] _blur = new Bitmap[Accounts.Length];

	private readonly Timer _copiedTimer = new Timer
	{
		Interval = 1600
	};

	private const int RevealMs = 8000;

	private readonly float[] _reveal = new float[Accounts.Length];

	private readonly bool[] _revealTarget = new bool[Accounts.Length];

	private readonly string?[] _revealKey = new string[Accounts.Length];

	private readonly long[] _revealAt = new long[Accounts.Length];

	private readonly (Font Font, string Text, int Width)?[] _fit = new (Font, string, int)?[Accounts.Length];

	private readonly string?[] _pillNote = new string[Accounts.Length];

	private int _hover = -1;

	private readonly FramePump _pump;

	private readonly Stopwatch _clock = Stopwatch.StartNew();

	private long _lastFrame;

	private readonly Timer _autoHide = new Timer
	{
		Interval = 500
	};

	private const int CardTop = 174;

	private static readonly Font TagF = new Font("Segoe UI", 8.5f, FontStyle.Bold);

	private static readonly Font TitleF = new Font("Segoe UI", 28f, FontStyle.Bold);

	private static readonly Font HintF = new Font("Segoe UI", 10f);

	private static readonly Font RowT = new Font("Segoe UI", 10f, FontStyle.Bold);

	private static readonly Font RowD = new Font("Segoe UI", 8f);

	private static readonly Font RowTBig = new Font("Segoe UI", 11f, FontStyle.Bold);

	private static readonly Font RowDBig = new Font("Segoe UI", 8.5f);

	private static readonly Font SmallB = new Font("Segoe UI", 7.5f, FontStyle.Bold);

	private static readonly Font BlurF = new Font("Consolas", 9f, FontStyle.Bold);

	private static readonly Font BlurBig = new Font("Consolas", 10.5f, FontStyle.Bold);

	private static readonly Font HintS = new Font("Segoe UI", 7.5f, FontStyle.Bold);

	private bool Roomy => _keyH >= 90;

	private int LeftW => Math.Min(380, (Width - 80 - 32) * 55 / 100);

	private int RightX => 40 + LeftW + 32;

	private int RightW => Width - RightX - 40;

	public event Action? AccountsChanged;

	public event Action? SignOutRequested;

	private int HeightOfRow(int r)
	{
		if (!_hasKey[_rows[r]])
		{
			return 36;
		}
		return _keyH;
	}

	private int OffsetOfRow(int r)
	{
		int num = 0;
		for (int i = 0; i < r; i++)
		{
			num += HeightOfRow(i);
		}
		return num;
	}

	private void ComputeRows()
	{
		int pack = Array.FindIndex(Accounts, ((Product Product, string Name) a) => a.Product == Product.Pack);
		(bool Signed, bool Saved, DateTimeOffset? Expires) tuple = Entitlements.SlotInfo(Product.Pack);
		bool item = tuple.Signed;
		bool item2 = tuple.Saved;
		_rows = ((!(item | item2)) ? (from i in Enumerable.Range(0, Accounts.Length)
			where i != pack
			select i).ToArray() : new int[1] { pack });
	}

	private void ComputeMetrics()
	{
		int num = 0;
		int num2 = 0;
		int[] rows = _rows;
		foreach (int num3 in rows)
		{
			if (_hasKey[num3])
			{
				num++;
			}
			else
			{
				num2++;
			}
		}
		int num4 = Math.Max(0, Height - 174 - 16) - num2 * 36;
		_keyH = ((num == 0) ? 62 : Math.Clamp(num4 / num, 62, 110));
	}

	private static Geo GeoFor(int h)
	{
		float num = Math.Clamp((float)(h - 62) / 48f, 0f, 1f);
		int num2 = (int)Math.Round(19f + 12f * num);
		int num3 = (int)Math.Round(5f + 10f * num);
		int statusY = num3 + 17 + (int)Math.Round(4f * num);
		int num4 = (int)Math.Round(5f + 14f * num);
		int btnH = (int)Math.Round(20f + 6f * num);
		return new Geo(num3, statusY, h - num2 - num4, num2, num3 - 1 - (int)Math.Round(3f * num), btnH);
	}

	private Rectangle PillRect(int r)
	{
		int num = 174 + OffsetOfRow(r);
		Geo geo = GeoFor(_keyH);
		return new Rectangle(RightX + 14, num + geo.PillY, RightW - 28, geo.PillH);
	}

	public SettingsView()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Black;
		_pump = new FramePump(this, OnRevealFrame);
		_autoHide.Tick += (object? _, EventArgs _) =>
		{
			AutoHide();
		};
		_prefs = new Pref[4]
		{
			new Pref("Auto inject", "Injects automatically at the GTA V main menu.", () => AppSettings.AutoInject, (bool v) =>
			{
				AppSettings.AutoInject = v;
			}),
			new Pref("Close after injecting", "Exit the loader once a payload has been injected.", () => AppSettings.CloseAfterInject, (bool v) =>
			{
				AppSettings.CloseAfterInject = v;
			}),
			new Pref("Sign in saved keys at launch", "Log in silently so pages open instantly.", () => AppSettings.AutoSignIn, (bool v) =>
			{
				AppSettings.AutoSignIn = v;
			}),
			new Pref("Remember keys by default", "Pre-tick “Remember on this PC” in the key dialog.", () => AppSettings.RememberKeys, (bool v) =>
			{
				AppSettings.RememberKeys = v;
			})
		};
		_toggles = new ToggleSwitch[_prefs.Length];
		for (int num = 0; num < _prefs.Length; num++)
		{
			int idx = num;
			ToggleSwitch t = new ToggleSwitch
			{
				Checked = _prefs[num].Get()
			};
			t.CheckedChanged += (object? _, EventArgs _) =>
			{
				_prefs[idx].Set(t.Checked);
			};
			_toggles[num] = t;
			Controls.Add(t);
		}
		_forget = new RoundedButton[Accounts.Length];
		_copy = new RoundedButton[Accounts.Length];
		for (int num2 = 0; num2 < Accounts.Length; num2++)
		{
			Product product = Accounts[num2].Product;
			int acct = num2;
			RoundedButton roundedButton = new RoundedButton
			{
				Text = "Forget",
				BackColor = Color.FromArgb(230, 8, 8, 10),
				HoverBackColor = Theme.SurfaceHi,
				BorderColor = Theme.BorderHi,
				ForeColor = Theme.Dim,
				Font = new Font("Segoe UI", 8f),
				CornerRadius = 10,
				CornerFill = CardBack,
				Cursor = Cursors.Hand,
				Visible = false
			};
			roundedButton.Click += async (object? _, EventArgs _) =>
			{
				await Task.Run(() =>
				{
					Entitlements.Forget(product);
				});
				RefreshState();
				AccountsChanged?.Invoke();
			};
			_forget[num2] = roundedButton;
			Controls.Add(roundedButton);
			RoundedButton roundedButton2 = new RoundedButton
			{
				Text = "Copy",
				BackColor = Color.FromArgb(230, 8, 8, 10),
				HoverBackColor = Theme.SurfaceHi,
				BorderColor = Theme.BorderHi,
				ForeColor = Theme.Text,
				Font = new Font("Segoe UI", 8f),
				CornerRadius = 10,
				CornerFill = CardBack,
				Cursor = Cursors.Hand,
				Visible = false
			};
			roundedButton2.Click += (object? _, EventArgs _) =>
			{
				CopyKey(acct);
			};
			_copy[num2] = roundedButton2;
			Controls.Add(roundedButton2);
		}
		_copiedTimer.Tick += (object? _, EventArgs _) =>
		{
			_copiedTimer.Stop();
			ResetCopyButtons();
			ClearNotes();
		};
		_signOutAll = new RoundedButton
		{
			Text = "Sign out of everything",
			BackColor = Color.FromArgb(230, 8, 8, 10),
			HoverBackColor = Color.FromArgb(48, 24, 28),
			BorderColor = Color.FromArgb(120, 70, 74),
			ForeColor = Color.FromArgb(232, 140, 148),
			Font = new Font("Segoe UI", 8.5f),
			CornerRadius = 10,
			Cursor = Cursors.Hand
		};
		_signOutAll.Click += (object? _, EventArgs _) =>
		{
			SignOutRequested?.Invoke();
		};
		Controls.Add(_signOutAll);
		VisibleChanged += (object? _, EventArgs _) =>
		{
			if (Visible)
			{
				RefreshState();
			}
			else
			{
				HideAllNow();
			}
		};
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_pump.Dispose();
			_autoHide.Dispose();
			_copiedTimer.Dispose();
			Bitmap[] blur = _blur;
			for (int i = 0; i < blur.Length; i++)
			{
				blur[i]?.Dispose();
			}
			(Font, string, int)?[] fit = _fit;
			for (int i = 0; i < fit.Length; i++)
			{
				fit[i]?.Item1.Dispose();
			}
		}
		base.Dispose(disposing);
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		DoLayout();
	}

	private void DoLayout()
	{
		if (Width < 300)
		{
			return;
		}
		ComputeMetrics();
		for (int i = 0; i < _toggles.Length; i++)
		{
			_toggles[i].Location = new Point(40 + LeftW - _toggles[i].Width, 152 + i * 44 + (44 - _toggles[i].Height) / 2 - 2);
		}
		_signOutAll.SetBounds(40, 152 + _prefs.Length * 44 + 10, LeftW, 30);
		Geo geo = GeoFor(_keyH);
		for (int j = 0; j < _rows.Length; j++)
		{
			int num = _rows[j];
			if (_hasKey[num])
			{
				int y = 174 + OffsetOfRow(j) + geo.BtnY;
				int num2 = RightX + RightW - 12 - 54;
				_forget[num].SetBounds(num2, y, 54, geo.BtnH);
				_copy[num].SetBounds(num2 - 6 - 48, y, 48, geo.BtnH);
			}
		}
	}

	public void RefreshState()
	{
		HideAllNow();
		ComputeRows();
		for (int i = 0; i < _toggles.Length; i++)
		{
			_toggles[i].Checked = _prefs[i].Get();
		}
		for (int j = 0; j < Accounts.Length; j++)
		{
			var (flag, flag2, _) = Entitlements.SlotInfo(Accounts[j].Product);
			_hasKey[j] = flag | flag2;
			bool visible = Enumerable.Contains(_rows, j) && _hasKey[j];
			_forget[j].Visible = visible;
			_copy[j].Visible = visible;
		}
		ResetCopyButtons();
		DoLayout();
		Invalidate();
	}

	private void CopyKey(int acct)
	{
		string text = Entitlements.KeyOf(Accounts[acct].Product);
		bool flag = !string.IsNullOrEmpty(text) && TryCopySecret(text);
		_copy[acct].Text = (flag ? "Copied" : "Failed");
		_copy[acct].ForeColor = (flag ? Theme.Success : Theme.Error);
		_copy[acct].Invalidate();
		_copiedTimer.Stop();
		_copiedTimer.Start();
	}

	private void ResetCopyButtons()
	{
		RoundedButton[] copy = _copy;
		foreach (RoundedButton roundedButton in copy)
		{
			if (!(roundedButton.Text == "Copy"))
			{
				roundedButton.Text = "Copy";
				roundedButton.ForeColor = Theme.Text;
				roundedButton.Invalidate();
			}
		}
	}

	private void ClearNotes()
	{
		bool flag = false;
		for (int i = 0; i < _pillNote.Length; i++)
		{
			if (_pillNote[i] != null)
			{
				_pillNote[i] = null;
				flag = true;
			}
		}
		if (flag)
		{
			Invalidate();
		}
	}

	private static bool TryCopySecret(string text)
	{
		try
		{
			DataObject dataObject = new DataObject();
			dataObject.SetText(text);
			dataObject.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[4]));
			dataObject.SetData("CanIncludeInClipboardHistory", new MemoryStream(new byte[4]));
			dataObject.SetData("CanUploadToCloudClipboard", new MemoryStream(new byte[4]));
			Clipboard.SetDataObject(dataObject, copy: true, 5, 60);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private int BoxAt(Point p)
	{
		for (int i = 0; i < _rows.Length; i++)
		{
			if (_hasKey[_rows[i]] && PillRect(i).Contains(p))
			{
				return _rows[i];
			}
		}
		return -1;
	}

	private Rectangle BoxOf(int acct)
	{
		for (int i = 0; i < _rows.Length; i++)
		{
			if (_rows[i] == acct)
			{
				return PillRect(i);
			}
		}
		return Rectangle.Empty;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		int num = BoxAt(e.Location);
		Cursor = ((num >= 0) ? Cursors.Hand : Cursors.Default);
		SetHover(num);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		SetHover(-1);
	}

	private void SetHover(int acct)
	{
		if (acct != _hover)
		{
			int hover = _hover;
			_hover = acct;
			if (hover >= 0)
			{
				Invalidate(Rectangle.Inflate(BoxOf(hover), 2, 2));
			}
			if (acct >= 0)
			{
				Invalidate(Rectangle.Inflate(BoxOf(acct), 2, 2));
			}
		}
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		if (e.Button == MouseButtons.Left)
		{
			int num = BoxAt(e.Location);
			if (num >= 0)
			{
				ToggleReveal(num);
			}
		}
	}

	private void ToggleReveal(int acct)
	{
		if (_revealTarget[acct])
		{
			_revealTarget[acct] = false;
			StartAnimation();
			return;
		}
		string text = Entitlements.KeyOf(Accounts[acct].Product);
		if (string.IsNullOrEmpty(text))
		{
			_pillNote[acct] = "Can't read this key";
			_copiedTimer.Stop();
			_copiedTimer.Start();
			Invalidate(BoxOf(acct));
		}
		else
		{
			_revealKey[acct] = text;
			_revealTarget[acct] = true;
			_revealAt[acct] = _clock.ElapsedMilliseconds;
			_autoHide.Start();
			StartAnimation();
		}
	}

	private void AutoHide()
	{
		bool flag = false;
		long elapsedMilliseconds = _clock.ElapsedMilliseconds;
		for (int i = 0; i < _revealTarget.Length; i++)
		{
			if (_revealTarget[i])
			{
				if (elapsedMilliseconds - _revealAt[i] >= 8000)
				{
					_revealTarget[i] = false;
					StartAnimation();
				}
				else
				{
					flag = true;
				}
			}
		}
		if (!flag)
		{
			_autoHide.Stop();
		}
	}

	private void StartAnimation()
	{
		_lastFrame = _clock.ElapsedMilliseconds;
		if (!_pump.Running)
		{
			_pump.Start();
		}
	}

	private void OnRevealFrame()
	{
		long elapsedMilliseconds = _clock.ElapsedMilliseconds;
		float num = Math.Max(0f, (float)(elapsedMilliseconds - _lastFrame) / 180f);
		_lastFrame = elapsedMilliseconds;
		bool flag = false;
		for (int i = 0; i < _reveal.Length; i++)
		{
			float num2 = (_revealTarget[i] ? 1f : 0f);
			if (_reveal[i] != num2)
			{
				_reveal[i] = ((num2 > _reveal[i]) ? Math.Min(1f, _reveal[i] + num) : Math.Max(0f, _reveal[i] - num));
				if (_reveal[i] == 0f)
				{
					ForgetRevealed(i);
				}
				else
				{
					flag = true;
				}
				Invalidate(Rectangle.Inflate(BoxOf(i), 2, 2));
			}
		}
		if (!flag)
		{
			_pump.Stop();
		}
	}

	private void ForgetRevealed(int acct)
	{
		_revealKey[acct] = null;
		_fit[acct]?.Font.Dispose();
		_fit[acct] = null;
	}

	private void HideAllNow()
	{
		_pump.Stop();
		_autoHide.Stop();
		for (int i = 0; i < _reveal.Length; i++)
		{
			_revealTarget[i] = false;
			_reveal[i] = 0f;
			_pillNote[i] = null;
			ForgetRevealed(i);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		graphics.Clear(Theme.Black);
		if (Width < 300)
		{
			return;
		}
		using (GraphicsPath graphicsPath = new GraphicsPath())
		{
			graphicsPath.AddEllipse(-120, -150, 560, 340);
			PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
			pathGradientBrush.CenterColor = Color.FromArgb(34, Theme.Accent);
			pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, Theme.Accent) };
			using PathGradientBrush brush = pathGradientBrush;
			graphics.FillPath(brush, graphicsPath);
		}
		using (SolidBrush b = new SolidBrush(Theme.Accent))
		{
			DrawSpaced(graphics, "SETTINGS", TagF, b, 41f, 40f, 3.2f);
		}
		using (SolidBrush brush2 = new SolidBrush(Theme.Text))
		{
			graphics.DrawString("Preferences", TitleF, brush2, new PointF(38f, 58f));
		}
		using (SolidBrush brush3 = new SolidBrush(Theme.Dim))
		{
			graphics.DrawString("Changes save automatically.", HintF, brush3, new PointF(40f, 112f));
		}
		using (SolidBrush brush4 = new SolidBrush(Theme.Accent))
		{
			graphics.FillRectangle(brush4, 40, 142, 36, 2);
		}
		PaintPrefs(graphics);
		PaintAccounts(graphics);
	}

	private void PaintPrefs(Graphics g)
	{
		using SolidBrush brush = new SolidBrush(Theme.Text);
		using SolidBrush brush2 = new SolidBrush(Theme.Sub);
		using Pen pen = new Pen(Color.FromArgb(70, 120, 120, 135), 1f);
		for (int i = 0; i < _prefs.Length; i++)
		{
			int num = 152 + i * 44;
			g.DrawString(_prefs[i].Title, RowT, brush, new PointF(40f, num + 2));
			g.DrawString(_prefs[i].Desc, RowD, brush2, new RectangleF(40f, num + 21, LeftW - 58, 20f));
			if (i < _prefs.Length - 1)
			{
				g.DrawLine(pen, 40, num + 44 - 3, 40 + LeftW, num + 44 - 3);
			}
		}
	}

	private void PaintAccounts(Graphics g)
	{
		using (SolidBrush b = new SolidBrush(Theme.Sub))
		{
			DrawSpaced(g, "KEYS ON THIS PC", SmallB, b, RightX + 2, 154f, 2.4f);
		}
		int num = OffsetOfRow(_rows.Length);
		using (GraphicsPath path = Rounded(new RectangleF((float)RightX + 0.5f, 174.5f, (float)RightW - 1f, (float)num - 1f), 12f))
		{
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(230, 10, 10, 13)))
			{
				g.FillPath(brush, path);
			}
			using Pen pen = new Pen(Theme.Border, 1f);
			g.DrawPath(pen, path);
		}
		using SolidBrush brush2 = new SolidBrush(Theme.Text);
		using Pen pen2 = new Pen(Color.FromArgb(60, 120, 120, 135), 1f);
		using StringFormat format = new StringFormat(StringFormat.GenericTypographic)
		{
			FormatFlags = StringFormatFlags.NoWrap,
			Trimming = StringTrimming.EllipsisCharacter
		};
		Geo geo = GeoFor(_keyH);
		for (int i = 0; i < _rows.Length; i++)
		{
			int num2 = _rows[i];
			int num3 = 174 + OffsetOfRow(i);
			int num4 = HeightOfRow(i);
			(Product Product, string Name) tuple = Accounts[num2];
			Product item = tuple.Product;
			string item2 = tuple.Name;
			(bool Signed, bool Saved, DateTimeOffset? Expires) tuple2 = Entitlements.SlotInfo(item);
			bool item3 = tuple2.Signed;
			bool item4 = tuple2.Saved;
			DateTimeOffset? item5 = tuple2.Expires;
			bool flag = item == Product.Pack;
			string text2;
			Color color;
			if (item3)
			{
				string text;
				if (item5.HasValue)
				{
					text = (Entitlements.IsLifetime(item5.Value) ? " · lifetime" : $" · until {item5.Value:yyyy-MM-dd}");
				}
				else
				{
					text = "";
				}
				if (flag)
				{
					text2 = "All products" + text;
				}
				else
				{
					text2 = ((!item5.HasValue) ? "Signed in" : ("Licensed" + text));
				}
				if (!item4)
				{
					text2 += " · not saved";
				}
				color = Theme.Success;
			}
			else if (item4)
			{
				text2 = (flag ? "Key saved · all products" : "Key saved");
				color = Theme.Accent;
			}
			else
			{
				text2 = "No key";
				color = Theme.Faint;
			}
			int num5 = RightW - 28;
			if (!_hasKey[num2])
			{
				g.DrawString(item2, RowT, brush2, new PointF(RightX + 14, (float)num3 + (float)(num4 - 20) / 2f));
				using SolidBrush brush3 = new SolidBrush(color);
				using StringFormat format2 = new StringFormat(format)
				{
					Alignment = StringAlignment.Far
				};
				g.DrawString(text2, RowD, brush3, new RectangleF(RightX + 14, (float)num3 + (float)(num4 - 14) / 2f + 1f, num5, 16f), format2);
			}
			else
			{
				g.DrawString(item2, Roomy ? RowTBig : RowT, brush2, new PointF(RightX + 14, num3 + geo.NameY));
				using SolidBrush brush4 = new SolidBrush(color);
				g.DrawString(text2, Roomy ? RowDBig : RowD, brush4, new RectangleF(RightX + 14, num3 + geo.StatusY, num5, 18f), format);
				PaintKeyBox(g, num2, PillRect(i));
			}
			if (i < _rows.Length - 1)
			{
				g.DrawLine(pen2, RightX + 12, num3 + num4, RightX + RightW - 12, num3 + num4);
			}
		}
	}

	private void PaintKeyBox(Graphics g, int acct, Rectangle pill)
	{
		float num = _reveal[acct];
		bool flag = _hover == acct && !_revealTarget[acct];
		using GraphicsPath path = Rounded(new RectangleF((float)pill.X + 0.5f, (float)pill.Y + 0.5f, (float)pill.Width - 1f, (float)pill.Height - 1f), (float)pill.Height / 2f);
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(255, 5, 5, 8)))
		{
			g.FillPath(brush, path);
		}
		GraphicsState gstate = g.Save();
		g.SetClip(path, CombineMode.Intersect);
		if (num < 1f)
		{
			Bitmap bitmap = _blur[acct];
			if (bitmap == null || bitmap.Width != pill.Width || bitmap.Height != pill.Height)
			{
				bitmap?.Dispose();
				bitmap = (_blur[acct] = BlurredText.Render(BlurredText.Decoy(acct * 7919 + 17), (pill.Height >= 26) ? BlurBig : BlurF, Color.FromArgb(170, 214, 238), pill.Width, pill.Height, 12, (pill.Height >= 26) ? 4 : 3, (pill.Height >= 26) ? 3 : 2));
			}
			DrawBlurred(g, bitmap, pill, (1f - num) * (flag ? 0.42f : 1f));
		}
		if (num > 0f)
		{
			string text = _revealKey[acct];
			if (text != null)
			{
				var (font, s) = FitKey(g, acct, text, pill.Width - 28);
				using StringFormat format = new StringFormat(StringFormat.GenericTypographic)
				{
					LineAlignment = StringAlignment.Center,
					Alignment = StringAlignment.Center,
					FormatFlags = StringFormatFlags.NoWrap
				};
				using SolidBrush brush2 = new SolidBrush(Color.FromArgb((int)(255f * num), Theme.Text));
				g.DrawString(s, font, brush2, new RectangleF(pill.X + 6, pill.Y, pill.Width - 12, pill.Height + 1), format);
			}
		}
		string text2 = _pillNote[acct];
		if (text2 != null || (flag && num < 0.5f))
		{
			using StringFormat format2 = new StringFormat(StringFormat.GenericTypographic)
			{
				LineAlignment = StringAlignment.Center,
				Alignment = StringAlignment.Center,
				FormatFlags = StringFormatFlags.NoWrap
			};
			Color baseColor = ((text2 != null) ? Theme.Error : Theme.Text);
			using SolidBrush brush3 = new SolidBrush(Color.FromArgb((text2 != null) ? 255 : ((int)(235f * (1f - num * 2f))), baseColor));
			g.DrawString(text2 ?? "Click to reveal", HintS, brush3, new RectangleF(pill.X, pill.Y, pill.Width, pill.Height + 1), format2);
		}
		g.Restore(gstate);
		using Pen pen = new Pen((flag || num > 0f) ? Color.FromArgb(150, Theme.Accent) : Theme.Border, 1f);
		g.DrawPath(pen, path);
	}

	private static void DrawBlurred(Graphics g, Bitmap bmp, Rectangle pill, float alpha)
	{
		if (alpha <= 0.004f)
		{
			return;
		}
		using ImageAttributes imageAttributes = new ImageAttributes();
		imageAttributes.SetColorMatrix(new ColorMatrix
		{
			Matrix33 = Math.Clamp(alpha, 0f, 1f)
		}, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
		InterpolationMode interpolationMode = g.InterpolationMode;
		g.InterpolationMode = InterpolationMode.NearestNeighbor;
		g.DrawImage(bmp, pill, 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, imageAttributes);
		g.InterpolationMode = interpolationMode;
	}

	private (Font Font, string Text) FitKey(Graphics g, int acct, string key, int maxW)
	{
		(Font, string, int)? tuple = _fit[acct];
		if (tuple.HasValue)
		{
			(Font, string, int) valueOrDefault = tuple.GetValueOrDefault();
			if (valueOrDefault.Item3 == maxW)
			{
				return (Font: valueOrDefault.Item1, Text: valueOrDefault.Item2);
			}
		}
		_fit[acct]?.Font.Dispose();
		using StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic)
		{
			FormatFlags = StringFormatFlags.NoWrap
		};
		Font font = null;
		string text = key;
		for (float num = (Roomy ? 10.5f : 9.5f); num >= 6.5f; num -= 0.5f)
		{
			font?.Dispose();
			font = new Font("Consolas", num, FontStyle.Bold);
			if (g.MeasureString(key, font, PointF.Empty, stringFormat).Width <= (float)maxW)
			{
				break;
			}
		}
		int num2 = key.Length / 2;
		while (num2 > 2 && g.MeasureString(text, font, PointF.Empty, stringFormat).Width > (float)maxW)
		{
			string text2 = key.Substring(0, num2);
			int num3 = num2;
			text = text2 + "…" + key.Substring(key.Length - num3);
			num2--;
		}
		_fit[acct] = (font, text, maxW);
		return (Font: font, Text: text);
	}

	private static void DrawSpaced(Graphics g, string text, Font f, Brush b, float x, float y, float spacing)
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
