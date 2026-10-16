using System.Collections.Generic;

namespace SylentLoader.Services.Auth;

internal sealed class Reply
{
	public bool Ok;

	public bool Transient;

	public string Message = "";

	public long ServerTime;

	public string? SessionId;

	public List<(string Name, long Expiry)> Subs = new List<(string, long)>();

	public static Reply Fail(string msg, bool transient = false)
	{
		return new Reply
		{
			Message = msg,
			Transient = transient
		};
	}
}
