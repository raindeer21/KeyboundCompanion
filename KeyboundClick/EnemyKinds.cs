namespace KeyboundClick;

internal static class EnemyKinds
{
    // KTG NPC identities for the current Chinese client; matched against its KTGBNpc sheet.
    // CollectEntities in the CN executable maps 19596 -> kind 1; 19592 -> kind 4 (magic pot).
    // Kinds 2/3 are non-enemy objects, including the barrier; exclude them from random attacks.
    internal static bool IsEnemy(uint dataId, uint kind) => kind is 0 or 1 or 4;
    internal static bool IsBoss(uint dataId, uint kind) => kind == 1 && dataId == 19596;
}
