// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using System.IO;
using System.Text;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Mini.Diagnostics;

/// <summary>
/// Mini: замер фризов. Клиент своих логов в файл не пишет (только консоль), поэтому каждый кадр
/// дольше <see cref="HitchSeconds"/> и каждая смена подконтрольной сущности дописываются в
/// <c>mini-hitches.log</c> в каталоге данных клиента. По нему видно, совпал ли фриз со сменой
/// сущности (вселение призраком/агхостом), какой был глазок и масштаб рендера и сколько кадр занял.
/// </summary>
public sealed partial class FrameHitchLoggerSystem : EntitySystem
{
    private const float HitchSeconds = 0.04f;
    private const int MaxLines = 400;

    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceManager _res = default!;

    private readonly Queue<string> _lines = new();
    private readonly ResPath _path = new("/mini-hitches.log");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnDetached);
    }

    private void OnAttached(LocalPlayerAttachedEvent args)
    {
        var uid = _player.LocalEntity;
        Note($"controlled entity -> {uid} ({(uid != null ? MetaData(uid.Value).EntityPrototype?.ID : "?") ?? "?"})");
    }

    private void OnDetached(LocalPlayerDetachedEvent args)
    {
        Note($"controlled entity detached");
    }

    public override void FrameUpdate(float frameTime)
    {
        if (frameTime < HitchSeconds)
            return;

        var eye = _eye.CurrentEye;
        Note($"hitch {frameTime * 1000f:F0} ms | tick {_timing.CurTick} | entity {_player.LocalEntity} " +
             $"| eye zoom {eye.Zoom} | eye map {eye.Position.MapId}");
    }

    private void Note(string text)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} {text}";
        _lines.Enqueue(line);

        while (_lines.Count > MaxLines)
            _lines.Dequeue();

        Log.Warning($"[hitch] {line}");

        try
        {
            using var stream = _res.UserData.Open(_path, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            foreach (var entry in _lines)
                writer.WriteLine(entry);
        }
        catch (Exception e)
        {
            Log.Warning($"[hitch] cannot write {_path}: {e.Message}");
        }
    }
}
