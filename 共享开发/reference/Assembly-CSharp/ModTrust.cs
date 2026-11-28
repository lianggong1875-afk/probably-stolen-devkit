using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class ModTrust
{
	[Serializable]
	public class StoppedEntry
	{
		public string id;

		public string hash;

		public string reason;

		public string at;
	}

	[Serializable]
	private class Store
	{
		public List<ModTrustRecord> records = new List<ModTrustRecord>();

		public List<StoppedEntry> stopped = new List<StoppedEntry>();
	}

	public const string ES3_KEY = "modTrust";

	private static Store store = new Store();

	private static bool loaded;

	public const string MAC_ES3_KEY = "modTrustMac";

	public static IReadOnlyList<ModTrustRecord> Records
	{
		get
		{
			EnsureLoaded();
			return store.records;
		}
	}

	public static bool TamperedOnLoad { get; private set; }

	public static IReadOnlyList<StoppedEntry> Stopped
	{
		get
		{
			EnsureLoaded();
			return store.stopped;
		}
	}

	public static void EnsureLoaded()
	{
		if (loaded)
		{
			return;
		}
		loaded = true;
		try
		{
			if (ES3.KeyExists("modTrust"))
			{
				string json = ES3.Load<string>("modTrust");
				Store store = JsonUtility.FromJson<Store>(json);
				if (store != null && store.records != null)
				{
					ModTrust.store = store;
				}
				Store store2 = ModTrust.store;
				if (store2.stopped == null)
				{
					store2.stopped = new List<StoppedEntry>();
				}
				string text = (ES3.KeyExists("modTrustMac") ? ES3.Load<string>("modTrustMac") : null);
				if (text == null)
				{
					Save();
				}
				else if (!string.Equals(text, ComputeMac(json), StringComparison.Ordinal))
				{
					TamperedOnLoad = true;
					Debug.LogError($"[ModTrust] The trust store does not match its signature; {ModTrust.store.records.Count} approval(s) discarded, {ModTrust.store.stopped.Count} stop(s) kept. Every active mod needs Review and enable.");
					ModTrust.store.records.Clear();
					Save();
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogError("[ModTrust] Could not read the trust store, starting empty: " + ex.Message);
			ModTrust.store = new Store();
		}
	}

	private static void Save()
	{
		string text = JsonUtility.ToJson(store);
		ES3.Save("modTrust", text);
		ES3.Save("modTrustMac", ComputeMac(text));
	}

	private static string ComputeMac(string json)
	{
		string s = "dse-mod-trust-1|" + SafeDeviceId() + "|" + Application.persistentDataPath + "|" + Application.productName;
		using SHA256 sHA = SHA256.Create();
		using HMACSHA256 hMACSHA = new HMACSHA256(sHA.ComputeHash(Encoding.UTF8.GetBytes(s)));
		return ModFingerprint.ToHexPublic(hMACSHA.ComputeHash(Encoding.UTF8.GetBytes(json)));
	}

	private static string SafeDeviceId()
	{
		try
		{
			return SystemInfo.deviceUniqueIdentifier ?? "";
		}
		catch
		{
			return "";
		}
	}

	public static ModTrustRecord Get(string id)
	{
		EnsureLoaded();
		return store.records.Find((ModTrustRecord r) => r.id == id);
	}

	[ModRestricted("loader-target", "writes an approval, which only the player's click does")]
	public static ModTrustRecord Approve(LoadedMod mod, ModFingerprint fingerprint, IEnumerable<string> detected)
	{
		ModLoader.RequireNoMod("write a trust record");
		EnsureLoaded();
		ModTrustRecord modTrustRecord = Get(mod.Manifest.ID);
		string text = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
		ModTrustRecord record = new ModTrustRecord
		{
			id = mod.Manifest.ID,
			source = mod.Source.ToString(),
			workshopId = mod.WorkshopId.ToString(CultureInfo.InvariantCulture),
			manifestHash = fingerprint.ManifestHash,
			files = fingerprint.FileEntries(),
			combinedHash = fingerprint.Combined,
			folderHash = fingerprint.FolderHash,
			declared = ModCapability.Order(mod.Manifest.Capabilities).ToList(),
			detected = ModCapability.Order(detected).ToList(),
			gameVersion = Application.version,
			approvedAt = text,
			updatedAt = text,
			developerMode = (modTrustRecord?.developerMode ?? false),
			scannerVersion = 4
		};
		store.records.RemoveAll((ModTrustRecord r) => r.id == record.id);
		store.records.Add(record);
		Save();
		return record;
	}

	[ModRestricted("loader-target", "rewrites an approval")]
	public static void Refresh(ModTrustRecord record, ModFingerprint fingerprint, IEnumerable<string> detected)
	{
		ModLoader.RequireNoMod("rewrite a trust record");
		record.manifestHash = fingerprint.ManifestHash;
		record.files = fingerprint.FileEntries();
		record.combinedHash = fingerprint.Combined;
		record.folderHash = fingerprint.FolderHash;
		if (detected != null)
		{
			record.detected = ModCapability.Order(detected).ToList();
		}
		record.updatedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
		record.scannerVersion = 4;
		Save();
	}

	[ModRestricted("loader-target", "rewrites an approval")]
	public static void StoreFolderHash(ModTrustRecord record, ModFingerprint fingerprint)
	{
		ModLoader.RequireNoMod("rewrite a trust record");
		if (record != null && fingerprint.FolderHash != null && !(record.folderHash == fingerprint.FolderHash))
		{
			record.folderHash = fingerprint.FolderHash;
			Save();
		}
	}

	[ModRestricted("loader-target", "records a stop, which only the game's tripwire does")]
	public static void MarkStopped(string id, string hash, string reason)
	{
		ModLoader.RequireNoMod("record a stop");
		EnsureLoaded();
		if (!string.IsNullOrEmpty(hash) && !store.stopped.Exists((StoppedEntry s) => s.hash == hash))
		{
			store.stopped.Add(new StoppedEntry
			{
				id = id,
				hash = hash,
				reason = reason,
				at = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
			});
			Save();
		}
	}

	public static bool IsStopped(string hash, out StoppedEntry entry)
	{
		EnsureLoaded();
		entry = (string.IsNullOrEmpty(hash) ? null : store.stopped.Find((StoppedEntry s) => s.hash == hash));
		return entry != null;
	}

	[ModRestricted("loader-target", "revokes an approval, which only the player or the game does")]
	public static bool Revoke(string id)
	{
		ModLoader.RequireNoMod("revoke a trust record");
		EnsureLoaded();
		int num = store.records.RemoveAll((ModTrustRecord r) => r.id == id);
		if (num > 0)
		{
			Save();
		}
		return num > 0;
	}

	[ModRestricted("loader-target", "turns developer mode on, which only the player does")]
	public static bool SetDeveloperMode(string id, bool on)
	{
		ModLoader.RequireNoMod("change developer mode");
		ModTrustRecord modTrustRecord = Get(id);
		if (modTrustRecord == null)
		{
			return false;
		}
		modTrustRecord.developerMode = on;
		Save();
		return true;
	}

	public static ModTrustState CompareFiles(ModTrustRecord record, ModFingerprint fingerprint, ModSource source, ulong workshopId)
	{
		if (record == null)
		{
			return ModTrustState.Unknown;
		}
		if (record.source != source.ToString())
		{
			return ModTrustState.Changed;
		}
		if (record.workshopId != workshopId.ToString(CultureInfo.InvariantCulture))
		{
			return ModTrustState.Changed;
		}
		if (record.combinedHash != fingerprint.Combined)
		{
			return ModTrustState.Updated;
		}
		if (record.folderHash != null && fingerprint.FolderHash != null && record.folderHash != fingerprint.FolderHash)
		{
			return ModTrustState.Updated;
		}
		return ModTrustState.Match;
	}

	public static bool CapabilitiesWithin(ModTrustRecord record, IEnumerable<string> detected)
	{
		return ModCapability.Undeclared(detected, record.detected).Count == 0;
	}
}
