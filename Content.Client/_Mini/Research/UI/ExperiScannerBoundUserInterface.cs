using Content.Shared._Mini.Research.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Mini.Research.UI;

[UsedImplicitly]
public sealed class ExperiScannerBoundUserInterface : BoundUserInterface
{
    private ExperiScannerMenu? _menu;

    public ExperiScannerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<ExperiScannerMenu>();
        _menu.OnServerButtonPressed += () => SendMessage(new OpenResearchServerMenuMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ExperiScannerBoundInterfaceState cast)
            _menu?.UpdateState(cast);
    }
}
