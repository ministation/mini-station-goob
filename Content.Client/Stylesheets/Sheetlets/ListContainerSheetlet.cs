using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets;

[CommonSheetlet]
public sealed class ListContainerSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet, IButtonConfig, IIconConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        IButtonConfig buttonCfg = sheet;

        // Dark base so list rows are not a pure-white bar under Mini StyleNano / AHelp.
        var box = new StyleBoxFlat()
        {
            BackgroundColor = new Color(55, 55, 68),
            ContentMarginLeftOverride = 8,
            ContentMarginTopOverride = 6,
            ContentMarginRightOverride = 6,
            ContentMarginBottomOverride = 6,
        };

        var rules = new List<StyleRule>(
        [
            E<ContainerButton>()
                .Class(ListContainer.StyleClassListContainerButton)
                .Box(box),
        ]);
        ButtonSheetlet<T>.MakeButtonRules<ContainerButton>(rules,
            buttonCfg.ButtonPalette,
            ListContainer.StyleClassListContainerButton);

        return rules.ToArray();
    }
}
