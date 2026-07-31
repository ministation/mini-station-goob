using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._Arcane.ERP;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CumOverlayComponent : Component
{
    public static readonly ResPath OverlayRsi = new("/Textures/_Arcane/Effects/cumoverlay.rsi");

    public enum Layer : byte
    {
        Base,
    }

    /// <summary>
    /// How many times cum was applied. 1 = cum_normal, 2+ = cum_large.
    /// </summary>
    [AutoNetworkedField]
    public int Count;
}
