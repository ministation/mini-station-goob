// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Mini.NeuroPlayer;
using NUnit.Framework;

namespace Content.Tests.Server._Mini;

/// <summary>
/// Мини: приказы игроков разбираются детерминированно, без LLM. Тест держит список фраз из брифа
/// и проверяет, что обычная болтовня не принимается за приказ.
/// </summary>
[TestFixture]
public sealed class NeuroOrderParserTest
{
    [Test]
    [TestCase("Вася, пошли со мной", NeuroOrderKind.Follow)]
    [TestCase("Пойдём со мной, нужна помощь", NeuroOrderKind.Follow)]
    [TestCase("иди за мной", NeuroOrderKind.Follow)]
    [TestCase("следуй за мной!", NeuroOrderKind.Follow)]
    [TestCase("ПОМОГИ МНЕ", NeuroOrderKind.Follow)]
    [TestCase("нужна твоя помощь", NeuroOrderKind.Follow)]
    [TestCase("стой", NeuroOrderKind.Stop)]
    [TestCase("Отстань от меня", NeuroOrderKind.Stop)]
    [TestCase("хватит", NeuroOrderKind.Stop)]
    [TestCase("жди здесь", NeuroOrderKind.Stop)]
    [TestCase("не иди за мной", NeuroOrderKind.Stop)]
    [TestCase("убери мусор", NeuroOrderKind.CleanTrash)]
    [TestCase("Уберись тут", NeuroOrderKind.CleanTrash)]
    [TestCase("убери в баре", NeuroOrderKind.CleanTrash)]
    [TestCase("собери мусор, пожалуйста", NeuroOrderKind.CleanTrash)]
    [TestCase("выброси мусор", NeuroOrderKind.CleanTrash)]
    public void ParsesOrders(string message, NeuroOrderKind expected)
    {
        Assert.That(NeuroOrderParser.TryParse(message, out var kind), Is.True);
        Assert.That(kind, Is.EqualTo(expected));
    }

    [Test]
    [TestCase("привет, как дела?")]
    [TestCase("где тут бар?")]
    [TestCase("спасибо, я сам")]
    [TestCase("пойдем в бар вместе?")]
    [TestCase("убери")]
    [TestCase("")]
    public void IgnoresSmallTalk(string message)
    {
        Assert.That(NeuroOrderParser.TryParse(message, out var kind), Is.False);
        Assert.That(kind, Is.EqualTo(NeuroOrderKind.None));
    }
}
