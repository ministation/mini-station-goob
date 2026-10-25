// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Client._Mini.Ninja.Systems;
using Content.Shared._Mini.Ninja;
using Content.Shared._Mini.Ninja.Prototypes;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Mini.Ninja.UI;

/// <summary>
/// Client side of the suit Spider OS: module selection and suit power control.
/// </summary>
public sealed class NinjaOsBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;

    private NinjaOsWindow? _window;
    private NinjaOsBootSystem? _boot;
    private NinjaOsBoundUserInterfaceState? _state;
    private bool _powerRequested;
    private bool? _expectedActivated;
    private string? _failReason;

    public NinjaOsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _boot ??= _systems.GetEntitySystem<NinjaOsBootSystem>();

        _window = new NinjaOsWindow();
        _window.OnModuleSelected += (tier, path) => SendMessage(new NinjaOsSelectModuleMessage(tier, path));
        _window.OnSuitPowerPressed += OnSuitPowerPressed;
        _window.OnClose += Close;
        _window.OpenCentered();

        UpdateWindow();
    }

    private void OnSuitPowerPressed()
    {
        if (_window == null || _state == null || _powerRequested)
            return;

        StartBoot(!_state.SuitActivated);
    }

    private void StartBoot(bool activated)
    {
        if (_window == null || _state == null)
            return;

        _powerRequested = true;
        _expectedActivated = activated;
        _failReason = null;
        _window.SetFailReason(null);

        var bootId = activated ? _state.ActivationBoot : _state.DeactivationBoot;

        if (_proto.TryIndex(bootId, out NinjaOsBootPrototype? boot) && boot.Steps.Count > 0)
        {
            _window.ShowBootLog(activated);
            _window.SetPowerButtonEnabled(false);

            _boot?.StartBoot(Owner, boot, new NinjaOsBootSystem.BootCallbacks
            {
                OnLog = (key, error) => _window?.AppendBootLog(Loc.GetString(key), error),
                OnProgress = progress => _window?.SetBootProgress(progress),
                OnFailed = reason => _failReason = reason,
                OnFinished = OnBootFinished,
            });
        }
        else
        {
            _window.SetPowerButtonEnabled(false);
        }

        SendMessage(new NinjaOsSetSuitPowerMessage(activated));
    }

    private void OnBootFinished()
    {
        _powerRequested = false;
        _window?.SetPowerButtonEnabled(true);
        _window?.SetFailReason(_failReason);
        UpdateWindow();
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        if (message is not NinjaOsResultMessage result)
            return;

        _powerRequested = false;

        if (!result.Success)
        {
            _failReason = string.IsNullOrEmpty(result.FailReason) ? "ninja-os-boot-fail-auth" : result.FailReason;
            _window?.SetFailReason(_failReason);
        }

        var handled = _boot?.ConfirmResult(Owner, result.Success, result.FailReason) ?? false;

        if (result.Success)
        {
            _failReason = null;
            _window?.SetFailReason(null);

            if (_expectedActivated is { } activated)
                _window?.UpdatePower(activated);

            _window?.SetPowerButtonEnabled(true);
        }
        else if (!handled)
        {
            _window?.SetPowerButtonEnabled(true);
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not NinjaOsBoundUserInterfaceState cast)
            return;

        _state = cast;
        UpdateWindow();
    }

    private void UpdateWindow()
    {
        if (_window == null || _state == null)
            return;

        var modules = GetModules(_state.Os, out var maxTier);
        _window.UpdateState(_state, modules, maxTier);
    }

    private List<NinjaModulePrototype> GetModules(string osId, out int maxTier)
    {
        var modules = new List<NinjaModulePrototype>();
        maxTier = 1;

        if (_proto.TryIndex<NinjaOsPrototype>(osId, out var os))
        {
            maxTier = Math.Max(1, os.MaxTier);

            if (os.Modules.Count > 0)
            {
                foreach (var id in os.Modules)
                {
                    if (_proto.TryIndex<NinjaModulePrototype>(id.Id, out var module))
                        modules.Add(module);
                }
            }
            else
            {
                AddAllModules(modules);
            }
        }
        else
        {
            AddAllModules(modules);
        }

        foreach (var module in modules)
        {
            maxTier = Math.Max(maxTier, module.Tier);
        }

        return modules;
    }

    private void AddAllModules(List<NinjaModulePrototype> modules)
    {
        foreach (var module in _proto.EnumeratePrototypes<NinjaModulePrototype>())
        {
            modules.Add(module);
        }

        modules.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : a.Path.CompareTo(b.Path));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _boot?.StopBoot(Owner);
        _window?.Dispose();
        _window = null;
    }
}
