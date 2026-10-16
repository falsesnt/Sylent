using System;
using System.Collections.Generic;
using SylentLoader.Models;

namespace SylentLoader.Services;

internal sealed class LoadWatcher
{
	public static readonly TimeSpan MinUptimeEnhanced = TimeSpan.FromSeconds(45.0);

	public static readonly TimeSpan FallbackUptimeEnhanced = TimeSpan.FromSeconds(180.0);

	public const int QuietTicksEnhanced = 5;

	public static readonly TimeSpan MinUptimeLegacy = TimeSpan.FromSeconds(15.0);

	public static readonly TimeSpan FallbackUptimeLegacy = TimeSpan.FromSeconds(30.0);

	public const int QuietTicksLegacy = 1;

	public const int WindowSeconds = 10;

	public const long QuietGrowth = 67108864L;

	public const long QuietReadRate = 4194304L;

	public const int FallbackWatch = 90;

	private readonly TimeSpan _minUptime;

	private readonly TimeSpan _fallbackUptime;

	private readonly int _quietTicks;

	private readonly Queue<(DateTime T, long Ws, long Read)> _samples = new Queue<(DateTime, long, long)>();

	private int _quiet;

	private int _watched;

	public LoadWatcher(GtaVersion version)
	{
		if (version == GtaVersion.Legacy)
		{
			_minUptime = MinUptimeLegacy;
			_fallbackUptime = FallbackUptimeLegacy;
			_quietTicks = 1;
		}
		else
		{
			_minUptime = MinUptimeEnhanced;
			_fallbackUptime = FallbackUptimeEnhanced;
			_quietTicks = 5;
		}
	}

	public bool Feed(DateTime now, TimeSpan uptime, bool windowUp, long workingSet, long readBytes)
	{
		_samples.Enqueue((now, workingSet, readBytes));
		while (_samples.Count > 11)
		{
			_samples.Dequeue();
		}
		if (!windowUp)
		{
			_quiet = 0;
			return false;
		}
		_watched++;
		if (_samples.Count < 11)
		{
			return false;
		}
		(DateTime, long, long) tuple = _samples.Peek();
		double num = Math.Max(1.0, (now - tuple.Item1).TotalSeconds);
		bool flag = workingSet - tuple.Item2 <= 67108864;
		bool flag2 = (double)(readBytes - tuple.Item3) / num <= 4194304.0;
		_quiet = ((flag & flag2) ? (_quiet + 1) : 0);
		if (uptime >= _fallbackUptime && _watched >= 90)
		{
			return true;
		}
		if (uptime >= _minUptime)
		{
			return _quiet >= _quietTicks;
		}
		return false;
	}
}
