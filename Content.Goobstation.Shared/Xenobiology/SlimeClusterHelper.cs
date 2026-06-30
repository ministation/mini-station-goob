using Content.Goobstation.Shared.Xenobiology.Components;

namespace Content.Goobstation.Shared.Xenobiology;

public static class SlimeClusterHelper
{
    public static bool IsMergedCluster(EntityUid uid, IEntityManager entMan) =>
        entMan.TryGetComponent(uid, out SlimeClusterComponent? cluster) && cluster.Count > 1;
}
