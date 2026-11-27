using System;

namespace ModDevBridge;

// One opaque action reference carries its observation precondition.
internal static class UiActionReference
{
    internal static string Pack(string reference, string token) => token == null ? null : reference + "@" + token;
    internal static string ObjectId(string reference)
    {
        int separator = (reference ?? "").IndexOf('@');
        return separator < 0 ? reference : reference.Substring(0,separator);
    }
    internal static string Token(string reference)
    {
        int separator = (reference ?? "").IndexOf('@');
        if (separator < 0) return null;
        string embedded = reference.Substring(separator+1);
        if (embedded.Length != 64 || Array.Exists(embedded.ToCharArray(),c => !Uri.IsHexDigit(c)))
            throw new BridgeFailure("ui_reference_invalid","Invalid action reference; copy a fresh query_ui targetRef unchanged","ui.preflight");
        return embedded;
    }
}
