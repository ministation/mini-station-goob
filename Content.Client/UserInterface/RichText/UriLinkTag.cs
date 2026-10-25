// SPDX-FileCopyrightText: 2026 Mini Station contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.RichText;

/// <summary>
/// Mini: clickable external link for rich text (AHelp chat).
/// Usage: <c>[urllink="https://example.com"]</c> (self-closing) or with custom text
/// <c>[urllink="https://example.com" text="example.com"]</c>.
/// Only absolute http(s) URLs are accepted; clicking one opens it in the system browser.
/// </summary>
[UsedImplicitly] // Registered automatically by MarkupTagManager via reflection.
public sealed partial class UriLinkTag : IMarkupTagHandler
{
    public const string TagName = "urllink";

    private static readonly Color LinkColor = Color.CornflowerBlue;
    private static readonly Color HoverColor = Color.LightSkyBlue;

    [Dependency] private readonly IUriOpener _uriOpener = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    public string Name => TagName;

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;

        // Self-closing tag: markup also produces a closing node without a value, ignore it.
        if (node.Closing
            || !node.Value.TryGetString(out var url)
            || !IsSupportedUrl(url))
        {
            return false;
        }

        var text = node.Attributes.TryGetValue("text", out var textParameter)
                   && textParameter.TryGetString(out var customText)
                   && !string.IsNullOrEmpty(customText)
            ? customText
            : url;

        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterMode.Stop,
            FontColorOverride = LinkColor,
            DefaultCursorShape = Control.CursorShape.Hand,
            // Match the chat output font (12 px stack) instead of the default UI label font (13 px).
            FontOverride = UiChatFonts.Get(_resourceCache),
        };

        if (!string.Equals(text, url, StringComparison.Ordinal))
            label.ToolTip = url;

        label.OnMouseEntered += _ => label.FontColorOverride = HoverColor;
        label.OnMouseExited += _ => label.FontColorOverride = LinkColor;
        label.OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;

            Open(url);
        };

        control = label;
        return true;
    }

    private void Open(string url)
    {
        if (!IsSupportedUrl(url))
            return;

        try
        {
            _uriOpener.OpenUri(url);
        }
        catch (Exception e)
        {
            Logger.ErrorS("ahelp-link", $"Failed to open link '{url}': {e}");
        }
    }

    private static bool IsSupportedUrl(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
