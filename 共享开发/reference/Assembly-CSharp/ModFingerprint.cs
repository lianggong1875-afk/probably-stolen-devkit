using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public class ModFingerprint
{
	public string ManifestHash;

	public List<(string path, string hash)> Files = new List<(string, string)>();

	public string Combined;

	public string FolderHash;

	public static ModFingerprint Compute(string folderPath, ModManifest manifest, bool localFolder = false)
	{
		ModFingerprint modFingerprint = new ModFingerprint();
		modFingerprint.ManifestHash = HashFile(Path.Combine(folderPath, "manifest.xml"));
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(Path.Combine(folderPath, "manifest.xml")) };
		foreach (string assembly in manifest.Assemblies)
		{
			string path = Path.Combine(folderPath, assembly);
			hashSet.Add(Path.GetFullPath(path));
			modFingerprint.Files.Add((assembly, File.Exists(path) ? HashFile(path) : "missing"));
		}
		StringBuilder stringBuilder = new StringBuilder(modFingerprint.ManifestHash);
		foreach (var (value, value2) in modFingerprint.Files)
		{
			stringBuilder.Append('\n').Append(value).Append('|')
				.Append(value2);
		}
		modFingerprint.Combined = HashBytes(Encoding.UTF8.GetBytes(stringBuilder.ToString()));
		modFingerprint.FolderHash = HashFolder(folderPath, hashSet, localFolder);
		return modFingerprint;
	}

	private static string HashFolder(string folderPath, HashSet<string> excluded, bool skipDotFolders)
	{
		try
		{
			string text = Path.GetFullPath(folderPath).TrimEnd('\\', '/');
			List<string> list = new List<string>();
			Stack<string> stack = new Stack<string>();
			stack.Push(text);
			while (stack.Count > 0)
			{
				string path = stack.Pop();
				foreach (string item in System.IO.Directory.EnumerateFiles(path))
				{
					string fullPath = Path.GetFullPath(item);
					if (!excluded.Contains(fullPath))
					{
						string text2 = fullPath.Substring(text.Length).TrimStart('\\', '/').Replace('\\', '/');
						list.Add(text2 + "|" + HashFile(fullPath));
					}
				}
				foreach (string item2 in System.IO.Directory.EnumerateDirectories(path))
				{
					if (!skipDotFolders || !Path.GetFileName(item2).StartsWith(".", StringComparison.Ordinal))
					{
						stack.Push(item2);
					}
				}
			}
			list.Sort(StringComparer.Ordinal);
			return HashBytes(Encoding.UTF8.GetBytes(string.Join("\n", list)));
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[ModTrust] Could not hash the files of " + folderPath + ": " + ex.Message);
			return null;
		}
	}

	public List<string> FileEntries()
	{
		return Files.Select(((string path, string hash) f) => f.path + "|" + f.hash).ToList();
	}

	public static string HashFile(string path)
	{
		using FileStream inputStream = File.OpenRead(path);
		using SHA256 sHA = SHA256.Create();
		return ToHex(sHA.ComputeHash(inputStream));
	}

	public static string HashBytes(byte[] bytes)
	{
		using SHA256 sHA = SHA256.Create();
		return ToHex(sHA.ComputeHash(bytes));
	}

	private static string ToHex(byte[] bytes)
	{
		StringBuilder stringBuilder = new StringBuilder(bytes.Length * 2);
		foreach (byte b in bytes)
		{
			stringBuilder.Append(b.ToString("x2"));
		}
		return stringBuilder.ToString();
	}

	internal static string ToHexPublic(byte[] bytes)
	{
		return ToHex(bytes);
	}

	public string HashOf(string relativePath)
	{
		string b = relativePath.Replace('\\', '/');
		foreach (var (text, result) in Files)
		{
			if (string.Equals(text.Replace('\\', '/'), b, StringComparison.OrdinalIgnoreCase))
			{
				return result;
			}
		}
		return null;
	}
}
