// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking;
using NUnit.Framework;

namespace Content.Tests.Server.Discord
{
    [TestFixture]
    public sealed class DiscordRoundEndFormatterTest
    {
        [Test]
        public void TruncateAtLineKeepsShortText()
        {
            Assert.That(DiscordRoundEndFormatter.TruncateAtLine("- objective", 40), Is.EqualTo("- objective"));
        }

        [Test]
        public void TruncateAtLineCutsAtLineEndAndFitsLimit()
        {
            var text = "первая строка\nвторая строка\nтретья строка";

            var cut = DiscordRoundEndFormatter.TruncateAtLine(text, 20);

            Assert.That(cut, Is.EqualTo("первая строка…"));
            Assert.That(cut.Length, Is.LessThanOrEqualTo(20));
        }

        [Test]
        public void TruncateAtLineHardCutsWithoutNewlines()
        {
            var text = new string('a', 30);

            var cut = DiscordRoundEndFormatter.TruncateAtLine(text, 10);

            Assert.That(cut, Is.EqualTo("aaaaaaaaa…"));
        }

        [Test]
        public void TruncateAtLineDoesNotSplitSurrogatePair()
        {
            var text = "x\uD83D\uDE00\uD83D\uDE00\uD83D\uDE00";

            var cut = DiscordRoundEndFormatter.TruncateAtLine(text, 3);

            Assert.That(cut, Is.EqualTo("x…"));
        }

        [Test]
        public void StripMarkupRemovesTagsAndKeepsEscapedBrackets()
        {
            var text = "[color=red]Френк[/color] (\\[TSF\\])\n- [bold]цель[/bold] | [color=green]Успех![/color]";

            var stripped = DiscordRoundEndFormatter.StripMarkup(text);

            Assert.That(stripped, Is.EqualTo("Френк ([TSF])\n- цель | Успех!"));
        }
    }
}
