// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

// SPDX-FileCopyrightText: 2026 Egorik1
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.Numerics;
using Content.Shared._Mini.NeuroAHelp;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._Mini.NeuroAHelp;

/// <summary>
/// Кольцо-индикатор нейропомощника в окне ахелпа: крутится, пока агент разбирает тикет,
/// горит зеленым после ответа, тускнеет, когда тикет у живого админа. Контекст работы
/// («читаю логи раунда») рисуется рядом отдельной подписью, кольцо показывает только состояние.
/// </summary>
public sealed class NeuroRingControl : Control
{
    private const float DisplaySize = 14f;
    private const float SpinSpeed = 260f; // градусов в секунду

    private static readonly Color TrackColor = Color.FromHex("#33334a");
    private static readonly Color WorkingColor = Color.FromHex("#8ab4f8");
    private static readonly Color DoneColor = Color.FromHex("#41f097");
    private static readonly Color FailedColor = Color.FromHex("#f0a041");
    private static readonly Color SilentColor = Color.FromHex("#5a5a78");
    private static readonly Color MutedColor = Color.FromHex("#b05555");

    private const float ArcSweep = 150f;
    private const int ArcSegments = 16;

    private float _angle;
    private NeuroAHelpState _state = NeuroAHelpState.Idle;

    public NeuroRingControl()
    {
        MinSize = new Vector2(DisplaySize, DisplaySize);
        MaxSize = new Vector2(DisplaySize, DisplaySize);
        VerticalAlignment = VAlignment.Center;
        MouseFilter = MouseFilterMode.Ignore;
    }

    public void SetState(NeuroAHelpState state)
    {
        _state = state;
        if (state != NeuroAHelpState.Working)
            _angle = 0f;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_state != NeuroAHelpState.Working)
            return;

        _angle = (_angle + SpinSpeed * args.DeltaSeconds) % 360f;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var center = PixelSizeBox.Center;
        var radius = MathF.Min(center.X, center.Y) - 1.5f;
        if (radius <= 2f)
            return;

        // Фоновое кольцо-трек под всеми состояниями.
        DrawCircleArc(handle, center, radius, 0f, 360f, TrackColor);

        switch (_state)
        {
            case NeuroAHelpState.Working:
                DrawCircleArc(handle, center, radius - 1f, _angle, ArcSweep, WorkingColor);
                DrawCircleArc(handle, center, radius, _angle, ArcSweep, WorkingColor);
                DrawCircleArc(handle, center, radius + 1f, _angle, ArcSweep, WorkingColor);
                break;
            case NeuroAHelpState.Done:
                DrawThickCircle(handle, center, radius, DoneColor);
                break;
            case NeuroAHelpState.Failed:
                DrawThickCircle(handle, center, radius, FailedColor);
                break;
            case NeuroAHelpState.Silent:
                DrawThickCircle(handle, center, radius, SilentColor);
                break;
            case NeuroAHelpState.Muted:
                DrawThickCircle(handle, center, radius, MutedColor);
                break;
        }
    }

    /// <summary>Полное кольцо в три прохода: на маленьком размере получается линия ~2-3 px.</summary>
    private static void DrawThickCircle(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
    {
        DrawCircleArc(handle, center, radius - 1f, 0f, 360f, color);
        DrawCircleArc(handle, center, radius, 0f, 360f, color);
        DrawCircleArc(handle, center, radius + 1f, 0f, 360f, color);
    }

    /// <summary>Дуга ломаной линией: от startDegrees по часовой на sweepDegrees.</summary>
    private static void DrawCircleArc(
        DrawingHandleScreen handle, Vector2 center, float radius, float startDegrees, float sweepDegrees, Color color)
    {
        var segments = Math.Max(2, (int) MathF.Ceiling(ArcSegments * sweepDegrees / 360f));
        var startRadians = MathF.PI / 180f * startDegrees;
        var sweepRadians = MathF.PI / 180f * sweepDegrees;

        var previous = new Vector2(
            center.X + radius * MathF.Sin(startRadians),
            center.Y - radius * MathF.Cos(startRadians));

        for (var i = 1; i <= segments; i++)
        {
            var t = startRadians + sweepRadians * i / segments;
            var point = new Vector2(center.X + radius * MathF.Sin(t), center.Y - radius * MathF.Cos(t));
            handle.DrawLine(previous, point, color);
            previous = point;
        }
    }
}
