namespace SylentLoader.Services.Security;

internal static class PayloadTokens
{
	public const string MenuLegacy = "d43a9f1c";

	public const string MenuEnhanced = "7be02a44";

	public const string RecoveryLegacy = "2f91cc08";

	public const string RecoveryEnhanced = "88a13de9";

	public const string Aurora = "c015f2a7";

	public const string Rdr2 = "5e9b7710";

	public const string PubLobby = "a2d40f63";

	public static string ResourceName(string token)
	{
		return "SylentLoader.Resources." + token + ".bin";
	}
}
