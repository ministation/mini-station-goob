// SPDX-License-Identifier: MPL-2.0

using Robust.Server;

namespace Content.Goobstation.Server;

internal static class Program
{
    public static void Main(string[] args)
    {
#if MINI_RELEASE_BUILD
        // Проверка активации релизной сборки до старта сервера (контракт v2, раздел 2).
        args = Activation.ActivationGate.Prepare(args);
#endif
        ContentStart.Start(args);
    }
}
