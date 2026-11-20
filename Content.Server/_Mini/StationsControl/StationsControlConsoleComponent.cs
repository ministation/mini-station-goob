namespace Content.Server._Mini.StationsControl;

[RegisterComponent]
public sealed partial class StationsControlConsoleComponent : Component
{
    [DataField]
    public List<string> AllowedAlertLevels = new();

    [DataField]
    public bool AllowAnyAlertLevel = false;
}
