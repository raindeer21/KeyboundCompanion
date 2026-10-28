using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.STD;

namespace KeyboundClick;

// Minimal read-only layouts adapted from AtmoOmen/FFXIVClientStructs.Expansion
// commit 22b9e96bca56aab9d18a60956dfb71c14d3c50c2 (MIT; see THIRD-PARTY-NOTICES.txt).
// Do not retain pointers or StdMap entries across framework ticks.
[StructLayout(LayoutKind.Explicit, Size = 752)]
internal unsafe struct KtgHud
{
    [FieldOffset(40)] internal StdMap<uint, KtgEntity> Entities;
    [FieldOffset(624)] internal int Stage;
    [FieldOffset(640)] internal uint InputAddonId;
    [FieldOffset(673)] internal byte IsShown;
    [FieldOffset(674)] internal byte HasDirector;
}

[StructLayout(LayoutKind.Explicit, Size = 376)]
internal struct KtgEntity
{
    [FieldOffset(0)] internal uint EntityId;
    [FieldOffset(4)] internal uint Kind;
    [FieldOffset(8)] internal Utf8String Name;
    [FieldOffset(112)] internal Utf8String Title;
    [FieldOffset(236)] internal byte IsVisible;
    [FieldOffset(244)] internal float HealthPercent;
    [FieldOffset(368)] internal byte Disposed;
}

[StructLayout(LayoutKind.Explicit, Size = 96)]
internal unsafe struct KtgInput
{
    [FieldOffset(40)] internal KtgHud* Hud;
}
