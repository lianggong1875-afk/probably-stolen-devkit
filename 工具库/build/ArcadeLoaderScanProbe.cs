using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

// Offline, read-only: invokes the installed game's scanner without loading the candidate.
internal static class ArcadeLoaderScanProbe
{
    private static int Main(string[] args)
    {
        if (args.Length < 3 || args.Length > 4) return 2;
        string managed = Path.GetFullPath(args[0]);
        string candidate = Path.GetFullPath(args[1]);
        string contentRoot = Path.GetFullPath(args[2]);
        string expectedRaw = args.Length >= 4 ? args[3] : "";
        var expected = (string.IsNullOrWhiteSpace(expectedRaw) || expectedRaw == "-" || expectedRaw.Equals("none", StringComparison.OrdinalIgnoreCase))
            ? Array.Empty<string>()
            : expectedRaw.Split(',', StringSplitOptions.RemoveEmptyEntries);
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string path = Path.Combine(managed, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromStream(new MemoryStream(File.ReadAllBytes(path))) : null;
        };
        try
        {
            var game = AssemblyLoadContext.Default.LoadFromStream(new MemoryStream(File.ReadAllBytes(Path.Combine(managed, "Assembly-CSharp.dll"))));
            var type = game.GetType("ModScanner", true);
            var scan = type.GetMethod("Scan", BindingFlags.Public | BindingFlags.Static);
            // The Unity loader fills its game-reference inventory from Managed at startup.
            // Pass that inventory explicitly to the detached scanner, without touching loader state.
            var known = Directory.EnumerateFiles(managed, "*.dll", SearchOption.AllDirectories)
                .Select(Path.GetFileNameWithoutExtension)
                .Append(Path.GetFileNameWithoutExtension(candidate))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            dynamic result = scan.Invoke(null, new object[] { new[]{candidate}, known, contentRoot, true });
            var capabilities = ((System.Collections.Generic.IEnumerable<string>)result.Capabilities).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var extra = capabilities.Except(expected, StringComparer.Ordinal).ToArray();
            Console.WriteLine("Candidate: " + candidate);
            Console.WriteLine("Refused: " + result.Refused + "; error: " + result.Error);
            Console.WriteLine("Capabilities: " + string.Join(", ", capabilities));
            Console.WriteLine("Extra capabilities: " + string.Join(", ", extra));
            Console.WriteLine(result.FormatFindings(35));
            return result.Refused || extra.Length > 0 ? 1 : 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 2;
        }
    }
}
