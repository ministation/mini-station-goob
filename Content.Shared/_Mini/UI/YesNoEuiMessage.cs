// SPDX-License-Identifier: LicenseRef-MiniStation-1.0


using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Mini.UI
{
    [Serializable, NetSerializable]
    public enum YesNoUiButton
    {
        No,
        Yes,
    }

    [Serializable, NetSerializable]
    public sealed class YesNoChoiceMessage : EuiMessageBase
    {
        public readonly YesNoUiButton Button;

        public YesNoChoiceMessage(YesNoUiButton button)
        {
            Button = button;
        }
    }

    [Serializable, NetSerializable]
    public sealed class YesNoEuiState : EuiStateBase
    {
        public string Title = string.Empty;
        public string Text = string.Empty;

        public YesNoEuiState(string title, string text)
        {
            Title = title;
            Text = text;
        }
    }
}
