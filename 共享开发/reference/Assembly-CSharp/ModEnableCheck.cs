using System.Collections.Generic;

public class ModEnableCheck
{
	public LoadedMod Mod;

	public ModFingerprint Fingerprint;

	public ModScanResult Scan;

	public ModListVerdict Verdict;

	public List<string> Undeclared = new List<string>();

	public List<string> PromptCapabilities = new List<string>();

	public List<string> NeedsVerification = new List<string>();

	public string Refusal;

	public bool NeedsPrompt;
}
