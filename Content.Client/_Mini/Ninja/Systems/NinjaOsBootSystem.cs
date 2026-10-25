// SPDX-FileCopyrightText: 2026 Mini Station
// Мини-станция, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/ministation/mini-station-goob/master/LICENSE.TXT

using Content.Shared._Mini.Ninja.Prototypes;

namespace Content.Client._Mini.Ninja.Systems;

/// <summary>
/// Plays the Spider OS boot log on the client. The sequence is cosmetic: the server
/// runs every boot check itself and reports the result with a separate message.
/// </summary>
public sealed class NinjaOsBootSystem : EntitySystem
{
    /// <summary>How long the client waits for the server result before aborting the boot.</summary>
    private const float ResultTimeoutSeconds = 20f;

    /// <summary>How long the rollback line is shown before returning to the normal window.</summary>
    private const float RollbackSeconds = 1.5f;

    public sealed class BootCallbacks
    {
        /// <summary>Called with a localization key and whether the line is an error.</summary>
        public Action<string, bool>? OnLog;

        public Action<float>? OnProgress;

        /// <summary>Called when the server rejected the request, with the fail reason key.</summary>
        public Action<string>? OnFailed;

        public Action? OnFinished;
    }

    private sealed class BootSession
    {
        public EntityUid Suit;
        public NinjaOsBootPrototype Script = default!;
        public BootCallbacks Callbacks = default!;
        public int StepIndex;
        public float Remaining;
        public float ResultElapsed;
        public float RollbackRemaining;
        public bool WaitingResult = true;
        public bool AnimationDone;
    }

    private readonly List<BootSession> _sessions = new();

    public void StartBoot(EntityUid suit, NinjaOsBootPrototype script, BootCallbacks callbacks)
    {
        StopBoot(suit);

        if (script.Steps.Count == 0)
        {
            callbacks.OnProgress?.Invoke(1f);
            callbacks.OnFinished?.Invoke();
            return;
        }

        var session = new BootSession
        {
            Suit = suit,
            Script = script,
            Callbacks = callbacks,
            Remaining = Math.Max(script.Steps[0].Delay, 0f),
        };

        _sessions.Add(session);

        callbacks.OnLog?.Invoke(script.Steps[0].Log, false);
        ReportProgress(session);
    }

    public void StopBoot(EntityUid suit)
    {
        for (var i = _sessions.Count - 1; i >= 0; i--)
        {
            if (_sessions[i].Suit == suit)
                _sessions.RemoveAt(i);
        }
    }

    /// <summary>
    /// Feed the server result into the running boot session.
    /// Returns false when this suit has no running session.
    /// </summary>
    public bool ConfirmResult(EntityUid suit, bool success, string failReason)
    {
        for (var i = 0; i < _sessions.Count; i++)
        {
            var session = _sessions[i];
            if (session.Suit != suit)
                continue;

            if (success)
            {
                session.WaitingResult = false;

                if (session.AnimationDone)
                {
                    _sessions.RemoveAt(i);
                    Finish(session);
                }
            }
            else
            {
                Fail(session, string.IsNullOrEmpty(failReason) ? "ninja-os-boot-fail-auth" : failReason);
            }

            return true;
        }

        return false;
    }

    public override void Update(float frameTime)
    {
        for (var i = _sessions.Count - 1; i >= 0; i--)
        {
            var session = _sessions[i];

            if (TerminatingOrDeleted(session.Suit))
            {
                _sessions.RemoveAt(i);
                continue;
            }

            if (session.RollbackRemaining > 0f)
            {
                session.RollbackRemaining -= frameTime;

                if (session.RollbackRemaining <= 0f)
                {
                    _sessions.RemoveAt(i);
                    Finish(session);
                }

                continue;
            }

            if (session.WaitingResult)
            {
                session.ResultElapsed += frameTime;

                if (session.ResultElapsed >= ResultTimeoutSeconds)
                {
                    Fail(session, "ninja-os-boot-fail-timeout");
                    continue;
                }
            }

            if (session.AnimationDone)
            {
                if (!session.WaitingResult)
                {
                    _sessions.RemoveAt(i);
                    Finish(session);
                }

                continue;
            }

            session.Remaining -= frameTime;

            if (session.Remaining > 0f)
            {
                ReportProgress(session);
                continue;
            }

            session.StepIndex++;

            if (session.StepIndex >= session.Script.Steps.Count)
            {
                session.AnimationDone = true;
                session.Callbacks.OnProgress?.Invoke(1f);

                if (!session.WaitingResult)
                {
                    _sessions.RemoveAt(i);
                    Finish(session);
                }

                continue;
            }

            var step = session.Script.Steps[session.StepIndex];
            session.Remaining = Math.Max(step.Delay, 0f);
            session.Callbacks.OnLog?.Invoke(step.Log, false);
            ReportProgress(session);
        }
    }

    private static void ReportProgress(BootSession session)
    {
        var count = session.Script.Steps.Count;

        if (count == 0)
        {
            session.Callbacks.OnProgress?.Invoke(1f);
            return;
        }

        var index = Math.Clamp(session.StepIndex, 0, count - 1);
        var delay = session.Script.Steps[index].Delay;
        var partial = delay > 0f ? Math.Clamp(1f - session.Remaining / delay, 0f, 1f) : 1f;
        session.Callbacks.OnProgress?.Invoke(Math.Clamp((index + partial) / count, 0f, 1f));
    }

    private static void Fail(BootSession session, string failReason)
    {
        session.WaitingResult = false;
        session.AnimationDone = true;
        session.Callbacks.OnFailed?.Invoke(failReason);
        session.Callbacks.OnLog?.Invoke(failReason, true);
        session.Callbacks.OnLog?.Invoke("ninja-os-boot-rollback", false);
        session.RollbackRemaining = RollbackSeconds;
    }

    private static void Finish(BootSession session)
    {
        session.Callbacks.OnProgress?.Invoke(1f);
        session.Callbacks.OnFinished?.Invoke();
    }
}
