using Robust.Shared.Configuration;

namespace Content.Shared._Mini.MiniCCVars;


[CVarDefs]
// ReSharper disable once InconsistentNaming
public sealed class MiniCCVars
{
    /*
	* GCF
	*/
    public static readonly CVarDef<int> MapVoteRecentBanDepth =
        CVarDef.Create("game.map_vote_recent_ban_depth", 1, CVar.SERVER | CVar.ARCHIVE);
    /// <summary>
    ///     Whether GCF being shown is enabled at all.
    /// </summary>

    public static readonly CVarDef<bool> GCFEnabled =
        CVarDef.Create("gcf_auto.enabled", true);

    /// <summary>
    ///     Notify for admin about GCF Clean.
    /// </summary>
    public static readonly CVarDef<bool> GCFNotify =
        CVarDef.Create("gcf_auto.notify", false);

    /// <summary>
    ///     The number of seconds between each GCF
    /// </summary>
    public static readonly CVarDef<float> GCFFrequency =
        CVarDef.Create("gcf_auto.frequency", 300f);

    /*
	* InfoLinks
	*/

    /// <summary>
    /// IPs address for reconnect.
    /// </summary>
    public static readonly CVarDef<string> InfoLinksIPs =
        CVarDef.Create("infolinks.ips", string.Empty, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Multiplier for playtime.
    /// </summary>
    public static readonly CVarDef<float> PlayTimeMultiplier =
        CVarDef.Create("playtime.multiplier", 1f, CVar.SERVER | CVar.REPLICATED);

    /*
	* TTS
	*/

    public static readonly CVarDef<float> TTSVolumeRadio =
        CVarDef.Create("tts.volume_radio", 3f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> RadioTTSSoundsEnabled =
        CVarDef.Create("audio.radio_tts_sounds_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Client toggle for Typan station war background music.
    /// </summary>
    public static readonly CVarDef<bool> WarMusicEnabled =
        CVarDef.Create("audio.war_music_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
    * Lobby ui
    */

    /// <summary>
    /// Lobby default background. Can be Parallax or Image.
    /// </summary>
    public static readonly CVarDef<string> Background =
        CVarDef.Create("ui.background", "Image", CVar.CLIENTONLY | CVar.ARCHIVE);

    /**
     * TTS (Text-To-Speech)
     */

    /// <summary>
    /// Option to disable TTS events for client
    /// </summary>
    public static readonly CVarDef<bool> TTSClientEnabled =
        CVarDef.Create("tts.client_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> TTSRadioGhostEnabled =
        CVarDef.Create("tts.radio_ghost_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Option to disable TTS queue in radio for client
    /// </summary>
    public static readonly CVarDef<bool> TTSClientQueueEnabled =
        CVarDef.Create("tts.queue_enabled", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> TTSRadioVolume =
        CVarDef.Create("tts.radio_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> TTSRadioEffect =
        CVarDef.Create("tts.radio_effect", "radio", CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> TTSAnnounceEffect =
        CVarDef.Create("tts.announce_effect", "tiny_room", CVar.SERVERONLY | CVar.ARCHIVE);

    /**
     * Ban Webhook
     */

    public static readonly CVarDef<string> DiscordBanWebhook =
        CVarDef.Create("discord.ban_webhook", string.Empty, CVar.SERVERONLY);

    /*
     * Service Authorization
     */

    public static readonly CVarDef<bool> ServiceAuthEnabled =
        CVarDef.Create("service_auth.enabled", false, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<string> ServiceAuthApiUrl =
        CVarDef.Create("service_auth.api_url", "", CVar.SERVERONLY);

    public static readonly CVarDef<string> ServiceAuthApiToken =
        CVarDef.Create("service_auth.api_token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<bool> ServiceAuthCheckTelegramMember =
        CVarDef.Create("service_auth.check_telegram_member", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> ServiceAuthCheckDiscordMember =
        CVarDef.Create("service_auth.check_discord_member", false, CVar.SERVERONLY);

    public static readonly CVarDef<string> ServiceAuthProjectName =
        CVarDef.Create("service_auth.project_name", string.Empty, CVar.SERVERONLY);

    /*
     * GodMode RoundEnd
     */

    public static readonly CVarDef<bool> GodModeRoundEnd =
        CVarDef.Create("game.godmode_end", false, CVar.SERVERONLY);
    /*
     *  Sponsor API
     */

    public static readonly CVarDef<string> SponsorApiUrl =
        CVarDef.Create("sponsor.api_url", "", CVar.SERVERONLY);

    public static readonly CVarDef<string> SponsorApiToken =
        CVarDef.Create("sponsor.api_token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> SponsorGhostTheme =
        CVarDef.Create("sponsor.ghost_theme", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> SponsorPet =
        CVarDef.Create("sponsor.pet", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> SponsorProjectName =
        CVarDef.Create("sponsor.project_name", string.Empty, CVar.SERVERONLY);

    /*
     *  Greetings
     */

    public static readonly CVarDef<bool> GreetingsEnable =
        CVarDef.Create("greetings.enable", true);

    public static readonly CVarDef<string> GreetingsMessage =
        CVarDef.Create("greetings.message", "Привет");

    public static readonly CVarDef<string> GreetingsAuthor =
        CVarDef.Create("greetings.author", "Сервер");

    /*
     * New Life
     */

    public static readonly CVarDef<bool> NewLifeEnable =
        CVarDef.Create("newlife.enable", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> NewLifeSponsorOnly =
        CVarDef.Create("newlife.sponsor_only", false, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> NewLifeTimeout =
        CVarDef.Create("newlife.timeout", 5, CVar.SERVERONLY);

    /*
     * Servers Hub
     */

    public static readonly CVarDef<bool> ServersHubEnable =
        CVarDef.Create("servers_hub.enable", false, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Список серверов отображаемых в хабе. Разделяются через запятую.
    /// </summary>
    public static readonly CVarDef<string> ServersHubList =
        CVarDef.Create("servers_hub.urls", "", CVar.SERVERONLY);

    /// <summary>
    /// Простое название сервера для отображения в хабе.
    /// </summary>

    public static readonly CVarDef<string> ServersHubShortName =
        CVarDef.Create("servers_hub.short_name", "SS14 SERVER", CVar.SERVERONLY);

    /**
     * Tape Player
     */

    /// <summary>
    /// Параметр отключения школьников с колонками у клиента.
    /// </summary>
    public static readonly CVarDef<bool> TapePlayerClientEnabled =
        CVarDef.Create("tape_player.client_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * INFOLINKS
     */

    /// <summary>
    /// Link to the site donate page to show in the launcher.
    /// </summary>
    public static readonly CVarDef<string> InfoLinksDonate =
        CVarDef.Create("infolinks.donate", "https://ministation.ru/donate", CVar.SERVER | CVar.REPLICATED);

    /**
     * Lobby
     */

    public static readonly CVarDef<string> LobbyBackgroundType =
        CVarDef.Create("lobby.background", "Random", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> LobbyArt =
        CVarDef.Create("lobby.art", "Random", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> LobbyAnimation =
        CVarDef.Create("lobby.animation", "Random", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> LobbyParallax =
        CVarDef.Create("lobby.parallax", "Random", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> LobbyOpacity =
        CVarDef.Create("lobby.lobby_opacity", 0.90f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Show "unlock early for coins" buttons on locked jobs and antags in the lobby.
    /// </summary>
    public static readonly CVarDef<bool> RoleUnlockEarlyPurchaseEnabled =
        CVarDef.Create("role_unlock.early_purchase_enabled", false, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<string> ServerName =
        CVarDef.Create("lobby.server_name", "Mini Station", CVar.SERVER | CVar.REPLICATED);

    /*
     * Planet Prison
     */

    public static readonly CVarDef<bool> MinPlayersEnable =
        CVarDef.Create("planet_prison.enable", false, CVar.SERVERONLY);

    public static readonly CVarDef<int> MinPlayersPlanetPrison =
        CVarDef.Create("planet_prison.min_players", 0, CVar.SERVERONLY);

    /*
     * MaxLoadedChunks
     */

    public static readonly CVarDef<int> MaxLoadedChunks =
        CVarDef.Create("chunk.max", 100, CVar.ARCHIVE | CVar.REPLICATED | CVar.SERVER);

    /**
     * Roadmap
     */

    public static readonly CVarDef<string> RoadmapId =
        CVarDef.Create("roadmap.id", "MiniRoadmap");

    /**
     * Lobby Changelog
     */

    public static readonly CVarDef<string> LobbyChangelogsList =
        CVarDef.Create("lobby_changelog.list", "ChangelogMini.yml", CVar.SERVER | CVar.REPLICATED);

    /*
     * Cryoteleport
     */

    public static readonly CVarDef<bool> CryoTeleportEnable =
        CVarDef.Create("cryo_teleport.enable", true, CVar.SERVERONLY);

    public static readonly CVarDef<int> CryoTeleportTransferDelay =
        CVarDef.Create("cryo_teleport.transfer_delay", 5, CVar.SERVERONLY);

    /*
     * Damage
     */

    public static readonly CVarDef<float> DamagePositiveVariance =
        CVarDef.Create("damage.positive_variance", 0f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> DamageNegativeVariance =
        CVarDef.Create("damage.negative_variance", 0.3f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> DamageModifier =
        CVarDef.Create("damage.damage_modifier", 1f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> HealModifier =
        CVarDef.Create("damage.heal_modifier", 1f, CVar.SERVER | CVar.REPLICATED);

    /*
     * NPCs
     */

    public static readonly CVarDef<bool> NPCDisableWithoutPlayers =
        CVarDef.Create("npc.disable_without_players", true, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> NPCDisableDistance =
        CVarDef.Create("npc.disable_distance", 20f, CVar.SERVERONLY | CVar.ARCHIVE);

    /*
     * Vote
     */

    public static readonly CVarDef<bool> ShowRestartVotes = CVarDef.Create("vote.show_restart_votes", true);

    public static readonly CVarDef<bool> ShowPresetVotes = CVarDef.Create("vote.show_preset_votes", true);

    public static readonly CVarDef<bool> ShowMapVotes = CVarDef.Create("vote.show_map_votes", true);

    public static readonly CVarDef<bool> RunMapVoteAfterRestart =
        CVarDef.Create("vote.run_map_vote_after_restart", false);

    public static readonly CVarDef<bool> RunPresetVoteAfterRestart =
        CVarDef.Create("vote.run_preset_vote_after_restart", false);

    public static readonly CVarDef<int> VotingsDelay = CVarDef.Create("vote.votings_delay", 60);

    public static readonly CVarDef<bool> VoteMusicDisable =
        CVarDef.Create("vote.music_disable", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> VoteDisableOOC =
        CVarDef.Create("vote.disable_ooc", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> VotePause =
        CVarDef.Create("vote.pause", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> ExcludeMaps = CVarDef.Create("vote.exclude_maps", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> ExcludePresets =
        CVarDef.Create("vote.exclude_presets", true, CVar.SERVERONLY);

    /*
     * Preset
     */

    public static readonly CVarDef<bool> ResetPresetAfterRestart =
        CVarDef.Create("game.reset_preset_after_restart", false);

    public static readonly CVarDef<string> GamePresetPool =
        CVarDef.Create("game.preset_pool", "DefaultHighPopPresetPool", CVar.SERVERONLY);

    /*
     * Ban links.
     */

    public static readonly CVarDef<string> BanDiscordLink =
        CVarDef.Create("cl.discord_link", "", CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<string> BanTelegramLink =
        CVarDef.Create("cl.telegram_link", "", CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /**
     * Jump
     */

    public static readonly CVarDef<bool> JumpEnable =
        CVarDef.Create("jump.enable", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> JumpDeadChance =
        CVarDef.Create("jump.dead_chance", 0.001f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> JumpCooldown =
        CVarDef.Create("jump.cooldown", 0.600f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> JumpSoundDisable =
        CVarDef.Create("jump.sound_disable", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> BunnyHopEnable =
        CVarDef.Create("bunny_hop.enable", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> BunnyHopSpeedUpPerJump =
        CVarDef.Create("bunny_hop.speed_up_per_jump", 0.005f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> BunnyHopSpeedLimit =
        CVarDef.Create("bunny_hop.speed_limit", 2.0f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> BunnyHopMinSpeedThreshold =
        CVarDef.Create("bunny_hop.min_speed_threshold", 4.0f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> BunnyHopSpeedBoostWindow =
        CVarDef.Create("bunny_hop.speed_boost_window", 0.610f, CVar.SERVER | CVar.REPLICATED);

    /**
     * Flip
     */

    public static readonly CVarDef<float> FlipDeadChance =
        CVarDef.Create("flip.dead_chance", 0.001f, CVar.SERVER | CVar.REPLICATED);

    /**
     * Slip
     */

    public static readonly CVarDef<float> SlipDeadChance =
        CVarDef.Create("slip.dead_chance", 0.001f, CVar.SERVER | CVar.REPLICATED);

    /**
     * Fall
     */

    public static readonly CVarDef<float> FallDeadChance =
        CVarDef.Create("fall.dead_chance", 0.01f, CVar.SERVER | CVar.REPLICATED);

    /**
     * VigersRay
     */

    public static readonly CVarDef<bool> VigersRayJoinNotifyEveryone =
        CVarDef.Create("vigers_ray.join_notify_everyone", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> VigersRayJoinSoundEveryone =
        CVarDef.Create("vigers_ray.join_sound_everyone", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> VigersRayJoinShockEveryone =
        CVarDef.Create("vigers_ray.join_shock_everyone", false, CVar.SERVERONLY);

    public static readonly CVarDef<string> VigersRayVictims =
        CVarDef.Create("vigers_ray.victims", "", CVar.SERVERONLY);

    /// <summary>
    ///     Flavor Profile
    /// </summary>
    public static readonly CVarDef<bool> FlavorTextSponsorOnly =
        CVarDef.Create("flavor_text.sponsor_only", true, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> FlavorTextBaseLength =
        CVarDef.Create("flavor_text.length", 512, CVar.SERVER | CVar.REPLICATED);

    /*
     * Damage Overlay
     */

    public static readonly CVarDef<bool> DamageOverlayEnable =
        CVarDef.Create("damage_overlay.enable", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> DamageOverlaySelf =
        CVarDef.Create("damage_overlay.self", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> DamageOverlayStructures =
        CVarDef.Create("damage_overlay.structures", true, CVar.CLIENTONLY | CVar.ARCHIVE);
    /**
     * Transit hub
     */

    /// <summary>
    /// До сколько часов общего наиграного времени игроки будут появляться на станции даже в позднем присоединеии.
    /// </summary>
    public static readonly CVarDef<int> ArrivalsMinHours =
        CVarDef.Create("transithub.arrivals_min_hours", 0, CVar.SERVER | CVar.ARCHIVE);

    public static readonly CVarDef<bool> ArrivalsRoundStartSpawn =
        CVarDef.Create("transithub.arrivals_round_start_spawn", false, CVar.SERVER | CVar.ARCHIVE);

    /*
     * Random items-artifacts
     */

    /// <summary>
    /// Включены ли артефакты-предметы? Переключение этого в моменты игры динамически включает и выключает фичу
    /// </summary>
    public static readonly CVarDef<bool> EnableRandomArtifacts =
        CVarDef.Create("random_artifacts.enable", false, CVar.SERVER | CVar.ARCHIVE);

    /// <summary>
    /// Соотношение артефактов-предметов к обычным предметам.
    /// </summary>
    public static readonly CVarDef<float> ItemToArtifactRatio =
        CVarDef.Create("random_artifacts.ratio", 0.55f, CVar.SERVER | CVar.ARCHIVE);

    /*
     * AntiSpam params
     */
    public static readonly CVarDef<bool> AntiSpamEnable =
        CVarDef.Create("anti_spam.enable", false, CVar.SERVER | CVar.ARCHIVE);
    public static readonly CVarDef<int> AntiSpamCounterShort =
        CVarDef.Create("anti_spam.counter_short", 1, CVar.SERVER | CVar.ARCHIVE);
    public static readonly CVarDef<int> AntiSpamCounterLong =
        CVarDef.Create("anti_spam.counter_long", 2, CVar.SERVER | CVar.ARCHIVE);
    public static readonly CVarDef<float> AntiSpamMuteDuration =
        CVarDef.Create("anti_spam.mute_duration", 10f, CVar.SERVER | CVar.ARCHIVE);
    public static readonly CVarDef<float> AntiSpamTimeShort =
        CVarDef.Create("anti_spam.time_short", 1.5f, CVar.SERVER | CVar.ARCHIVE);
    public static readonly CVarDef<float> AntiSpamTimeLong =
        CVarDef.Create("anti_spam.time_long", 5f, CVar.SERVER | CVar.ARCHIVE);

    /// <summary>
    /// Вроде все очевидно
    /// </summary>
    public static readonly CVarDef<string> IpWhitelist =
        CVarDef.Create("admin.ip_whitelist", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /*
     * Radio chat icons
     */

    public static readonly CVarDef<bool> ChatIconsEnable =
        CVarDef.Create("chat_icon.enable", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * Pointing chat visuals
     */

    public static readonly CVarDef<bool> ChatPointingVisuals =
        CVarDef.Create("chat_icon_pointing.enable", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * Mute new ghost role sound
     */

    public static readonly CVarDef<bool> MuteGhostRoleNotification =
        CVarDef.Create("ghost.mute_role_notification", false, CVar.CLIENTONLY | CVar.ARCHIVE);
    /*
     * Chat sanitization
     */

    /// <summary>
    /// Включена ли санитизация чата (антиспам от набегаторов)
    /// </summary>
    public static readonly CVarDef<bool> ChatSanitizationEnable =
        CVarDef.Create("chatsan.enable", true, CVar.SERVER | CVar.ARCHIVE);

    /// <summary>
    /// Контроллирует поведение санитизации.
    /// Агрессивное: если сообщение не проходит критерии - блокировать полностью его.
    /// Обычное: в сообщении, которое не проходит критерии, удалять не проходящие критерии части.
    /// </summary>
    public static readonly CVarDef<bool> ChatSanitizationAggressive =
        CVarDef.Create("chatsan.aggressive", true, CVar.SERVER | CVar.ARCHIVE);

    public static readonly CVarDef<bool> TracesEnabled =
        CVarDef.Create("opt.traces_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Client toggle for lower-body foot walk bob (shoes / hardsuit boots).
    /// </summary>
    public static readonly CVarDef<bool> FootWalkAnimationEnabled =
        CVarDef.Create("accessibility.foot_walk_animation", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Client toggle for the Stardew-style whole-body walk bounce. Disabled by default because
    /// it lifts the shoes on top of the per-leg foot bob. The foot bob and arm swing remain active.
    /// </summary>
    public static readonly CVarDef<bool> FootWalkBodyBounceEnabled =
        CVarDef.Create("accessibility.foot_walk_body_bounce", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> HoldLookUp =
        CVarDef.Create("scope.hold_look_up", true, CVar.CLIENT | CVar.ARCHIVE);

    /// <summary>
    /// Server toggle for the second station spawned every round (Typan/Aspid via
    /// AdditionalMapLoaderSystem). Loading a second full map costs a lot of round-start time
    /// and RAM, so it defaults to OFF for development builds; the production server config
    /// must set it to true to keep the feature live.
    /// </summary>
    public static readonly CVarDef<bool> AdditionalMapsEnabled =
        CVarDef.Create("ministation.additional_maps", false, CVar.SERVER | CVar.ARCHIVE);

    /*
     * Connecting UI - cached coin balance from last AntagToken sync
     */

    /// <summary>
    /// Last known antag-token / coin balance for the connecting screen.
    /// </summary>
    public static readonly CVarDef<int> CachedCoinBalance =
        CVarDef.Create("ministation.cached_coin_balance", 0, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether <see cref="CachedCoinBalance"/> has been synced at least once this install.
    /// Distinguishes "0 coins" from "never received a balance".
    /// </summary>
    public static readonly CVarDef<bool> CachedCoinBalanceKnown =
        CVarDef.Create("ministation.cached_coin_balance_known", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /*
     * Neuro players - LLM-driven passenger bots
     */

    /// <summary>Master switch for neuro player bots.</summary>
    public static readonly CVarDef<bool> NeuroPlayerEnabled =
        CVarDef.Create("neuroplayer.enabled", false, CVar.SERVER | CVar.ARCHIVE);

    /// <summary>OpenAI-compatible chat completions endpoint.</summary>
    public static readonly CVarDef<string> NeuroPlayerApiUrl =
        CVarDef.Create("neuroplayer.api_url", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>Bearer key for the LLM API.</summary>
    public static readonly CVarDef<string> NeuroPlayerApiKey =
        CVarDef.Create("neuroplayer.api_key", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> NeuroPlayerModel =
        CVarDef.Create("neuroplayer.model", "glm-5.3-flash", CVar.SERVERONLY | CVar.ARCHIVE);

    /*
     * Neuro player LLM providers.
     *
     * neuroplayer.provider picks the preset (custom, deepseek, zai, openai, openrouter).
     * "custom" keeps the legacy behaviour: the endpoint/key/model above are used as-is.
     * Every other provider carries its own endpoint, model and key cvar, so switching
     * providers is a one-line config change and back and forth keys do not clash:
     *
     *   neuroplayer.provider = "deepseek"
     *   neuroplayer.api_key = "sk-..."          # shared bearer key
     *   # или отдельный ключ под провайдера:
     *   neuroplayer.deepseek.api_key = "sk-..."
     *
     * An empty per-provider cvar falls back to the preset default (endpoint/model) or to
     * the shared key. See <see cref="Content.Shared._Mini.NeuroPlayer.NeuroProviderPresets"/>.
     */

    /// <summary>Active LLM provider preset: custom, deepseek, zai, openai or openrouter.</summary>
    public static readonly CVarDef<string> NeuroPlayerProvider =
        CVarDef.Create("neuroplayer.provider", "custom", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Provider used when the active one fails (5xx, timeout, bad key). Empty disables the retry;
    /// the fallback must be a different provider, e.g. "deepseek".
    /// </summary>
    public static readonly CVarDef<string> NeuroPlayerFallbackProvider =
        CVarDef.Create("neuroplayer.fallback_provider", "", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Global cap on LLM requests per minute across all bots (sliding window). 0 disables the cap.
    /// Per-bot cooldowns alone allowed roughly 45 requests per minute during name-spam.
    /// </summary>
    public static readonly CVarDef<int> NeuroPlayerRequestsPerMinute =
        CVarDef.Create("neuroplayer.requests_per_minute", 40, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>DeepSeek chat completions endpoint.</summary>
    public static readonly CVarDef<string> NeuroPlayerDeepSeekApiUrl =
        CVarDef.Create("neuroplayer.deepseek.api_url", "https://api.deepseek.com/chat/completions",
            CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>DeepSeek bearer key; empty falls back to <see cref="NeuroPlayerApiKey"/>.</summary>
    public static readonly CVarDef<string> NeuroPlayerDeepSeekApiKey =
        CVarDef.Create("neuroplayer.deepseek.api_key", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    /// DeepSeek model. "deepseek-chat" is the non-reasoning V3 chat model - the right pick for
    /// one-line bot replies. "deepseek-reasoner" also works but spends the whole max_tokens
    /// budget on hidden thinking, so raise <see cref="NeuroPlayerMaxTokens"/> for it.
    /// </summary>
    public static readonly CVarDef<string> NeuroPlayerDeepSeekModel =
        CVarDef.Create("neuroplayer.deepseek.model", "deepseek-chat", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Z.ai (GLM coding plan) chat completions endpoint.</summary>
    public static readonly CVarDef<string> NeuroPlayerZaiApiUrl =
        CVarDef.Create("neuroplayer.zai.api_url", "https://api.z.ai/api/paas/v4/chat/completions",
            CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> NeuroPlayerZaiApiKey =
        CVarDef.Create("neuroplayer.zai.api_key", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> NeuroPlayerZaiModel =
        CVarDef.Create("neuroplayer.zai.model", "glm-5.3-flash", CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> NeuroPlayerOpenAiApiUrl =
        CVarDef.Create("neuroplayer.openai.api_url", "https://api.openai.com/v1/chat/completions",
            CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> NeuroPlayerOpenAiApiKey =
        CVarDef.Create("neuroplayer.openai.api_key", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> NeuroPlayerOpenAiModel =
        CVarDef.Create("neuroplayer.openai.model", "gpt-4o-mini", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>OpenRouter endpoint - handy to reach DeepSeek models through one key.</summary>
    public static readonly CVarDef<string> NeuroPlayerOpenRouterApiUrl =
        CVarDef.Create("neuroplayer.openrouter.api_url", "https://openrouter.ai/api/v1/chat/completions",
            CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> NeuroPlayerOpenRouterApiKey =
        CVarDef.Create("neuroplayer.openrouter.api_key", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> NeuroPlayerOpenRouterModel =
        CVarDef.Create("neuroplayer.openrouter.model", "deepseek/deepseek-chat", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Total number of neuro players. The strict core composition (4 NT bots + CentComm maid +
    /// Typan chef = 6) is always spawned; values above it add random NT passengers from spare
    /// personas, values at or below it keep the mandated composition. 0 disables the bots.
    /// </summary>
    public static readonly CVarDef<int> NeuroPlayerBotCount =
        CVarDef.Create("neuroplayer.bot_count", 3, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Local speech hearing radius for bots (tiles).</summary>
    public static readonly CVarDef<float> NeuroPlayerHearRadius =
        CVarDef.Create("neuroplayer.hear_radius", 10f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> NeuroPlayerMaxContext =
        CVarDef.Create("neuroplayer.max_context_messages", 8, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Per-bot cooldown between LLM answers.</summary>
    public static readonly CVarDef<int> NeuroPlayerCooldownSeconds =
        CVarDef.Create("neuroplayer.response_cooldown_seconds", 8, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> NeuroPlayerMaxTokens =
        CVarDef.Create("neuroplayer.max_tokens", 512, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> NeuroPlayerTemperature =
        CVarDef.Create("neuroplayer.temperature", 0.9f, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> NeuroPlayerTimeoutSeconds =
        CVarDef.Create("neuroplayer.request_timeout_seconds", 10, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Chance to join face-to-face speech (<= 3 tiles) without being named.</summary>
    public static readonly CVarDef<float> NeuroPlayerDirectChance =
        CVarDef.Create("neuroplayer.direct_chance", 0.5f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Chance to join background conversation nearby without being named.</summary>
    public static readonly CVarDef<float> NeuroPlayerProactiveChance =
        CVarDef.Create("neuroplayer.proactive_chance", 0.15f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Cooldown between unprompted replies of one bot (anti-spam).</summary>
    public static readonly CVarDef<int> NeuroPlayerProactiveCooldownSeconds =
        CVarDef.Create("neuroplayer.proactive_cooldown_seconds", 45, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Interval between random point-of-interest visits per bot (seconds).</summary>
    public static readonly CVarDef<int> NeuroPlayerPoiIntervalSeconds =
        CVarDef.Create("neuroplayer.poi_interval_seconds", 240, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>If no player is within this radius (tiles), the bot walks toward one.</summary>
    public static readonly CVarDef<float> NeuroPlayerSeekRadius =
        CVarDef.Create("neuroplayer.seek_radius", 30f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Cooldown between LLM small-talk exchanges between bots.</summary>
    public static readonly CVarDef<int> NeuroPlayerBotChatterCooldownSeconds =
        CVarDef.Create("neuroplayer.bot_chatter_cooldown_seconds", 240, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Chance that a bot answers another bot's line when the cooldown allows it. 0 disables
    /// bot-to-bot small talk entirely; the exchange is always one line, never a chain.
    /// </summary>
    public static readonly CVarDef<float> NeuroPlayerBotChatterChance =
        CVarDef.Create("neuroplayer.bot_chatter_chance", 0.2f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Comma separated words that must never appear in a bot's reply (morphology is tolerated:
    /// "мат" also blocks "матом", but not "материал"). A reply containing one is dropped and logged.
    /// </summary>
    public static readonly CVarDef<string> NeuroPlayerBlockedWords =
        CVarDef.Create("neuroplayer.blocked_words", "", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Drop replies where the bot admits being an AI ("я бот", "языковая модель", "я не человек").
    /// The system prompt forbids it, but models still slip now and then.
    /// </summary>
    public static readonly CVarDef<bool> NeuroPlayerBlockAiTalk =
        CVarDef.Create("neuroplayer.block_ai_talk", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// GLM-4.5/4.6 style reasoning mode. Off by default: with a small max_tokens budget
    /// thinking consumes the whole response and the bot stays silent.
    /// Sent only when the model name contains "glm".
    /// </summary>
    public static readonly CVarDef<bool> NeuroPlayerThinkingEnabled =
        CVarDef.Create("neuroplayer.thinking_enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /*
     * Neuro ahelp assistant - LLM first-line responder in admin help while no admins are online.
     * Uses the same LLM provider as the neuro players (neuroplayer.provider).
     */

    /// <summary>Master switch for the ahelp assistant.</summary>
    public static readonly CVarDef<bool> NeuroAHelpEnabled =
        CVarDef.Create("neuroahelp.enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Name the assistant introduces itself with; also used as the message prefix.</summary>
    public static readonly CVarDef<string> NeuroAHelpName =
        CVarDef.Create("neuroahelp.name", "Нейропомощник", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Grace period before answering a ticket: a live admin gets the first shot at it, and the
    /// assistant only steps in if nobody picked the ticket up.
    /// </summary>
    public static readonly CVarDef<int> NeuroAHelpDelaySeconds =
        CVarDef.Create("neuroahelp.delay_seconds", 8, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>Per-player cooldown between assistant answers (anti-spam and anti-cost).</summary>
    public static readonly CVarDef<int> NeuroAHelpCooldownSeconds =
        CVarDef.Create("neuroahelp.cooldown_seconds", 120, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>How many recent ahelp lines are sent to the model as context.</summary>
    public static readonly CVarDef<int> NeuroAHelpMaxContext =
        CVarDef.Create("neuroahelp.max_context_messages", 8, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> NeuroAHelpMaxTokens =
        CVarDef.Create("neuroahelp.max_tokens", 120, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Hard cap for an assistant answer in characters. The model is asked for 1-2 short sentences,
    /// and anything longer is cut at a sentence (or word) boundary.
    /// </summary>
    public static readonly CVarDef<int> NeuroAHelpMaxReplyChars =
        CVarDef.Create("neuroahelp.max_reply_chars", 260, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Text/markdown file with facts about this build (mechanics, modes, jobs, controls). Relative
    /// paths resolve against the server working directory. Included into the assistant's prompt.
    /// </summary>
    public static readonly CVarDef<string> NeuroAHelpKnowledgeFile =
        CVarDef.Create("neuroahelp.knowledge_file", "", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>How much of the knowledge file is sent to the model (characters).</summary>
    public static readonly CVarDef<int> NeuroAHelpKnowledgeMaxChars =
        CVarDef.Create("neuroahelp.knowledge_max_chars", 6000, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Separate request budget, so the IC bots cannot starve the support channel
    /// (0 = unlimited, like neuroplayer.requests_per_minute).
    /// </summary>
    public static readonly CVarDef<int> NeuroAHelpRequestsPerMinute =
        CVarDef.Create("neuroahelp.requests_per_minute", 20, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Free-form extra instructions for the assistant (server rules summary, ссылки, тон общения).
    /// Appended to the built-in system prompt.
    /// </summary>
    public static readonly CVarDef<string> NeuroAHelpExtraPrompt =
        CVarDef.Create("neuroahelp.extra_prompt", "", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Награды «за время»: ежедневная награда со стриком, билеты за наигранное время и монеты
    ///     за онлайн. Включены по умолчанию; cvar остаётся аварийным выключателем на случай, если
    ///     прогрессию нужно временно приостановить. Еженедельные квесты и достижения имеют свою
    ///     награду и от этого флага не зависят.
    /// </summary>
    public static readonly CVarDef<bool> TimeRewardsEnabled =
        CVarDef.Create("mini.time_rewards_enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);
}
