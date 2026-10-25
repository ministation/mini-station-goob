// SPDX-FileCopyrightText: 2026 Mini Station contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client.Administration.UI.Bwoink;
using Content.Client.UserInterface.RichText;
using NUnit.Framework;
using Robust.Shared.Utility;

namespace Content.Tests.Client.Administration;

[TestFixture]
public sealed class AHelpLinkHelperTest
{
    [Test]
    [TestCase("https://example.com", "[urllink=\"https://example.com\" /]")]
    [TestCase("https://example.com/path?a=1&b=2", "[urllink=\"https://example.com/path?a=1&b=2\" /]")]
    [TestCase("HTTPS://EXAMPLE.COM/Path", "[urllink=\"HTTPS://EXAMPLE.COM/Path\" /]")]
    [TestCase("https://example.com.", "[urllink=\"https://example.com\" /].")]
    [TestCase("(https://example.com)", "([urllink=\"https://example.com\" /])")]
    [TestCase("https://ru.wikipedia.org/wiki/Функция", "[urllink=\"https://ru.wikipedia.org/wiki/Функция\" /]")]
    [TestCase("www.example.com", "[urllink=\"https://www.example.com\" text=\"www.example.com\" /]")]
    [TestCase("см. https://example.com, потом", "см. [urllink=\"https://example.com\" /], потом")]
    public void LinkifiesUrls(string input, string expected)
    {
        Assert.That(AHelpLinkHelper.Linkify(input), Is.EqualTo(expected));
    }

    [Test]
    [TestCase("")]
    [TestCase("обычный текст без ссылок")]
    [TestCase("xhttps://example.com")]
    [TestCase("https://")]
    [TestCase("[color=gray]без ссылок[/color]")]
    public void KeepsPlainText(string input)
    {
        Assert.That(AHelpLinkHelper.Linkify(input), Is.EqualTo(input));
    }

    [Test]
    [TestCase("см. https://example.com, потом")]
    [TestCase("www.example.com")]
    [TestCase("https://ru.wikipedia.org/wiki/Функция")]
    [TestCase("https://ru.wikipedia.org/wiki/Функция_(математика)")]
    [TestCase("[color=gray]https://example.com/?a=1&b=2[/color]")]
    public void LinkifiedMarkupIsValid(string input)
    {
        var markup = AHelpLinkHelper.Linkify(input);
        Assert.That(FormattedMessage.TryFromMarkup(markup, out var message, out var error), Is.True, error);
        Assert.That(message!.Nodes.Count(n => n.Name == UriLinkTag.TagName && !n.Closing), Is.EqualTo(1));
    }

    [Test]
    public void WwwLinkKeepsVisibleText()
    {
        var markup = AHelpLinkHelper.Linkify("www.example.com");
        Assert.That(FormattedMessage.TryFromMarkup(markup, out var message, out _), Is.True);

        var node = message!.Nodes.First(n => n.Name == UriLinkTag.TagName && !n.Closing);
        Assert.That(node.Value.StringValue, Is.EqualTo("https://www.example.com"));
        Assert.That(node.Attributes["text"].StringValue, Is.EqualTo("www.example.com"));
    }

    [Test]
    public void LongLinksKeepFullTargetButShortVisibleText()
    {
        const string url = "https://example.com/very/long/path/that/keeps/going/and/going/for/quite/a/long/time?with=query&and=more";
        var markup = AHelpLinkHelper.Linkify("см. " + url);

        Assert.That(FormattedMessage.TryFromMarkup(markup, out var message, out var error), Is.True, error);

        var node = message!.Nodes.First(n => n.Name == UriLinkTag.TagName && !n.Closing);
        Assert.That(node.Value.StringValue, Is.EqualTo(url));
        Assert.That(node.Attributes["text"].StringValue, Has.Length.LessThanOrEqualTo(AHelpLinkHelper.MaxDisplayLength));
        Assert.That(node.Attributes["text"].StringValue, Does.EndWith("..."));
    }

    [Test]
    public void LinkifiesSeveralUrls()
    {
        const string input = "https://a.example и https://b.example/page";
        const string expected = "[urllink=\"https://a.example\" /] и [urllink=\"https://b.example/page\" /]";
        Assert.That(AHelpLinkHelper.Linkify(input), Is.EqualTo(expected));
    }
}
