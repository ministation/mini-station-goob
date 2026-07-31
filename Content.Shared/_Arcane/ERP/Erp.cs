namespace Content.Shared._Arcane.ERP;

public enum ErpPreference
{
    No = 0,
    Ask = 1,
    Yes = 2,
}

public sealed class ErpPreferenceChangedEvent(ErpPreference oldPreference, ErpPreference newPreference) : EntityEventArgs
{
    public ErpPreference OldPreference = oldPreference;
    public ErpPreference NewPreference = newPreference;
}
