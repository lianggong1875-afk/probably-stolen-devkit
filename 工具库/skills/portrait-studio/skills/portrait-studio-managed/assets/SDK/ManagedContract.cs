using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PortraitStudio.StaticV1;

namespace PortraitStudio.Managed
{
    // Metadata only: no Unity objects, message subscription, image IO or display ownership.
    public sealed class ManagedCatalog
    {
        public const string Format = "portrait-studio-managed/1", Contract = "static-manager-rendering/1";
        public const string FileName = "portrait-studio-managed.json", Manager = "PortraitStudio";
        public readonly Catalog Resources;
        public readonly string ProviderId, Revision;
        readonly Dictionary<string, Target> targets = new Dictionary<string, Target>(StringComparer.Ordinal);
        public IEnumerable<Target> Targets => targets.Values;
        ManagedCatalog(Catalog resources, string revision) { Resources = resources; ProviderId = resources.providerId; Revision = revision; }
        static void Fields(JObject o, params string[] fields)
        {
            Check.That(o != null && o.Properties().Count() == fields.Length && fields.All(k => o[k] != null), "Missing/unexpected managed field");
        }
        static string Id(JObject o, string key) { Check.That(o[key]?.Type == JTokenType.String && Check.Id((string)o[key]), "Invalid managed ID: " + key); return (string)o[key]; }
        public Target GetTarget(string id) { Check.That(targets.TryGetValue(id, out var t), "Unknown managed target"); return t; }
        public static ManagedCatalog Read(string root, string expectedId)
        {
            var path = Catalog.SafePath(root, FileName);
            Check.That(new FileInfo(path).Length <= 1048576, "Managed declaration exceeds 1 MiB");
            var o = JObject.Parse(File.ReadAllText(path), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            Fields(o, "format", "contract", "providerId", "revision", "renderMode", "renderOwner", "targets");
            Check.That((string)o["format"] == Format && (string)o["contract"] == Contract && (string)o["renderMode"] == "static" && (string)o["renderOwner"] == Manager && Id(o,"providerId") == expectedId, "Explicit managed static ownership required");
            var resources = Catalog.Read(root, expectedId);
            Check.That(resources.role == "resources", "Managed provider publishes resources without a writer bridge");
            var result = new ManagedCatalog(resources, Id(o,"revision"));
            var rows = o["targets"] as JArray;
            Check.That(rows != null && rows.Count > 0 && rows.Count <= 4096, "Managed targets required");
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var raw = row as JObject;
                Fields(raw, "id", "kind", "subject", "slot", "renderMode", "binding", "defaultResource", "candidateResources");
                var copy = (JObject)raw.DeepClone(); copy["defaultMode"] = "resource";
                var target = Check.Parse<Target>(copy.ToString()); target.Validate();
                Check.That(target.candidateResources.Length > 0 && target.candidateResources.Any(r => r.Key == target.defaultResource.Key), "Managed default must be a candidate");
                Check.That(!result.targets.ContainsKey(target.id) && identities.Add(target.Identity), "Duplicate managed target/identity");
                foreach (var reference in target.candidateResources.Concat(new[] { target.defaultResource }))
                {
                    Check.That(reference.providerId == expectedId, "Managed resources must be published in this provider");
                    var set = resources.GetSet(reference.setId);
                    Check.That(set.Identity == target.Identity, "Managed resource identity mismatch", "identity-mismatch");
                    var asset = set.GetAsset(reference.id, reference.revision);
                    if (target.binding.type == "scene-key") Check.That((long)asset.geometry.rect.width * 9 == (long)asset.geometry.rect.height * 16, "Native scene must use a 16:9 rect");
                }
                result.targets.Add(target.id, target);
            }
            resources.ValidatePortraitSetTargets(result.Targets);
            return result;
        }
    }
}
