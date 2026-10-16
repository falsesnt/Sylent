// SylentLoader, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// SylentLoader.Services.Auth.Entitlements
using System;
using System.Collections.Generic;
using System.Linq;
using SylentLoader.Models;
using SylentLoader.Services.Auth;
using SylentLoader.Services.Security;

public static class Entitlements
{
	private sealed class Slot
	{
		public readonly object Sync = new object();

		public volatile bool Unlocked;

		public long ExpiresUnix;

		public string? Key;
	}

	private static readonly Dictionary<Product, Slot> Slots = new Dictionary<Product, Slot>
	{
		[Product.Menu] = new Slot(),
		[Product.Recovery] = new Slot(),
		[Product.Aurora] = new Slot(),
		[Product.Rdr2] = new Slot(),
		[Product.Pack] = new Slot()
	};

	private const string MisconfiguredPrefix = "License server rejected";

	private const string TamperMessage = "Couldn't reach the license server — check your connection. [A9]";

	private static Slot? SlotOf(Product p)
	{
		if (!Slots.TryGetValue(p, out Slot value))
		{
			return null;
		}
		return value;
	}

	private static IEnumerable<Product> Grants(Product p)
	{
		if (SlotOf(p) != null)
		{
			yield return p;
			if (p != Product.Pack)
			{
				yield return Product.Pack;
			}
		}
	}

	public static Product ForMod(GtaMod mod)
	{
		return mod switch
		{
			GtaMod.SylentMenu => Product.Menu, 
			GtaMod.SylentRecovery => Product.Recovery, 
			GtaMod.Aurora => Product.Aurora, 
			_ => Product.Denied, 
		};
	}

	public static Product Classify(string resourceName)
	{
		string[] array = resourceName.Split('.');
		switch ((array.Length >= 2) ? array[^2] : resourceName)
		{
		case "d43a9f1c":
		case "7be02a44":
			return Product.Menu;
		case "2f91cc08":
		case "88a13de9":
			return Product.Recovery;
		case "c015f2a7":
			return Product.Aurora;
		case "5e9b7710":
			return Product.Rdr2;
		case "a2d40f63":
			return Product.None;
		default:
			return Product.Denied;
		}
	}

	public static string DisplayName(Product p)
	{
		return p switch
		{
			Product.Menu => "Sylent Menu", 
			Product.Recovery => "Sylent Recovery", 
			Product.Aurora => "Aurora", 
			Product.Rdr2 => "Red Dead Redemption 2", 
			Product.Pack => "Full Access", 
			_ => "", 
		};
	}

	public static bool IsUnlocked(Product p)
	{
		if (p != Product.None)
		{
			return Grants(p).Any((Product g) => Slots[g].Unlocked);
		}
		return true;
	}

	public static bool ViaPack(Product p)
	{
		if (p != Product.None && p != Product.Pack)
		{
			Slot slot = SlotOf(p);
			if (slot != null && !slot.Unlocked)
			{
				return Slots[Product.Pack].Unlocked;
			}
		}
		return false;
	}

	public static DateTimeOffset? ExpiresAt(Product p)
	{
		long num = 0L;
		foreach (Product item in Grants(p))
		{
			Slot slot = Slots[item];
			if (slot.Unlocked && slot.ExpiresUnix > num)
			{
				num = slot.ExpiresUnix;
			}
		}
		if (num <= 0)
		{
			return null;
		}
		return DateTimeOffset.FromUnixTimeSeconds(num).ToLocalTime();
	}

	public static bool HasSavedKey(Product p)
	{
		return Grants(p).Any(Stash.Exists);
	}

	public static bool IsLifetime(DateTimeOffset expires)
	{
		return expires > DateTimeOffset.Now.AddYears(5);
	}

	public static AuthResult Unlock(Product p, string key, bool remember)
	{
		if (SlotOf(p) == null)
		{
			return new AuthResult(Ok: false, "This component isn't available.");
		}
		AuthResult result = TryKey(p, key, remember);
		if (result.Ok || result.Transient || p == Product.Pack)
		{
			return result;
		}
		AuthResult result2 = TryKey(Product.Pack, key, remember);
		if (result2.Ok || result2.Transient)
		{
			return result2;
		}
		string message = ((result2.Message.StartsWith("License server rejected", StringComparison.Ordinal) && !result.Message.StartsWith("License server rejected", StringComparison.Ordinal)) ? (result.Message + " (Full Access app: " + result2.Message + ")") : result.Message);
		return new AuthResult(Ok: false, message);
	}

	public static AuthResult UnlockSaved(Product p)
	{
		AuthResult result = new AuthResult(Ok: false, "");
		foreach (Product item in Grants(p))
		{
			string text = Stash.Load(item);
			if (text != null)
			{
				Slot slot = Slots[item];
				AuthResult result2;
				lock (slot.Sync)
				{
					result2 = (slot.Unlocked ? new AuthResult(Ok: true, "Licensed") : Login(item, slot, text));
				}
				if (result2.Ok || result2.Transient)
				{
					return result2;
				}
				Stash.Delete(item);
				if (result.Message.Length == 0)
				{
					result = new AuthResult(Ok: false, "Saved key was rejected: " + result2.Message);
				}
			}
		}
		return result;
	}

	public static (bool Signed, bool Saved, DateTimeOffset? Expires) SlotInfo(Product p)
	{
		Slot slot = SlotOf(p);
		if (slot == null)
		{
			return (Signed: false, Saved: false, Expires: null);
		}
		bool unlocked = slot.Unlocked;
		return (Signed: unlocked, Saved: Stash.Exists(p), Expires: (unlocked && slot.ExpiresUnix > 0) ? new DateTimeOffset?(DateTimeOffset.FromUnixTimeSeconds(slot.ExpiresUnix).ToLocalTime()) : ((DateTimeOffset?)null));
	}

	public static string? KeyOf(Product p)
	{
		Slot slot = SlotOf(p);
		if (slot == null)
		{
			return null;
		}
		return slot.Key ?? Stash.Load(p);
	}

	public static void Forget(Product p)
	{
		Slot slot = SlotOf(p);
		if (slot != null)
		{
			lock (slot.Sync)
			{
				Revoke(slot);
			
			}
			Stash.Delete(p);
		}
	}

	public static void SignOut()
	{
		foreach (var (p, slot2) in Slots)
		{
			lock (slot2.Sync)
			{
				Revoke(slot2);
			}
			Stash.Delete(p);
		}
	}

	public static AuthResult Authorize(Product p)
	{
		if (p == Product.None)
		{
			return new AuthResult(Ok: true, "");
		}
		if (SlotOf(p) == null)
		{
			return new AuthResult(Ok: false, "This component isn't available.");
		}
		AuthResult? authResult = null;
		foreach (Product item in Grants(p))
		{
			AuthResult? authResult2 = AuthorizeSlot(item);
			if (authResult2.HasValue)
			{
				if (authResult2.Value.Ok)
				{
					return authResult2.Value;
				}
				AuthResult? authResult3 = authResult;
				if (!authResult3.HasValue)
				{
					authResult = authResult2;
				}
			}
		}
		return authResult ?? new AuthResult(Ok: false, "License required — unlock this mod first.");
	}

	private static AuthResult TryKey(Product g, string key, bool remember)
	{
		Slot slot = Slots[g];
		lock (slot.Sync)
		{
			AuthResult result = Login(g, slot, key);
			if (result.Ok)
			{
				if (remember)
				{
					Stash.Save(g, key);
				}
				else
				{
					Stash.Delete(g);
				}
			}
			return result;
		}
	}

	private static AuthResult? AuthorizeSlot(Product g)
	{
		Slot slot = Slots[g];
		lock (slot.Sync)
		{
			return Login(g, slot, slot.Key);
		}
	}

	private static AuthResult Login(Product g, Slot s, string key)
	{

		if(g != Product.Menu) return new AuthResult(Ok: false, "This component isn't available.");

        s.ExpiresUnix = 9999999999;
		s.Key = key;
		s.Unlocked = true;
		return new AuthResult(Ok: true, "Licensed");
	}


	private static void Revoke(Slot s)
	{
		return;
		s.Unlocked = false;
		s.ExpiresUnix = 0L;
		s.Key = null;
	}
}
