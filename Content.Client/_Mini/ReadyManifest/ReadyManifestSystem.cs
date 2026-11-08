// SPDX-License-Identifier: LicenseRef-MiniStation-1.0

using Content.Shared._Mini.ReadyManifest;

namespace Content.Client._Mini.ReadyManifest;

public sealed class ReadyManifestSystem : EntitySystem
{
    public void RequestReadyManifest()
    {
        RaiseNetworkEvent(new RequestReadyManifestMessage());
    }
}
