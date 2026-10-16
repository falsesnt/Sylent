namespace SylentLoader.Models;

public class ModFramework
{
	public string Name { get; set; } = "";

	public bool Detected { get; set; }

	public bool Enabled { get; set; } = true;

	public string? Version { get; set; }
}
