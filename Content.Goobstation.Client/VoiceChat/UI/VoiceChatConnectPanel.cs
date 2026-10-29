// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Client.Guidebook.Richtext;
using Content.Client.Popups;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Goobstation.Client.VoiceChat.UI;

/// <summary>
/// Mini Station style connect block: two flat cards (desktop / phone) with the link, code and
/// status, all using the same palette as the rest of the Mini windows.
/// </summary>
public sealed partial class VoiceChatConnectPanel : BoxContainer, IDocumentTag
{
    [Dependency] private IClipboardManager _clipboard = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private VoiceChatManager _voice = default!;

    private readonly Label _status;
    private readonly RichTextLabel _detail;
    private readonly Button _open;
    private readonly Button _copy;
    private readonly Label _pageUrl;
    private readonly Button _code;

    public VoiceChatConnectPanel()
    {
        IoCManager.InjectDependencies(this);

        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 8;

        _status = new Label { StyleClasses = { "LabelHeading" } };

        _detail = new RichTextLabel
        {
            HorizontalExpand = true,
            Visible = false,
            Modulate = VoiceChatStyles.Danger,
        };

        _open = VoiceChatStyles.Button(Loc.GetString("voice-link-open"), accent: true);
        _open.HorizontalExpand = true;
        _copy = VoiceChatStyles.Button(Loc.GetString("voice-link-copy"));
        _copy.HorizontalExpand = true;
        _open.OnPressed += _ => _voice.OpenLink();
        _copy.OnPressed += _ =>
        {
            if (_voice.LinkUrl is { } url)
                _clipboard.SetText(url);
        };

        var buttons = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
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
        computer.AddChild(VoiceChatStyles.WrappedMuted(Loc.GetString("voice-link-computer-text")));
        computer.AddChild(buttons);

        _pageUrl = new Label
        {
            StyleClasses = { "LabelKeyText" },
            FontColorOverride = VoiceChatStyles.Accent,
            ClipText = true,
        };
        _code = new Button
        {
            StyleBoxOverride = VoiceChatStyles.CodeButtonStyle(),
            TextAlign = Label.AlignMode.Center,
            HorizontalExpand = true,
            ToolTip = Loc.GetString("voice-link-code-copy-hint"),
        };
        _code.Label.StyleClasses.Add("LabelHeadingBigger");
        _code.Label.FontColorOverride = VoiceChatStyles.Accent;
        _code.OnPressed += _ =>
        {
            if (_voice.LinkCode is not { } code)
                return;

            _clipboard.SetText(code);
            _entityManager.System<PopupSystem>().PopupCursor(Loc.GetString("voice-link-code-copied"));
        };

        var phone = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SizeFlagsStretchRatio = 1f,
            SeparationOverride = 4,
        };
        phone.AddChild(VoiceChatStyles.Heading(Loc.GetString("voice-link-phone-title")));
        phone.AddChild(VoiceChatStyles.WrappedMuted(Loc.GetString("voice-link-phone-text")));
        phone.AddChild(_pageUrl);
        phone.AddChild(_code);

        var columns = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 10,
        };
        columns.AddChild(VoiceChatStyles.CardHost(computer));
        columns.AddChild(VoiceChatStyles.CardHost(phone));

        AddChild(_status);
        AddChild(_detail);
        AddChild(columns);
        AddChild(VoiceChatStyles.WrappedMuted(Loc.GetString("voice-link-expiry")));
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = this;
        return true;
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = this;
        return true;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        _voice.LinkChanged += UpdateState;
        _voice.WebConnectedChanged += OnWebConnectedChanged;

        _voice.RequestLink();

        _voice.RequestLink();

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
        // Mini: a voice chat ban replaces the link with the ban reason and expiry.
        if (_voice.VoiceBanned)
        {
            _status.Text = Loc.GetString("voice-link-banned");
            _status.FontColorOverride = VoiceChatStyles.Danger;
            _detail.Visible = true;
            _detail.SetMessage(FormattedMessage.FromMarkupPermissive(
                FormattedMessage.EscapeText(BuildBanDetail())));
            _pageUrl.Text = _voice.PageUrl ?? Loc.GetString("voice-link-loading");
            _pageUrl.FontColorOverride = VoiceChatStyles.Offline;
            _code.Text = "--------";
            _open.Disabled = true;
            _copy.Disabled = true;
            _open.Modulate = VoiceChatStyles.Offline;
            _copy.Modulate = VoiceChatStyles.Offline;
            return;
        }

        _detail.Visible = false;
        _pageUrl.FontColorOverride = VoiceChatStyles.Accent;

        var ready = _voice.LinkUrl != null;

        _status.Text = Loc.GetString(_voice.WebConnected ? "voice-link-connected" : "voice-link-disconnected");
        _status.FontColorOverride = _voice.WebConnected ? VoiceChatStyles.Success : VoiceChatStyles.Danger;
        _pageUrl.Text = _voice.PageUrl ?? Loc.GetString("voice-link-loading");
        _code.Text = _voice.LinkCode ?? "--------";
        _open.Disabled = !ready;
        _copy.Disabled = !ready;
        _code.Disabled = !ready;
        _open.Modulate = ready ? Color.White : VoiceChatStyles.Offline;
        _copy.Modulate = ready ? Color.White : VoiceChatStyles.Offline;
        _code.Modulate = ready ? Color.White : VoiceChatStyles.Offline;
    }

    private string BuildBanDetail()
    {
        var expires = _voice.VoiceBanExpiresUnix > 0
            ? Loc.GetString("voice-ban-message-expires",
                ("time", DateTimeOffset.FromUnixTimeSeconds(_voice.VoiceBanExpiresUnix).ToLocalTime().ToString("f")))
            : Loc.GetString("voice-ban-message-permanent");

        var reason = string.IsNullOrWhiteSpace(_voice.VoiceBanReason) ? "-" : _voice.VoiceBanReason;
        return Loc.GetString("voice-link-banned-detail", ("reason", reason), ("expires", expires));
    }
}
