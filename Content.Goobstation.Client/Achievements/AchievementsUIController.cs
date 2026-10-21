using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Goobstation.Client.Achievements.UI;
using Content.Goobstation.Shared.Achievements;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Goobstation.Client.Achievements;

/// <summary>
/// Shows unlock toasts and plays the unlock sound. The achievements browser itself lives in the
/// shared rewards menu (see <see cref="AchievementsPanel"/> and <see cref="AchievementsPanelProvider"/>).
/// </summary>
public sealed partial class AchievementsUIController : UIController, IOnSystemChanged<AchievementsSystem>
{
    [Dependency] private IGameTiming _timing = default!;

    [UISystemDependency] private readonly AudioSystem? _audio = default!;
    [UISystemDependency] private readonly SpriteSystem? _sprite = default!;

    private static readonly SoundSpecifier UnlockSound = new SoundPathSpecifier("/Audio/Machines/chime.ogg");
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan ToastFade = TimeSpan.FromSeconds(1);
    private const int MaxToasts = 3;
    private const float ToastTopOffset = 0.08f;

    private BoxContainer? _toastStack;

    public void OnSystemLoaded(AchievementsSystem system)
    {
        system.Unlocked += OnUnlocked;
    }

    public void OnSystemUnloaded(AchievementsSystem system)
    {
        system.Unlocked -= OnUnlocked;
    }

    private void OnUnlocked(AchievementData data)
    {
        if (_sprite == null)
            return;

        EnsureToastStack();

        while (_toastStack!.ChildCount >= MaxToasts)
        {
            _toastStack.GetChild(0).Orphan();
        }

        _toastStack.AddChild(new AchievementToast(data, _sprite, _timing.RealTime + ToastDuration));
        _audio?.PlayGlobal(UnlockSound, Filter.Local(), false);
    }

    private void EnsureToastStack()
    {
        if (_toastStack is { Disposed: false, Parent: not null })
            return;

        _toastStack = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MouseFilter = Control.MouseFilterMode.Ignore,
            SeparationOverride = 8,
        };

        UIManager.PopupRoot.AddChild(_toastStack);
    }

    private void PositionToastStack(BoxContainer stack)
    {
        var position = Vector2.Zero;
        var size = UIManager.PopupRoot.Size;

        if (UIManager.ActiveScreen?.GetWidget<MainViewport>() is { } viewport)
        {
            position = viewport.GlobalPosition;
            size = viewport.Size;
        }

        LayoutContainer.SetPosition(stack, new Vector2(
            position.X + (size.X - stack.Size.X) / 2f,
            position.Y + size.Y * ToastTopOffset));
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_toastStack == null || _toastStack.ChildCount == 0)
            return;

        PositionToastStack(_toastStack);

        var now = _timing.RealTime;
        for (var i = _toastStack.ChildCount - 1; i >= 0; i--)
        {
            if (_toastStack.GetChild(i) is not AchievementToast toast)
                continue;

            var remaining = toast.ExpireTime - now;
            if (remaining <= TimeSpan.Zero)
            {
                toast.Orphan();
                continue;
            }

            toast.Modulate = Color.White.WithAlpha(remaining < ToastFade
                ? (float) (remaining / ToastFade)
                : 1f);
        }
    }
}
