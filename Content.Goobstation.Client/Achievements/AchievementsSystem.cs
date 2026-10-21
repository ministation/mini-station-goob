using Content.Client._Mini.DailyRewards;
using Content.Goobstation.Client.Achievements.UI;
using Content.Goobstation.Shared.Achievements;

namespace Content.Goobstation.Client.Achievements;

public sealed class AchievementsSystem : EntitySystem
{
    public List<AchievementData>? Achievements { get; private set; }

    public event Action? Updated;
    public event Action<AchievementData>? Unlocked;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AchievementsStateEvent>(OnState);
        SubscribeNetworkEvent<AchievementUnlockedEvent>(OnUnlocked);

        // The rewards menu embeds the achievements browser. Hand it a panel factory so the menu
        // does not have to reference this content module.
        AchievementsPanelSlot.Factory = () => new AchievementsPanel();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        AchievementsPanelSlot.Factory = null;
    }

    public void RequestState()
    {
        RaiseNetworkEvent(new RequestAchievementsEvent());
    }

    private void OnState(AchievementsStateEvent ev)
    {
        Achievements = ev.Achievements;
        Updated?.Invoke();
    }

    private void OnUnlocked(AchievementUnlockedEvent ev)
    {
        if (Achievements != null)
        {
            var index = Achievements.FindIndex(a => a.Id == ev.Achievement.Id);
            if (index >= 0)
                Achievements[index] = ev.Achievement;
            else
                Achievements.Add(ev.Achievement);
        }

        Unlocked?.Invoke(ev.Achievement);
        Updated?.Invoke();
    }
}
