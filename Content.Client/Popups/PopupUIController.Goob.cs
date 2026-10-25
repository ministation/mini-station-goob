using Content.Client.Resources;
using Content.Client.Chat.UI;
using Content.Goobstation.Common.CCVar;
using Content.Shared.Popups;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Configuration;

namespace Content.Client.Popups;

public sealed partial class PopupUIController
{
    [Dependency] private IConfigurationManager _cfg = default!;


    private bool _runechat;
    private int _runechatFontSize;
    private float _runechatOutlineScale;

    private void InitializeRunechat()
    {
        _cfg.OnValueChanged(GoobCVars.ChatRunechat, v => _runechat = v, true);
        _cfg.OnValueChanged(GoobCVars.ChatRunechatFontSize, v => _runechatFontSize = v, true);
        _cfg.OnValueChanged(GoobCVars.ChatRunechatOutlineScale, v => _runechatOutlineScale = v, true);
    }

    public static (Font Font, Color Color, float Outline) GetPopupStyle(PopupType type, bool runechat, int fontSize, float outlineScale)
    {
        var (baseSize, color) = type switch
        {
            PopupType.SmallCaution => (10, Color.Red),
            PopupType.Medium => (12, Color.LightGray),
            PopupType.MediumCaution => (12, Color.Red),
            PopupType.Large => (14, Color.LightGray),
            PopupType.LargeCaution => (14, Color.Red),
            _ => (10, Color.White),
        };

        if (!runechat)
        {
            // Mini: follow the selectable UI font stack (Pixeloid etc.) instead of a hardcoded popup font
            var cache = IoCManager.Resolve<IResourceCache>();
            return (cache.GetStack(baseSize >= 14 ? "Bold" : "Regular", baseSize), color, 0f);
        }

        fontSize = Math.Clamp(fontSize, RunechatStyle.MinFontSize, RunechatStyle.MaxFontSize);
        var size = RunechatStyle.ScaleSize(baseSize, fontSize);
        return (RunechatStyle.GetFont(RunechatStyle.FontPath, size), color, 2f * size / baseSize * outlineScale);
    }
}
