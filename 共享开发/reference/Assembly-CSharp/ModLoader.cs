using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using QFSW.QC;
using UnityEngine;
using UnityEngine.Networking;

public class ModLoader : MonoBehaviour
{
	private sealed class PrivilegedScope : IDisposable
	{
		public void Dispose()
		{
			if (privilegedDepth > 0)
			{
				privilegedDepth--;
			}
		}
	}

	private class TrustGate
	{
		public bool Load;

		public ModTrustState State;

		public string Note;

		public ModFingerprint Fingerprint;

		public ModTrustRecord Record;

		public ModListVerdict Verdict;

		public ModScanResult Scan;
	}

	private struct DiscoveredMod
	{
		public string Folder;

		public ModSource Source;

		public ulong WorkshopId;

		public ModManifest Manifest;
	}

	private static readonly List<LoadedMod> mods = new List<LoadedMod>();

	private static List<string> _modOrder = new List<string>();

	private static int _rejectedLocalFolders;

	public const double START_FETCH_IF_OLDER_THAN_HOURS = 6.0;

	public const int START_FETCH_TIMEOUT_MS = 3000;

	private static HashSet<string> gameAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static (string folder, ModManifest manifest)? loadingMod;

	private static ModFingerprint loadingFingerprint;

	private static readonly Dictionary<string, Harmony> harmonies = new Dictionary<string, Harmony>();

	private static readonly Dictionary<string, ModFingerprint> fingerprints = new Dictionary<string, ModFingerprint>();

	private float nextGuardCheck;

	[ThreadStatic]
	private static int privilegedDepth;

	private const long AUDIT_MAX_BYTES = 524288L;

	private static readonly byte[] IModNameBytes = new byte[5] { 73, 77, 111, 100, 0 };

	public static ModLoader Instance { get; private set; }

	public static List<LoadedMod> Mods => new List<LoadedMod>(mods);

	public static HashSet<string> EnabledMods { get; private set; } = new HashSet<string>();

	public static string ModsPath => Path.Combine(Application.dataPath, "..", "Mods");

	public static bool RestartRequired { get; private set; }

	public static string AuditPath => Path.Combine(Application.persistentDataPath, "mod-audit.log");

	public static event Action OnTrustRefreshed;

	public static void MarkRestartRequired()
	{
		RestartRequired = true;
	}

	public static bool IsGameAssemblyName(string assemblyName)
	{
		return gameAssemblyNames.Contains(assemblyName);
	}

	public static string CallingModId()
	{
		List<(LoadedMod, string)> folders = ModFolders();
		StackTrace stackTrace = new StackTrace(1, fNeedFileInfo: false);
		for (int i = 0; i < stackTrace.FrameCount; i++)
		{
			StackFrame frame = stackTrace.GetFrame(i);
			MethodBase methodBase = frame?.GetMethod();
			if (methodBase == null)
			{
				continue;
			}
			Assembly assembly = methodBase.DeclaringType?.Assembly ?? methodBase.Module?.Assembly;
			if (assembly == null || assembly.IsDynamic || methodBase.DeclaringType == null)
			{
				string text = TranspilingModOf(frame, methodBase, folders);
				if (text != null)
				{
					return text;
				}
			}
			else if (!(assembly == typeof(ModLoader).Assembly))
			{
				string text2 = ModIdOfAssembly(assembly, folders);
				if (text2 != null)
				{
					return text2;
				}
			}
		}
		return null;
	}

	private static string TranspilingModOf(StackFrame frame, MethodBase replacement, List<(LoadedMod mod, string folder)> folders)
	{
		MethodBase methodBase = null;
		try
		{
			methodBase = Harmony.GetOriginalMethodFromStackframe(frame);
		}
		catch
		{
		}
		if (methodBase == null && replacement is MethodInfo replacement2)
		{
			try
			{
				methodBase = Harmony.GetOriginalMethod(replacement2);
			}
			catch
			{
			}
		}
		if (methodBase == null)
		{
			return null;
		}
		Patches patchInfo;
		try
		{
			patchInfo = Harmony.GetPatchInfo(methodBase);
		}
		catch
		{
			return null;
		}
		if (patchInfo == null)
		{
			return null;
		}
		foreach (Patch transpiler in patchInfo.Transpilers)
		{
			Assembly assembly = transpiler.PatchMethod?.DeclaringType?.Assembly;
			if (!(assembly == null))
			{
				string text = ModIdOfAssembly(assembly, folders);
				if (text != null)
				{
					return text;
				}
			}
		}
		return null;
	}

	public static string ModIdOfAssembly(Assembly assembly)
	{
		if (assembly == null || assembly == typeof(ModLoader).Assembly)
		{
			return null;
		}
		return ModIdOfAssembly(assembly, ModFolders());
	}

	private static string ModIdOfAssembly(Assembly assembly, List<(LoadedMod mod, string folder)> folders)
	{
		string text = null;
		try
		{
			if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
			{
				text = Path.GetFullPath(assembly.Location);
			}
		}
		catch
		{
		}
		foreach (var (loadedMod, text2) in folders)
		{
			if (loadedMod.Assembly == assembly)
			{
				return loadedMod.Manifest.ID;
			}
			if (text != null && text2 != null && text.StartsWith(text2, StringComparison.OrdinalIgnoreCase))
			{
				return loadedMod.Manifest.ID;
			}
		}
		return null;
	}

	private static List<(LoadedMod mod, string folder)> ModFolders()
	{
		List<(LoadedMod, string)> list = new List<(LoadedMod, string)>();
		foreach (LoadedMod mod in mods)
		{
			string item = null;
			try
			{
				if (!string.IsNullOrEmpty(mod.FolderPath))
				{
					item = Path.GetFullPath(mod.FolderPath).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
				}
			}
			catch
			{
			}
			list.Add((mod, item));
		}
		return list;
	}

	public static bool IsValidModId(string id)
	{
		if (string.IsNullOrEmpty(id) || id.Length > 64 || id[0] == '.')
		{
			return false;
		}
		foreach (char c in id)
		{
			if ((c < 'a' || c > 'z') && (c < 'A' || c > 'Z') && (c < '0' || c > '9') && c != '_' && c != '-' && c != '.')
			{
				return false;
			}
		}
		if (id != ".")
		{
			return id != "..";
		}
		return false;
	}

	private static HashSet<string> CollectGameAssemblyNames()
	{
		HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			if (!assembly.IsDynamic)
			{
				names.Add(assembly.GetName().Name);
			}
		}
		if (Application.isEditor)
		{
			AddFolder(Path.GetDirectoryName(typeof(UnityEngine.Object).Assembly.Location), recursive: true);
			AddFolder(Path.GetDirectoryName(typeof(object).Assembly.Location), recursive: true);
			AddFolder(Path.Combine(Application.dataPath, "..", "Library", "ScriptAssemblies"), recursive: false);
			AddFolder(Path.Combine(Application.dataPath, "..", "Library", "PackageCache"), recursive: true);
			AddFolder(Path.Combine(Application.dataPath, "Plugins"), recursive: true);
		}
		else
		{
			AddFolder(Path.Combine(Application.dataPath, "Managed"), recursive: true);
		}
		return names;
		void AddFolder(string folder, bool recursive)
		{
			try
			{
				if (!string.IsNullOrEmpty(folder) && System.IO.Directory.Exists(folder))
				{
					string[] files = System.IO.Directory.GetFiles(folder, "*.dll", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
					foreach (string path in files)
					{
						names.Add(Path.GetFileNameWithoutExtension(path));
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[ModLoader] Could not list assemblies in " + folder + ": " + ex.Message);
			}
		}
	}

	private void Awake()
	{
		if (Instance != null)
		{
			UnityEngine.Object.Destroy(gameObject);
			return;
		}
		Instance = this;
		UnityEngine.Object.DontDestroyOnLoad(gameObject);
		gameAssemblyNames = CollectGameAssemblyNames();
		AppDomain.CurrentDomain.AssemblyResolve -= ResolveModDependency;
		AppDomain.CurrentDomain.AssemblyResolve += ResolveModDependency;
		ModTrust.EnsureLoaded();
		ModTrustList.LoadCache();
		ModTrustList.OnChanged -= ReapplyListVerdicts;
		ModTrustList.OnChanged += ReapplyListVerdicts;
		ModWorkshop.OnDetailsUpdated -= ReapplyListVerdicts;
		ModWorkshop.OnDetailsUpdated += ReapplyListVerdicts;
		LoadModOrder();
		if (EnabledMods.Count > 0 && ModTrustList.IsConfigured && (ModTrustList.CacheAgeHours ?? double.MaxValue) > 6.0)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			bool flag = ModTrustList.TryFetchBlocking(3000);
			UnityEngine.Debug.Log(string.Format("[ModLoader] Trust list refreshed before loading mods: {0} ({1} ms)", flag ? "new list applied" : "no newer list", stopwatch.ElapsedMilliseconds));
		}
		LoadDiscoveredMods();
		ApplySavedOrder();
		ValidatePrerequisites();
		LoadModAssets();
		LogAudit();
		StartCoroutine(ModTrustList.Fetch());
	}

	private void Start()
	{
		if (!(Instance != this) && (_rejectedLocalFolders > 0 || mods.Any((LoadedMod m) => m.LoadError != null && m.Source == ModSource.Local)) && QuantumConsole.Instance != null)
		{
			QuantumConsole.Instance.Activate();
		}
	}

	private void Update()
	{
		if (!(Instance != this))
		{
			ModSteamGuard.Drain();
			ModSteamAuth.Update();
			ModHelper.DrainMainThread();
			if (Time.unscaledTime >= nextGuardCheck)
			{
				nextGuardCheck = Time.unscaledTime + 10f;
				ModSteamGuard.Verify();
			}
		}
	}

	public static string GetModLabel(ModManifest manifest)
	{
		if (!string.IsNullOrEmpty(manifest.Name))
		{
			return manifest.Name;
		}
		return manifest.ID;
	}

	private static string DescribeException(Exception e)
	{
		Exception ex = e.GetBaseException();
		if (ex is ReflectionTypeLoadException { LoaderExceptions: var loaderExceptions })
		{
			Exception ex3 = loaderExceptions?.FirstOrDefault((Exception x) => x != null);
			if (ex3 != null)
			{
				ex = ex3;
			}
		}
		return ex.GetType().Name + ": " + ex.Message;
	}

	private static void ReportLoadError(LoadedMod mod, Exception e, string context)
	{
		if (mod.LoadError == null)
		{
			mod.LoadError = DescribeException(e);
		}
		Debug.LogError($"[{GetModLabel(mod.Manifest)}] Error: {DescribeException(e)} ({context})\n{e}");
	}

	private void LoadModOrder()
	{
		if (ES3.KeyExists("modOrder"))
		{
			_modOrder = ES3.Load<List<string>>("modOrder");
			EnabledMods = new HashSet<string>(_modOrder);
			UnityEngine.Debug.Log($"[ModLoader] {EnabledMods.Count} mod(s) enabled");
		}
	}

	internal static IDisposable Privileged()
	{
		privilegedDepth++;
		return new PrivilegedScope();
	}

	internal static void RequireNoMod(string action)
	{
		if (privilegedDepth <= 0)
		{
			string text = CallingModId();
			if (text != null)
			{
				Debug.LogError("[ModLoader] '" + text + "' tried to " + action + "; refused (the trust layer is the player's and the game's).");
				throw new UnauthorizedAccessException("Mod '" + text + "' may not " + action);
			}
		}
	}

	[ModRestricted("loader-target", "enables or disables mods, which only the player does")]
	public static void SetModEnabled(string id, bool enabled)
	{
		RequireNoMod("enable or disable a mod");
		if (enabled)
		{
			if (EnabledMods.Add(id))
			{
				_modOrder.Add(id);
			}
		}
		else
		{
			EnabledMods.Remove(id);
			_modOrder.Remove(id);
			ModTrust.Revoke(id);
		}
		ES3.Save("modOrder", _modOrder);
		RestartRequired = true;
		UnityEngine.Debug.Log($"[ModLoader] '{id}' enabled={enabled} (restart required)");
	}

	private static void DisableSilently(string id)
	{
		if (EnabledMods.Remove(id))
		{
			_modOrder.Remove(id);
			ES3.Save("modOrder", _modOrder);
		}
	}

	[ModRestricted("loader-target", "reorders mods, which only the player does")]
	public static void ReorderMods(List<string> orderedIDs)
	{
		RequireNoMod("reorder mods");
		List<string> list = orderedIDs.Where((string id) => EnabledMods.Contains(id)).ToList();
		foreach (string item in _modOrder)
		{
			if (!list.Contains(item))
			{
				list.Add(item);
			}
		}
		_modOrder = list;
		mods.Sort((LoadedMod a, LoadedMod b) =>
		{
			int num = _modOrder.IndexOf(a.Manifest.ID);
			int num2 = _modOrder.IndexOf(b.Manifest.ID);
			if (num < 0)
			{
				num = int.MaxValue;
			}
			if (num2 < 0)
			{
				num2 = int.MaxValue;
			}
			return num.CompareTo(num2);
		});
		ES3.Save("modOrder", _modOrder);
		RestartRequired = true;
		UnityEngine.Debug.Log("[ModLoader] Mod order saved.");
	}

	public static bool IsModEnabled(string id)
	{
		return EnabledMods.Contains(id);
	}

	private static TrustGate EvaluateTrust(string folderPath, ModManifest manifest, ModSource source, ulong workshopId, List<string> unlisted)
	{
		TrustGate trustGate = new TrustGate();
		string iD = manifest.ID;
		string modLabel = GetModLabel(manifest);
		trustGate.Fingerprint = ModFingerprint.Compute(folderPath, manifest, source == ModSource.Local);
		trustGate.Verdict = ModTrustList.Check(iD, manifest.Author, trustGate.Fingerprint, workshopId, WorkshopOwner(workshopId), out var reason);
		if (trustGate.Verdict == ModListVerdict.Blocked)
		{
			trustGate.State = ModTrustState.Blocked;
			trustGate.Note = reason;
			ModTrust.Revoke(iD);
			DisableSilently(iD);
			return trustGate;
		}
		if (ModTrust.IsStopped(trustGate.Fingerprint.Combined, out var entry))
		{
			trustGate.State = ModTrustState.Stopped;
			trustGate.Note = entry.reason;
			ModTrust.Revoke(iD);
			DisableSilently(iD);
			return trustGate;
		}
		if (ModTrustList.IsPaused(trustGate.Verdict))
		{
			trustGate.State = ModTrustState.Paused;
			trustGate.Note = ModTrustList.KillSwitchMessage;
			return trustGate;
		}
		if (ModTrustList.WORKSHOP_VERIFIED_ONLY_CAPABILITIES && ModTrustList.NeedsVerificationFor(source, manifest.Capabilities, trustGate.Verdict).Count > 0)
		{
			trustGate.State = ModTrustState.Changed;
			trustGate.Note = LocHelper.GetLocalizedUIOrDefault("ui_mods_note_needs_verification", "it uses files or the internet and is not yet verified by the developers");
			UnityEngine.Debug.Log("[" + modLabel + "] Not loaded: Workshop mod with " + string.Join(", ", ModTrustList.NeedsVerificationFor(source, manifest.Capabilities, trustGate.Verdict)) + " and not verified");
			return trustGate;
		}
		trustGate.Record = ModTrust.Get(iD);
		ModTrustState modTrustState = ModTrust.CompareFiles(trustGate.Record, trustGate.Fingerprint, source, workshopId);
		switch (modTrustState)
		{
		case ModTrustState.Unknown:
			trustGate.State = ModTrustState.Unknown;
			return trustGate;
		case ModTrustState.Changed:
			trustGate.State = ModTrustState.Changed;
			trustGate.Note = ((trustGate.Record.source != source.ToString()) ? LocHelper.GetLocalizedUIOrDefault("ui_mods_note_source_changed", "it now comes from a different place than the approved copy") : LocHelper.GetLocalizedUIOrDefault("ui_mods_note_workshop_item", "it is a different Workshop item"));
			UnityEngine.Debug.Log($"[{modLabel}] Changed: record source {trustGate.Record.source}/{trustGate.Record.workshopId}, now {source}/{workshopId}");
			return trustGate;
		default:
		{
			if (unlisted.Count > 0)
			{
				trustGate.State = ModTrustState.Changed;
				trustGate.Note = LocHelper.GetLocalizedUIOrDefault("ui_mods_note_unlisted", "its folder contains DLLs the manifest does not list: {0}", string.Join(", ", unlisted));
				return trustGate;
			}
			bool flag = modTrustState == ModTrustState.Match && trustGate.Record.scannerVersion != 4;
			if (modTrustState == ModTrustState.Match && !flag)
			{
				trustGate.State = ModTrustState.Match;
				trustGate.Load = true;
				ModTrust.StoreFolderHash(trustGate.Record, trustGate.Fingerprint);
				return trustGate;
			}
			trustGate.Scan = ModScanner.Scan(ListedAssemblyPaths(folderPath, manifest), ListedAssemblyNames(manifest), folderPath, source == ModSource.Local);
			if (trustGate.Record.developerMode && source == ModSource.Local)
			{
				trustGate.State = ModTrustState.DeveloperMode;
				trustGate.Load = true;
				ModTrust.Refresh(trustGate.Record, trustGate.Fingerprint, trustGate.Scan.Refused ? null : trustGate.Scan.Capabilities);
				if (trustGate.Scan.Refused)
				{
					Debug.LogWarning("[" + modLabel + "] Developer mode: loaded although the scan refused it (" + (trustGate.Scan.Error ?? trustGate.Scan.FormatFindings(4)) + ")");
				}
				return trustGate;
			}
			if (trustGate.Scan.Refused)
			{
				trustGate.State = ModTrustState.Changed;
				trustGate.Note = (flag ? LocHelper.GetLocalizedUIOrDefault("ui_mods_note_rescan_refused", "the game's updated mod scanner refuses it") : LocHelper.GetLocalizedUIOrDefault("ui_mods_note_scan_failed", "the new version was refused by the mod scanner"));
				Debug.LogWarning("[" + modLabel + "] Changed: scan refused" + (flag ? (" (rescan under scanner " + 4 + ")") : "") + " (" + (trustGate.Scan.Error ?? trustGate.Scan.FormatFindings(6)) + ")");
				return trustGate;
			}
			List<string> detected = trustGate.Scan.Capabilities.ToList();
			List<string> list = ModCapability.Undeclared(detected, manifest.Capabilities);
			if (list.Count > 0)
			{
				trustGate.State = ModTrustState.Changed;
				trustGate.Note = (flag ? LocHelper.GetLocalizedUIOrDefault("ui_mods_note_rescan_undeclared", "the game's updated mod scanner found capabilities it does not declare: {0}", string.Join(", ", list.Select(ModCapability.DisplayName))) : LocHelper.GetLocalizedUIOrDefault("ui_mods_note_undeclared", "the new version uses capabilities it does not declare: {0}", string.Join(", ", list.Select(ModCapability.DisplayName))));
				Debug.LogWarning("[" + modLabel + "] Changed: undeclared " + string.Join(", ", list) + "\n" + trustGate.Scan.FormatFindings(10));
				return trustGate;
			}
			if (!ModTrust.CapabilitiesWithin(trustGate.Record, detected))
			{
				List<string> list2 = ModCapability.Undeclared(detected, trustGate.Record.detected);
				trustGate.State = ModTrustState.Changed;
				trustGate.Note = (flag ? LocHelper.GetLocalizedUIOrDefault("ui_mods_note_rescan_new_capabilities", "the game's updated mod scanner found capabilities you did not approve: {0}", string.Join(", ", list2.Select(ModCapability.DisplayName))) : LocHelper.GetLocalizedUIOrDefault("ui_mods_note_new_capabilities", "the new version asks for new capabilities: {0}", string.Join(", ", list2.Select(ModCapability.DisplayName))));
				UnityEngine.Debug.Log("[" + modLabel + "] Changed: new capabilities " + string.Join(", ", list2));
				return trustGate;
			}
			trustGate.State = (flag ? ModTrustState.Match : ModTrustState.Updated);
			trustGate.Load = true;
			ModTrust.Refresh(trustGate.Record, trustGate.Fingerprint, trustGate.Scan.Capabilities);
			return trustGate;
		}
		}
	}

	private static List<string> ListedAssemblyNames(ModManifest manifest)
	{
		return manifest.Assemblies.Select(Path.GetFileNameWithoutExtension).Concat(PrerequisiteAssemblyNames(manifest)).ToList();
	}

	public static List<string> PrerequisiteAssemblyNames(ModManifest manifest)
	{
		List<string> list = new List<string>();
		foreach (string prerequisite in manifest.Prerequisites)
		{
			LoadedMod loadedMod = mods.Find((LoadedMod m) => m.Manifest.ID == prerequisite);
			if (loadedMod != null && loadedMod.Manifest != manifest)
			{
				list.AddRange(loadedMod.Manifest.Assemblies.Select(Path.GetFileNameWithoutExtension));
			}
		}
		return list;
	}

	private static IEnumerable<LoadedMod> RunningPrerequisites(ModManifest manifest)
	{
		foreach (string prerequisite in manifest.Prerequisites)
		{
			LoadedMod loadedMod = mods.Find((LoadedMod m) => m.Manifest.ID == prerequisite);
			if (loadedMod != null && loadedMod.Manifest != manifest && loadedMod.Assembly != null)
			{
				yield return loadedMod;
			}
		}
	}

	public static ModEnableCheck CheckEnable(LoadedMod mod)
	{
		ModEnableCheck modEnableCheck = new ModEnableCheck
		{
			Mod = mod
		};
		try
		{
			return CheckEnableCore(mod, modEnableCheck);
		}
		catch (Exception ex)
		{
			Debug.LogError($"[{GetModLabel(mod.Manifest)}] Enable check failed: {ex.Message}\n{ex}");
			modEnableCheck.Refusal = LocHelper.GetLocalizedUIOrDefault("ui_mods_enable_unreadable", "{0} could not be scanned: {1}", GetModLabel(mod.Manifest), ex.Message);
			return modEnableCheck;
		}
	}

	private static ModEnableCheck CheckEnableCore(LoadedMod mod, ModEnableCheck check)
	{
		string modLabel = GetModLabel(mod.Manifest);
		check.Fingerprint = ModFingerprint.Compute(mod.FolderPath, mod.Manifest, mod.Source == ModSource.Local);
		if (mod.WorkshopId != 0L && !ModWorkshop.TryGetOwner(mod.WorkshopId, out var _))
		{
			ModWorkshop.RequestDetails(new ulong[1] { mod.WorkshopId });
		}
		List<string> list = (mod.UnlistedDlls = UnlistedAssemblyFiles(mod.FolderPath, mod.Manifest));
		if (list.Count > 0)
		{
			check.Refusal = LocHelper.GetLocalizedUIOrDefault("ui_mods_enable_unlisted", "{0} has DLLs in its folder that its manifest does not list, so it cannot be enabled: {1}", modLabel, string.Join(", ", list));
			return check;
		}
		check.Verdict = ModTrustList.Check(mod.Manifest.ID, mod.Manifest.Author, check.Fingerprint, mod.WorkshopId, WorkshopOwner(mod.WorkshopId), out var reason);
		if (check.Verdict == ModListVerdict.Blocked)
		{
			check.Refusal = LocHelper.GetLocalizedUIOrDefault("ui_mods_enable_blocked", "{0} is blocked by the developers: {1}", modLabel, reason);
			return check;
		}
		List<string> list2 = ModCapability.Order(mod.Manifest.Capabilities.Where(ModCapability.IsRefused)).ToList();
		if (list2.Count > 0)
		{
			check.Refusal = LocHelper.GetLocalizedUIOrDefault("ui_mods_enable_refused_capability", "{0} declares capabilities mods can no longer use:\n{1}\n\nThe mod author needs to update it. Details are in the console (mod-scan).", modLabel, ModCapability.FormatList(list2));
			return check;
		}
		if (ModTrust.IsStopped(check.Fingerprint.Combined, out var entry))
		{
			check.Refusal = LocHelper.GetLocalizedUIOrDefault("ui_mods_enable_stopped", "{0} was stopped by the game on this computer because {1}. This version cannot be enabled again.", modLabel, entry.reason);
			return check;
		}
		if (ModTrustList.IsPaused(check.Verdict))
		{
			check.Refusal = LocHelper.GetLocalizedUIOrDefault("ui_mods_killswitch", "Mod loading is paused by the developers. {0}", ModTrustList.KillSwitchMessage);
			return check;
		}
		check.NeedsVerification = ModTrustList.NeedsVerificationFor(mod.Source, mod.Manifest.Capabilities, check.Verdict);
		if (ModTrustList.WORKSHOP_VERIFIED_ONLY_CAPABILITIES && check.NeedsVerification.Count > 0)
		{
			check.Refusal = LocHelper.GetLocalizedUIOrDefault("ui_mods_enable_needs_verification", "{0} is a Workshop mod that uses files or the internet ({1}). Such mods can only be activated once the developers have verified them; this version is not verified yet.", modLabel, string.Join(", ", check.NeedsVerification.Select(ModCapability.DisplayName)));
			return check;
		}
		List<string> list3 = ListedAssemblyPaths(mod.FolderPath, mod.Manifest);
		List<string> list4 = new List<string>();
		foreach (string item in list3)
		{
			if (!File.Exists(item))
			{
				check.Refusal = LocHelper.GetLocalizedUIOrDefault("ui_mods_enable_missing_dll", "{0} lists {1} in its manifest but the file is missing.", modLabel, Path.GetFileName(item));
				return check;
			}
			try
			{
				string text = AssemblyName.GetAssemblyName(item).Name;
				if (IsGameAssemblyName(text))
				{
					list4.Add(Path.GetFileName(item) + " (" + text + ")");
				}
			}
			catch (Exception ex)
			{
				check.Refusal = LocHelper.GetLocalizedUIOrDefault("ui_mods_enable_unreadable", "{0} could not be scanned: {1}", modLabel, Path.GetFileName(item) + ": " + ex.Message);
				return check;
			}
		}
		if (list4.Count > 0)
		{
			check.Refusal = LocHelper.GetLocalizedUIOrDefault("ui_mods_enable_shadowing", "{0} ships assemblies with the same name as the game's own and cannot be enabled: {1}", modLabel, string.Join(", ", list4));
			return check;
		}
		check.Scan = ModScanner.Scan(list3, ListedAssemblyNames(mod.Manifest), mod.FolderPath, mod.Source == ModSource.Local);
		if (check.Scan.Error != null)
		{
			check.Refusal = LocHelper.GetLocalizedUIOrDefault("ui_mods_enable_unreadable", "{0} could not be scanned: {1}", modLabel, check.Scan.Error);
			return check;
		}
		if (check.Scan.HardFlags.Count > 0)
		{
			check.Refusal = LocHelper.GetLocalizedUIOrDefault("ui_mods_enable_refused_flags", "{0} was refused by the mod scanner:\n{1}", modLabel, check.Scan.FormatFindings(6));
			return check;
		}
		check.Undeclared = ModCapability.Undeclared(check.Scan.Capabilities, mod.Manifest.Capabilities);
		if (check.Undeclared.Count > 0)
		{
			check.Refusal = LocHelper.GetLocalizedUIOrDefault("ui_mods_enable_refused_undeclared", "{0} uses capabilities its manifest does not declare:\n{1}\n\nThe mod author needs to update the manifest. Details are in the console (mod-scan).", modLabel, ModCapability.FormatList(check.Undeclared));
			return check;
		}
		check.PromptCapabilities = ModCapability.Order(mod.Manifest.Capabilities.Concat(check.Scan.DeprecatedCapabilities)).ToList();
		check.NeedsPrompt = check.PromptCapabilities.Count > 0 && check.Verdict != ModListVerdict.Verified;
		return check;
	}

	[ModRestricted("loader-target", "approves a mod, which only the player does")]
	public static void ApproveAndEnable(LoadedMod mod, ModEnableCheck check)
	{
		RequireNoMod("approve a mod");
		if (check != null && check.Refusal == null && check.Mod == mod)
		{
			mod.TrustRecord = ModTrust.Approve(mod, check.Fingerprint, check.Scan.Capabilities);
			mod.Fingerprint = check.Fingerprint;
			mod.ListVerdict = check.Verdict;
			mod.LastScan = check.Scan;
			mod.TrustState = ModTrustState.Match;
			mod.TrustNote = null;
			SetModEnabled(mod.Manifest.ID, enabled: true);
			UnityEngine.Debug.Log($"[ModLoader] '{mod.Manifest.ID}' approved: hash {check.Fingerprint.Combined}, capabilities {ModCapability.FormatIds(check.Scan.Capabilities)}, list {check.Verdict}");
		}
	}

	[ModRestricted("loader-target", "turns developer mode on, which only the player does")]
	public static bool SetDeveloperMode(LoadedMod mod, bool on)
	{
		RequireNoMod("change developer mode");
		if (mod.Source != ModSource.Local)
		{
			return false;
		}
		bool flag = ModTrust.SetDeveloperMode(mod.Manifest.ID, on);
		if (flag)
		{
			mod.TrustRecord = ModTrust.Get(mod.Manifest.ID);
		}
		return flag;
	}

	private static Assembly ResolveModDependency(object sender, ResolveEventArgs args)
	{
		string text;
		try
		{
			text = new AssemblyName(args.Name).Name;
		}
		catch
		{
			return null;
		}
		if (IsGameAssemblyName(text))
		{
			return null;
		}
		List<(string, ModManifest)> list = new List<(string, ModManifest)>();
		if (loadingMod.HasValue)
		{
			list.Add(loadingMod.Value);
		}
		list.AddRange(from m in mods
			where m.IsEnabled || m.Assembly != null
			select (FolderPath: m.FolderPath, Manifest: m.Manifest));
		if (args.RequestingAssembly != null && !args.RequestingAssembly.IsDynamic && !string.IsNullOrEmpty(args.RequestingAssembly.Location))
		{
			string location = Path.GetFullPath(args.RequestingAssembly.Location);
			List<(string, ModManifest)> list2 = list.Where(((string folder, ModManifest manifest) c) => c.folder != null && location.StartsWith(Path.GetFullPath(c.folder), StringComparison.OrdinalIgnoreCase)).ToList();
			if (list2.Count > 0)
			{
				foreach (var item2 in list2.ToList())
				{
					ModManifest item = item2.Item2;
					list2.AddRange(from p in RunningPrerequisites(item)
						select (FolderPath: p.FolderPath, Manifest: p.Manifest));
				}
				list = list2;
			}
		}
		foreach (var item3 in list)
		{
			var (text2, manifest) = item3;
			foreach (string item4 in ListedAssemblyPaths(text2, manifest))
			{
				if (!string.Equals(Path.GetFileNameWithoutExtension(item4), text, StringComparison.OrdinalIgnoreCase) || !File.Exists(item4))
				{
					continue;
				}
				try
				{
					ModFingerprint checkedFiles = ((loadingMod.HasValue && loadingMod.Value.manifest == manifest) ? loadingFingerprint : mods.Find((LoadedMod m) => m.Manifest == manifest)?.Fingerprint);
					VerifyUnchanged(item4, text2, checkedFiles);
					ModSteamGuard.Install();
					return Assembly.LoadFrom(item4);
				}
				catch (Exception ex)
				{
					Debug.LogWarning("[ModLoader] Could not resolve '" + text + "' for '" + manifest.ID + "' from " + item4 + ": " + ex.Message);
				}
			}
		}
		return null;
	}

	private static ulong WorkshopOwner(ulong workshopId)
	{
		if (workshopId == 0L)
		{
			return 0uL;
		}
		if (!ModWorkshop.TryGetOwner(workshopId, out var ownerSteamId))
		{
			return 0uL;
		}
		return ownerSteamId;
	}

	private static void ReapplyListVerdicts()
	{
		try
		{
			foreach (LoadedMod mod in mods)
			{
				ModFingerprint modFingerprint = (fingerprints.TryGetValue(mod.Manifest.ID, out var value) ? value : mod.Fingerprint);
				if (modFingerprint == null || mod.TrustState == ModTrustState.Blocked || mod.TrustState == ModTrustState.Stopped || !IsModEnabled(mod.Manifest.ID))
				{
					continue;
				}
				ModListVerdict modListVerdict = (mod.ListVerdict = ModTrustList.Check(mod.Manifest.ID, mod.Manifest.Author, modFingerprint, mod.WorkshopId, WorkshopOwner(mod.WorkshopId), out var reason));
				if (modListVerdict == ModListVerdict.Blocked)
				{
					bool flag = harmonies.ContainsKey(mod.Manifest.ID) || mod.HarmonyInstance != null;
					ModTrust.Revoke(mod.Manifest.ID);
					DisableSilently(mod.Manifest.ID);
					if (flag)
					{
						StopRunningMod(mod, callOnDisable: true);
					}
					mod.TrustState = ModTrustState.Blocked;
					mod.TrustNote = reason;
					Debug.LogWarning("[" + GetModLabel(mod.Manifest) + "] Blocked by the trust list (" + reason + "); " + (flag ? "stopped now" : "it will not load again") + ".");
				}
				else if (ModTrustList.IsPaused(modListVerdict) && (harmonies.ContainsKey(mod.Manifest.ID) || mod.HarmonyInstance != null))
				{
					StopRunningMod(mod, callOnDisable: true);
					mod.TrustState = ModTrustState.Paused;
					mod.TrustNote = ModTrustList.KillSwitchMessage;
					Debug.LogWarning("[" + GetModLabel(mod.Manifest) + "] Stopped: mod loading is paused by the developers.");
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogError($"[ModLoader] Re-check against the trust list failed: {ex.Message}\n{ex}");
		}
		OnTrustRefreshed?.Invoke();
	}

	internal static void StopRunningMod(LoadedMod mod, bool callOnDisable)
	{
		string modLabel = GetModLabel(mod.Manifest);
		if (callOnDisable && mod.Instance != null)
		{
			try
			{
				mod.Instance.OnDisable();
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[" + modLabel + "] OnDisable threw while stopping: " + ex.Message);
			}
		}
		Harmony harmony = (harmonies.TryGetValue(mod.Manifest.ID, out var value) ? value : mod.HarmonyInstance);
		if (harmony != null)
		{
			try
			{
				harmony.UnpatchAll(harmony.Id);
			}
			catch (Exception ex2)
			{
				Debug.LogWarning("[" + modLabel + "] Could not remove its patches: " + ex2.Message);
			}
		}
		mod.HarmonyInstance = null;
		harmonies.Remove(mod.Manifest.ID);
		mod.IsEnabled = false;
		RestartRequired = true;
		ModSteamAuth.CancelFor(mod.Manifest.ID);
		ModHelper.ForgetModServices(mod.Manifest.ID);
		ModHelper.StopBackgroundWork(mod.Manifest.ID);
		List<Assembly> assemblies = AssembliesOf(mod);
		try
		{
			int num = ModHook.RemoveHandlersOf(assemblies);
			if (num > 0)
			{
				UnityEngine.Debug.Log($"[{modLabel}] {num} hook subscription(s) removed.");
			}
		}
		catch (Exception ex3)
		{
			Debug.LogWarning("[" + modLabel + "] Could not remove its hook subscriptions: " + ex3.Message);
		}
		try
		{
			int num2 = DestroyBehavioursOf(assemblies);
			if (num2 > 0)
			{
				UnityEngine.Debug.Log($"[{modLabel}] {num2} MonoBehaviour(s) of its own types destroyed.");
			}
		}
		catch (Exception ex4)
		{
			Debug.LogWarning("[" + modLabel + "] Could not destroy its MonoBehaviours: " + ex4.Message);
		}
	}

	public static List<Assembly> AssembliesOf(LoadedMod mod)
	{
		List<Assembly> list = new List<Assembly>();
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			if (!assembly.IsDynamic && (assembly == mod.Assembly || ModIdOfAssembly(assembly) == mod.Manifest.ID))
			{
				list.Add(assembly);
			}
		}
		return list;
	}

	private static int DestroyBehavioursOf(ICollection<Assembly> assemblies)
	{
		if (assemblies.Count == 0)
		{
			return 0;
		}
		int num = 0;
		MonoBehaviour[] array = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
		foreach (MonoBehaviour monoBehaviour in array)
		{
			if (!(monoBehaviour == null) && assemblies.Contains(monoBehaviour.GetType().Assembly) && monoBehaviour.gameObject.scene.IsValid())
			{
				UnityEngine.Object.Destroy(monoBehaviour);
				num++;
			}
		}
		return num;
	}

	internal static void OnSteamGuardTripped(string modId, string call, bool processLaunch)
	{
		LoadedMod loadedMod = ((modId != null) ? mods.Find((LoadedMod m) => m.Manifest.ID == modId) : null);
		if (loadedMod == null)
		{
			Debug.LogError("[ModSteamGuard] Blocked " + call + " from code that belongs to no mod; no mod was stopped.");
			return;
		}
		string modLabel = GetModLabel(loadedMod.Manifest);
		string text = (processLaunch ? LocHelper.GetLocalizedUIOrDefault("ui_mods_note_stopped_process", "it tried to start another program, which mods are never allowed to do") : LocHelper.GetLocalizedUIOrDefault("ui_mods_note_stopped", "it tried to use your Steam account in a way mods are never allowed to ({0})", call));
		if (loadedMod.TrustState != ModTrustState.Stopped)
		{
			using (Privileged())
			{
				ModFingerprint modFingerprint = (fingerprints.TryGetValue(loadedMod.Manifest.ID, out var value) ? value : (loadedMod.Fingerprint ?? ModFingerprint.Compute(loadedMod.FolderPath, loadedMod.Manifest)));
				ModTrust.MarkStopped(loadedMod.Manifest.ID, modFingerprint.Combined, text);
				SetModEnabled(loadedMod.Manifest.ID, enabled: false);
				StopRunningMod(loadedMod, callOnDisable: false);
			}
			loadedMod.TrustState = ModTrustState.Stopped;
			loadedMod.TrustNote = text;
			OnTrustRefreshed?.Invoke();
		}
		Debug.LogError(string.Format("[{0}] Stopped by ModSteamGuard: blocked {1} (workshop={2}, owner={3}, hash={4}). Report this mod to the developers.", modLabel, call, loadedMod.WorkshopId, WorkshopOwner(loadedMod.WorkshopId), loadedMod.Fingerprint?.Combined ?? "-"));
	}

	private static void LogAudit()
	{
		List<string> list = new List<string>();
		list.Add("[ModLoader] audit: game " + Application.version + ", trust list: " + ModTrustList.Describe());
		foreach (LoadedMod mod in mods)
		{
			string text = mod.Fingerprint?.Combined ?? "-";
			string text2;
			if (mod.LastScan != null)
			{
				text2 = ModCapability.FormatIds(mod.LastScan.Capabilities);
			}
			else
			{
				text2 = ((mod.TrustRecord != null) ? ModCapability.FormatIds(mod.TrustRecord.detected) : ("declared " + ModCapability.FormatIds(mod.Manifest.Capabilities)));
			}
			list.Add(string.Format("[ModLoader] audit: {0} v{1} source={2} workshop={3} owner={4} running={5} trust={6}{7} caps={8} hash={9}{10}{11}", mod.Manifest.ID, mod.Manifest.ModVersion, mod.Source, mod.WorkshopId, WorkshopOwner(mod.WorkshopId), mod.IsEnabled, mod.TrustState, (mod.ListVerdict != ModListVerdict.None) ? ("/" + mod.ListVerdict) : "", text2, text, (mod.TrustNote != null) ? (" note=\"" + mod.TrustNote + "\"") : "", (mod.LoadError != null) ? (" error=\"" + mod.LoadError + "\"") : ""));
		}
		foreach (string item in list)
		{
			UnityEngine.Debug.Log(item);
		}
		try
		{
			string stamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
			if (File.Exists(AuditPath) && new FileInfo(AuditPath).Length > 524288)
			{
				string[] array = File.ReadAllLines(AuditPath);
				File.WriteAllLines(AuditPath, array.Skip(array.Length / 2));
			}
			File.AppendAllLines(AuditPath, list.Select((string l) => stamp + " " + l).Concat(new string[1] { stamp + (ModTrust.TamperedOnLoad ? " [ModLoader] audit: trust store signature did not match; approvals discarded" : " [ModLoader] audit: end") }));
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[ModLoader] Could not write " + AuditPath + ": " + ex.Message);
		}
	}

	private void OnDestroy()
	{
		if (!(Instance != this))
		{
			string text = CallingModId();
			Debug.LogError("[ModLoader] The mod loader object was destroyed" + ((text != null) ? (" by mod '" + text + "'") : "") + "; blocks and tripwire hits can no longer stop mods this session.");
		}
	}

	private static ModManifest ParseManifest(string folderPath)
	{
		ModManifest modManifest = ParseManifestCore(folderPath, out var problem, out var missing);
		if (modManifest == null)
		{
			if (missing)
			{
				Debug.LogWarning("[ModLoader] " + problem + ", skipping folder.");
				return modManifest;
			}
			Debug.LogError("[ModLoader] " + problem + ", skipping.");
		}
		return modManifest;
	}

	public static ModManifest ParseManifestForTools(string folderPath, out string problem)
	{
		bool missing;
		return ParseManifestCore(folderPath, out problem, out missing);
	}

	private static ModManifest ParseManifestCore(string folderPath, out string problem, out bool missing)
	{
		problem = null;
		missing = false;
		string text = Path.Combine(folderPath, "manifest.xml");
		if (!File.Exists(text))
		{
			missing = true;
			problem = "No manifest.xml found in " + folderPath;
			return null;
		}
		try
		{
			XElement root = XDocument.Load(text).Root;
			ModManifest modManifest = new ModManifest
			{
				ID = (root.Element("ID")?.Value?.Trim() ?? ""),
				Name = (root.Element("Name")?.Value?.Trim() ?? ""),
				Author = (root.Element("Author")?.Value?.Trim() ?? ""),
				ModVersion = (root.Element("ModVersion")?.Value?.Trim() ?? ""),
				Description = (root.Element("Description")?.Value?.Trim() ?? "")
			};
			XElement xElement = root.Element("GameVersions");
			if (xElement != null)
			{
				modManifest.GameVersions = (from e in xElement.Elements("Version")
					select e.Value.Trim() into v
					where !string.IsNullOrEmpty(v)
					select v).ToList();
			}
			XElement xElement2 = root.Element("Prerequisites");
			if (xElement2 != null)
			{
				modManifest.Prerequisites = (from e in xElement2.Elements("Mod")
					select e.Value.Trim() into v
					where !string.IsNullOrEmpty(v)
					select v).ToList();
			}
			if (string.IsNullOrEmpty(modManifest.ID))
			{
				problem = "manifest.xml in " + folderPath + " is missing a required <ID> element";
				return null;
			}
			if (!IsValidModId(modManifest.ID))
			{
				problem = "manifest.xml in " + folderPath + " has an invalid <ID> '" + modManifest.ID + "' (letters, digits, '_', '-' and '.', not starting with '.', at most 64 characters)";
				return null;
			}
			XElement xElement3 = root.Element("Assemblies");
			if (xElement3 == null)
			{
				problem = "manifest.xml of '" + modManifest.ID + "' has no <Assemblies> element (list every DLL to load: <Assemblies><Assembly>MyMod.dll</Assembly></Assemblies>)";
				return null;
			}
			modManifest.Assemblies = (from e in xElement3.Elements("Assembly")
				select e.Value.Trim().Replace('\\', '/') into v
				where !string.IsNullOrEmpty(v)
				select v).ToList();
			HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			modManifest.Assemblies = modManifest.Assemblies.Where((string a) =>
			{
				string item;
				try
				{
					item = Path.GetFullPath(Path.Combine(folderPath, a));
				}
				catch
				{
					item = a;
				}
				return seenPaths.Add(item);
			}).ToList();
			if (modManifest.Assemblies.Count == 0)
			{
				problem = "manifest.xml of '" + modManifest.ID + "' lists no <Assembly> under <Assemblies>";
				return null;
			}
			foreach (string assembly in modManifest.Assemblies)
			{
				if (assembly.Contains("..") || Path.IsPathRooted(assembly) || !assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
				{
					problem = "manifest.xml of '" + modManifest.ID + "' lists an invalid assembly path '" + assembly + "' (relative .dll paths inside the mod folder only)";
					return null;
				}
			}
			XElement xElement4 = root.Element("Capabilities");
			if (xElement4 == null)
			{
				problem = "manifest.xml of '" + modManifest.ID + "' has no <Capabilities> element (write <Capabilities/> when the mod needs none)";
				return null;
			}
			modManifest.Capabilities = (from e in xElement4.Elements("Capability")
				select e.Value.Trim().ToLowerInvariant() into v
				where !string.IsNullOrEmpty(v)
				select v).Distinct().ToList();
			foreach (string capability in modManifest.Capabilities)
			{
				if (!ModCapability.IsKnown(capability))
				{
					problem = "manifest.xml of '" + modManifest.ID + "' declares an unknown capability '" + capability + "' (known: " + string.Join(", ", ModCapability.All) + ")";
					return null;
				}
			}
			return modManifest;
		}
		catch (Exception ex)
		{
			problem = "Failed to parse manifest.xml in " + folderPath + ": " + ex.Message;
			return null;
		}
	}

	private void LoadDiscoveredMods()
	{
		List<DiscoveredMod> list = new List<DiscoveredMod>();
		foreach (string item3 in LocalModFolders())
		{
			ModManifest modManifest = ParseManifest(item3);
			if (modManifest != null)
			{
				list.Add(new DiscoveredMod
				{
					Folder = item3,
					Source = ModSource.Local,
					Manifest = modManifest
				});
			}
		}
		foreach (var item4 in WorkshopModFolders())
		{
			ulong item = item4.id;
			string item2 = item4.folder;
			ModManifest modManifest2 = ParseManifest(item2);
			if (modManifest2 != null)
			{
				list.Add(new DiscoveredMod
				{
					Folder = item2,
					Source = ModSource.Workshop,
					WorkshopId = item,
					Manifest = modManifest2
				});
			}
		}
		foreach (DiscoveredMod item5 in OrderByPrerequisites(list))
		{
			LoadModFromFolder(item5.Folder, item5.Manifest, item5.Source, item5.WorkshopId);
		}
		UnityEngine.Debug.Log($"[ModLoader] === {mods.Count} mod(s) registered, {Enumerable.Count(mods, (LoadedMod m) => m.IsEnabled)} enabled ===");
	}

	private IEnumerable<string> LocalModFolders()
	{
		if (!System.IO.Directory.Exists(ModsPath))
		{
			System.IO.Directory.CreateDirectory(ModsPath);
			UnityEngine.Debug.Log("[ModLoader] Created Mods folder at: " + ModsPath);
			return Enumerable.Empty<string>();
		}
		string[] directories = System.IO.Directory.GetDirectories(ModsPath);
		UnityEngine.Debug.Log($"[ModLoader] Found {directories.Length} mod folder(s) in {ModsPath}");
		return directories;
	}

	private static List<DiscoveredMod> OrderByPrerequisites(List<DiscoveredMod> discovered)
	{
		List<DiscoveredMod> ordered = new List<DiscoveredMod>(discovered.Count);
		HashSet<int> done = new HashSet<int>();
		HashSet<int> visiting = new HashSet<int>();
		for (int i = 0; i < discovered.Count; i++)
		{
			Visit(i);
		}
		return ordered;
		void Visit(int index)
		{
			if (!done.Contains(index))
			{
				if (!visiting.Add(index))
				{
					Debug.LogWarning("[ModLoader] '" + discovered[index].Manifest.ID + "' is part of a prerequisite cycle; its load order is left as found.");
				}
				else
				{
					foreach (string prerequisite in discovered[index].Manifest.Prerequisites)
					{
						if (!(prerequisite == discovered[index].Manifest.ID))
						{
							for (int j = 0; j < discovered.Count; j++)
							{
								if (discovered[j].Manifest.ID == prerequisite)
								{
									Visit(j);
								}
							}
						}
					}
					visiting.Remove(index);
					if (done.Add(index))
					{
						ordered.Add(discovered[index]);
					}
				}
			}
		}
	}

	private void ApplySavedOrder()
	{
		if (_modOrder.Count == 0)
		{
			return;
		}
		mods.Sort((LoadedMod a, LoadedMod b) =>
		{
			int num = _modOrder.IndexOf(a.Manifest.ID);
			int num2 = _modOrder.IndexOf(b.Manifest.ID);
			if (num < 0)
			{
				num = int.MaxValue;
			}
			if (num2 < 0)
			{
				num2 = int.MaxValue;
			}
			return num.CompareTo(num2);
		});
	}

	private List<(ulong id, string folder)> WorkshopModFolders()
	{
		if (!ModWorkshop.IsAvailable)
		{
			UnityEngine.Debug.Log("[ModLoader] Steam Workshop not available (not the main app, or Steam not running); local mods only.");
			return new List<(ulong, string)>();
		}
		ModWorkshop.EnsureCallbacks();
		List<(ulong, string)> installedItems = ModWorkshop.GetInstalledItems();
		UnityEngine.Debug.Log($"[ModLoader] Found {installedItems.Count} installed Workshop item(s), {ModWorkshop.PendingDownloads} still downloading");
		ModWorkshop.RequestDetails(installedItems.Select(((ulong id, string folder) i) => i.id));
		return installedItems;
	}

	[ModRestricted("loader-target", "drops a mod from the list, which only the player does")]
	public static void ForgetMod(string id)
	{
		RequireNoMod("forget a mod");
		SetModEnabled(id, enabled: false);
		mods.RemoveAll((LoadedMod m) => m.Manifest.ID == id);
	}

	public static List<string> ListedAssemblyPaths(string folderPath, ModManifest manifest)
	{
		return manifest.Assemblies.Select((string a) => Path.GetFullPath(Path.Combine(folderPath, a))).ToList();
	}

	public static List<string> UnlistedAssemblyFiles(string folderPath, ModManifest manifest)
	{
		HashSet<string> listed = new HashSet<string>(ListedAssemblyPaths(folderPath, manifest), StringComparer.OrdinalIgnoreCase);
		string root = Path.GetFullPath(folderPath).TrimEnd('\\', '/');
		return (from p in FolderDlls(folderPath).Select(Path.GetFullPath)
			where !listed.Contains(p)
			select p.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/')).ToList();
	}

	private static IEnumerable<string> FolderDlls(string folderPath)
	{
		return new string[2]
		{
			folderPath,
			Path.Combine(folderPath, "Assemblies")
		}.Where((string p) => System.IO.Directory.Exists(p)).SelectMany((string p) => System.IO.Directory.GetFiles(p, "*.dll"));
	}

	private static bool AnyReferencesIMod(IEnumerable<string> dllPaths)
	{
		foreach (string dllPath in dllPaths)
		{
			try
			{
				if (ContainsBytes(File.ReadAllBytes(dllPath), IModNameBytes))
				{
					return true;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[ModLoader] Could not read " + dllPath + ": " + ex.Message);
			}
		}
		return false;
	}

	private static bool ContainsBytes(byte[] data, byte[] pattern)
	{
		for (int i = 0; i <= data.Length - pattern.Length; i++)
		{
			int j;
			for (j = 0; j < pattern.Length && data[i + j] == pattern[j]; j++)
			{
			}
			if (j == pattern.Length)
			{
				return true;
			}
		}
		return false;
	}

	private static bool IsWellFormed(string folderPath, ModManifest manifest, ModSource source)
	{
		string text = null;
		List<string> list = ListedAssemblyPaths(folderPath, manifest);
		string root = Path.GetFullPath(folderPath).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
		string text2 = list.FirstOrDefault((string p) => !p.StartsWith(root, StringComparison.OrdinalIgnoreCase));
		string text3 = list.FirstOrDefault((string p) => !File.Exists(p));
		if (text2 != null)
		{
			text = "<Assemblies> entry '" + Path.GetFileName(text2) + "' points outside the mod folder";
		}
		else if (text3 != null)
		{
			text = "listed assembly not found: " + Path.GetFileName(text3) + " (every <Assembly> in the manifest must exist)";
		}
		else if (!AnyReferencesIMod(list))
		{
			text = "none of the listed DLLs contains a class implementing IMod";
		}
		if (text == null)
		{
			return true;
		}
		Debug.LogError("[" + GetModLabel(manifest) + "] Error: " + text + ". The mod is not loaded (" + folderPath + ").");
		if (source == ModSource.Local)
		{
			_rejectedLocalFolders++;
		}
		return false;
	}

	private void LoadModFromFolder(string folderPath, ModManifest manifest, ModSource source, ulong workshopId)
	{
		if (manifest == null)
		{
			return;
		}
		LoadedMod loadedMod = mods.FirstOrDefault((LoadedMod m) => m.Manifest.ID == manifest.ID);
		if (loadedMod != null && loadedMod.FolderPath != folderPath)
		{
			int num;
			if (loadedMod.Source == ModSource.Local)
			{
				num = ((source == ModSource.Workshop) ? 1 : 0);
				if (num != 0 && loadedMod.ShadowedWorkshopId == 0L)
				{
					loadedMod.ShadowedWorkshopId = workshopId;
				}
			}
			else
			{
				num = 0;
			}
			string text = ((num != 0) ? "the local copy wins over the Workshop copy" : "first folder wins");
			Debug.LogWarning("[ModLoader] Duplicate mod ID '" + manifest.ID + "' in '" + folderPath + "' (already loaded from '" + loadedMod.FolderPath + "'), skipping: " + text + ".");
		}
		else
		{
			if (!IsWellFormed(folderPath, manifest, source))
			{
				return;
			}
			bool flag = IsModEnabled(manifest.ID);
			List<string> list = UnlistedAssemblyFiles(folderPath, manifest);
			if (list.Count > 0)
			{
				Debug.LogWarning(string.Format("[{0}] {1} DLL(s) in the mod folder are not listed in <Assemblies> and are never loaded: {2}", GetModLabel(manifest), list.Count, string.Join(", ", list)));
			}
			if (!flag)
			{
				LoadedMod loadedMod2 = new LoadedMod
				{
					Manifest = manifest,
					FolderPath = folderPath,
					IsEnabled = false,
					Source = source,
					WorkshopId = workshopId,
					UnlistedDlls = list
				};
				if (ModTrust.Stopped.Any((ModTrust.StoppedEntry s) => s.id == manifest.ID))
				{
					try
					{
						loadedMod2.Fingerprint = ModFingerprint.Compute(folderPath, manifest);
						if (ModTrust.IsStopped(loadedMod2.Fingerprint.Combined, out var entry))
						{
							loadedMod2.TrustState = ModTrustState.Stopped;
							loadedMod2.TrustNote = entry.reason;
						}
					}
					catch (Exception ex)
					{
						Debug.LogWarning("[" + GetModLabel(manifest) + "] Could not hash the folder: " + ex.Message);
					}
				}
				mods.Add(loadedMod2);
				UnityEngine.Debug.Log($"[ModLoader]   Registered (disabled, {source}): {manifest.Name} v{manifest.ModVersion}");
				return;
			}
			TrustGate trustGate;
			try
			{
				trustGate = EvaluateTrust(folderPath, manifest, source, workshopId, list);
			}
			catch (Exception ex2)
			{
				Debug.LogError($"[{GetModLabel(manifest)}] Trust check failed: {ex2.Message}\n{ex2}");
				trustGate = new TrustGate
				{
					State = ModTrustState.Changed,
					Note = LocHelper.GetLocalizedUIOrDefault("ui_mods_note_check_failed", "its files could not be checked")
				};
			}
			if (!trustGate.Load)
			{
				mods.Add(new LoadedMod
				{
					Manifest = manifest,
					FolderPath = folderPath,
					IsEnabled = false,
					Source = source,
					WorkshopId = workshopId,
					UnlistedDlls = list,
					TrustState = trustGate.State,
					TrustNote = trustGate.Note,
					Fingerprint = trustGate.Fingerprint,
					TrustRecord = trustGate.Record,
					ListVerdict = trustGate.Verdict,
					LastScan = trustGate.Scan
				});
				Debug.LogWarning(string.Format("[{0}] Not loaded: {1}{2}.", GetModLabel(manifest), trustGate.State, (trustGate.Note != null) ? (" (" + trustGate.Note + ")") : ""));
				return;
			}
			List<(string, Exception)> list2 = new List<(string, Exception)>();
			loadingMod = (folderPath, manifest);
			loadingFingerprint = trustGate.Fingerprint;
			try
			{
				foreach (string item2 in ListedAssemblyPaths(folderPath, manifest))
				{
					try
					{
						LoadModDll(item2, folderPath, manifest, source, workshopId, trustGate.Fingerprint);
					}
					catch (Exception item)
					{
						list2.Add((Path.GetFileName(item2), item));
					}
				}
			}
			finally
			{
				loadingMod = null;
				loadingFingerprint = null;
			}
			LoadedMod loadedMod3 = mods.LastOrDefault((LoadedMod m) => m.Manifest == manifest);
			if (loadedMod3 == null)
			{
				loadedMod3 = new LoadedMod
				{
					Manifest = manifest,
					FolderPath = folderPath,
					IsEnabled = false,
					Source = source,
					WorkshopId = workshopId
				};
				mods.Add(loadedMod3);
			}
			loadedMod3.UnlistedDlls = list;
			loadedMod3.TrustState = trustGate.State;
			loadedMod3.TrustNote = trustGate.Note;
			loadedMod3.Fingerprint = trustGate.Fingerprint;
			loadedMod3.TrustRecord = trustGate.Record;
			loadedMod3.ListVerdict = trustGate.Verdict;
			loadedMod3.LastScan = trustGate.Scan;
			if (loadedMod3.Instance != null && list2.Count == 0)
			{
				return;
			}
			foreach (var (text2, e) in list2)
			{
				ReportLoadError(loadedMod3, e, "loading " + text2);
			}
			if (loadedMod3.LoadError == null)
			{
				loadedMod3.LoadError = "No class in the mod's DLLs implements IMod";
				Debug.LogError("[" + GetModLabel(manifest) + "] Error: " + loadedMod3.LoadError + ". The mod is not loaded.");
			}
		}
	}

	private void LoadModDll(string dllPath, string modFolder, ModManifest manifest, ModSource source, ulong workshopId, ModFingerprint checkedFiles)
	{
		string fileName = Path.GetFileName(dllPath);
		UnityEngine.Debug.Log("[ModLoader] Loading: " + fileName);
		AssemblyName assemblyName = AssemblyName.GetAssemblyName(dllPath);
		if (IsGameAssemblyName(assemblyName.Name))
		{
			throw new InvalidOperationException(fileName + " has the assembly name '" + assemblyName.Name + "', which belongs to the game or its frameworks; a mod cannot ship its own copy");
		}
		VerifyUnchanged(dllPath, modFolder, checkedFiles);
		ModSteamGuard.Install();
		Assembly assembly = Assembly.LoadFrom(dllPath);
		List<Type> list = (from t in assembly.GetTypes()
			where typeof(IMod).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract
			select t).ToList();
		if (list.Count == 0)
		{
			Debug.LogWarning("[ModLoader]   No IMod implementation found in " + fileName + ", skipping.");
			return;
		}
		if (list.Count > 1)
		{
			Debug.LogWarning(string.Format("[ModLoader]   {0} contains {1} IMod classes ({2}); only the first will be used.", fileName, list.Count, string.Join(", ", list.Select((Type t) => t.FullName))));
		}
		IMod mod = (IMod)Activator.CreateInstance(list[0]);
		string text = "mod." + manifest.ID;
		Harmony harmony = new Harmony(text);
		bool flag = false;
		Exception ex = null;
		try
		{
			harmony.PatchAll(assembly);
			int num = harmony.GetPatchedMethods().Count();
			UnityEngine.Debug.Log($"[ModLoader]   Harmony: {num} method(s) patched");
			mod.Init(manifest);
			mod.OnEnable();
			flag = true;
		}
		catch (Exception ex2)
		{
			ex = ex2;
			try
			{
				harmony.UnpatchAll(text);
			}
			catch (Exception ex3)
			{
				Debug.LogWarning("[ModLoader] Cleanup unpatch failed for '" + manifest.Name + "': " + ex3.Message);
			}
			harmony = null;
		}
		LoadedMod loadedMod = new LoadedMod
		{
			Instance = mod,
			Manifest = manifest,
			Assembly = assembly,
			HarmonyInstance = harmony,
			FolderPath = modFolder,
			DllPath = dllPath,
			IsEnabled = flag,
			Source = source,
			WorkshopId = workshopId
		};
		mods.Add(loadedMod);
		if (harmony != null)
		{
			harmonies[manifest.ID] = harmony;
		}
		if (checkedFiles != null)
		{
			fingerprints[manifest.ID] = checkedFiles;
		}
		if (ex != null)
		{
			ReportLoadError(loadedMod, ex, "Harmony patching or Init");
		}
		UnityEngine.Debug.Log("[ModLoader]   " + (flag ? "Initialized" : "Init failed") + ": " + manifest.Name + " v" + manifest.ModVersion);
	}

	private static void VerifyUnchanged(string dllPath, string modFolder, ModFingerprint checkedFiles)
	{
		if (checkedFiles == null)
		{
			return;
		}
		string text = Path.GetFullPath(modFolder).TrimEnd('\\', '/');
		string relativePath = Path.GetFullPath(dllPath).Substring(text.Length).TrimStart('\\', '/')
			.Replace('\\', '/');
		string text2 = checkedFiles.HashOf(relativePath);
		if (text2 != null)
		{
			string text3 = ModFingerprint.HashFile(dllPath);
			if (!string.Equals(text2, text3, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException(Path.GetFileName(dllPath) + " changed between the trust check and the load (expected " + text2.Substring(0, 12) + "..., found " + text3.Substring(0, 12) + "...); restart the game");
			}
		}
	}

	private void ValidatePrerequisites()
	{
		HashSet<string> hashSet = new HashSet<string>(from m in mods
			where m.IsEnabled
			select m.Manifest.ID);
		foreach (LoadedMod item in mods.Where((LoadedMod m) => m.IsEnabled))
		{
			foreach (string prerequisite in item.Manifest.Prerequisites)
			{
				if (!hashSet.Contains(prerequisite))
				{
					Debug.LogWarning("[ModLoader] '" + item.Manifest.Name + "' requires '" + prerequisite + "' which is not loaded/enabled.");
				}
			}
		}
	}

	private void LoadModAssets()
	{
		foreach (LoadedMod mod in mods)
		{
			if (mod.IsEnabled)
			{
				LoadTextures(mod);
				LoadLocalization(mod);
				ModLootFile.Load(mod);
				PaperBookRegistry.LoadMod(mod);
				StartCoroutine(LoadSoundsCoroutine(mod));
			}
		}
	}

	private void LoadTextures(LoadedMod mod)
	{
		string text = Path.Combine(mod.FolderPath, "Textures");
		if (System.IO.Directory.Exists(text))
		{
			LoadTexturesFromFolder(mod, text, "");
			string text2 = Path.Combine(text, "Items");
			if (System.IO.Directory.Exists(text2))
			{
				LoadTexturesFromFolder(mod, text2, "Items/", pixelArt: true);
			}
			string text3 = Path.Combine(text, "Paper");
			if (System.IO.Directory.Exists(text3))
			{
				LoadPaperTextures(mod, text3);
			}
			UnityEngine.Debug.Log($"[ModLoader] {mod.Manifest.Name}: {mod.Textures.Count} texture(s), {mod.Sprites.Count} item sprite(s), {mod.PaperSprites.Count} paper sprite(s) loaded");
		}
	}

	private void LoadTexturesFromFolder(LoadedMod mod, string folderPath, string keyPrefix, bool pixelArt = false)
	{
		foreach (string item in System.IO.Directory.GetFiles(folderPath, "*.png").Concat(System.IO.Directory.GetFiles(folderPath, "*.jpg")))
		{
			Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, !pixelArt);
			texture2D.LoadImage(File.ReadAllBytes(item));
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item);
			if (pixelArt)
			{
				texture2D.filterMode = FilterMode.Point;
				texture2D.wrapMode = TextureWrapMode.Clamp;
				texture2D.Apply();
				mod.Sprites[fileNameWithoutExtension] = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f);
				UnityEngine.Debug.Log("[ModLoader] Item sprite loaded: " + fileNameWithoutExtension + " (" + mod.Manifest.Name + ")");
			}
			else
			{
				mod.Textures[keyPrefix + fileNameWithoutExtension] = texture2D;
				UnityEngine.Debug.Log("[ModLoader] Texture loaded: " + keyPrefix + fileNameWithoutExtension + " (" + mod.Manifest.Name + ")");
			}
		}
	}

	private void LoadPaperTextures(LoadedMod mod, string folderPath)
	{
		foreach (string item in System.IO.Directory.GetFiles(folderPath, "*.png").Concat(System.IO.Directory.GetFiles(folderPath, "*.jpg")))
		{
			Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			if (!texture2D.LoadImage(File.ReadAllBytes(item)))
			{
				Debug.LogWarning("[ModLoader] Paper texture " + Path.GetFileName(item) + " could not be decoded (" + mod.Manifest.Name + ")");
				UnityEngine.Object.Destroy(texture2D);
				continue;
			}
			texture2D.filterMode = FilterMode.Bilinear;
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item);
			Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
			sprite.name = fileNameWithoutExtension;
			mod.PaperSprites[fileNameWithoutExtension] = sprite;
			mod.Textures["Paper/" + fileNameWithoutExtension] = texture2D;
			UnityEngine.Debug.Log("[ModLoader] Paper sprite loaded: " + fileNameWithoutExtension + " (" + mod.Manifest.Name + ")");
		}
	}

	private void LoadLocalization(LoadedMod mod)
	{
		string path = Path.Combine(mod.FolderPath, "Localization");
		if (!System.IO.Directory.Exists(path))
		{
			return;
		}
		int num = 0;
		string[] files = System.IO.Directory.GetFiles(path, "*.csv");
		foreach (string path2 in files)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path2);
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			string[] array = File.ReadAllLines(path2);
			for (int j = 0; j < array.Length; j++)
			{
				string text = array[j];
				if (string.IsNullOrWhiteSpace(text) || text.TrimStart().StartsWith("#"))
				{
					continue;
				}
				int num2 = text.IndexOf(',');
				if (num2 < 0)
				{
					Debug.LogWarning($"[ModLoader] {Path.GetFileName(path2)} line {j + 1}: no comma separator, line skipped ({mod.Manifest.Name})");
					continue;
				}
				string text2 = text.Substring(0, num2).Trim();
				if (text2.Length == 0)
				{
					Debug.LogWarning($"[ModLoader] {Path.GetFileName(path2)} line {j + 1}: empty key, line skipped ({mod.Manifest.Name})");
				}
				else
				{
					dictionary[text2] = ParseCsvValue(text.Substring(num2 + 1));
				}
			}
			mod.Localization[fileNameWithoutExtension] = dictionary;
			num += dictionary.Count;
		}
		if (mod.Localization.Count > 0)
		{
			UnityEngine.Debug.Log($"[ModLoader] {mod.Manifest.Name}: {mod.Localization.Count} locale(s), {num} localization entries loaded");
		}
	}

	private static string ParseCsvValue(string raw)
	{
		string text = raw.Trim();
		if (text.Length >= 2 && text.StartsWith("\"") && text.EndsWith("\""))
		{
			text = text.Substring(1, text.Length - 2).Replace("\"\"", "\"");
		}
		return text.Replace("\\n", "\n");
	}

	private IEnumerator LoadSoundsCoroutine(LoadedMod mod)
	{
		string soundsPath = Path.Combine(mod.FolderPath, "Sounds");
		if (System.IO.Directory.Exists(soundsPath))
		{
			(string, AudioType)[] array = new (string, AudioType)[3]
			{
				("*.wav", AudioType.WAV),
				("*.ogg", AudioType.OGGVORBIS),
				("*.mp3", AudioType.MPEG)
			};
			(string, AudioType)[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				(string, AudioType) tuple = array2[i];
				string item = tuple.Item1;
				AudioType audioType = tuple.Item2;
				string[] files = System.IO.Directory.GetFiles(soundsPath, item);
				foreach (string file in files)
				{
					using UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + file, audioType);
					yield return www.SendWebRequest();
					if (www.result == UnityWebRequest.Result.Success)
					{
						string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
						AudioClip content = DownloadHandlerAudioClip.GetContent(www);
						mod.Sounds[fileNameWithoutExtension] = content;
						AudioDirectory.RegisterModClip(ModHelper.GetSoundIdentifier(mod.Manifest.ID, fileNameWithoutExtension), content);
						UnityEngine.Debug.Log("[ModLoader] Sound loaded: " + Path.GetFileName(file) + " (" + mod.Manifest.Name + ")");
					}
					else
					{
						Debug.LogWarning("[ModLoader] Failed to load sound " + Path.GetFileName(file) + ": " + www.error);
					}
				}
			}
			UnityEngine.Debug.Log($"[ModLoader] {mod.Manifest.Name}: {mod.Sounds.Count} sound(s) loaded");
		}
		ModHook.FireOnModAssetsLoaded(mod);
	}

	private void OnApplicationQuit()
	{
		ModSteamAuth.CancelAll();
		foreach (LoadedMod mod in mods)
		{
			if (mod.HarmonyInstance != null)
			{
				try
				{
					mod.Instance.OnDisable();
					mod.HarmonyInstance.UnpatchAll(mod.HarmonyInstance.Id);
				}
				catch (Exception ex)
				{
					Debug.LogWarning("[ModLoader] Error disabling " + mod.Manifest.Name + ": " + ex.Message);
				}
			}
		}
	}
}
