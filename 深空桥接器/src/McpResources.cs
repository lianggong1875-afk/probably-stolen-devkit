using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

internal sealed class McpResources
{
    private sealed class Guide
    {
        public string Key, File, Name, Description;
    }
    private static readonly Guide[] Guides = {
        new Guide { Key="skill", File="SKILL.md", Name="Development facts", Description="Environment, discovery and ownership." },
        new Guide { Key="bootstrap", File="AI_GUIDE.md", Name="First connection", Description="Workshop entry, discovery, host configuration and diagnosis." },
        new Guide { Key="engineering", File="references/ENGINEERING.md", Name="Native engineering", Description="Native APIs, i18n, compatibility and UI experience." },
        new Guide { Key="results", File="references/RESULTS.md", Name="Result semantics", Description="Pending, partial results, completeness and cleanup." }
    };
    private const int MaxBytes = 65536;
    private readonly string directory;
    public McpResources(string documentationDirectory = null)
    {
        directory = Path.GetFullPath(documentationDirectory ?? Path.GetDirectoryName(typeof(McpResources).Assembly.Location));
    }
    public JObject List()
    {
        var resources = new JArray();
        foreach (var guide in Guides)
        {
            var row = new JObject { ["uri"]="moddevbridge://guides/" + guide.Key, ["name"]=guide.Name,
                ["description"]=guide.Description, ["mimeType"]="text/markdown" };
            var meta = new JObject { ["relativePath"]=guide.File };
            try
            {
                var file = new FileInfo(Path.Combine(directory, guide.File));
                meta["available"] = file.Exists;
                if (file.Exists) row["size"] = file.Length;
            }
            catch (Exception error) { meta["available"] = false; meta["error"] = error.Message; }
            row["_meta"] = meta; resources.Add(row);
        }
        return new JObject { ["resources"] = resources };
    }
    public JObject Read(string uri)
    {
        var guide = Guides.FirstOrDefault(r => "moddevbridge://guides/" + r.Key == uri);
        if (guide == null) return null;
        // URI maps only to fixed packaged paths, never to a user-supplied path.
        string path = Path.Combine(directory, guide.File);
        if (!File.Exists(path)) throw new FileNotFoundException("Packaged guide missing; restore the workshop package: " + path, path);
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var body = new MemoryStream();
        var buffer = new byte[4096]; int count;
        while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (body.Length + count > MaxBytes) throw new IOException("Packaged guide exceeds 64 KiB: " + guide.File);
            body.Write(buffer, 0, count);
        }
        string text = new UTF8Encoding(false, true).GetString(body.ToArray()).TrimStart('\uFEFF');
        return new JObject { ["contents"] = new JArray(new JObject { ["uri"]=uri, ["mimeType"]="text/markdown", ["text"]=text }) };
    }
}
