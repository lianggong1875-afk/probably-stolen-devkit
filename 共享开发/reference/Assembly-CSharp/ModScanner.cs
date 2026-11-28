using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DseCecil;
using DseCecil.Cil;
using HarmonyLib;
using Mono.Collections.Generic;
using UnityEngine;

public static class ModScanner
{
	private sealed class Pass
	{
		private sealed class PatchTarget
		{
			public TypeReference Type;

			public string TypeName;

			public string MethodName;

			public PatchTarget Clone()
			{
				return new PatchTarget
				{
					Type = Type,
					TypeName = TypeName,
					MethodName = MethodName
				};
			}
		}

		private readonly ModScanResult result;

		private readonly AssemblyDefinition assembly;

		private readonly string fileName;

		private readonly HashSet<string> allowedReferences;

		private static readonly string[] StealerPatterns = new string[24]
		{
			"local storage\\leveldb", "local storage/leveldb", "\\login data", "/login data", "\\web data", "/web data", "\\cookies", "/cookies", "loginusers.vdf", "ssfn",
			"\\discord\\", "/discord/", "discordcanary", "discordptb", "\\wallets\\", "wallet.dat", "metamask", "exodus\\", "electrum\\", "\\tdata",
			"/tdata", "config.vdf", "\\steam\\config", "/steam/config"
		};

		private static readonly string[] FileTypes = new string[19]
		{
			"System.IO.File", "System.IO.FileStream", "System.IO.Directory", "System.IO.FileInfo", "System.IO.DirectoryInfo", "System.IO.FileSystemInfo", "System.IO.FileSystemWatcher", "System.IO.DriveInfo", "System.IO.Compression.ZipFile", "UnityEngine.Windows.File",
			"UnityEngine.Windows.Directory", "UnityEngine.ScreenCapture", "Microsoft.Win32.Registry", "Microsoft.Win32.RegistryKey", "ES3File", "ES3Writer", "ES3Reader", "ES3Stream", "ES3Directory"
		};

		private static readonly string[] FilePrefixes = new string[2] { "System.IO.IsolatedStorage.", "System.IO.MemoryMappedFiles." };

		private static readonly string[] AssetBundlePathMethods = new string[3] { "LoadFromFile", "LoadFromFileAsync", "RecompressAssetBundleAsync" };

		private static readonly string[] ES3PathMethods = new string[23]
		{
			"SaveRaw", "AppendRaw", "LoadRawBytes", "LoadRawString", "SaveImage", "LoadImage", "LoadAudio", "CopyFile", "CopyDirectory", "RenameFile",
			"RenameDirectory", "DeleteFile", "DeleteDirectory", "FileExists", "DirectoryExists", "GetFiles", "GetDirectories", "CreateBackup", "RestoreBackup", "GetTimestamp",
			"StoreCachedFile", "CacheFile", "Init"
		};

		private static readonly string[] SteamAuthHelperMethods = new string[2] { "RequestSteamAuthTicket", "CancelSteamAuthTicket" };

		private static readonly string[] TrustKeys = new string[5] { "modTrust", "modTrustMac", "modOrder", "workshopOwners", "modUiWarningAcceptedVersion" };

		private static readonly string[] PathStringTypes = new string[5] { "System.Xml.Linq.XDocument", "System.Xml.Linq.XElement", "System.Xml.XmlDocument", "System.Xml.XmlReader", "System.Xml.XmlWriter" };

		private static readonly string[] NetworkPrefixes = new string[6] { "System.Net.", "UnityEngine.Networking.UnityWebRequest", "Steamworks.SteamHTTP", "Steamworks.SteamNetworking", "Steamworks.SteamNetworkingSockets", "Steamworks.SteamNetworkingMessages" };

		private static readonly string[] NetworkTypes = new string[2] { "UnityEngine.WWW", "UnityEngine.Ping" };

		private static readonly string[] NetworkExceptions = new string[4] { "System.Net.WebUtility", "System.Net.IPAddress", "System.Net.IPEndPoint", "System.Net.Mime." };

		private static readonly string[] ProcessTypes = new string[2] { "System.Diagnostics.Process", "System.Diagnostics.ProcessStartInfo" };

		private static readonly string[] DynamicCodePrefixes = new string[6] { "System.Reflection.Emit.", "System.Runtime.Loader.", "System.CodeDom.", "Microsoft.CSharp.CSharpCodeProvider", "System.Runtime.Serialization.Formatters.", "System.Runtime.Serialization.NetDataContractSerializer" };

		private static readonly string[] DynamicCodeExceptions = new string[10] { "System.Reflection.Emit.OpCode", "System.Reflection.Emit.OpCodes", "System.Reflection.Emit.ILGenerator", "System.Reflection.Emit.Label", "System.Reflection.Emit.LocalBuilder", "System.Reflection.Emit.FlowControl", "System.Reflection.Emit.OperandType", "System.Reflection.Emit.OpCodeType", "System.Reflection.Emit.StackBehaviour", "System.Reflection.Emit.PackingSize" };

		private static readonly string[] ReflectionExternalPrefixes = new string[2] { "Microsoft.CSharp.RuntimeBinder.", "System.Runtime.CompilerServices.CallSite" };

		private static readonly string[] AssemblyLoadMethods = new string[8] { "Load", "LoadFrom", "LoadFile", "LoadWithPartialName", "ReflectionOnlyLoad", "ReflectionOnlyLoadFrom", "UnsafeLoadFrom", "LoadModule" };

		private static readonly string[] AppDomainLoadMethods = new string[8] { "Load", "DefineDynamicAssembly", "ExecuteAssembly", "ExecuteAssemblyByName", "CreateInstanceFrom", "CreateInstanceFromAndUnwrap", "CreateInstance", "CreateInstanceAndUnwrap" };

		private static readonly string[] NativeTypes = new string[4] { "System.Runtime.InteropServices.Marshal", "System.Runtime.InteropServices.NativeLibrary", "System.Runtime.CompilerServices.Unsafe", "Unity.Collections.LowLevel.Unsafe.UnsafeUtility" };

		private static readonly string[] TypeMemberLookups = new string[15]
		{
			"GetMethod", "GetMethods", "GetField", "GetFields", "GetProperty", "GetProperties", "GetMember", "GetMembers", "GetEvent", "GetEvents",
			"GetConstructor", "GetConstructors", "InvokeMember", "GetNestedType", "GetNestedTypes"
		};

		private static readonly string[] ExternalScopePrefixes = new string[12]
		{
			"mscorlib", "netstandard", "System", "Mono.", "Microsoft.", "UnityEngine", "UnityEditor", "Unity.", "0Harmony", "MonoMod",
			"Steamworks", "com.rlabrecque"
		};

		private static readonly string[] ExternalNamePrefixes = new string[10] { "System.", "Mono.", "Microsoft.", "UnityEngine.", "UnityEditor.", "Unity.", "HarmonyLib.", "MonoMod.", "Steamworks.", "ES3" };

		private static readonly string[] ThreadStartTypes = new string[3] { "System.Threading.Tasks.Parallel", "System.Threading.Timer", "System.Timers.Timer" };

		private static readonly string[] TaskStartMethods = new string[6] { "Run", ".ctor", "Start", "Delay", "ContinueWith", "RunSynchronously" };

		private static readonly string[] TaskFactoryMethods = new string[4] { "StartNew", "ContinueWhenAll", "ContinueWhenAny", "FromAsync" };

		private static readonly string[] ThreadPoolMethods = new string[5] { "QueueUserWorkItem", "UnsafeQueueUserWorkItem", "RegisterWaitForSingleObject", "UnsafeRegisterWaitForSingleObject", "UnsafeQueueNativeOverlapped" };

		private readonly Dictionary<string, (string rule, string reason)> restrictionCache = new Dictionary<string, (string, string)>();

		public Pass(ModScanResult result, AssemblyDefinition assembly, string fileName, HashSet<string> allowedReferences)
		{
			this.result = result;
			this.assembly = assembly;
			this.fileName = fileName;
			this.allowedReferences = allowedReferences;
		}

		public void Run()
		{
			string name = assembly.Name.Name;
			if (name == "0Harmony" || name.StartsWith("MonoMod", StringComparison.Ordinal) || name.StartsWith("Mono.Cecil", StringComparison.Ordinal))
			{
				Hard("bundled-framework", fileName, "assembly name " + name);
			}
			foreach (ModuleDefinition module in assembly.Modules)
			{
				foreach (AssemblyNameReference assemblyReference in module.AssemblyReferences)
				{
					if (!ModLoader.IsGameAssemblyName(assemblyReference.Name) && !allowedReferences.Contains(assemblyReference.Name))
					{
						Hard("unlisted-reference", fileName, "references " + assemblyReference.Name + ", which is not a game assembly, not listed in <Assemblies>, and not an assembly of an installed mod listed in <Prerequisites>");
					}
				}
				ScanResources(module);
				foreach (TypeDefinition type in module.GetTypes())
				{
					ScanType(type);
				}
			}
		}

		private void ScanResources(ModuleDefinition module)
		{
			foreach (Resource resource in module.Resources)
			{
				if (resource is EmbeddedResource embeddedResource)
				{
					byte[] resourceData;
					try
					{
						resourceData = embeddedResource.GetResourceData();
					}
					catch (Exception ex)
					{
						Hard("unreadable", resource.Name, ex.Message);
						continue;
					}
					CheckBlob(resource.Name, resourceData);
				}
			}
		}

		private void CheckBlob(string name, byte[] data)
		{
			if (data != null && data.Length != 0)
			{
				if (StartsWith(data, "MZ") || ContainsAscii(data, "This program cannot be run in DOS mode"))
				{
					Hard("payload-resource", name, "embedded executable");
				}
				else if (!IsKnownMedia(data) && data.Length >= 512 && Entropy(data) > 7.5)
				{
					Hard("opaque-resource", name, $"{data.Length} bytes, entropy {Entropy(data):0.00} bits/byte, unknown format");
				}
			}
		}

		internal static bool IsExecutableHeader(byte[] d)
		{
			if (StartsWith(d, "MZ") || StartsWith(d, "\u007fELF") || StartsWith(d, "#!"))
			{
				return true;
			}
			if (d.Length >= 4)
			{
				uint num = (uint)(d[0] | (d[1] << 8) | (d[2] << 16) | (d[3] << 24));
				if (num == 4277009102u || num == 4277009103u || num == 3472551422u || num == 3489328638u || num == 3199925962u)
				{
					return true;
				}
			}
			return false;
		}

		internal static bool IsKnownMedia(byte[] d)
		{
			if (StartsWith(d, "\u0089PNG"))
			{
				return true;
			}
			if (StartsWith(d, "BSJB") || StartsWith(d, "Microsoft C/C++ MSF 7.00"))
			{
				return true;
			}
			if (StartsWith(d, "\0\u0001\0\0") || StartsWith(d, "OTTO") || StartsWith(d, "true") || StartsWith(d, "wOFF") || StartsWith(d, "wOF2"))
			{
				return true;
			}
			if (StartsWith(d, "UnityFS"))
			{
				return true;
			}
			if (StartsWith(d, "fLaC") || (d.Length > 8 && d[4] == 102 && d[5] == 116 && d[6] == 121 && d[7] == 112))
			{
				return true;
			}
			if (d.Length > 4 && d[0] == 26 && d[1] == 69 && d[2] == 223 && d[3] == 163)
			{
				return true;
			}
			if (d.Length > 2 && ((d[0] == byte.MaxValue && d[1] == 254) || (d[0] == 254 && d[1] == byte.MaxValue)))
			{
				return true;
			}
			if (d.Length > 3 && d[0] == byte.MaxValue && d[1] == 216 && d[2] == byte.MaxValue)
			{
				return true;
			}
			if (StartsWith(d, "GIF8"))
			{
				return true;
			}
			if (StartsWith(d, "BM"))
			{
				return true;
			}
			if (StartsWith(d, "OggS"))
			{
				return true;
			}
			if (StartsWith(d, "RIFF"))
			{
				return true;
			}
			if (StartsWith(d, "ID3") || (d.Length > 2 && d[0] == byte.MaxValue && (d[1] & 0xE0) == 224))
			{
				return true;
			}
			if (d.Length > 4 && d[0] == 206 && d[1] == 202 && d[2] == 239 && d[3] == 190)
			{
				return true;
			}
			if (StartsWith(d, "<?xml") || StartsWith(d, "{") || StartsWith(d, "[") || StartsWith(d, "<") || (d.Length > 3 && d[0] == 239 && d[1] == 187 && d[2] == 191))
			{
				return MostlyPrintable(d);
			}
			return false;
		}

		internal static bool MostlyPrintable(byte[] d, int max = 256)
		{
			int num = Math.Min(d.Length, max);
			if (num == 0)
			{
				return false;
			}
			int num2 = 0;
			for (int i = 0; i < num; i++)
			{
				byte b = d[i];
				if ((b >= 32 && b < 127) || b == 10 || b == 13 || b == 9 || b >= 128)
				{
					num2++;
				}
			}
			return (double)num2 >= (double)num * 0.95;
		}

		internal static double Entropy(byte[] data)
		{
			int[] array = new int[256];
			foreach (byte b in data)
			{
				array[b]++;
			}
			double num = 0.0;
			int[] array2 = array;
			foreach (int num2 in array2)
			{
				if (num2 != 0)
				{
					double num3 = (double)num2 / (double)data.Length;
					num -= num3 * Math.Log(num3, 2.0);
				}
			}
			return num;
		}

		private void ScanType(TypeDefinition type)
		{
			string text = type.Namespace ?? "";
			if (text == "HarmonyLib" || text.StartsWith("HarmonyLib.", StringComparison.Ordinal) || text == "MonoMod" || text.StartsWith("MonoMod.", StringComparison.Ordinal) || text.StartsWith("Mono.Cecil", StringComparison.Ordinal))
			{
				Hard("bundled-framework", type.FullName, "framework type compiled into the mod");
			}
			PatchTarget classTarget = CheckPatchAttributes(type.CustomAttributes, type.FullName, null);
			CheckJsonAttributes(type.CustomAttributes, type.FullName);
			foreach (PropertyDefinition property in type.Properties)
			{
				CheckJsonAttributes(property.CustomAttributes, type.FullName + "." + property.Name);
			}
			foreach (FieldDefinition field in type.Fields)
			{
				CheckJsonAttributes(field.CustomAttributes, type.FullName + "." + field.Name);
				if (ContainsPointer(field.FieldType))
				{
					Native(type.FullName + "." + field.Name, "pointer field");
				}
				if (field.HasLayoutInfo || field.RVA != 0)
				{
					byte[] array = null;
					try
					{
						array = field.InitialValue;
					}
					catch
					{
					}
					if (array != null && array.Length != 0)
					{
						CheckBlob(type.FullName + "." + field.Name, array);
					}
				}
			}
			foreach (MethodDefinition method in type.Methods)
			{
				ScanMethod(type, method, classTarget);
			}
		}

		private PatchTarget CheckPatchAttributes(IEnumerable<CustomAttribute> attributes, string location, PatchTarget inherited)
		{
			PatchTarget patchTarget = inherited?.Clone();
			bool flag = false;
			foreach (CustomAttribute attribute in attributes)
			{
				if (attribute.AttributeType.FullName != "HarmonyLib.HarmonyPatch")
				{
					continue;
				}
				flag = true;
				Collection<CustomAttributeArgument> constructorArguments = attribute.ConstructorArguments;
				if (!attribute.IsResolved || (attribute.Constructor.Parameters.Count > 0 && constructorArguments.Count == 0))
				{
					Hard("unreadable", location, "[HarmonyPatch] arguments could not be read");
					continue;
				}
				if (patchTarget == null)
				{
					patchTarget = new PatchTarget();
				}
				object obj = ((constructorArguments.Count > 0) ? Unwrap(constructorArguments[0]) : null);
				object obj2 = ((constructorArguments.Count > 1) ? Unwrap(constructorArguments[1]) : null);
				bool flag2 = obj is string && obj2 is string;
				if (obj is TypeReference type)
				{
					ClassifyTypeReference(location, "[HarmonyPatch]", type, newRule: true);
					patchTarget.Type = type;
					patchTarget.TypeName = null;
					if (obj2 is string methodName)
					{
						patchTarget.MethodName = methodName;
					}
				}
				else if (flag2)
				{
					ClassifyLiteralTypeName(location, "[HarmonyPatch]", (string)obj, newRule: true);
					patchTarget.TypeName = (string)obj;
					patchTarget.Type = null;
					patchTarget.MethodName = (string)obj2;
				}
				else if (obj is string methodName2)
				{
					patchTarget.MethodName = methodName2;
				}
				for (int i = (flag2 ? 1 : 0); i < constructorArguments.Count; i++)
				{
					if (Unwrap(constructorArguments[i]) is string text && IsGuardTypeName(text))
					{
						Hard("loader-target", location, "[HarmonyPatch] on \"" + text + "\"");
					}
				}
			}
			if (flag || inherited == null)
			{
				CheckPatchedRestriction(location, patchTarget);
			}
			return patchTarget;
			static object Unwrap(CustomAttributeArgument argument)
			{
				if (!(argument.Value is CustomAttributeArgument customAttributeArgument))
				{
					return argument.Value;
				}
				return customAttributeArgument.Value;
			}
		}

		private void CheckJsonAttributes(IEnumerable<CustomAttribute> attributes, string location)
		{
			foreach (CustomAttribute attribute in attributes)
			{
				if (attribute.AttributeType.Namespace != "Newtonsoft.Json" || !attribute.HasProperties)
				{
					continue;
				}
				foreach (CustomAttributeNamedArgument property in attribute.Properties)
				{
					if (property.Name.EndsWith("TypeNameHandling", StringComparison.Ordinal) && (!(property.Argument.Value is IConvertible value) || Convert.ToInt64(value) != 0L))
					{
						DynamicCode(location, $"[{attribute.AttributeType.Name}({property.Name} = {property.Argument.Value})]", newRule: true);
					}
				}
			}
		}

		private static bool ContainsPointer(TypeReference type)
		{
			while (type != null)
			{
				if (type.IsPointer || type.IsFunctionPointer)
				{
					return true;
				}
				if (type is TypeSpecification typeSpecification)
				{
					type = typeSpecification.ElementType;
					continue;
				}
				return false;
			}
			return false;
		}

		private void ScanMethod(TypeDefinition type, MethodDefinition method, PatchTarget classTarget = null)
		{
			string location = type.FullName + "." + method.Name;
			CheckPatchAttributes(method.CustomAttributes, location, classTarget);
			CheckJsonAttributes(method.CustomAttributes, location);
			if (method.IsPInvokeImpl || method.HasPInvokeInfo)
			{
				Native(location, method.HasPInvokeInfo ? ("DllImport " + method.PInvokeInfo.Module.Name + "!" + method.PInvokeInfo.EntryPoint) : "DllImport");
				if (method.HasPInvokeInfo && ModSteamRules.IsSteamNativeLibrary(method.PInvokeInfo.Module?.Name))
				{
					Hard("steam-account", location, "DllImport " + method.PInvokeInfo.Module.Name + "!" + method.PInvokeInfo.EntryPoint);
				}
			}
			if (ContainsPointer(method.ReturnType) || method.Parameters.Any((ParameterDefinition p) => ContainsPointer(p.ParameterType)))
			{
				Native(location, "pointer in signature");
			}
			if (!method.HasBody)
			{
				return;
			}
			result.MethodsScanned++;
			MethodBody body;
			try
			{
				body = method.Body;
			}
			catch (Exception ex)
			{
				Hard("unreadable", location, ex.Message);
				return;
			}
			if (body.HasVariables && body.Variables.Any((VariableDefinition v) => ContainsPointer(v.VariableType)))
			{
				Native(location, "pointer local");
			}
			Collection<Instruction> instructions = body.Instructions;
			HashSet<Instruction> branchTargets = BranchTargets(instructions);
			for (int num = 0; num < instructions.Count; num++)
			{
				Instruction instruction = instructions[num];
				switch (instruction.OpCode.Code)
				{
				case DseCecil.Cil.Code.Calli:
					Native(location, "calli");
					break;
				case DseCecil.Cil.Code.Ldstr:
					CheckString(location, (string)instruction.Operand);
					break;
				case DseCecil.Cil.Code.Call:
				case DseCecil.Cil.Code.Callvirt:
				case DseCecil.Cil.Code.Newobj:
				case DseCecil.Cil.Code.Ldftn:
				case DseCecil.Cil.Code.Ldvirtftn:
					if (instruction.Operand is MethodReference mr)
					{
						CheckCall(location, instructions, num, mr, branchTargets);
					}
					break;
				case DseCecil.Cil.Code.Ldtoken:
					if (instruction.Operand is MethodReference mr2)
					{
						CheckCall(location, instructions, num, mr2, branchTargets);
					}
					else if (instruction.Operand is FieldReference fieldReference2)
					{
						CheckTypeUse(location, fieldReference2.DeclaringType.GetElementType(), fieldReference2.DeclaringType.Name + "." + fieldReference2.Name);
					}
					else if (instruction.Operand is TypeReference typeReference)
					{
						CheckTypeUse(location, typeReference.GetElementType(), "typeof(" + typeReference.Name + ")");
					}
					break;
				case DseCecil.Cil.Code.Stfld:
				case DseCecil.Cil.Code.Stsfld:
					if (instruction.Operand is FieldReference fieldReference && fieldReference.DeclaringType.FullName == "ES3Settings" && fieldReference.Name == "path")
					{
						Capability("files", location, "ES3Settings.path =");
					}
					break;
				}
			}
		}

		private void CheckTypeUse(string location, TypeReference declaring, string target)
		{
			string typeName = declaring.FullName;
			if (Enumerable.Contains(FileTypes, typeName) || FilePrefixes.Any((string p) => typeName.StartsWith(p, StringComparison.Ordinal)))
			{
				Capability("files", location, target);
			}
			else if (IsNetworkType(typeName))
			{
				Capability("network", location, target);
			}
			else if (Enumerable.Contains(ProcessTypes, typeName))
			{
				Hard("process-start", location, target);
			}
			else if (IsDynamicCodeType(typeName))
			{
				DynamicCode(location, target);
			}
			else if (Enumerable.Contains(NativeTypes, typeName))
			{
				Native(location, target);
			}
		}

		private void CheckString(string location, string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return;
			}
			string text = value.ToLowerInvariant();
			string[] stealerPatterns = StealerPatterns;
			foreach (string value2 in stealerPatterns)
			{
				if (text.Contains(value2))
				{
					Hard("stealer-strings", location, "\"" + Truncate(value, 60) + "\"");
					break;
				}
			}
			stealerPatterns = ModSteamRules.InternalStrings;
			foreach (string value3 in stealerPatterns)
			{
				if (text.Contains(value3))
				{
					Hard("steam-account", location, "\"" + Truncate(value, 60) + "\"");
					break;
				}
			}
			if (value.Length >= 1024 && IsBase64Alphabet(value))
			{
				Hard("opaque-string", location, $"{value.Length}-character base64 constant");
			}
		}

		private static bool IsBase64Alphabet(string s)
		{
			foreach (char c in s)
			{
				if ((c < 'A' || c > 'Z') && (c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '+' && c != '/' && c != '=' && c != '\n' && c != '\r')
				{
					return false;
				}
			}
			return true;
		}

		private static bool IsDynamicCodeType(string typeName)
		{
			if (Enumerable.Contains(DynamicCodeExceptions, typeName))
			{
				return false;
			}
			string[] dynamicCodePrefixes = DynamicCodePrefixes;
			foreach (string value in dynamicCodePrefixes)
			{
				if (typeName.StartsWith(value, StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}

		private static bool IsNetworkType(string typeName)
		{
			if (NetworkExceptions.Any((string p) => typeName.StartsWith(p, StringComparison.Ordinal)))
			{
				return false;
			}
			if (!Enumerable.Contains(NetworkTypes, typeName))
			{
				return NetworkPrefixes.Any((string p) => typeName.StartsWith(p, StringComparison.Ordinal));
			}
			return true;
		}

		private void CheckCall(string location, IList<Instruction> instructions, int index, MethodReference mr, HashSet<Instruction> branchTargets)
		{
			TypeReference elementType = mr.DeclaringType.GetElementType();
			string typeName = elementType.FullName;
			string name = mr.Name;
			string text = elementType.Name + "." + name;
			int num = Enumerable.Count(mr.Parameters, (ParameterDefinition p) => p.ParameterType.FullName == "System.String");
			bool flag = mr.Parameters.Count > 0 && mr.Parameters[0].ParameterType.FullName == "System.String";
			if (mr is GenericInstanceMethod genericInstanceMethod)
			{
				foreach (TypeReference genericArgument in genericInstanceMethod.GenericArguments)
				{
					CheckTypeUse(location, genericArgument.GetElementType(), text + "<" + genericArgument.Name + ">");
				}
			}
			if (typeName == "ModHelper" && Enumerable.Contains(SteamAuthHelperMethods, name))
			{
				Capability("steam-auth", location, text);
				return;
			}
			if (!IsExternalScope(elementType) && !(elementType.Scope is ModuleDefinition) && TryGetRestriction(mr, out var rule, out var reason))
			{
				ApplyRestriction(rule, reason, location, text);
				return;
			}
			if (IsThreadStart(typeName, name))
			{
				Retiring("threads", location, text, allowCapability: false, newRule: true);
				return;
			}
			if (typeName == "UnityEngine.MonoBehaviour" && mr.HasThis)
			{
				switch (name)
				{
				case "StartCoroutine":
				case "Invoke":
				case "InvokeRepeating":
				{
					Instruction instruction = FindProducer(instructions, index, 0, mr.Parameters.Count + 1, branchTargets);
					if (instruction == null || instruction.OpCode.Code != DseCecil.Cil.Code.Ldarg_0)
					{
						Retiring("threads", location, text + " on another object's MonoBehaviour", allowCapability: false, newRule: true);
					}
					return;
				}
				}
			}
			if (Enumerable.Contains(FileTypes, typeName) || FilePrefixes.Any((string p) => typeName.StartsWith(p, StringComparison.Ordinal)))
			{
				Capability("files", location, text);
				return;
			}
			if (typeName == "UnityEngine.AssetBundle" && Enumerable.Contains(AssetBundlePathMethods, name))
			{
				Capability("files", location, text + "(path)");
				return;
			}
			if (((typeName == "System.IO.StreamWriter" || typeName == "System.IO.StreamReader") && name == ".ctor") & flag)
			{
				Capability("files", location, text + "(path)");
				return;
			}
			if ((Enumerable.Contains(PathStringTypes, typeName) && (name == "Load" || name == "Save" || name == "Create")) & flag)
			{
				Capability("files", location, text + "(path)");
				return;
			}
			if (typeName == "ES3")
			{
				if (Enumerable.Contains(ES3PathMethods, name) || num >= 2)
				{
					Capability("files", location, text + "(path)");
					return;
				}
				bool flag2 = name.StartsWith("Load", StringComparison.Ordinal) || name == "KeyExists" || name == "GetKeys";
				if (flag && !mr.HasThis && !flag2)
				{
					Instruction instruction2 = FindProducer(instructions, index, 0, mr.Parameters.Count, branchTargets);
					if (instruction2 != null && instruction2.OpCode.Code == DseCecil.Cil.Code.Ldstr && Enumerable.Contains(TrustKeys, (string)instruction2.Operand))
					{
						Hard("loader-target", location, text + "(\"" + (string)instruction2.Operand + "\")");
					}
				}
				return;
			}
			if (typeName == "ES3Settings" && name == ".ctor" && num > 0)
			{
				Capability("files", location, text + "(path)");
				return;
			}
			if (IsNetworkType(typeName))
			{
				Capability("network", location, text);
				return;
			}
			switch (ModSteamRules.Classify(typeName, name))
			{
			case ModSteamUse.WorkshopPublish:
				Hard("workshop-publish", location, text);
				return;
			case ModSteamUse.Account:
				Hard("steam-account", location, text);
				return;
			case ModSteamUse.Capability:
				if (!IsValueType(elementType))
				{
					Capability("steam", location, text);
				}
				return;
			}
			if (ReflectionExternalPrefixes.Any((string p) => typeName.StartsWith(p, StringComparison.Ordinal)))
			{
				ReflectionExternal(location, text + " (dynamic)");
				return;
			}
			if ((typeName == "System.Type" && (name == "GetTypeFromProgID" || name == "GetTypeFromCLSID")) || (typeName == "System.Runtime.InteropServices.Marshal" && (name == "GetActiveObject" || name == "BindToMoniker")))
			{
				ReflectionExternal(location, text + " (COM activation)", newRule: true);
				return;
			}
			if ((typeName == "Newtonsoft.Json.JsonSerializerSettings" || typeName == "Newtonsoft.Json.JsonSerializer") && name == "set_TypeNameHandling")
			{
				Instruction instruction3 = FindProducer(instructions, index, 1, 2, branchTargets);
				if (instruction3 == null || instruction3.OpCode.Code != DseCecil.Cil.Code.Ldc_I4_0)
				{
					DynamicCode(location, (instruction3 != null && instruction3.OpCode.Code == DseCecil.Cil.Code.Ldc_I4) ? $"{text} = {instruction3.Operand}" : (text + " (not None)"), newRule: true);
				}
				return;
			}
			if ((typeName == "System.Linq.Expressions.LambdaExpression" || typeName == "System.Linq.Expressions.Expression`1") && name == "Compile")
			{
				DynamicCode(location, text);
				return;
			}
			if (Enumerable.Contains(ProcessTypes, typeName))
			{
				Hard("process-start", location, text);
				return;
			}
			if (typeName == "UnityEngine.Application" && name == "OpenURL")
			{
				Instruction instruction4 = FindProducer(instructions, index, 0, mr.Parameters.Count, branchTargets);
				string text2 = ((instruction4 != null && instruction4.OpCode.Code == DseCecil.Cil.Code.Ldstr) ? ((string)instruction4.Operand) : null);
				if (text2 != null && (text2.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || text2.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
				{
					Capability("process", location, text + "(\"" + Truncate(text2, 60) + "\")");
				}
				else
				{
					Hard("process-start", location, (text2 != null) ? (text + "(\"" + Truncate(text2, 60) + "\")") : (text + " with a computed argument"));
				}
				return;
			}
			if (!IsDynamicCodeType(typeName) && (!(typeName == "System.Reflection.Assembly") || !Enumerable.Contains(AssemblyLoadMethods, name)) && (!(typeName == "System.AppDomain") || !Enumerable.Contains(AppDomainLoadMethods, name)))
			{
				if (!(typeName == "System.Activator"))
				{
					goto IL_07f6;
				}
				switch (name)
				{
				case "CreateInstance":
					if (mr.Parameters.Count > 0 && mr.Parameters[0].ParameterType.FullName == "System.String")
					{
						break;
					}
					goto IL_07f6;
				case "CreateInstanceFrom":
				case "CreateComInstanceFrom":
					break;
				default:
					goto IL_07f6;
				}
			}
			DynamicCode(location, text);
			return;
			IL_07f6:
			if (Enumerable.Contains(NativeTypes, typeName))
			{
				Native(location, text);
				return;
			}
			if (typeName == "HarmonyLib.Harmony" && name == "UnpatchAll" && mr.HasThis && mr.Parameters.Count == 1)
			{
				Instruction instruction5 = FindProducer(instructions, index, 1, 2, branchTargets);
				if (instruction5 != null && instruction5.OpCode.Code == DseCecil.Cil.Code.Ldnull)
				{
					Hard("unpatch-all", location, "Harmony.UnpatchAll() without an id");
				}
			}
			if (typeName == "HarmonyLib.Harmony" && name.StartsWith("Unpatch", StringComparison.Ordinal))
			{
				int num2 = (mr.HasThis ? 1 : 0);
				for (int num3 = 0; num3 < mr.Parameters.Count; num3++)
				{
					if (mr.Parameters[num3].ParameterType.FullName != "System.String")
					{
						continue;
					}
					Instruction instruction6 = FindProducer(instructions, index, num3 + num2, mr.Parameters.Count + num2, branchTargets);
					if (instruction6 != null && instruction6.OpCode.Code == DseCecil.Cil.Code.Ldstr)
					{
						string text3 = (string)instruction6.Operand;
						if (text3 == "*")
						{
							Hard("unpatch-all", location, "Harmony." + name + "(\"*\")");
						}
						else if (text3.StartsWith("dse.", StringComparison.Ordinal))
						{
							Hard("loader-target", location, "Harmony." + name + "(\"" + Truncate(text3, 40) + "\")");
						}
					}
				}
				return;
			}
			if (typeName == "System.Type" && name == "GetType" && mr.Parameters.Count > 0 && mr.Parameters[0].ParameterType.FullName == "System.String")
			{
				ClassifyStringTarget(location, text, FindProducer(instructions, index, 0, mr.Parameters.Count, branchTargets));
				return;
			}
			if (typeName == "System.Type" && Enumerable.Contains(TypeMemberLookups, name))
			{
				ClassifyReceiver(location, text, instructions, index, mr, branchTargets);
				return;
			}
			if (typeName == "System.Reflection.Assembly")
			{
				switch (name)
				{
				case "GetType":
				case "GetTypes":
				case "GetExportedTypes":
				case "CreateInstance":
				{
					Instruction instruction7 = ResolveThroughLocals(instructions, FindProducer(instructions, index, 0, mr.Parameters.Count + 1, branchTargets), branchTargets);
					if ((name == "GetType") & flag)
					{
						Instruction instruction8 = ResolveThroughLocals(instructions, FindProducer(instructions, index, 1, mr.Parameters.Count + 1, branchTargets), branchTargets);
						if (instruction8 != null && instruction8.OpCode.Code == DseCecil.Cil.Code.Ldstr)
						{
							ClassifyLiteralTypeName(location, text, (string)instruction8.Operand);
						}
						else
						{
							ReflectionExternal(location, text + " with a computed name");
						}
						return;
					}
					if (instruction7 != null && instruction7.Operand is MethodReference methodReference && (methodReference.Name == "GetExecutingAssembly" || methodReference.Name == "GetCallingAssembly"))
					{
						return;
					}
					if (instruction7 != null && instruction7.Operand is MethodReference { Name: "get_Assembly" } methodReference2 && methodReference2.DeclaringType.FullName == "System.Type")
					{
						int callIndex = instructions.IndexOf(instruction7);
						Instruction producer = FindProducer(instructions, callIndex, 0, 1, branchTargets);
						TypeReference typeReference = TypeofTarget(instructions, producer, branchTargets);
						if (typeReference != null)
						{
							ClassifyTypeReference(location, text, typeReference);
							return;
						}
					}
					ReflectionExternal(location, text + " on a computed assembly");
					return;
				}
				}
			}
			if (!(typeName == "HarmonyLib.AccessTools") && !(typeName == "HarmonyLib.Traverse") && (!(typeName == "HarmonyLib.CodeInstruction") || mr.HasThis))
			{
				return;
			}
			bool newRule = typeName == "HarmonyLib.CodeInstruction";
			if (mr is GenericInstanceMethod genericInstanceMethod2)
			{
				{
					foreach (TypeReference genericArgument2 in genericInstanceMethod2.GenericArguments)
					{
						ClassifyTypeReference(location, text, genericArgument2, newRule);
					}
					return;
				}
			}
			if (mr.Parameters.Count == 0)
			{
				return;
			}
			string fullName = mr.Parameters[0].ParameterType.FullName;
			if (mr.HasThis)
			{
				return;
			}
			if (fullName == "System.String")
			{
				ClassifyStringTarget(location, text, FindProducer(instructions, index, 0, mr.Parameters.Count, branchTargets), newRule);
				return;
			}
			if (!(fullName == "System.Type"))
			{
				return;
			}
			string member = null;
			if (name == "Constructor" || name == "DeclaredConstructor")
			{
				member = ".ctor";
			}
			else if (mr.Parameters.Count > 1 && mr.Parameters[1].ParameterType.FullName == "System.String")
			{
				Instruction instruction9 = FindProducer(instructions, index, 1, mr.Parameters.Count, branchTargets);
				if (instruction9 != null && instruction9.OpCode.Code == DseCecil.Cil.Code.Ldstr)
				{
					member = (string)instruction9.Operand;
				}
			}
			Instruction producer2 = FindProducer(instructions, index, 0, mr.Parameters.Count, branchTargets);
			if (TryResolveTypeSource(instructions, producer2, branchTargets, out var type, out var literalName))
			{
				if (type != null)
				{
					ClassifyTypeReference(location, text, type, newRule, member);
				}
				else
				{
					ClassifyLiteralTypeName(location, text, literalName, newRule, member);
				}
			}
			else
			{
				ReflectionExternal(location, text + " on a computed type", newRule);
			}
		}

		private void ReflectionExternal(string location, string target, bool newRule = false)
		{
			Retiring("reflection-external", location, target, allowCapability: false, newRule);
		}

		private void Native(string location, string target, bool newRule = false)
		{
			Retiring("native", location, target, allowCapability: false, newRule);
		}

		private void DynamicCode(string location, string target, bool newRule = false)
		{
			Retiring("dynamic-code", location, target, allowCapability: false, newRule);
		}

		private void Pending(string rule, string location, string target)
		{
			if (result.Pending.Count < 400)
			{
				result.Pending.Add(new ModScanFinding
				{
					Kind = rule,
					Location = location,
					Target = target
				});
			}
		}

		private void Retiring(string capability, string location, string target, bool allowCapability = false, bool newRule = false)
		{
			bool flag = Enumerable.Contains(ModCapability.Retiring, capability);
			if (flag && REFUSE_UNSCANNABLE_CAPABILITIES)
			{
				Hard(capability, location, target);
			}
			else if (flag & newRule)
			{
				Pending(capability, location, target);
			}
			else if (flag | allowCapability)
			{
				Capability(capability, location, target);
			}
			else
			{
				Hard(capability, location, target);
			}
		}

		private bool ClassifyDangerousTarget(string location, string target, string fullName)
		{
			if (Enumerable.Contains(ProcessTypes, fullName))
			{
				Hard("process-start", location, target + " on " + fullName);
				return true;
			}
			if (Enumerable.Contains(FileTypes, fullName) || FilePrefixes.Any((string p) => fullName.StartsWith(p, StringComparison.Ordinal)))
			{
				Capability("files", location, target + " on " + fullName);
				return true;
			}
			if (IsNetworkType(fullName))
			{
				Capability("network", location, target + " on " + fullName);
				return true;
			}
			if (Enumerable.Contains(NativeTypes, fullName))
			{
				Native(location, target + " on " + fullName);
				return true;
			}
			if (IsDynamicCodeType(fullName))
			{
				DynamicCode(location, target + " on " + fullName);
				return true;
			}
			if (IsThreadStart(fullName, ".ctor"))
			{
				Retiring("threads", location, target + " on " + fullName, allowCapability: false, newRule: true);
				return true;
			}
			return false;
		}

		private static bool IsThreadStart(string typeName, string methodName)
		{
			if (Enumerable.Contains(ThreadStartTypes, typeName))
			{
				return true;
			}
			switch (typeName)
			{
			case "System.Threading.Thread":
				if (!(methodName == ".ctor"))
				{
					return methodName == "Start";
				}
				return true;
			case "System.Threading.ThreadPool":
				return Enumerable.Contains(ThreadPoolMethods, methodName);
			default:
				if (!typeName.StartsWith("System.Threading.Tasks.Task`", StringComparison.Ordinal))
				{
					if (typeName == "System.Threading.Tasks.TaskFactory" || typeName.StartsWith("System.Threading.Tasks.TaskFactory`", StringComparison.Ordinal))
					{
						return Enumerable.Contains(TaskFactoryMethods, methodName);
					}
					return false;
				}
				goto case "System.Threading.Tasks.Task";
			case "System.Threading.Tasks.Task":
				return Enumerable.Contains(TaskStartMethods, methodName);
			}
		}

		private void ApplyRestriction(string rule, string reason, string location, string target)
		{
			string target2 = ((reason != null) ? (target + " (" + reason + ")") : target);
			if (rule == "loader-target")
			{
				Hard(rule, location, target2);
			}
			else if (!REFUSE_UNSCANNABLE_CAPABILITIES)
			{
				Pending(rule, location, target2);
			}
			else if (ModCapability.IsKnown(rule))
			{
				Retiring(rule, location, target2, allowCapability: true);
			}
			else
			{
				Hard(rule, location, target2);
			}
		}

		private void CheckPatchedRestriction(string location, PatchTarget patch)
		{
			if (patch == null || (patch.Type == null && patch.TypeName == null))
			{
				return;
			}
			TypeDefinition typeDefinition = null;
			try
			{
				if (patch.Type != null)
				{
					if (patch.Type.Scope is ModuleDefinition || IsExternalScope(patch.Type))
					{
						return;
					}
					typeDefinition = patch.Type.Resolve();
				}
				else
				{
					typeDefinition = ResolveGameType(patch.TypeName);
				}
			}
			catch (Exception)
			{
				typeDefinition = null;
			}
			if (typeDefinition == null)
			{
				return;
			}
			string target = "[HarmonyPatch] on " + typeDefinition.FullName + ((patch.MethodName != null) ? ("." + patch.MethodName) : "");
			CustomAttribute customAttribute = FindRestrictionAttribute(typeDefinition.CustomAttributes);
			if (customAttribute != null)
			{
				ApplyRestriction(RuleOf(customAttribute), ReasonOf(customAttribute), location, target);
			}
			else
			{
				if (patch.MethodName == null)
				{
					return;
				}
				foreach (MethodDefinition method in typeDefinition.Methods)
				{
					if (!(method.Name != patch.MethodName))
					{
						CustomAttribute customAttribute2 = FindRestrictionAttribute(method.CustomAttributes);
						if (customAttribute2 != null)
						{
							ApplyRestriction(RuleOf(customAttribute2), ReasonOf(customAttribute2), location, target);
							break;
						}
					}
				}
			}
		}

		private TypeDefinition ResolveGameType(string name)
		{
			IAssemblyResolver assemblyResolver = assembly.MainModule.AssemblyResolver;
			if (assemblyResolver == null || string.IsNullOrEmpty(name))
			{
				return null;
			}
			string text = name.Split(',')[0].Trim();
			string[] array = new string[2] { "Assembly-CSharp", "Assembly-CSharp-firstpass" };
			foreach (string name2 in array)
			{
				AssemblyDefinition assemblyDefinition;
				try
				{
					assemblyDefinition = assemblyResolver.Resolve(new AssemblyNameReference(name2, new Version(0, 0, 0, 0)));
				}
				catch (Exception)
				{
					continue;
				}
				if (assemblyDefinition == null)
				{
					continue;
				}
				TypeDefinition type = assemblyDefinition.MainModule.GetType(text.Replace('+', '/'));
				if (type != null)
				{
					return type;
				}
				if (text.Contains('.'))
				{
					continue;
				}
				foreach (TypeDefinition type2 in assemblyDefinition.MainModule.Types)
				{
					if (type2.Name == text)
					{
						return type2;
					}
				}
			}
			return null;
		}

		private static CustomAttribute FindRestrictionAttribute(IEnumerable<CustomAttribute> attributes)
		{
			if (attributes == null)
			{
				return null;
			}
			foreach (CustomAttribute attribute in attributes)
			{
				if (attribute.AttributeType.Name == "ModRestrictedAttribute")
				{
					return attribute;
				}
			}
			return null;
		}

		private static string RuleOf(CustomAttribute mark)
		{
			if (mark.ConstructorArguments.Count <= 0)
			{
				return null;
			}
			return mark.ConstructorArguments[0].Value as string;
		}

		private static string ReasonOf(CustomAttribute mark)
		{
			if (mark.ConstructorArguments.Count <= 1)
			{
				return null;
			}
			return mark.ConstructorArguments[1].Value as string;
		}

		private bool TryGetRestriction(MethodReference mr, out string rule, out string reason)
		{
			rule = null;
			reason = null;
			string fullName = mr.FullName;
			if (restrictionCache.TryGetValue(fullName, out (string, string) value))
			{
				(rule, reason) = value;
				return rule != null;
			}
			try
			{
				MethodDefinition methodDefinition = mr.Resolve();
				CustomAttribute customAttribute = ((methodDefinition != null) ? (FindRestrictionAttribute(methodDefinition.CustomAttributes) ?? FindRestrictionAttribute(methodDefinition.DeclaringType?.CustomAttributes)) : null);
				if (customAttribute != null)
				{
					rule = RuleOf(customAttribute);
					reason = ReasonOf(customAttribute);
				}
			}
			catch (Exception)
			{
				rule = null;
			}
			restrictionCache[fullName] = (rule, reason);
			return rule != null;
		}

		private static bool IsAllowedExternalName(string typeName)
		{
			return ReflectionAllowedExternalPrefixes.Any((string p) => typeName.StartsWith(p, StringComparison.Ordinal));
		}

		private static bool IsAllowedExternalType(TypeReference type)
		{
			TypeReference typeReference = type;
			while (typeReference.DeclaringType != null)
			{
				typeReference = typeReference.DeclaringType;
			}
			if (!string.IsNullOrEmpty(typeReference.Namespace))
			{
				return IsAllowedExternalName(typeReference.Namespace + ".");
			}
			return false;
		}

		private void ClassifyReceiver(string location, string target, IList<Instruction> instructions, int index, MethodReference mr, HashSet<Instruction> branchTargets)
		{
			Instruction producer = FindProducer(instructions, index, 0, mr.Parameters.Count + 1, branchTargets);
			string member = null;
			if (mr.Name == "GetConstructor")
			{
				member = ".ctor";
			}
			else if (mr.Parameters.Count > 0 && mr.Parameters[0].ParameterType.FullName == "System.String")
			{
				Instruction instruction = FindProducer(instructions, index, 1, mr.Parameters.Count + 1, branchTargets);
				if (instruction != null && instruction.OpCode.Code == DseCecil.Cil.Code.Ldstr)
				{
					member = (string)instruction.Operand;
				}
			}
			if (TryResolveTypeSource(instructions, producer, branchTargets, out var type, out var literalName))
			{
				if (type != null)
				{
					ClassifyTypeReference(location, target, type, newRule: false, member);
				}
				else
				{
					ClassifyLiteralTypeName(location, target, literalName, newRule: false, member);
				}
			}
			else
			{
				ReflectionExternal(location, target + " on a computed type");
			}
		}

		private TypeReference TypeofTarget(IList<Instruction> instructions, Instruction producer, HashSet<Instruction> branchTargets)
		{
			producer = ResolveThroughLocals(instructions, producer, branchTargets);
			if (producer == null)
			{
				return null;
			}
			if (producer.Operand is MethodReference { Name: "GetTypeFromHandle" } methodReference && methodReference.DeclaringType.FullName == "System.Type")
			{
				int callIndex = instructions.IndexOf(producer);
				Instruction instruction = FindProducer(instructions, callIndex, 0, 1, branchTargets);
				if (instruction != null && instruction.OpCode.Code == DseCecil.Cil.Code.Ldtoken && instruction.Operand is TypeReference typeReference)
				{
					return typeReference;
				}
			}
			return null;
		}

		private Instruction ResolveThroughLocals(IList<Instruction> instructions, Instruction producer, HashSet<Instruction> branchTargets, int depth = 0)
		{
			if (producer == null || depth > 6)
			{
				return producer;
			}
			if (producer.OpCode.Code == DseCecil.Cil.Code.Dup)
			{
				Instruction instruction = FindProducer(instructions, instructions.IndexOf(producer), 0, 1, branchTargets);
				if (instruction != null)
				{
					return ResolveThroughLocals(instructions, instruction, branchTargets, depth + 1);
				}
				return producer;
			}
			if (!TryLocalIndex(producer, isLoad: true, out var index))
			{
				return producer;
			}
			Instruction instruction2 = null;
			foreach (Instruction instruction5 in instructions)
			{
				if (instruction5.OpCode.Code == DseCecil.Cil.Code.Ldloca || instruction5.OpCode.Code == DseCecil.Cil.Code.Ldloca_S)
				{
					if (TryLocalIndex(instruction5, isLoad: false, out var index2, allowAddress: true) && index2 == index)
					{
						return producer;
					}
				}
				else
				{
					if (!TryLocalIndex(instruction5, isLoad: false, out var index3) || index3 != index)
					{
						continue;
					}
					Instruction instruction3 = FindProducer(instructions, instructions.IndexOf(instruction5), 0, 1, branchTargets);
					if (instruction3 == null || instruction3.OpCode.Code != DseCecil.Cil.Code.Ldnull)
					{
						if (instruction2 != null)
						{
							return producer;
						}
						instruction2 = instruction5;
					}
				}
			}
			if (instruction2 == null)
			{
				return producer;
			}
			Instruction instruction4 = FindProducer(instructions, instructions.IndexOf(instruction2), 0, 1, branchTargets);
			if (instruction4 == null)
			{
				return producer;
			}
			return ResolveThroughLocals(instructions, instruction4, branchTargets, depth + 1);
		}

		private static bool TryLocalIndex(Instruction ins, bool isLoad, out int index, bool allowAddress = false)
		{
			index = -1;
			switch (ins.OpCode.Code)
			{
			case DseCecil.Cil.Code.Ldloc_0:
				if (isLoad)
				{
					index = 0;
				}
				break;
			case DseCecil.Cil.Code.Ldloc_1:
				if (isLoad)
				{
					index = 1;
				}
				break;
			case DseCecil.Cil.Code.Ldloc_2:
				if (isLoad)
				{
					index = 2;
				}
				break;
			case DseCecil.Cil.Code.Ldloc_3:
				if (isLoad)
				{
					index = 3;
				}
				break;
			case DseCecil.Cil.Code.Stloc_0:
				if (!isLoad)
				{
					index = 0;
				}
				break;
			case DseCecil.Cil.Code.Stloc_1:
				if (!isLoad)
				{
					index = 1;
				}
				break;
			case DseCecil.Cil.Code.Stloc_2:
				if (!isLoad)
				{
					index = 2;
				}
				break;
			case DseCecil.Cil.Code.Stloc_3:
				if (!isLoad)
				{
					index = 3;
				}
				break;
			case DseCecil.Cil.Code.Ldloc_S:
			case DseCecil.Cil.Code.Ldloc:
				if (isLoad && ins.Operand is VariableReference variableReference3)
				{
					index = variableReference3.Index;
				}
				break;
			case DseCecil.Cil.Code.Stloc_S:
			case DseCecil.Cil.Code.Stloc:
				if (!isLoad && ins.Operand is VariableReference variableReference2)
				{
					index = variableReference2.Index;
				}
				break;
			case DseCecil.Cil.Code.Ldloca_S:
			case DseCecil.Cil.Code.Ldloca:
				if (allowAddress && ins.Operand is VariableReference variableReference)
				{
					index = variableReference.Index;
				}
				break;
			}
			return index >= 0;
		}

		private bool TryResolveTypeSource(IList<Instruction> instructions, Instruction producer, HashSet<Instruction> branchTargets, out TypeReference type, out string literalName)
		{
			type = null;
			literalName = null;
			producer = ResolveThroughLocals(instructions, producer, branchTargets);
			if (producer == null)
			{
				return false;
			}
			TypeReference typeReference = TypeofTarget(instructions, producer, branchTargets);
			if (typeReference != null)
			{
				type = typeReference;
				return true;
			}
			if (producer.Operand is MethodReference methodReference)
			{
				int callIndex = instructions.IndexOf(producer);
				string fullName = methodReference.DeclaringType.FullName;
				Instruction producer2 = null;
				if (fullName == "System.Type" && methodReference.Name == "GetType" && methodReference.Parameters.Count > 0 && methodReference.Parameters[0].ParameterType.FullName == "System.String")
				{
					producer2 = FindProducer(instructions, callIndex, 0, methodReference.Parameters.Count, branchTargets);
				}
				else if (fullName == "System.Reflection.Assembly" && methodReference.Name == "GetType" && methodReference.HasThis && methodReference.Parameters.Count > 0 && methodReference.Parameters[0].ParameterType.FullName == "System.String")
				{
					producer2 = FindProducer(instructions, callIndex, 1, methodReference.Parameters.Count + 1, branchTargets);
				}
				else if (fullName == "HarmonyLib.AccessTools" && methodReference.Name == "TypeByName" && methodReference.Parameters.Count == 1)
				{
					producer2 = FindProducer(instructions, callIndex, 0, 1, branchTargets);
				}
				producer2 = ResolveThroughLocals(instructions, producer2, branchTargets);
				if (producer2 != null && producer2.OpCode.Code == DseCecil.Cil.Code.Ldstr)
				{
					literalName = (string)producer2.Operand;
					return true;
				}
			}
			return false;
		}

		private bool IsKnownBareTypeName(string bare)
		{
			foreach (TypeDefinition type in assembly.MainModule.GetTypes())
			{
				if (type.Name == bare || type.FullName == bare)
				{
					return true;
				}
			}
			return ResolveGameType(bare) != null;
		}

		private void ClassifyTypeReference(string location, string target, TypeReference type, bool newRule = false, string member = null)
		{
			if (IsGuardType(type))
			{
				Hard("loader-target", location, target + " on " + type.FullName);
			}
			else if (!IsAllowedExternalType(type) && IsExternalScope(type))
			{
				string fullName = type.GetElementType().FullName;
				if (member != null)
				{
					ClassifyReflectedMember(location, target, fullName, member, newRule || IsEs3Type(type));
					return;
				}
				ClassifyDangerousTarget(location, target, fullName);
				ReflectionExternal(location, target + " on " + type.FullName, newRule || IsEs3Type(type));
			}
		}

		private void ClassifyStringTarget(string location, string target, Instruction producer, bool newRule = false, string member = null)
		{
			if (producer == null || producer.OpCode.Code != DseCecil.Cil.Code.Ldstr)
			{
				ReflectionExternal(location, target + " with a computed name", newRule);
			}
			else
			{
				ClassifyLiteralTypeName(location, target, (string)producer.Operand, newRule, member);
			}
		}

		private void ClassifyLiteralTypeName(string location, string target, string literal, bool newRule = false, string member = null)
		{
			string typeName = literal.Split(':')[0].Split(',')[0].Trim();
			if (member == null && literal.Contains(':'))
			{
				member = literal.Substring(literal.IndexOf(':') + 1).Split(',')[0].Trim();
			}
			if (IsGuardTypeName(typeName))
			{
				Hard("loader-target", location, target + "(\"" + Truncate(literal, 60) + "\")");
			}
			else
			{
				if (IsAllowedExternalName(typeName))
				{
					return;
				}
				if (!ExternalNamePrefixes.Any((string p) => typeName.StartsWith(p, StringComparison.Ordinal)))
				{
					if (!typeName.Contains('.') && !IsKnownBareTypeName(typeName.Split('/')[0].Split('+')[0]))
					{
						ReflectionExternal(location, target + "(\"" + Truncate(literal, 60) + "\") names no type of the game or the mod", newRule);
					}
				}
				else if (!string.IsNullOrEmpty(member))
				{
					ClassifyReflectedMember(location, target, typeName, member, newRule);
				}
				else
				{
					ClassifyDangerousTarget(location, target, typeName);
					ReflectionExternal(location, target + "(\"" + Truncate(literal, 60) + "\")", newRule);
				}
			}
		}

		private void ClassifyReflectedMember(string location, string target, string typeName, string member, bool newRule)
		{
			string text = target + " on " + typeName + "." + member;
			if (Enumerable.Contains(ProcessTypes, typeName) || (typeName == "UnityEngine.Application" && member == "OpenURL"))
			{
				Hard("process-start", location, text);
				return;
			}
			if (Enumerable.Contains(FileTypes, typeName) || FilePrefixes.Any((string p) => typeName.StartsWith(p, StringComparison.Ordinal)) || typeName == "ES3" || typeName == "ES3Settings" || typeName.StartsWith("ES3", StringComparison.Ordinal) || typeName == "System.IO.StreamWriter" || typeName == "System.IO.StreamReader" || Enumerable.Contains(PathStringTypes, typeName) || (typeName == "UnityEngine.AssetBundle" && Enumerable.Contains(AssetBundlePathMethods, member)))
			{
				Capability("files", location, text);
				return;
			}
			if (IsNetworkType(typeName))
			{
				Capability("network", location, text);
				return;
			}
			if (Enumerable.Contains(NativeTypes, typeName))
			{
				Native(location, text);
				return;
			}
			if (IsDynamicCodeType(typeName) || (typeName == "System.Reflection.Assembly" && Enumerable.Contains(AssemblyLoadMethods, member)) || (typeName == "System.AppDomain" && Enumerable.Contains(AppDomainLoadMethods, member)) || (typeName == "System.Activator" && member.StartsWith("Create", StringComparison.Ordinal)) || (typeName.StartsWith("System.Linq.Expressions.", StringComparison.Ordinal) && member == "Compile"))
			{
				DynamicCode(location, text);
				return;
			}
			if (typeName == "System.Type" || typeName.StartsWith("System.Reflection.", StringComparison.Ordinal) || typeName.StartsWith("HarmonyLib.", StringComparison.Ordinal) || typeName.StartsWith("MonoMod.", StringComparison.Ordinal) || typeName == "System.Delegate" || typeName == "System.RuntimeTypeHandle" || typeName == "System.RuntimeMethodHandle")
			{
				ReflectionExternal(location, text + " (reflection on reflection)", newRule);
				return;
			}
			switch (ModSteamRules.Classify(typeName, member))
			{
			case ModSteamUse.WorkshopPublish:
				Hard("workshop-publish", location, text);
				return;
			case ModSteamUse.Account:
				Hard("steam-account", location, text);
				return;
			case ModSteamUse.Capability:
				Capability("steam", location, text);
				return;
			}
			if (!IsThreadStart(typeName, member))
			{
				if (!(typeName == "UnityEngine.MonoBehaviour"))
				{
					return;
				}
				switch (member)
				{
				default:
					return;
				case "StartCoroutine":
				case "Invoke":
				case "InvokeRepeating":
					break;
				}
			}
			Retiring("threads", location, text, allowCapability: false, newRule: true);
		}

		private static bool IsEs3Type(TypeReference type)
		{
			TypeReference typeReference = type;
			while (typeReference.DeclaringType != null)
			{
				typeReference = typeReference.DeclaringType;
			}
			if (!(typeReference.Scope is ModuleDefinition) && string.IsNullOrEmpty(typeReference.Namespace))
			{
				return typeReference.Name.StartsWith("ES3", StringComparison.Ordinal);
			}
			return false;
		}

		private bool IsExternalScope(TypeReference type)
		{
			TypeReference typeReference = type;
			while (typeReference.DeclaringType != null)
			{
				typeReference = typeReference.DeclaringType;
			}
			if (typeReference.Scope is ModuleDefinition)
			{
				return false;
			}
			if (string.IsNullOrEmpty(typeReference.Namespace) && typeReference.Name.StartsWith("ES3", StringComparison.Ordinal))
			{
				return true;
			}
			string scopeName = typeReference.Scope?.Name ?? "";
			if (scopeName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
			{
				scopeName = scopeName.Substring(0, scopeName.Length - 4);
			}
			if (scopeName == "Assembly-CSharp" || scopeName == "Assembly-CSharp-firstpass")
			{
				return false;
			}
			return ExternalScopePrefixes.Any((string p) => scopeName.StartsWith(p, StringComparison.Ordinal));
		}

		private static bool IsGuardType(TypeReference type)
		{
			TypeReference typeReference = type;
			while (typeReference.DeclaringType != null)
			{
				typeReference = typeReference.DeclaringType;
			}
			if (typeReference.Scope is ModuleDefinition)
			{
				return false;
			}
			if (string.IsNullOrEmpty(typeReference.Namespace))
			{
				return Enumerable.Contains(GuardTypes, typeReference.Name);
			}
			if (typeReference.Namespace == "Steamworks")
			{
				return ModSteamRules.IsGuardType(typeReference.FullName);
			}
			return false;
		}

		private static bool IsGuardTypeName(string name)
		{
			string text = name.Split('/')[0].Split('+')[0].Trim();
			if (!Enumerable.Contains(GuardTypes, text))
			{
				if (text.StartsWith("Steamworks.", StringComparison.Ordinal) || !text.Contains("."))
				{
					return ModSteamRules.IsGuardType(text);
				}
				return false;
			}
			return true;
		}

		private static bool IsValueType(TypeReference type)
		{
			if (type.IsValueType)
			{
				return true;
			}
			try
			{
				TypeDefinition typeDefinition = type.Resolve();
				if (typeDefinition != null)
				{
					return typeDefinition.IsValueType || typeDefinition.IsEnum;
				}
			}
			catch
			{
			}
			return type.Name.EndsWith("_t", StringComparison.Ordinal);
		}

		private static Instruction FindProducer(IList<Instruction> instructions, int callIndex, int argIndex, int totalArgs, HashSet<Instruction> branchTargets)
		{
			int num = totalArgs - argIndex;
			for (int num2 = callIndex - 1; num2 >= 0; num2--)
			{
				Instruction instruction = instructions[num2];
				if (branchTargets.Contains(instructions[num2 + 1]))
				{
					return null;
				}
				if (instruction.OpCode.Code != DseCecil.Cil.Code.Nop)
				{
					if (instruction.OpCode.FlowControl == FlowControl.Branch || instruction.OpCode.FlowControl == FlowControl.Cond_Branch || instruction.OpCode.FlowControl == FlowControl.Return || instruction.OpCode.FlowControl == FlowControl.Throw)
					{
						return null;
					}
					int num3 = PushCount(instruction);
					if (num3 < 0)
					{
						return null;
					}
					if (num3 >= num)
					{
						return instruction;
					}
					int num4 = PopCount(instruction);
					if (num4 < 0)
					{
						return null;
					}
					num = num - num3 + num4;
				}
			}
			return null;
		}

		private static HashSet<Instruction> BranchTargets(IList<Instruction> instructions)
		{
			HashSet<Instruction> hashSet = new HashSet<Instruction>();
			foreach (Instruction instruction in instructions)
			{
				if (instruction.Operand is Instruction item)
				{
					hashSet.Add(item);
				}
				else if (instruction.Operand is Instruction[] array)
				{
					Instruction[] array2 = array;
					foreach (Instruction item2 in array2)
					{
						hashSet.Add(item2);
					}
				}
			}
			return hashSet;
		}

		private static int PushCount(Instruction ins)
		{
			switch (ins.OpCode.StackBehaviourPush)
			{
			case StackBehaviour.Push0:
				return 0;
			case StackBehaviour.Push1:
			case StackBehaviour.Pushi:
			case StackBehaviour.Pushi8:
			case StackBehaviour.Pushr4:
			case StackBehaviour.Pushr8:
			case StackBehaviour.Pushref:
				return 1;
			case StackBehaviour.Push1_push1:
				return 2;
			case StackBehaviour.Varpush:
				if (ins.OpCode.Code == DseCecil.Cil.Code.Newobj)
				{
					return 1;
				}
				if (ins.Operand is MethodReference methodReference)
				{
					if (!(methodReference.ReturnType.FullName == "System.Void"))
					{
						return 1;
					}
					return 0;
				}
				return -1;
			default:
				return -1;
			}
		}

		private static int PopCount(Instruction ins)
		{
			switch (ins.OpCode.StackBehaviourPop)
			{
			case StackBehaviour.Pop0:
				return 0;
			case StackBehaviour.Pop1:
			case StackBehaviour.Popi:
			case StackBehaviour.Popref:
				return 1;
			case StackBehaviour.Pop1_pop1:
			case StackBehaviour.Popi_pop1:
			case StackBehaviour.Popi_popi:
			case StackBehaviour.Popi_popi8:
			case StackBehaviour.Popi_popr4:
			case StackBehaviour.Popi_popr8:
			case StackBehaviour.Popref_pop1:
			case StackBehaviour.Popref_popi:
				return 2;
			case StackBehaviour.Popi_popi_popi:
			case StackBehaviour.Popref_popi_popi:
			case StackBehaviour.Popref_popi_popi8:
			case StackBehaviour.Popref_popi_popr4:
			case StackBehaviour.Popref_popi_popr8:
			case StackBehaviour.Popref_popi_popref:
				return 3;
			case StackBehaviour.Varpop:
				if (ins.Operand is MethodReference methodReference)
				{
					int num = methodReference.Parameters.Count;
					if (methodReference.HasThis && ins.OpCode.Code != DseCecil.Cil.Code.Newobj)
					{
						num++;
					}
					return num;
				}
				return -1;
			default:
				return -1;
			}
		}

		private void Capability(string capability, string location, string target)
		{
			result.Capabilities.Add(capability);
			if (result.Findings.Count < 400)
			{
				result.Findings.Add(new ModScanFinding
				{
					Kind = capability,
					Location = location,
					Target = target
				});
			}
		}

		private void Hard(string flag, string location, string target)
		{
			if (result.HardFlags.Count < 100)
			{
				result.HardFlags.Add(new ModScanFinding
				{
					Kind = flag,
					Location = location,
					Target = target,
					IsHardFlag = true
				});
			}
		}

		private static bool StartsWith(byte[] data, string ascii)
		{
			if (data.Length < ascii.Length)
			{
				return false;
			}
			for (int i = 0; i < ascii.Length; i++)
			{
				if (data[i] != (byte)ascii[i])
				{
					return false;
				}
			}
			return true;
		}

		internal static bool ContainsAscii(byte[] data, string ascii)
		{
			byte[] bytes = Encoding.ASCII.GetBytes(ascii);
			for (int i = 0; i <= data.Length - bytes.Length; i++)
			{
				int j;
				for (j = 0; j < bytes.Length && data[i + j] == bytes[j]; j++)
				{
				}
				if (j == bytes.Length)
				{
					return true;
				}
			}
			return false;
		}

		private static string Truncate(string s, int max)
		{
			if (s.Length > max)
			{
				return s.Substring(0, max) + "...";
			}
			return s;
		}
	}

	public const string FLAG_PAYLOAD_RESOURCE = "payload-resource";

	public const string FLAG_OPAQUE_RESOURCE = "opaque-resource";

	public const string FLAG_STEALER_STRINGS = "stealer-strings";

	public const string FLAG_OPAQUE_STRING = "opaque-string";

	public const string FLAG_UNPATCH_ALL = "unpatch-all";

	public const string FLAG_LOADER_TARGET = "loader-target";

	public const string FLAG_BUNDLED_FRAMEWORK = "bundled-framework";

	public const string FLAG_UNREADABLE = "unreadable";

	public const string FLAG_UNLISTED_REFERENCE = "unlisted-reference";

	public const string FLAG_WORKSHOP_PUBLISH = "workshop-publish";

	public const string FLAG_STEAM_ACCOUNT = "steam-account";

	public const string FLAG_PAYLOAD_FILE = "payload-file";

	public const string FLAG_OVERSIZED = "oversized";

	public const string FLAG_PROCESS_START = "process-start";

	public const string FLAG_REFLECTION_EXTERNAL = "reflection-external";

	public const string FLAG_NATIVE = "native";

	public const string FLAG_DYNAMIC_CODE = "dynamic-code";

	public static readonly bool REFUSE_UNSCANNABLE_CAPABILITIES = false;

	public const int VERSION = 4;

	public const int MAX_FOLDER_FILES = 50000;

	private const long MAX_OPAQUE_FILE_BYTES = 8388608L;

	public static readonly string[] GuardTypes = new string[15]
	{
		"ModLoader", "ModTrust", "ModTrustRecord", "ModFingerprint", "ModTrustList", "ModScanner", "ModCapability", "ModSecurityDebug", "ModSteamRules", "ModSteamGuard",
		"ModSteamAuth", "ModWorkshop", "SteamManager", "PaperBookRegistry", "ModReflect"
	};

	private static readonly string[] ExecutableExtensions = new string[29]
	{
		".exe", ".dll", ".com", ".scr", ".pif", ".cpl", ".msi", ".msp", ".sys", ".drv",
		".ocx", ".bat", ".cmd", ".ps1", ".psm1", ".psd1", ".vbs", ".vbe", ".js", ".jse",
		".wsf", ".wsh", ".hta", ".lnk", ".url", ".reg", ".jar", ".so", ".dylib"
	};

	public static readonly string[] ReflectionAllowedExternalPrefixes = new string[3] { "UnityEngine.UI.", "TMPro.", "UnityEngine.EventSystems." };

	public static bool REFUSE_REFLECTION_EXTERNAL => REFUSE_UNSCANNABLE_CAPABILITIES;

	public static ModScanResult Scan(IEnumerable<string> dllPaths, IEnumerable<string> allowedReferences = null, string folderPath = null, bool localFolder = false)
	{
		ModScanResult modScanResult = new ModScanResult();
		List<string> list = dllPaths.ToList();
		HashSet<string> allowedReferences2 = new HashSet<string>((allowedReferences ?? Enumerable.Empty<string>()).Concat(list.Select(Path.GetFileNameWithoutExtension)), StringComparer.OrdinalIgnoreCase);
		using DefaultAssemblyResolver defaultAssemblyResolver = new DefaultAssemblyResolver();
		foreach (string item in ResolverDirectories(list))
		{
			defaultAssemblyResolver.AddSearchDirectory(item);
		}
		foreach (string item2 in list)
		{
			try
			{
				using MemoryStream stream = new MemoryStream(File.ReadAllBytes(item2), writable: false);
				using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(stream, new ReaderParameters
				{
					ReadingMode = ReadingMode.Immediate,
					InMemory = true,
					AssemblyResolver = defaultAssemblyResolver
				});
				new Pass(modScanResult, assembly, Path.GetFileName(item2), allowedReferences2).Run();
			}
			catch (Exception ex)
			{
				modScanResult.Error = Path.GetFileName(item2) + " is not a readable .NET assembly (" + ex.GetType().Name + ": " + ex.Message + ")";
				return modScanResult;
			}
		}
		if (folderPath != null)
		{
			ScanFolderFiles(modScanResult, folderPath, list, localFolder);
		}
		return modScanResult;
	}

	private static void ScanFolderFiles(ModScanResult result, string folderPath, List<string> listedDlls, bool localFolder)
	{
		string text;
		IEnumerable<string> enumerable;
		try
		{
			text = Path.GetFullPath(folderPath).TrimEnd('\\', '/');
			enumerable = EnumerateModFiles(text, localFolder);
		}
		catch (Exception ex)
		{
			result.Error = "the mod folder could not be listed (" + ex.GetType().Name + ": " + ex.Message + ")";
			return;
		}
		HashSet<string> hashSet = new HashSet<string>(listedDlls.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
		int num = 0;
		try
		{
			foreach (string item in enumerable)
			{
				if (++num > 50000)
				{
					Flag("oversized", "mod folder", $"more than {50000} files");
					break;
				}
				string fullPath = Path.GetFullPath(item);
				if (hashSet.Contains(fullPath))
				{
					continue;
				}
				string location = fullPath.Substring(text.Length).TrimStart('\\', '/').Replace('\\', '/');
				long length = new FileInfo(fullPath).Length;
				string text2 = Path.GetExtension(fullPath).ToLowerInvariant();
				if (Enumerable.Contains(ExecutableExtensions, text2))
				{
					Flag("payload-file", location, text2 + " file");
				}
				else
				{
					if (length == 0L)
					{
						continue;
					}
					byte[] array = ReadHead(fullPath, 4096);
					if (Pass.IsExecutableHeader(array))
					{
						Flag("payload-file", location, "executable content");
					}
					else
					{
						if (Pass.IsKnownMedia(array) || Pass.MostlyPrintable(array, array.Length) || length < 512)
						{
							continue;
						}
						if (length > 8388608)
						{
							Flag("opaque-resource", location, $"{length} bytes of unknown format");
							continue;
						}
						byte[] array2 = File.ReadAllBytes(fullPath);
						double num2 = Pass.Entropy(array2);
						if (num2 > 7.5)
						{
							Flag("opaque-resource", location, $"{array2.Length} bytes, entropy {num2:0.00} bits/byte, unknown format");
						}
						else if (Pass.ContainsAscii(array2, "This program cannot be run in DOS mode"))
						{
							Flag("payload-file", location, "embedded executable");
						}
					}
				}
			}
		}
		catch (Exception ex2)
		{
			result.Error = "a file in the mod folder could not be read (" + ex2.GetType().Name + ": " + ex2.Message + ")";
		}
		void Flag(string flag, string location2, string target)
		{
			if (result.HardFlags.Count < 100)
			{
				result.HardFlags.Add(new ModScanFinding
				{
					Kind = flag,
					Location = location2,
					Target = target,
					IsHardFlag = true
				});
			}
		}
	}

	private static IEnumerable<string> EnumerateModFiles(string root, bool skipDotFolders)
	{
		Stack<string> folders = new Stack<string>();
		folders.Push(root);
		while (folders.Count > 0)
		{
			string folder = folders.Pop();
			foreach (string item in System.IO.Directory.EnumerateFiles(folder))
			{
				yield return item;
			}
			foreach (string item2 in System.IO.Directory.EnumerateDirectories(folder))
			{
				if (!skipDotFolders || !Path.GetFileName(item2).StartsWith(".", StringComparison.Ordinal))
				{
					folders.Push(item2);
				}
			}
		}
	}

	private static byte[] ReadHead(string path, int max)
	{
		using FileStream fileStream = File.OpenRead(path);
		byte[] array = new byte[(int)Math.Min(max, fileStream.Length)];
		int i;
		int num;
		for (i = 0; i < array.Length; i += num)
		{
			num = fileStream.Read(array, i, array.Length - i);
			if (num <= 0)
			{
				break;
			}
		}
		if (i < array.Length)
		{
			Array.Resize(ref array, i);
		}
		return array;
	}

	private static IEnumerable<string> ResolverDirectories(IEnumerable<string> dllPaths)
	{
		HashSet<string> dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Add(typeof(object).Assembly.Location);
		Add(typeof(Harmony).Assembly.Location);
		Add(typeof(ModScanner).Assembly.Location);
		Add(typeof(UnityEngine.Object).Assembly.Location);
		foreach (string dllPath in dllPaths)
		{
			Add(dllPath);
		}
		return dirs;
		void Add(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return;
			}
			try
			{
				string directoryName = Path.GetDirectoryName(path);
				if (!string.IsNullOrEmpty(directoryName) && System.IO.Directory.Exists(directoryName))
				{
					dirs.Add(directoryName);
				}
				string text = Path.Combine(directoryName ?? "", "Facades");
				if (System.IO.Directory.Exists(text))
				{
					dirs.Add(text);
				}
			}
			catch
			{
			}
		}
	}
}
