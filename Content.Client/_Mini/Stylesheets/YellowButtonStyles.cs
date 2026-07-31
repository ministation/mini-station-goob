// SPDX-FileCopyrightText: 2024 Your Name <you@example.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Maths;

namespace Content.Client.Stylesheets
{
    public static class YellowButtonStyles
    {
        // Oasis purple palette for Discord auth button (was yellow)

        public static readonly Color ButtonColorDefaultYellow = Color.FromHex("#8B00FFCC");
        public static readonly Color ButtonColorHoveredYellow = Color.FromHex("#A855FFCC");
        public static readonly Color ButtonColorPressedYellow = Color.FromHex("#6B00CCCC");
        public static readonly Color ButtonColorDisabledYellow = Color.FromHex("#3A2A4ACC");

        public static readonly Color ButtonColorTextYellow = Color.FromHex("#FFFFFF");
        public static readonly Color ButtonColorTextYellowDark = Color.FromHex("#E9D5FF");

        public const string StyleClassButtonColorYellow = "ButtonColorYellow";
        public const string StyleClassButtonColorYellowBright = "ButtonColorYellowBright";
        public const string StyleClassButtonColorYellowDark = "ButtonColorYellowDark";
        public const string StyleClassButtonColorYellowCaution = "ButtonColorYellowCaution";

        public static readonly Color ButtonColorDefaultYellowBright = Color.FromHex("#A855FFE6");
        public static readonly Color ButtonColorHoveredYellowBright = Color.FromHex("#C084FCE6");
        public static readonly Color ButtonColorPressedYellowBright = Color.FromHex("#8B00FFE6");
        public static readonly Color ButtonColorDisabledYellowBright = Color.FromHex("#5B3A7AE6");

        public static readonly Color ButtonColorDefaultYellowDark = Color.FromHex("#5B3A7ACC");
        public static readonly Color ButtonColorHoveredYellowDark = Color.FromHex("#7B4F9ACC");
        public static readonly Color ButtonColorPressedYellowDark = Color.FromHex("#4A2A6ACC");
        public static readonly Color ButtonColorDisabledYellowDark = Color.FromHex("#3A2A4ACC");

        public static readonly Color ButtonColorDefaultYellowCaution = Color.FromHex("#8B00FFE6");
        public static readonly Color ButtonColorHoveredYellowCaution = Color.FromHex("#A855FFE6");
        public static readonly Color ButtonColorPressedYellowCaution = Color.FromHex("#6B00CCE6");
        public static readonly Color ButtonColorDisabledYellowCaution = Color.FromHex("#3A2A4AE6");

        public static readonly Color ButtonColorTextYellowBright = Color.FromHex("#FFFFFF");
        public static readonly Color ButtonColorTextYellowCaution = Color.FromHex("#FFFFFF");
    }
}
