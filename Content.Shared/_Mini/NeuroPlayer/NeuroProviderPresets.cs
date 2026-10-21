// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Configuration;
// Content.Shared._Mini.MiniCCVars is both a namespace and the CVar class inside it, so alias the type.
using NeuroCVars = Content.Shared._Mini.MiniCCVars.MiniCCVars;

namespace Content.Shared._Mini.NeuroPlayer;

/// <summary>
/// An LLM provider preset for the neuro players: default OpenAI-compatible endpoint, default model
/// and the cvars an admin can use to override them per provider.
/// </summary>
/// <param name="Id">Value of <c>neuroplayer.provider</c> that selects this preset.</param>
/// <param name="DefaultEndpoint">Chat completions URL used when the endpoint cvar is empty.</param>
/// <param name="DefaultModel">Model used when the model cvar is empty.</param>
/// <param name="EndpointCvar">Per-provider endpoint override cvar (null for <c>custom</c>).</param>
/// <param name="KeyCvar">Per-provider bearer key cvar (null for <c>custom</c>).</param>
/// <param name="ModelCvar">Per-provider model override cvar (null for <c>custom</c>).</param>
/// <param name="SupportsThinking">Whether the API accepts the GLM-style <c>thinking</c> request field.</param>
public sealed record NeuroProviderPreset(
    string Id,
    string DefaultEndpoint,
    string DefaultModel,
    CVarDef<string>? EndpointCvar,
    CVarDef<string>? KeyCvar,
    CVarDef<string>? ModelCvar,
    bool SupportsThinking);

/// <summary>
/// Ready-made LLM providers for the neuro players. Adding a provider is one record here plus its
/// cvars in <c>MiniCCVars</c>; nothing else in the bot pipeline is provider-specific.
/// </summary>
public static class NeuroProviderPresets
{
    /// <summary>Legacy provider: the endpoint, key and model cvars are used verbatim.</summary>
    public const string CustomId = "custom";

    public static readonly NeuroProviderPreset Custom = new(
        CustomId,
        string.Empty,
        string.Empty,
        EndpointCvar: null,
        KeyCvar: null,
        ModelCvar: null,
        SupportsThinking: false);

    /// <summary>
    /// DeepSeek (https://api.deepseek.com). OpenAI-compatible; <c>deepseek-chat</c> is the cheap
    /// non-reasoning V3 model, which is what short bot replies want.
    /// </summary>
    public static readonly NeuroProviderPreset DeepSeek = new(
        "deepseek",
        "https://api.deepseek.com/chat/completions",
        "deepseek-chat",
        NeuroCVars.NeuroPlayerDeepSeekApiUrl,
        NeuroCVars.NeuroPlayerDeepSeekApiKey,
        NeuroCVars.NeuroPlayerDeepSeekModel,
        SupportsThinking: false);

    /// <summary>Z.ai GLM coding plan - the fork's original provider, supports the <c>thinking</c> toggle.</summary>
    public static readonly NeuroProviderPreset Zai = new(
        "zai",
        "https://api.z.ai/api/paas/v4/chat/completions",
        "glm-5.3-flash",
        NeuroCVars.NeuroPlayerZaiApiUrl,
        NeuroCVars.NeuroPlayerZaiApiKey,
        NeuroCVars.NeuroPlayerZaiModel,
        SupportsThinking: true);

    public static readonly NeuroProviderPreset OpenAi = new(
        "openai",
        "https://api.openai.com/v1/chat/completions",
        "gpt-4o-mini",
        NeuroCVars.NeuroPlayerOpenAiApiUrl,
        NeuroCVars.NeuroPlayerOpenAiApiKey,
        NeuroCVars.NeuroPlayerOpenAiModel,
        SupportsThinking: false);

    /// <summary>OpenRouter - one key for many models, including <c>deepseek/deepseek-chat</c>.</summary>
    public static readonly NeuroProviderPreset OpenRouter = new(
        "openrouter",
        "https://openrouter.ai/api/v1/chat/completions",
        "deepseek/deepseek-chat",
        NeuroCVars.NeuroPlayerOpenRouterApiUrl,
        NeuroCVars.NeuroPlayerOpenRouterApiKey,
        NeuroCVars.NeuroPlayerOpenRouterModel,
        SupportsThinking: false);

    public static readonly IReadOnlyList<NeuroProviderPreset> All =
        [Custom, DeepSeek, Zai, OpenAi, OpenRouter];

    /// <summary>Comma separated provider ids for logs and the <c>neurobots</c> command.</summary>
    public static string AvailableIds => string.Join(", ", All.Select(p => p.Id));

    public static NeuroProviderPreset Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return Custom;

        return All.FirstOrDefault(p => p.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Custom;
    }

    public static bool IsKnown(string? id)
    {
        return !string.IsNullOrWhiteSpace(id) &&
               All.Any(p => p.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Resolves the provider into the concrete endpoint, key and model an HTTP request needs.
    /// Precedence: per-provider cvar override, then provider default, then the legacy
    /// <c>neuroplayer.api_url</c> / <c>neuroplayer.api_key</c> / <c>neuroplayer.model</c> values.
    /// </summary>
    public static NeuroProviderConfig Resolve(
        IConfigurationManager cfg,
        string providerId,
        string? legacyApiUrl,
        string? legacyApiKey,
        string? legacyModel)
    {
        var preset = Find(providerId);

        var endpoint = (Override(cfg, preset.EndpointCvar) ?? preset.DefaultEndpoint).Trim();
        if (endpoint.Length == 0)
            endpoint = (legacyApiUrl ?? string.Empty).Trim();

        var key = (Override(cfg, preset.KeyCvar) ?? legacyApiKey ?? string.Empty).Trim();

        var model = (Override(cfg, preset.ModelCvar) ?? preset.DefaultModel).Trim();
        if (model.Length == 0)
            model = (legacyModel ?? string.Empty).Trim();

        // GLM models understand the thinking flag; other providers reject the field outright.
        var thinking = preset.SupportsThinking ||
                       model.Contains("glm", StringComparison.OrdinalIgnoreCase);

        return new NeuroProviderConfig(preset.Id, endpoint, key, model, thinking);
    }

    private static string? Override(IConfigurationManager cfg, CVarDef<string>? def)
    {
        if (def == null)
            return null;

        var value = cfg.GetCVar(def);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

/// <summary>Concrete provider settings for one LLM request.</summary>
public readonly record struct NeuroProviderConfig(
    string Provider,
    string Endpoint,
    string ApiKey,
    string Model,
    bool SupportsThinking)
{
    /// <summary>True when the endpoint and the key are both present.</summary>
    public bool IsConfigured => !string.IsNullOrEmpty(Endpoint) && !string.IsNullOrEmpty(ApiKey);
}
