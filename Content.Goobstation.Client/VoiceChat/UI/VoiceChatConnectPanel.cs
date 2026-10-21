// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Goobstation.Client.VoiceChat.UI;

/// <summary>
/// Mini Station style connect block: two flat cards (desktop / phone) with the link, code and
/// status, all using the same palette as the rest of the Mini windows.
/// </summary>
public sealed partial class VoiceChatConnectPanel : BoxContainer
{
    [Dependency] private IClipboardManager _clipboard = default!;
    [Dependency] private VoiceChatManager _voice = default!;

    private readonly Label _status;
    private readonly Button _open;
    private readonly Button _copy;
    private readonly Label _pageUrl;
    private readonly Label _code;

    public VoiceChatConnectPanel()
    {
        IoCManager.InjectDependencies(this);

        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 8;

        _status = new Label { StyleClasses = { "LabelHeading" } };

        _open = VoiceChatStyles.Button(Loc.GetString("voice-link-open"), accent: true);
        _open.HorizontalExpand = true;
        _copy = VoiceChatStyles.Button(Loc.GetString("voice-link-copy"));
        _open.OnPressed += _ => _voice.OpenLink();
        _copy.OnPressed += _ =>
        {
            if (_voice.LinkUrl is { } url)
                _clipboard.SetText(url);
        };

        var buttons = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };
        buttons.AddChild(_open);
        buttons.AddChild(_copy);

        var computer = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SizeFlagsStretchRatio = 1f,
            SeparationOverride = 4,
        };
        computer.AddChild(VoiceChatStyles.Heading(Loc.GetString("voice-link-computer-title")));
        computer.AddChild(VoiceChatStyles.Muted(Loc.GetString("voice-link-computer-text")));
        computer.AddChild(buttons);

        _pageUrl = new Label
        {
            StyleClasses = { "LabelKeyText" },
            FontColorOverride = VoiceChatStyles.Accent,
            ClipText = true,
        };
        _code = new Label
        {
            StyleClasses = { "LabelHeadingBigger" },
            FontColorOverride = VoiceChatStyles.Accent,
            HorizontalAlignment = HAlignment.Center,
        };

        var phone = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SizeFlagsStretchRatio = 1f,
            SeparationOverride = 4,
        };
        phone.AddChild(VoiceChatStyles.Heading(Loc.GetString("voice-link-phone-title")));
        phone.AddChild(VoiceChatStyles.Muted(Loc.GetString("voice-link-phone-text")));
        phone.AddChild(_pageUrl);
        phone.AddChild(_code);

        var columns = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 10,
        };
        columns.AddChild(VoiceChatStyles.CardHost(computer));
        columns.AddChild(VoiceChatStyles.CardHost(phone));

        AddChild(_status);
        AddChild(columns);
        AddChild(VoiceChatStyles.Muted(Loc.GetString("voice-link-expiry")));
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        _voice.LinkChanged += UpdateState;
        _voice.WebConnectedChanged += OnWebConnectedChanged;
        UpdateState();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _voice.LinkChanged -= UpdateState;
        _voice.WebConnectedChanged -= OnWebConnectedChanged;
    }

    private void OnWebConnectedChanged(bool connected)
    {
        UpdateState();
    }

    private void UpdateState()
    {
        var ready = _voice.LinkUrl != null;

        _status.Text = Loc.GetString(_voice.WebConnected ? "voice-link-connected" : "voice-link-disconnected");
        _status.FontColorOverride = _voice.WebConnected ? VoiceChatStyles.Success : VoiceChatStyles.Danger;
        _pageUrl.Text = _voice.PageUrl ?? Loc.GetString("voice-link-loading");
        _code.Text = _voice.LinkCode ?? "--------";
        _open.Disabled = !ready;
        _copy.Disabled = !ready;
        _open.Modulate = ready ? Color.White : VoiceChatStyles.Offline;
        _copy.Modulate = ready ? Color.White : VoiceChatStyles.Offline;
    }
}
