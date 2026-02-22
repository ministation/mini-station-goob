using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Mini.Helpers;

public sealed class ChatIconsHelpersSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public const string JobIconsRsiPath = "/Textures/Interface/Misc/job_icons.rsi";
    public const string NoIdIconState = "NoId";

    /// <summary>
    /// Собирает и возвращает иконку для переданной работы
    /// </summary>
    [PublicAPI]
    public string GetJobIcon(ProtoId<JobPrototype>? job, int scale = 1)
    {
        if (!_prototype.TryIndex(job, out var jobPrototype))
        {
            return Loc.GetString("texture-tag-rsi",
                ("path", JobIconsRsiPath),
                ("state", NoIdIconState),
                ("scale", scale)
            );
        }

        var icon = _prototype.Index(jobPrototype.Icon);

        return icon.Icon switch
        {
            SpriteSpecifier.Texture tex => Loc.GetString("texture-tag",
                ("path", tex.TexturePath.CanonPath),
                ("scale", scale)
            ),
            SpriteSpecifier.Rsi rsi => Loc.GetString("texture-tag-rsi",
                ("path", rsi.RsiPath.CanonPath),
                ("state", rsi.RsiState),
                ("scale", scale)
            ),
            _ => Loc.GetString("texture-tag-rsi",
                ("path", JobIconsRsiPath),
                ("state", NoIdIconState),
                ("scale", scale)
            ),
        };
    }
}
