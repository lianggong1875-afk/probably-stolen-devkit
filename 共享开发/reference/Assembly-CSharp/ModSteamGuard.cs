using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Steamworks;
using UnityEngine;

public static class ModSteamGuard
{
	public const string HARMONY_ID = "dse.steamguard";

	private static Harmony harmony;

	private static HarmonyMethod prefix;

	private static HarmonyMethod processPrefix;

	private static HarmonyMethod openUrlPrefix;

	private static MethodBase openUrlMethod;

	private static readonly List<MethodBase> guarded = new List<MethodBase>();

	private static readonly List<MethodBase> guardedProcess = new List<MethodBase>();

	private static readonly ConcurrentQueue<(string modId, string call, bool process)> pending = new ConcurrentQueue<(string, string, bool)>();

	private static readonly HashSet<string> reported = new HashSet<string>();

	private static int blockedCount;

	private static int mainThreadId;

	public static int GuardedCount => guarded.Count + guardedProcess.Count;

	public static int BlockedCount => blockedCount;

	public static void Install()
	{
		if (harmony != null)
		{
			return;
		}
		mainThreadId = Thread.CurrentThread.ManagedThreadId;
		harmony = new Harmony("dse.steamguard");
		ModSteamAuth.Install();
		prefix = new HarmonyMethod(AccessTools.Method(typeof(ModSteamGuard), "Prefix"))
		{
			priority = 800
		};
		processPrefix = new HarmonyMethod(AccessTools.Method(typeof(ModSteamGuard), "PrefixProcess"))
		{
			priority = 800
		};
		Stopwatch stopwatch = Stopwatch.StartNew();
		MethodInfo[] methods = typeof(Process).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
		foreach (MethodInfo methodInfo in methods)
		{
			if (!(methodInfo.Name != "Start"))
			{
				try
				{
					harmony.Patch(methodInfo, processPrefix);
					guardedProcess.Add(methodInfo);
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"[ModSteamGuard] Could not guard Process.Start({methodInfo.GetParameters().Length} args): {ex.Message}");
				}
			}
		}
		openUrlPrefix = new HarmonyMethod(AccessTools.Method(typeof(ModSteamGuard), "PrefixOpenUrl"))
		{
			priority = 800
		};
		openUrlMethod = AccessTools.Method(typeof(Application), "OpenURL", new Type[1] { typeof(string) });
		if (openUrlMethod != null)
		{
			try
			{
				harmony.Patch(openUrlMethod, openUrlPrefix);
				guardedProcess.Add(openUrlMethod);
			}
			catch (Exception ex2)
			{
				Debug.LogWarning("[ModSteamGuard] Could not guard Application.OpenURL: " + ex2.Message);
				openUrlMethod = null;
			}
		}
		Assembly assembly = typeof(SteamUGC).Assembly;
		string[] runtimeGuardedTypes = ModSteamRules.RuntimeGuardedTypes;
		foreach (string text in runtimeGuardedTypes)
		{
			Type type = assembly.GetType("Steamworks." + text);
			if (type == null)
			{
				continue;
			}
			methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);
			foreach (MethodInfo methodInfo2 in methods)
			{
				if (ModSteamRules.IsGuardedAtRuntime(text, methodInfo2.Name))
				{
					try
					{
						harmony.Patch(methodInfo2, prefix);
						guarded.Add(methodInfo2);
					}
					catch (Exception ex3)
					{
						Debug.LogWarning("[ModSteamGuard] Could not guard " + text + "." + methodInfo2.Name + ": " + ex3.Message);
					}
				}
			}
		}
		UnityEngine.Debug.Log($"[ModSteamGuard] {guarded.Count} Steam method(s) and {guardedProcess.Count} Process.Start overload(s) guarded in {stopwatch.ElapsedMilliseconds} ms");
	}

	public static void Verify()
	{
		if (ModSteamGuard.harmony == null)
		{
			return;
		}
		foreach (MethodBase item in guarded.Concat(guardedProcess))
		{
			Patches patchInfo = Harmony.GetPatchInfo(item);
			if (patchInfo == null || !patchInfo.Prefixes.Any((Patch p) => p.owner == "dse.steamguard"))
			{
				Debug.LogError("[ModSteamGuard] The guard on " + item.DeclaringType?.Name + "." + item.Name + " was removed; reinstalling it.");
				try
				{
					Harmony harmony = ModSteamGuard.harmony;
					MethodBase original = item;
					HarmonyMethod harmonyMethod;
					if (item == openUrlMethod)
					{
						harmonyMethod = openUrlPrefix;
					}
					else
					{
						harmonyMethod = (guardedProcess.Contains(item) ? processPrefix : prefix);
					}
					harmony.Patch(original, harmonyMethod);
				}
				catch (Exception ex)
				{
					Debug.LogError("[ModSteamGuard] Could not reinstall the guard on " + item.Name + ": " + ex.Message);
				}
			}
		}
	}

	public static void Drain()
	{
		(string, string, bool) result;
		while (pending.TryDequeue(out result))
		{
			Handle(result.Item1, result.Item2, result.Item3);
		}
	}

	private static void Report(string modId, string call, bool process)
	{
		Interlocked.Increment(ref blockedCount);
		if (Thread.CurrentThread.ManagedThreadId == mainThreadId)
		{
			Handle(modId, call, process);
		}
		else
		{
			pending.Enqueue((modId, call, process));
		}
	}

	private static void Handle(string modId, string call, bool process)
	{
		if (!reported.Add((modId ?? "?") + "|" + call))
		{
			return;
		}
		try
		{
			ModLoader.OnSteamGuardTripped(modId, call, process);
		}
		catch (Exception arg)
		{
			Debug.LogError($"[ModSteamGuard] Handling a blocked call failed: {arg}");
		}
	}

	private static bool Prefix(MethodBase __originalMethod)
	{
		if (ModSteamAuth.IsIssuing && __originalMethod != null && __originalMethod.Name == "GetAuthSessionTicket" && __originalMethod.DeclaringType == typeof(SteamUser))
		{
			return true;
		}
		string call = (__originalMethod?.DeclaringType?.Name ?? "Steam") + "." + (__originalMethod?.Name ?? "?");
		string modId = null;
		try
		{
			modId = CallingMod();
		}
		catch
		{
		}
		Report(modId, call, process: false);
		return false;
	}

	private static bool PrefixOpenUrl(string url)
	{
		if (url != null && (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
		{
			return true;
		}
		string text = null;
		try
		{
			text = CallingMod();
		}
		catch
		{
		}
		if (text == null)
		{
			return true;
		}
		string text2;
		if (url == null)
		{
			text2 = "null";
		}
		else
		{
			text2 = ((url.Length > 60) ? (url.Substring(0, 60) + "...") : url);
		}
		Report(text, "Application.OpenURL(\"" + text2 + "\")", process: true);
		return false;
	}

	private static bool PrefixProcess(MethodBase __originalMethod)
	{
		string text = null;
		try
		{
			text = CallingMod();
		}
		catch
		{
		}
		if (text == null)
		{
			return true;
		}
		Report(text, "Process.Start", process: true);
		return false;
	}

	private static string CallingMod()
	{
		return ModLoader.CallingModId();
	}
}
