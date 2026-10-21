// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Input;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Goobstation.Client.VoiceChat.UI;

public sealed partial class VoiceChatGuideWindow : DefaultWindow
{
    [Dependency] private IInputManager _input = default!;

    private static readonly string[] Sections = { "talking", "radio", "station", "controls", "rules" };

    public event Action? NeverAskPressed;

    public VoiceChatGuideWindow(bool prompt)
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("voice-guide-title");
        SetSize = new Vector2(760, 700);
        MinSize = new Vector2(640, 480);

        var backdrop = new PanelContainer
        {
            PanelOverride = VoiceChatStyles.Window(),
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        Contents.AddChild(backdrop);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            Margin = new Thickness(16, 12),
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        backdrop.AddChild(root);

        // Guide text lives in a dark card with the Mini accent headings.
        var guide = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true,
        };
        guide.AddChild(CreateText(Loc.GetString("voice-guide-intro")));

        var keys = new (string, object)[]
        {
            ("local", KeyName(ContentKeyFunctions.VoicePushToTalk)),
            ("radio", KeyName(ContentKeyFunctions.VoicePushToTalkRadio)),
        };

        foreach (var section in Sections)
        {
            var heading = VoiceChatStyles.Heading(Loc.GetString($"voice-guide-{section}-title"));
            heading.Margin = new Thickness(0, 8, 0, 0);
            guide.AddChild(heading);
            guide.AddChild(CreateText(Loc.GetString($"voice-guide-{section}", keys)));
        }

        var scroll = new ScrollContainer
        {
            HScrollEnabled = false,
            VerticalExpand = true,
            HorizontalExpand = true,
        };
        scroll.AddChild(guide);

        var guideCard = VoiceChatStyles.CardHost(scroll);
        guideCard.VerticalExpand = true;
        root.AddChild(guideCard);

        root.AddChild(VoiceChatStyles.Divider());

        root.AddChild(new Label
        {
            Text = Loc.GetString("voice-guide-connect-title"),
            StyleClasses = { "LabelHeadingBigger" },
            FontColorOverride = VoiceChatStyles.Accent,
        });

        root.AddChild(VoiceChatStyles.CardHost(new VoiceChatConnectPanel()));

        var close = VoiceChatStyles.Button(
            Loc.GetString(prompt ? "voice-prompt-later" : "voice-link-close"),
            accent: !prompt);
        close.OnPressed += _ => Close();

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Right,
            SeparationOverride = 6,
            Margin = new Thickness(0, 4, 0, 0),
        };
        buttons.AddChild(close);

        if (prompt)
        {
            var never = VoiceChatStyles.Button(Loc.GetString("voice-prompt-never"));
            never.OnPressed += _ =>
            {
                NeverAskPressed?.Invoke();
                Close();
            };
            buttons.AddChild(never);
        }

        root.AddChild(buttons);
    }

    private static RichTextLabel CreateText(string markup)
    {
        var label = new RichTextLabel
        {
            HorizontalExpand = true,
            Modulate = VoiceChatStyles.Text,
        };
        label.SetMessage(FormattedMessage.FromMarkupPermissive(markup));
        return label;
    }

    private string KeyName(BoundKeyFunction function)
    {
        return _input.TryGetKeyBinding(function, out var binding)
            ? FormattedMessage.EscapeText(binding.GetKeyString())
            : Loc.GetString("voice-guide-unbound");
    }
}
