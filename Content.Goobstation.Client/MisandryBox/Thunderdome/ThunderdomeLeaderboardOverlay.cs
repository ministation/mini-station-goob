using System.Numerics;
using Content.Goobstation.Shared.MisandryBox.Thunderdome;
using Content.Shared.Examine;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using static Robust.Shared.Maths.Color;

namespace Content.Goobstation.Client.MisandryBox.Thunderdome;

/// <summary>
/// Overlay that renders Thunderdome leaderboards in screen space with FOV support and background.
/// </summary>
public sealed partial class ThunderdomeLeaderboardOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IPlayerManager _playerMgr = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private readonly TransformSystem _transform;
    private readonly ExamineSystemShared _examine;

    private const string DefaultFontPrototype = "Default";

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    internal ThunderdomeLeaderboardOverlay()
    {
        IoCManager.InjectDependencies(this);

        _transform = _entity.System<TransformSystem>();
        _examine = _entity.System<ExamineSystemShared>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();

        var ourEntity = _playerMgr.LocalEntity;
        if (ourEntity == null)
            return; // Нет локального игрока — нечего рисовать

        var viewPos = _transform.GetMapCoordinates(ourEntity.Value);

        var query = _entity.AllEntityQueryEnumerator<ThunderdomeLeaderboardComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var leaderboard, out var xform))
        {
            var mapPos = _transform.GetMapCoordinates(uid, xform);

            if (mapPos.MapId != args.MapId)
                continue;

            // FOV check
            var distance = (mapPos.Position - viewPos.Position).Length();
            if (!args.WorldBounds.Contains(mapPos.Position) ||
                !_examine.InRangeUnOccluded(viewPos, mapPos, distance, e => e == uid || e == ourEntity, entMan: _entity))
                continue;

            // Get font - use Default font prototype
            if (!_prototypeManager.TryIndex<FontPrototype>(DefaultFontPrototype, out var fontProto))
                continue;

            var fontResource = _resourceCache.GetResource<FontResource>(fontProto.Path);
            var font = new VectorFont(fontResource, leaderboard.FontSize);

            // Build text lines
            var lines = new List<string>();
            lines.Add("════════════════════════════════════════");
            lines.Add($"     {leaderboard.Title}");
            lines.Add("════════════════════════════════════════");
            lines.Add("");

            if (leaderboard.Entries.Count == 0)
            {
                lines.Add("        No players yet...");
                lines.Add("");
            }
            else
            {
                foreach (var entry in leaderboard.Entries)
                {
                    var nameStr = entry.Name.Length > 20 ? entry.Name[..20] : entry.Name;
                    var prefix = entry.Rank switch
                    {
                        1 => "[1st]",
                        2 => "[2nd]",
                        3 => "[3rd]",
                        _ => $"[{entry.Rank}th]"
                    };

                    lines.Add($" {prefix} {nameStr}");
                    lines.Add($"      K:{entry.Kills} D:{entry.Deaths} KD:{entry.KD:F2} Best:{entry.BestStreak}");

                    if (entry.Rank < leaderboard.Entries.Count)
                        lines.Add("  ────────────────────────────────────");
                }
            }

            lines.Add("");
            lines.Add("════════════════════════════════════════");

            // Calculate dimensions
            var maxWidth = 0f;
            var totalHeight = 0f;
            foreach (var line in lines)
            {
                var dimensions = handle.GetDimensions(font, line, 1f);
                if (dimensions.X > maxWidth)
                    maxWidth = dimensions.X;
                totalHeight += dimensions.Y;
            }

            // Transform world position to screen position
            var worldPos = mapPos.Position + leaderboard.Offset;
            var screenPos = Vector2.Transform(worldPos, matrix);

            // Draw background
            var padding = 5f;
            var bgBox = new UIBox2(
                screenPos.X - maxWidth / 2 - padding,
                screenPos.Y - totalHeight / 2 - padding,
                screenPos.X + maxWidth / 2 + padding,
                screenPos.Y + totalHeight / 2 + padding
            );
            handle.DrawRect(bgBox, Black.WithAlpha(192));

            // Draw text lines
            var yPos = screenPos.Y - totalHeight / 2;
            foreach (var line in lines)
            {
                var lineDimensions = handle.GetDimensions(font, line, 1f);
                var xPos = screenPos.X - lineDimensions.X / 2;
                handle.DrawString(font, new Vector2(xPos, yPos), line, leaderboard.Color);
                yPos += lineDimensions.Y;
            }
        }
    }
}
