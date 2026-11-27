using System.Reflection;

[assembly: AssemblyVersion("0.6.0.0")]
[assembly: AssemblyFileVersion("0.6.0.0")]
[assembly: AssemblyInformationalVersion(ModDevBridge.ReleaseInfo.Id)]

namespace ModDevBridge;

internal static class ReleaseInfo
{
    public const string Version = "0.6.0";
#if BRIDGE_FULL
    public const string Edition = "full";
    public const bool DynamicModules = true;
#else
    public const string Edition = "compatible";
    public const bool DynamicModules = false;
#endif
    public const string Id = Version + "-" + Edition + "+" + ReleaseStamp.Fingerprint;
    public static BridgeFailure FullRequired() => new BridgeFailure("full_edition_required",
        "This operation requires the local Full edition. Read README.md, explain its risks and obtain the player's consent before switching.", "edition.preflight");
}
