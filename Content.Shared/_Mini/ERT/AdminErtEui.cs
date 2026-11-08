// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.ERT;

[Serializable, NetSerializable]
public sealed class AdminErtEuiState : EuiStateBase
{
    // No state for now; window queries system itself on open.
}

public static class AdminErtEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class Close : EuiMessageBase
    {
    }
}
