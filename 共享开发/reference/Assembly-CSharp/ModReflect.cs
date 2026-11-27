using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using UnityEngine;

public static class ModReflect
{
	private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly Dictionary<Type, Dictionary<string, MemberInfo>> valueMembers = new Dictionary<Type, Dictionary<string, MemberInfo>>();

	private static readonly Dictionary<Type, Dictionary<string, MethodInfo>> methodsBySignature = new Dictionary<Type, Dictionary<string, MethodInfo>>();

	private static readonly HashSet<string> logged = new HashSet<string>();

	public static Type FindType(string fullName)
	{
		if (string.IsNullOrEmpty(fullName))
		{
			return null;
		}
		string caller = ModLoader.CallingModId();
		foreach (Assembly item in ScopeAssemblies(caller))
		{
			Type type = null;
			try
			{
				type = item.GetType(fullName, throwOnError: false);
			}
			catch
			{
			}
			if (type != null && InScope(type, caller, out var _))
			{
				return type;
			}
		}
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			if (!assembly.IsDynamic)
			{
				Type type2 = null;
				try
				{
					type2 = assembly.GetType(fullName, throwOnError: false);
				}
				catch
				{
				}
				if (type2 != null && IsAllowedEngineType(type2))
				{
					return type2;
				}
			}
		}
		return null;
	}

	public static IEnumerable<Type> Types(string modId = null)
	{
		string caller = ModLoader.CallingModId();
		List<Type> list = new List<Type>();
		if (modId == null)
		{
			foreach (Assembly item in GameAssemblies())
			{
				list.AddRange(from t in LoadableTypes(item)
					where InScope(t, caller, out var _)
					select t);
			}
			return list;
		}
		LoadedMod loadedMod = ModLoader.Mods.Find((LoadedMod m) => m.Manifest.ID == modId);
		if (loadedMod == null || loadedMod.Assembly == null)
		{
			throw new ModReflectAccessException("'" + modId + "' is not a running mod");
		}
		if (caller != modId && !IsRunningPrerequisite(caller, modId))
		{
			throw new ModReflectAccessException("'" + modId + "' is neither the calling mod nor one of its prerequisites (list it in <Prerequisites> to reach its types)");
		}
		foreach (Assembly item2 in ModAssemblies(loadedMod))
		{
			list.AddRange(LoadableTypes(item2));
		}
		return list;
	}

	public static bool Has(object target, string member)
	{
		if (target == null || string.IsNullOrEmpty(member))
		{
			return false;
		}
		return FindValueMember(target.GetType(), member) != null;
	}

	public static object Get(object target, string member)
	{
		if (target == null)
		{
			throw new ArgumentNullException("target");
		}
		return ReadMember(target.GetType(), target, member);
	}

	public static T Get<T>(object target, string member)
	{
		return Convert<T>(Get(target, member), member);
	}

	public static object GetStatic(Type type, string member)
	{
		if (type == null)
		{
			throw new ArgumentNullException("type");
		}
		return ReadMember(type, null, member);
	}

	public static T GetStatic<T>(Type type, string member)
	{
		return Convert<T>(GetStatic(type, member), member);
	}

	public static T GetStatic<T>(string typeName, string member)
	{
		return Convert<T>(GetStatic(RequireType(typeName), member), member);
	}

	public static void Set(object target, string member, object value)
	{
		if (target == null)
		{
			throw new ArgumentNullException("target");
		}
		WriteMember(target.GetType(), target, member, value);
	}

	public static void SetStatic(Type type, string member, object value)
	{
		if (type == null)
		{
			throw new ArgumentNullException("type");
		}
		WriteMember(type, null, member, value);
	}

	public static void SetStatic(string typeName, string member, object value)
	{
		SetStatic(RequireType(typeName), member, value);
	}

	public static object Call(object target, string method, params object[] args)
	{
		if (target == null)
		{
			throw new ArgumentNullException("target");
		}
		return Invoke(target.GetType(), target, method, null, args);
	}

	public static object Call(object target, string method, Type[] signature, params object[] args)
	{
		if (target == null)
		{
			throw new ArgumentNullException("target");
		}
		return Invoke(target.GetType(), target, method, signature ?? Type.EmptyTypes, args);
	}

	public static object CallStatic(Type type, string method, params object[] args)
	{
		if (type == null)
		{
			throw new ArgumentNullException("type");
		}
		return Invoke(type, null, method, null, args);
	}

	public static object CallStatic(Type type, string method, Type[] signature, params object[] args)
	{
		if (type == null)
		{
			throw new ArgumentNullException("type");
		}
		return Invoke(type, null, method, signature ?? Type.EmptyTypes, args);
	}

	public static object CallStatic(string typeName, string method, params object[] args)
	{
		return Invoke(RequireType(typeName), null, method, null, args);
	}

	public static object New(Type type, params object[] args)
	{
		if (type == null)
		{
			throw new ArgumentNullException("type");
		}
		string caller = ModLoader.CallingModId();
		Require(type, caller, type.FullName);
		ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		MethodBase[] candidates = constructors;
		ConstructorInfo chosen = (ConstructorInfo)ChooseOverload(candidates, args, type.FullName + "..ctor");
		return Unwrap(() => chosen.Invoke(args));
	}

	public static object New(string typeName, params object[] args)
	{
		return New(RequireType(typeName), args);
	}

	public static TDelegate Method<TDelegate>(object target, string method) where TDelegate : Delegate
	{
		if (target == null)
		{
			throw new ArgumentNullException("target");
		}
		MethodInfo method2 = FindBySignature(target.GetType(), method, DelegateParameters<TDelegate>(), isStatic: false);
		return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), target, method2);
	}

	public static TDelegate StaticMethod<TDelegate>(Type type, string method) where TDelegate : Delegate
	{
		if (type == null)
		{
			throw new ArgumentNullException("type");
		}
		MethodInfo method2 = FindBySignature(type, method, DelegateParameters<TDelegate>(), isStatic: true);
		return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), method2);
	}

	public static TDelegate StaticMethod<TDelegate>(string typeName, string method) where TDelegate : Delegate
	{
		return StaticMethod<TDelegate>(RequireType(typeName), method);
	}

	public static MethodBase Target(Type type, string method, Type[] signature = null)
	{
		if (type == null)
		{
			throw new ArgumentNullException("type");
		}
		string caller = ModLoader.CallingModId();
		List<MethodInfo> list = MethodsNamed(type, method).ToList();
		if (list.Count == 0)
		{
			throw Missing(type, method);
		}
		MethodInfo methodInfo;
		if (signature != null)
		{
			methodInfo = list.FirstOrDefault((MethodInfo m) => ParametersMatch(m.GetParameters(), signature));
			if (methodInfo == null)
			{
				throw new ModReflectAccessException(type.FullName + "." + method + " has no overload (" + string.Join(", ", signature.Select((Type t) => t.Name)) + ")");
			}
		}
		else
		{
			if (list.Count > 1)
			{
				throw new ModReflectAccessException($"{type.FullName}.{method} has {list.Count} overloads; pass their parameter types");
			}
			methodInfo = list[0];
		}
		Require(methodInfo.DeclaringType, caller, type.FullName + "." + method);
		return methodInfo;
	}

	public static MethodBase Target(string typeName, string method, Type[] signature = null)
	{
		return Target(RequireType(typeName), method, signature);
	}

	private static object ReadMember(Type type, object instance, string member)
	{
		string caller = ModLoader.CallingModId();
		MemberInfo memberInfo = FindValueMember(type, member) ?? throw Missing(type, member);
		Require(memberInfo.DeclaringType, caller, type.FullName + "." + member);
		if (memberInfo is FieldInfo fieldInfo)
		{
			return fieldInfo.GetValue(instance);
		}
		PropertyInfo property = (PropertyInfo)memberInfo;
		if (!property.CanRead)
		{
			throw new ModReflectAccessException(type.FullName + "." + member + " is write-only");
		}
		return Unwrap(() => property.GetValue(instance));
	}

	private static void WriteMember(Type type, object instance, string member, object value)
	{
		string caller = ModLoader.CallingModId();
		MemberInfo memberInfo = FindValueMember(type, member) ?? throw Missing(type, member);
		Require(memberInfo.DeclaringType, caller, type.FullName + "." + member);
		if (memberInfo is FieldInfo fieldInfo)
		{
			if (fieldInfo.IsInitOnly || fieldInfo.IsLiteral)
			{
				throw new ModReflectAccessException(type.FullName + "." + member + " is read-only");
			}
			fieldInfo.SetValue(instance, value);
			return;
		}
		PropertyInfo property = (PropertyInfo)memberInfo;
		if (!property.CanWrite)
		{
			throw new ModReflectAccessException(type.FullName + "." + member + " is read-only");
		}
		Unwrap(() =>
		{
			property.SetValue(instance, value);
			return (object)null;
		});
	}

	private static MemberInfo FindValueMember(Type type, string member)
	{
		if (string.IsNullOrEmpty(member))
		{
			return null;
		}
		lock (valueMembers)
		{
			if (!valueMembers.TryGetValue(type, out var value))
			{
				value = (valueMembers[type] = new Dictionary<string, MemberInfo>());
			}
			if (value.TryGetValue(member, out var value2))
			{
				return value2;
			}
			MemberInfo memberInfo = null;
			Type type2 = type;
			while (type2 != null && memberInfo == null)
			{
				memberInfo = (MemberInfo)(((object)type2.GetField(member, BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) ?? ((object)type2.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((PropertyInfo p) => p.Name == member && p.GetIndexParameters().Length == 0)));
				type2 = type2.BaseType;
			}
			value[member] = memberInfo;
			return memberInfo;
		}
	}

	private static object Invoke(Type type, object instance, string method, Type[] signature, object[] args)
	{
		string caller = ModLoader.CallingModId();
		args = args ?? new object[0];
		MethodInfo chosen;
		if (signature != null)
		{
			chosen = FindBySignature(type, method, signature, instance == null, caller);
		}
		else
		{
			List<MethodInfo> list = (from m in MethodsNamed(type, method)
				where m.IsStatic == (instance == null)
				select m).ToList();
			if (list.Count == 0)
			{
				throw Missing(type, method);
			}
			MethodBase[] candidates = list.ToArray();
			chosen = (MethodInfo)ChooseOverload(candidates, args, type.FullName + "." + method);
			Require(chosen.DeclaringType, caller, type.FullName + "." + method);
		}
		return Unwrap(() => chosen.Invoke(instance, args));
	}

	private static MethodInfo FindBySignature(Type type, string method, Type[] signature, bool isStatic, string caller = null)
	{
		caller = caller ?? ModLoader.CallingModId();
		string key = method + "(" + string.Join(",", signature.Select((Type t) => t.FullName)) + ")" + (isStatic ? "S" : "I");
		MethodInfo value2;
		lock (methodsBySignature)
		{
			if (!methodsBySignature.TryGetValue(type, out var value))
			{
				value = (methodsBySignature[type] = new Dictionary<string, MethodInfo>());
			}
			if (!value.TryGetValue(key, out value2))
			{
				value2 = (value[key] = MethodsNamed(type, method).FirstOrDefault((MethodInfo m) => m.IsStatic == isStatic && ParametersMatch(m.GetParameters(), signature)));
			}
		}
		if (value2 == null)
		{
			throw new ModReflectAccessException(type.FullName + "." + method + "(" + string.Join(", ", signature.Select((Type t) => t.Name)) + ") does not exist" + (isStatic ? " as a static method" : ""));
		}
		Require(value2.DeclaringType, caller, type.FullName + "." + method);
		return value2;
	}

	private static IEnumerable<MethodInfo> MethodsNamed(Type type, string method)
	{
		if (string.IsNullOrEmpty(method))
		{
			yield break;
		}
		HashSet<string> seen = new HashSet<string>();
		Type t = type;
		while (t != null)
		{
			MethodInfo[] methods = t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (MethodInfo methodInfo in methods)
			{
				if (!(methodInfo.Name != method))
				{
					string item = (methodInfo.IsStatic ? "S" : "I") + string.Join(",", from p in methodInfo.GetParameters()
						select p.ParameterType.FullName);
					if (seen.Add(item))
					{
						yield return methodInfo;
					}
				}
			}
			t = t.BaseType;
		}
	}

	private static MethodBase ChooseOverload(MethodBase[] candidates, object[] args, string what)
	{
		List<MethodBase> list = candidates.Where((MethodBase m) => m.GetParameters().Length == args.Length).ToList();
		if (list.Count == 0)
		{
			throw new ModReflectAccessException($"{what} has no overload with {args.Length} parameter(s)");
		}
		if (list.Count == 1)
		{
			return list[0];
		}
		List<MethodBase> list2 = list.Where((MethodBase m) => ArgumentsMatch(m.GetParameters(), args, exact: true)).ToList();
		if (list2.Count == 1)
		{
			return list2[0];
		}
		List<MethodBase> list3 = list.Where((MethodBase m) => ArgumentsMatch(m.GetParameters(), args, exact: false)).ToList();
		if (list3.Count == 1)
		{
			return list3[0];
		}
		if (list3.Count == 0)
		{
			throw new ModReflectAccessException(what + ": no overload accepts (" + string.Join(", ", args.Select((object a) => a?.GetType().Name ?? "null")) + ")");
		}
		throw new ModReflectAccessException($"{what}: {list3.Count} overloads accept these arguments; pass their parameter types");
	}

	private static bool ArgumentsMatch(ParameterInfo[] parameters, object[] args, bool exact)
	{
		for (int i = 0; i < parameters.Length; i++)
		{
			Type type = parameters[i].ParameterType;
			if (type.IsByRef)
			{
				type = type.GetElementType();
			}
			object obj = args[i];
			if (obj == null)
			{
				if (type.IsValueType && Nullable.GetUnderlyingType(type) == null)
				{
					return false;
				}
			}
			else if (exact ? (type != obj.GetType()) : (!type.IsInstanceOfType(obj)))
			{
				return false;
			}
		}
		return true;
	}

	private static bool ParametersMatch(ParameterInfo[] parameters, Type[] signature)
	{
		if (parameters.Length != signature.Length)
		{
			return false;
		}
		for (int i = 0; i < parameters.Length; i++)
		{
			if (parameters[i].ParameterType != signature[i])
			{
				return false;
			}
		}
		return true;
	}

	private static Type[] DelegateParameters<TDelegate>() where TDelegate : Delegate
	{
		return (from p in (typeof(TDelegate).GetMethod("Invoke") ?? throw new ArgumentException(typeof(TDelegate).Name + " is not a delegate type")).GetParameters()
			select p.ParameterType).ToArray();
	}

	private static Type RequireType(string typeName)
	{
		return FindType(typeName) ?? throw new ModReflectAccessException("no type '" + typeName + "' in the game, this mod or its prerequisites");
	}

	private static T Convert<T>(object value, string member)
	{
		if (value == null)
		{
			return default;
		}
		if (value is T)
		{
			return (T)value;
		}
		throw new ModReflectAccessException(member + " holds a " + value.GetType().Name + ", not a " + typeof(T).Name);
	}

	private static ModReflectAccessException Missing(Type type, string member)
	{
		return new ModReflectAccessException(type.FullName + " has no member '" + member + "' (looked on the type and its base types)");
	}

	private static object Unwrap(Func<object> invoke)
	{
		try
		{
			return invoke();
		}
		catch (TargetInvocationException ex) when (ex.InnerException != null)
		{
			ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
			throw;
		}
	}

	private static void Require(Type declaring, string caller, string what)
	{
		if (InScope(declaring, caller, out var why))
		{
			return;
		}
		string text = what + " is out of reach for " + ((caller != null) ? ("mod '" + caller + "'") : "code that belongs to no mod") + ": " + why;
		if (logged.Add(caller + "|" + what))
		{
			UnityEngine.Debug.LogWarning("[ModReflect] " + text);
		}
		throw new ModReflectAccessException(text);
	}

	private static bool InScope(Type type, string caller, out string why)
	{
		why = null;
		if (type == null)
		{
			why = "no declaring type";
			return false;
		}
		Type type2 = type;
		while (type2.DeclaringType != null)
		{
			type2 = type2.DeclaringType;
		}
		Assembly assembly = type.Assembly;
		if (IsGameAssembly(assembly))
		{
			if (string.IsNullOrEmpty(type2.Namespace) && Enumerable.Contains(ModScanner.GuardTypes, type2.Name))
			{
				why = type2.Name + " is part of the mod loader and trust layer";
				return false;
			}
			if (string.IsNullOrEmpty(type2.Namespace) && type2.Name.StartsWith("ES3", StringComparison.Ordinal))
			{
				why = "Easy Save types are raw file tools; use ModHelper.SetModData or the ModData file helpers";
				return false;
			}
			return true;
		}
		if (IsAllowedEngineType(type))
		{
			return true;
		}
		string ownerId = ModLoader.ModIdOfAssembly(assembly);
		if (caller != null && ownerId != null)
		{
			if (ownerId == caller)
			{
				return true;
			}
			LoadedMod loadedMod = ModLoader.Mods.Find((LoadedMod m) => m.Manifest.ID == caller);
			LoadedMod loadedMod2 = ModLoader.Mods.Find((LoadedMod m) => m.Manifest.ID == ownerId);
			if (loadedMod != null && loadedMod2 != null && loadedMod2.Assembly != null && loadedMod.Manifest.Prerequisites.Contains(ownerId))
			{
				return true;
			}
		}
		if (ownerId != null)
		{
			why = ((caller != null) ? ("it belongs to mod '" + ownerId + "', which is not a prerequisite of '" + caller + "'") : ("it belongs to mod '" + ownerId + "' and the caller is not a mod"));
			return false;
		}
		why = assembly.GetName().Name + " is an engine or system assembly; mods may only reflect into the game, themselves, their prerequisites and the UI namespaces (" + string.Join(", ", ModScanner.ReflectionAllowedExternalPrefixes.Select((string p) => p.TrimEnd('.'))) + ")";
		return false;
	}

	private static bool IsGameAssembly(Assembly assembly)
	{
		string name = assembly.GetName().Name;
		if (!(name == "Assembly-CSharp"))
		{
			return name == "Assembly-CSharp-firstpass";
		}
		return true;
	}

	private static bool IsAllowedEngineType(Type type)
	{
		Type type2 = type;
		while (type2.DeclaringType != null)
		{
			type2 = type2.DeclaringType;
		}
		if (string.IsNullOrEmpty(type2.Namespace))
		{
			return false;
		}
		string ns = type2.Namespace + ".";
		return ModScanner.ReflectionAllowedExternalPrefixes.Any((string p) => ns.StartsWith(p, StringComparison.Ordinal));
	}

	private static bool IsRunningPrerequisite(string caller, string modId)
	{
		if (caller == null)
		{
			return false;
		}
		LoadedMod loadedMod = ModLoader.Mods.Find((LoadedMod m) => m.Manifest.ID == caller);
		LoadedMod loadedMod2 = ModLoader.Mods.Find((LoadedMod m) => m.Manifest.ID == modId);
		if (loadedMod != null && loadedMod2 != null && loadedMod2.Assembly != null)
		{
			return loadedMod.Manifest.Prerequisites.Contains(modId);
		}
		return false;
	}

	private static IEnumerable<Assembly> GameAssemblies()
	{
		return from a in AppDomain.CurrentDomain.GetAssemblies()
			where !a.IsDynamic && IsGameAssembly(a)
			select a;
	}

	private static IEnumerable<Assembly> ScopeAssemblies(string caller)
	{
		foreach (Assembly item in GameAssemblies())
		{
			yield return item;
		}
		if (caller == null)
		{
			yield break;
		}
		LoadedMod own = ModLoader.Mods.Find((LoadedMod m) => m.Manifest.ID == caller);
		if (own == null)
		{
			yield break;
		}
		foreach (Assembly item2 in ModAssemblies(own))
		{
			yield return item2;
		}
		foreach (string prerequisite in own.Manifest.Prerequisites)
		{
			LoadedMod loadedMod = ModLoader.Mods.Find((LoadedMod m) => m.Manifest.ID == prerequisite);
			if (loadedMod == null || loadedMod.Assembly == null)
			{
				continue;
			}
			foreach (Assembly item3 in ModAssemblies(loadedMod))
			{
				yield return item3;
			}
		}
	}

	private static List<Assembly> ModAssemblies(LoadedMod mod)
	{
		List<Assembly> list = new List<Assembly>();
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (Assembly assembly in assemblies)
		{
			if (!assembly.IsDynamic && (assembly == mod.Assembly || ModLoader.ModIdOfAssembly(assembly) == mod.Manifest.ID))
			{
				list.Add(assembly);
			}
		}
		return list;
	}

	private static IEnumerable<Type> LoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where((Type t) => t != null);
		}
	}

	public static string Describe()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine($"ModReflect: {logged.Count} refused lookup(s) this session");
		foreach (string item in logged)
		{
			stringBuilder.AppendLine("  " + item.Replace("|", ": "));
		}
		return stringBuilder.ToString();
	}
}
