// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Reflection;

namespace Content.Client.Stylesheets
{
    public sealed class StylesheetManager : IStylesheetManager
    {
        [Dependency] private readonly ILogManager _logManager = default!;
        [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private readonly IReflectionManager _reflection = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] private readonly IFontManager _fontManager = default!;
        [Dependency] private readonly IResourceCache _resCache = default!;
        [Dependency] private readonly IUiFontStackManager _uiFontStackManager = default!;

        public Stylesheet SheetNanotrasen { get; private set; } = default!;
        public Stylesheet SheetSystem { get; private set; } = default!;

        [Obsolete("Update to use SheetNanotrasen instead")]
        public Stylesheet SheetNano { get; private set; } = default!;

        [Obsolete("Update to use SheetSystem instead")]
        public Stylesheet SheetSpace { get; private set; } = default!;

        public event Action? StylesheetsUpdated;

        private Dictionary<string, Stylesheet> Stylesheets { get; set; } = default!;

        public bool TryGetStylesheet(string name, [MaybeNullWhen(false)] out Stylesheet stylesheet)
        {
            return Stylesheets.TryGetValue(name, out stylesheet);
        }

        public HashSet<Type> UnusedSheetlets { get; private set; } = [];

        public void Initialize()
        {
            var sawmill = _logManager.GetSawmill("style");
            sawmill.Debug("Initializing Stylesheets...");
            var sw = Stopwatch.StartNew();

            _uiFontStackManager.Initialize();

            // add all sheetlets to the hashset
            var tys = _reflection.FindTypesWithAttribute<CommonSheetletAttribute>();
            UnusedSheetlets = [..tys];

            Stylesheets = new Dictionary<string, Stylesheet>();
            SheetNanotrasen = Init(new NanotrasenStylesheet(new BaseStylesheet.NoConfig(), this));
            SheetSystem = Init(new SystemStylesheet(new BaseStylesheet.NoConfig(), this));

            // Mini: StyleNano remains the active skin (lobby, accents, custom fonts).
            _configurationManager.OnValueChanged(CCVars.InterfaceAccentRed, _ => UpdateMiniStyles(), false);
            _configurationManager.OnValueChanged(CCVars.InterfaceAccentGreen, _ => UpdateMiniStyles(), false);
            _configurationManager.OnValueChanged(CCVars.InterfaceAccentBlue, _ => UpdateMiniStyles(), false);
            _configurationManager.OnValueChanged(CCVars.UiFontStyle, _ => UpdateMiniStyles(), false);
            UpdateMiniStyles();

            // warn about unused sheetlets
            if (UnusedSheetlets.Count > 0)
            {
                var sheetlets = UnusedSheetlets.AsEnumerable()
                    .Take(5)
                    .Select(t => t.FullName ?? "<could not get FullName>")
                    .ToArray();
                sawmill.Error($"There are unloaded sheetlets: {string.Join(", ", sheetlets)}");
            }

            sawmill.Debug($"Initialized {_styleRuleCount} style rules in {sw.Elapsed}");
        }

        private void UpdateMiniStyles()
        {
            var accent = new Color(
                (byte) _configurationManager.GetCVar(CCVars.InterfaceAccentRed),
                (byte) _configurationManager.GetCVar(CCVars.InterfaceAccentGreen),
                (byte) _configurationManager.GetCVar(CCVars.InterfaceAccentBlue));

            SheetNano = new StyleNano(_resCache, accent).Stylesheet;
            SheetSpace = new StyleSpace(_resCache, accent).Stylesheet;
            _fontManager.ClearFontCache();
            _userInterfaceManager.Stylesheet = SheetNano;
            StylesheetsUpdated?.Invoke();
        }

        private int _styleRuleCount;

        private Stylesheet Init(BaseStylesheet baseSheet)
        {
            Stylesheets.Add(baseSheet.StylesheetName, baseSheet.Stylesheet);
            _styleRuleCount += baseSheet.Stylesheet.Rules.Count;
            return baseSheet.Stylesheet;
        }
    }
}
