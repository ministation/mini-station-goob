// SPDX-FileCopyrightText: 2021 20kdc <asdd2808@gmail.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration
{
    public abstract class SharedBwoinkSystem : EntitySystem
    {
        public static NetUserId SystemUserId { get; private set; } = new NetUserId(Guid.Empty);

        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<BwoinkTextMessage>(OnBwoinkTextMessage);
        }

        protected virtual void OnBwoinkTextMessage(BwoinkTextMessage message, EntitySessionEventArgs eventArgs) { }

        protected void LogBwoink(BwoinkTextMessage message) { }

        [Serializable, NetSerializable]
        public sealed class BwoinkTextMessage : EntityEventArgs
        {
            public DateTime SentAt { get; }
            public NetUserId UserId { get; }
            public NetUserId TrueSender { get; }
            public string Text { get; }
            public bool PlaySound { get; }
            public readonly bool AdminOnly;
            public string? IconPath { get; }  // <-- ╨¥╨₧╨Æ╨₧╨ò ╨ƒ╨₧╨¢╨ò

            public BwoinkTextMessage(
                NetUserId userId,
                NetUserId trueSender,
                string text,
                DateTime? sentAt = default,
                bool playSound = true,
                bool adminOnly = false,
                string? iconPath = null)  // <-- ╨¥╨₧╨Æ╨½╨Ö ╨ƒ╨É╨á╨É╨£╨ò╨ó╨á
            {
                SentAt = sentAt ?? DateTime.Now;
                UserId = userId;
                TrueSender = trueSender;
                Text = text;
                PlaySound = playSound;
                AdminOnly = adminOnly;
                IconPath = iconPath;
            }
        }
    }

    [Serializable, NetSerializable]
    public sealed class BwoinkAHelpOpenRequestEvent : EntityEventArgs;

    [Serializable, NetSerializable]
    public sealed class BwoinkDiscordRelayUpdated : EntityEventArgs
    {
        public bool DiscordRelayEnabled { get; }
        public BwoinkDiscordRelayUpdated(bool enabled) { DiscordRelayEnabled = enabled; }
    }

    [Serializable, NetSerializable]
    public sealed class BwoinkClientTypingUpdated : EntityEventArgs
    {
        public NetUserId Channel { get; }
        public bool Typing { get; }
        public BwoinkClientTypingUpdated(NetUserId channel, bool typing) { Channel = channel; Typing = typing; }
    }

    [Serializable, NetSerializable]
    public sealed class BwoinkPlayerTypingUpdated : EntityEventArgs
    {
        public NetUserId Channel { get; }
        public string PlayerName { get; }
        public bool Typing { get; }
        public BwoinkPlayerTypingUpdated(NetUserId channel, string playerName, bool typing)
        {
            Channel = channel; PlayerName = playerName; Typing = typing;
        }
    }
}
