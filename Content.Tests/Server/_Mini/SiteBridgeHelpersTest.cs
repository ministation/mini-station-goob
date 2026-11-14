// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Server._Mini.SiteBridge;
using NUnit.Framework;

namespace Content.Tests.Server._Mini;

/// <summary>
/// Мини: чистая логика site bridge (адрес сайта, адрес хоста, обрезка вывода).
/// </summary>
[TestFixture]
public sealed class SiteBridgeHelpersTest
{
    [Test]
    [TestCase("https://ministation.ru", "https://ministation.ru/")]
    [TestCase(" http://127.0.0.1:8765/ ", "http://127.0.0.1:8765/")]
    [TestCase("https://example.org/site-bridge", "https://example.org/site-bridge/")]
    public void NormalizesBaseUrl(string raw, string expected)
    {
        Assert.That(SiteBridgeHelpers.TryNormalizeBaseUrl(raw, out var url), Is.True);
        Assert.That(url!.ToString(), Is.EqualTo(expected));
    }

    [Test]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("ftp://example.org")]
    [TestCase("not a url")]
    [TestCase(null)]
    public void RejectsBadBaseUrl(string raw)
    {
        Assert.That(SiteBridgeHelpers.TryNormalizeBaseUrl(raw, out var url), Is.False);
        Assert.That(url, Is.Null);
    }

    [Test]
    [TestCase("ss14s://ss14.ministation.ru", "ss14.ministation.ru")]
    [TestCase("ss14://example.org:1212", "example.org")]
    [TestCase("example.org:1212", "example.org")]
    [TestCase("https://example.org/path", "example.org")]
    [TestCase("", "")]
    [TestCase(null, "")]
    public void ExtractsAddress(string raw, string expected)
    {
        Assert.That(SiteBridgeHelpers.ExtractAddress(raw), Is.EqualTo(expected));
    }

    [Test]
    public void TruncatesByUtf8Bytes()
    {
        Assert.That(SiteBridgeHelpers.TruncateUtf8("abcdef", 4), Is.EqualTo("abcd"));
        // Каждая кириллическая буква это 2 байта, поэтому в 5 байт влезает 2 буквы.
        Assert.That(SiteBridgeHelpers.TruncateUtf8("привет", 5), Is.EqualTo("пр"));
        Assert.That(SiteBridgeHelpers.TruncateUtf8("ok", 100), Is.EqualTo("ok"));
        Assert.That(SiteBridgeHelpers.TruncateUtf8(null, 10), Is.EqualTo(""));
        Assert.That(SiteBridgeHelpers.TruncateUtf8("abc", 0), Is.EqualTo(""));
    }

    [Test]
    public void DoesNotSplitSurrogatePair()
    {
        // Эмодзи это 4 байта; лимит в 4 байта оставляет только "a", парные
        // суррогаты не разрезаются.
        Assert.That(SiteBridgeHelpers.TruncateUtf8("a\U0001F600", 4), Is.EqualTo("a"));
        Assert.That(SiteBridgeHelpers.TruncateUtf8("a\U0001F600", 5), Is.EqualTo("a\U0001F600"));
    }

    [Test]
    public void KeepsOutputLimitSmall()
    {
        Assert.That(SiteBridgeHelpers.MaxOutputBytes, Is.EqualTo(64 * 1024));
    }
}
