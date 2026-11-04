// SPDX-FileCopyrightText: 2024 Your Name <you@example.com>
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Maths;

namespace Content.Client.Stylesheets
{
    public static class YellowButtonStyles
    {
        // Константы цветов - адаптированы для прозрачного тёмного интерфейса
        // Добавлена прозрачность (alpha = 0.85-0.95) и более светлые оттенки

        // Основные жёлтые (светлые, полупрозрачные)
        public static readonly Color ButtonColorDefaultYellow = new Color(1.52f, 1.32f, 0.16f, 0.92f); // 0.80 прозрачности
        public static readonly Color ButtonColorHoveredYellow = new Color(1.61f, 1.44f, 0.62f, 0.92f);
        public static readonly Color ButtonColorPressedYellow = new Color(1.34f, 1.14f, 0.00f, 0.92f);
        public static readonly Color ButtonColorDisabledYellow = new Color(0.68f, 0.61f, 0.33f, 0.86f);

        // Текст - светлый для контраста с тёмным фоном
        public static readonly Color ButtonColorTextYellow = Color.FromHex("#FFFFFF");
        public static readonly Color ButtonColorTextYellowDark = Color.FromHex("#1A1A1A");

        // Классы стилей
        public const string StyleClassButtonColorYellow = "ButtonColorYellow";
        public const string StyleClassButtonColorYellowBright = "ButtonColorYellowBright";
        public const string StyleClassButtonColorYellowDark = "ButtonColorYellowDark";
        public const string StyleClassButtonColorYellowCaution = "ButtonColorYellowCaution";

        // Яркие варианты (ещё светлее, почти белые с жёлтым оттенком)
        public static readonly Color ButtonColorDefaultYellowBright = new Color(1.62f, 1.58f, 0.62f, 1.03f); // ~0.90 прозрачности
        public static readonly Color ButtonColorHoveredYellowBright = new Color(1.70f, 1.68f, 0.81f, 1.03f);
        public static readonly Color ButtonColorPressedYellowBright = new Color(1.61f, 1.44f, 0.55f, 1.03f);
        public static readonly Color ButtonColorDisabledYellowBright = new Color(1.09f, 1.04f, 0.35f, 1.03f);

        // Тёмные варианты (но всё равно светлее оригинала, с прозрачностью)
        public static readonly Color ButtonColorDefaultYellowDark = new Color(1.68f, 1.17f, 0.09f, 1.03f);
        public static readonly Color ButtonColorHoveredYellowDark = new Color(1.72f, 1.28f, 0.20f, 1.03f);
        public static readonly Color ButtonColorPressedYellowDark = new Color(1.66f, 0.98f, 0.04f, 1.03f);
        public static readonly Color ButtonColorDisabledYellowDark = new Color(0.84f, 0.65f, 0.02f, 0.86f);

        // Предупреждающие варианты (оранжево-жёлтые, яркие)
        public static readonly Color ButtonColorDefaultYellowCaution = new Color(1.69f, 1.17f, 0.32f, 1.03f);
        public static readonly Color ButtonColorHoveredYellowCaution = new Color(1.72f, 1.31f, 0.09f, 1.03f);
        public static readonly Color ButtonColorPressedYellowCaution = new Color(1.66f, 1.04f, 0.04f, 1.03f);
        public static readonly Color ButtonColorDisabledYellowCaution = new Color(1.13f, 0.64f, 0.02f, 1.03f);

        // Текст для разных вариантов
        public static readonly Color ButtonColorTextYellowBright = Color.FromHex("#1A1A1A"); // тёмный текст для ярких кнопок
        public static readonly Color ButtonColorTextYellowCaution = Color.FromHex("#FFFFFF"); // белый текст для предупреждений
    }
}
