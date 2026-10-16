using System.Threading.Tasks;
using System.Windows.Forms;
using SylentLoader.Services.Auth;

namespace SylentLoader.Controls;

public static class ModGate
{
	public static async Task<bool> EnsureAsync(IWin32Window? owner, Product product)
	{
		if (Entitlements.IsUnlocked(product))
		{
			return true;
		}
		string note = null;
		if (Entitlements.HasSavedKey(product))
		{
			AuthResult authResult = await Task.Run(() => Entitlements.UnlockSaved(product));
			if (authResult.Ok)
			{
				return true;
			}
			note = authResult.Message;
		}
		using UnlockForm unlockForm = new UnlockForm(product, note);
		return unlockForm.ShowDialog(owner) == DialogResult.OK;
	}
}
