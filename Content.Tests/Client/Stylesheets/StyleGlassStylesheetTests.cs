// SPDX-FileCopyrightText: 2026 Contributors
//
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Client.Stylesheets;
using NUnit.Framework;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.UnitTesting;

namespace Content.Tests.Client.Stylesheets;

[TestFixture]
public sealed class StyleGlassStylesheetTests : RobustUnitTest
{
    public override UnitTestProject Project => UnitTestProject.Client;

    [OneTimeSetUp]
    public void Setup()
    {
        IoCManager.Resolve<IUserInterfaceManager>().InitializeTesting();
    }

    [Test]
    public void NanoWindowPanelUsesTranslucentFlatStyle()
    {
        var sheet = new StyleNano(IoCManager.Resolve<IResourceCache>()).Stylesheet;
        var rule = FindLastRule(sheet,
            selector => HasClass(selector, DefaultWindow.StyleClassWindowPanel),
            PanelContainer.StylePropertyPanel);

        var panel = (StyleBoxFlat) GetProperty(rule, PanelContainer.StylePropertyPanel).Value;
        Assert.That(panel.BackgroundColor.A, Is.LessThan((byte) 255));
        Assert.That(panel.BorderThickness.Left, Is.EqualTo(1).Within(0.01f));
    }

    [Test]
    public void NanoButtonsUseFlatGlassStateStyles()
    {
        var sheet = new StyleNano(IoCManager.Resolve<IResourceCache>()).Stylesheet;
        var rule = FindLastRule(sheet,
            selector => selector.ElementType == typeof(ContainerButton)
                        && HasClass(selector, ContainerButton.StyleClassButton)
                        && HasPseudo(selector, ContainerButton.StylePseudoClassHover),
            ContainerButton.StylePropertyStyleBox);

        var box = (StyleBoxFlat) GetProperty(rule, ContainerButton.StylePropertyStyleBox).Value;
        Assert.That(box.BackgroundColor.A, Is.LessThan((byte) 255));
        Assert.That(box.BorderThickness.Left, Is.EqualTo(1).Within(0.01f));
    }

    [Test]
    public void NanoLineEditUsesFlatGlassStyle()
    {
        var sheet = new StyleNano(IoCManager.Resolve<IResourceCache>()).Stylesheet;
        var rule = FindLastRule(sheet,
            selector => selector.ElementType == typeof(LineEdit),
            LineEdit.StylePropertyStyleBox);

        var box = (StyleBoxFlat) GetProperty(rule, LineEdit.StylePropertyStyleBox).Value;
        Assert.That(box.BackgroundColor.A, Is.LessThan((byte) 255));
        Assert.That(box.BorderThickness.Left, Is.EqualTo(1).Within(0.01f));
    }

    [Test]
    public void SpaceWindowPanelUsesTranslucentFlatStyle()
    {
        var sheet = new StyleSpace(IoCManager.Resolve<IResourceCache>()).Stylesheet;
        var rule = FindLastRule(sheet,
            selector => HasClass(selector, DefaultWindow.StyleClassWindowPanel),
            PanelContainer.StylePropertyPanel);

        var panel = (StyleBoxFlat) GetProperty(rule, PanelContainer.StylePropertyPanel).Value;
        Assert.That(panel.BackgroundColor.A, Is.LessThan((byte) 255));
        Assert.That(panel.BorderThickness.Left, Is.EqualTo(1).Within(0.01f));
    }

    private static StyleRule FindLastRule(
        Stylesheet sheet,
        System.Func<SelectorElement, bool> selectorPredicate,
        string propertyName)
    {
        var rule = sheet.Rules.LastOrDefault(rule =>
            rule.Selector is SelectorElement selector
            && selectorPredicate(selector)
            && rule.Properties.Any(property => property.Name == propertyName));

        Assert.That(rule, Is.Not.Null, $"Expected rule with property '{propertyName}'.");
        return rule!;
    }

    private static StyleProperty GetProperty(StyleRule rule, string propertyName)
    {
        var property = rule.Properties.LastOrDefault(property => property.Name == propertyName);
        Assert.That(property.Name, Is.EqualTo(propertyName));
        return property;
    }

    private static bool HasClass(SelectorElement selector, string styleClass)
    {
        return selector.ElementClasses != null && selector.ElementClasses.Contains(styleClass);
    }

    private static bool HasPseudo(SelectorElement selector, string pseudoClass)
    {
        return selector.PseudoClasses != null && selector.PseudoClasses.Contains(pseudoClass);
    }
}
