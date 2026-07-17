// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Communications;
using Robust.Shared.Audio;
using Content.Shared.Containers.ItemSlots;

namespace Content.Server.Communications
{
    [RegisterComponent]
    public sealed partial class CommunicationsConsoleComponent : SharedCommunicationsConsoleComponent
    {
        public float UIUpdateAccumulator = 0f;

        /// <summary>
        /// Remaining cooldown between making announcements.
        /// </summary>
        [ViewVariables]
        [DataField]
        public float AnnouncementCooldownRemaining;

        [ViewVariables]
        [DataField]
        public float BroadcastCooldownRemaining;

        [ViewVariables]
        [DataField]
        public float CallERTCooldownRemaining;

        /// <summary>
        /// Fluent ID for the announcement title
        /// If a Fluent ID isn't found, just uses the raw string
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField(required: true)]
        public LocId Title = "comms-console-announcement-title-station";

        /// <summary>
        /// Announcement color
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField]
        public Color Color = Color.Gold;

        /// <summary>
        /// Time in seconds between announcement delays on a per-console basis
        /// </summary>
        [ViewVariables]
        [DataField]
        public int DelayBetweenAnnouncements = 60;


        /// <summary>
        /// Time in seconds of announcement cooldown when a new console is created on a per-console basis
        /// </summary>
        [ViewVariables]
        [DataField]
        public int InitialDelay = 30;

        /// <summary>
        /// Can call or recall the shuttle
        /// </summary>
        [ViewVariables]
        [DataField]
        public bool CanCallShuttle = true;

        /// <summary>
        /// Can recall the shuttle after it has already been called.
        /// </summary>
        [ViewVariables]
        [DataField]
        public bool CanRecallShuttle = true;

        /// <summary>
        /// Can call or recall the ERT
        /// </summary>

        /// <summary>
        /// Announce on all grids (for nukies)
        /// </summary>
        [DataField]
        public bool Global = false;

        /// <summary>
        /// Announce sound file path
        /// </summary>
        [DataField]
        public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Announcements/announce.ogg");

        /// <summary>
        /// Hides the sender identity (If they even have one).
        /// In practise this removes the "Sent by ScugMcWawa (Slugcat Captain)" at the bottom of the announcement.
        /// </summary>
        [DataField]
        public bool AnnounceSentBy = true;
        public SoundSpecifier AnnouncementSound = new SoundPathSpecifier("/Audio/_Mini/Announcements/announce.ogg");

        /// <summary>
        /// Goobstation
        /// What alert level to set it to if the console is emagged.
        /// </summary>
        [DataField] public string AlertLevelOnEmag = "honk";
    }
}
