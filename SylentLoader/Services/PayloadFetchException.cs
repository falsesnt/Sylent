using System;

namespace SylentLoader.Services;

internal sealed class PayloadFetchException : Exception
{
	public string Code { get; }

	public PayloadFetchException(string code)
		: base(code)
	{
		Code = code;
	}
}
