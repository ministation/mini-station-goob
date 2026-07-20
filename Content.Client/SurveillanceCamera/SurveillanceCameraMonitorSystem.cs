// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client.SurveillanceCamera;

public sealed class SurveillanceCameraMonitorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        UpdatesOutsidePrediction = true;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ActiveSurveillanceCameraMonitorVisualsComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            comp.TimeLeft -= frameTime;

            if (comp.TimeLeft <= 0)
            {
                comp.OnFinish?.Invoke();

                RemCompDeferred<ActiveSurveillanceCameraMonitorVisualsComponent>(uid);
            }
        }
    }

    public void AddTimer(EntityUid uid, Action onFinish)
    {
        var comp = EnsureComp<ActiveSurveillanceCameraMonitorVisualsComponent>(uid);
        comp.OnFinish = onFinish;
        comp.TimeLeft = 1f;
    }

    public void RemoveTimer(EntityUid uid)
    {
        RemCompDeferred<ActiveSurveillanceCameraMonitorVisualsComponent>(uid);
    }
}
