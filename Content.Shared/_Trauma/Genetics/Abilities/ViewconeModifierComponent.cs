namespace Content.Shared._Trauma.Genetics.Abilities;

[RegisterComponent, NetworkedComponent]
public sealed partial class ViewconeModifierComponent : Component
{
    [DataField]
    public float AngleModifier = 1f;
}
