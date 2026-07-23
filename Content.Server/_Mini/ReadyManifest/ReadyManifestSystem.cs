using System.Linq;
using Content.Server._Mini.AntagTokens;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Preferences.Managers;
using Content.Shared._Mini.ReadyManifest;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Mini.ReadyManifest;

public sealed class ReadyManifestSystem : EntitySystem
{
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly AntagTokenSystem _antagTokens = default!;

    private readonly Dictionary<ICommonSession, ReadyManifestEui> _openEuis = new();
    private Dictionary<ProtoId<JobPrototype>, List<string>> _jobCharacters = new();

    private const int MinJobWeightForAutoInclude = 10;

    public override void Initialize()
    {
        SubscribeNetworkEvent<RequestReadyManifestMessage>(OnRequestReadyManifest);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<PlayerToggleReadyEvent>(OnPlayerToggleReady);
        SubscribeLocalEvent<AntagTokenQueueChangedEvent>(OnAntagQueueChanged);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        foreach (var (_, eui) in _openEuis)
        {
            eui.Close();
        }

        _openEuis.Clear();
        _jobCharacters.Clear();
    }

    private void OnRequestReadyManifest(RequestReadyManifestMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession is not { } sessionCast)
            return;

        if (!_configManager.GetCVar(CCVars.CrewManifestWithoutEntity))
            return;

        // Only useful in pre-round lobby.
        if (_gameTicker.RunLevel != GameRunLevel.PreRoundLobby)
            return;

        BuildReadyManifest();
        OpenEui(sessionCast, args.SenderSession.AttachedEntity);
    }

    private void OnPlayerToggleReady(PlayerToggleReadyEvent ev)
    {
        var userId = ev.PlayerSession.UserId;

        if (!_prefsManager.TryGetCachedPreferences(userId, out var preferences))
            return;

        if (preferences.SelectedCharacter is not HumanoidCharacterProfile profile)
            return;

        var profileJobs = FilterPlayerJobs(profile);
        var characterName = profile.Name;

        if (_gameTicker.PlayerGameStatuses.TryGetValue(userId, out var status) &&
            status == PlayerGameStatus.ReadyToPlay)
        {
            foreach (var job in profileJobs)
            {
                if (!_jobCharacters.TryGetValue(job, out var list))
                {
                    list = new List<string>();
                    _jobCharacters[job] = list;
                }

                if (!list.Contains(characterName))
                    list.Add(characterName);
            }
        }
        else
        {
            foreach (var job in profileJobs)
            {
                if (!_jobCharacters.TryGetValue(job, out var characters))
                    continue;

                characters.Remove(characterName);
                if (characters.Count == 0)
                    _jobCharacters.Remove(job);
            }
        }

        UpdateEuis();
    }

    private void OnAntagQueueChanged(AntagTokenQueueChangedEvent ev)
    {
        UpdateEuis();
    }

    private void BuildReadyManifest()
    {
        var jobCharacters = new Dictionary<ProtoId<JobPrototype>, List<string>>();

        foreach (var (userId, status) in _gameTicker.PlayerGameStatuses)
        {
            if (status != PlayerGameStatus.ReadyToPlay)
                continue;

            if (!_prefsManager.TryGetCachedPreferences(userId, out var preferences))
                continue;

            if (preferences.SelectedCharacter is not HumanoidCharacterProfile profile)
                continue;

            var characterName = profile.Name;
            var profileJobs = FilterPlayerJobs(profile);

            foreach (var jobId in profileJobs)
            {
                if (!jobCharacters.TryGetValue(jobId, out var list))
                {
                    list = new List<string>();
                    jobCharacters[jobId] = list;
                }

                if (!list.Contains(characterName))
                    list.Add(characterName);
            }
        }

        _jobCharacters = jobCharacters;
    }

    private List<ProtoId<JobPrototype>> FilterPlayerJobs(HumanoidCharacterProfile profile)
    {
        List<ProtoId<JobPrototype>> priorityJobs = [];
        foreach (var (job, priority) in profile.JobPriorities)
        {
            if (priority == JobPriority.High ||
                (_prototypeManager.Index(job).Weight >= MinJobWeightForAutoInclude && priority > JobPriority.Never))
            {
                priorityJobs.Add(job);
            }
        }

        return priorityJobs;
    }

    public IReadOnlyDictionary<ProtoId<JobPrototype>, List<string>> GetReadyManifest()
    {
        return _jobCharacters;
    }

    public List<ReadyManifestAntagEntry> GetAntagQueue()
    {
        return _antagTokens.GetLobbyAntagQueueForManifest();
    }

    public void OpenEui(ICommonSession session, EntityUid? owner = null)
    {
        if (_openEuis.TryGetValue(session, out var existing))
        {
            existing.StateDirty();
            return;
        }

        var eui = new ReadyManifestEui(owner, this);
        _openEuis.Add(session, eui);
        _euiManager.OpenEui(eui, session);
        eui.StateDirty();
    }

    private void UpdateEuis()
    {
        foreach (var (_, eui) in _openEuis)
        {
            eui.StateDirty();
        }
    }

    public void CloseEui(ICommonSession session, EntityUid? owner = null)
    {
        if (!_openEuis.TryGetValue(session, out var eui))
            return;

        if (eui.Owner != owner)
            return;

        _openEuis.Remove(session);
    }
}
