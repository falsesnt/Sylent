using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SylentLoader.Controls;

internal sealed class FramePump : IDisposable
{
	private static int _periodUsers;

	private static readonly object PeriodLock = new object();

	private readonly Control _host;

	private readonly Action _onFrame;

	private CancellationTokenSource? _cts;

	private volatile bool _pending;

	public bool Running => _cts != null;

	[DllImport("winmm.dll")]
	private static extern uint timeBeginPeriod(uint ms);

	[DllImport("winmm.dll")]
	private static extern uint timeEndPeriod(uint ms);

	public FramePump(Control host, Action onFrame)
	{
		_host = host;
		_onFrame = onFrame;
	}

	public void Start()
	{
		if (_cts != null || !_host.IsHandleCreated)
		{
			return;
		}
		CancellationTokenSource cts = (_cts = new CancellationTokenSource());
		lock (PeriodLock)
		{
			if (_periodUsers++ == 0)
			{
				timeBeginPeriod(1u);
			}
		}
		Task.Run(() =>
		{
			CancellationToken token = cts.Token;
			while (!token.IsCancellationRequested)
			{
				Thread.Sleep(7);
				if (!_pending)
				{
					_pending = true;
					try
					{
						_host.BeginInvoke(() =>
						{
							try
							{
								if (!token.IsCancellationRequested)
								{
									_onFrame();
								}
							}
							finally
							{
								_pending = false;
							}
						});
					}
					catch
					{
						_pending = false;
						break;
					}
				}
			}
		});
	}

	public void Stop()
	{
		CancellationTokenSource cts = _cts;
		if (cts == null)
		{
			return;
		}
		_cts = null;
		cts.Cancel();
		lock (PeriodLock)
		{
			if (--_periodUsers == 0)
			{
				timeEndPeriod(1u);
			}
		}
	}

	public void Dispose()
	{
		Stop();
	}
}
