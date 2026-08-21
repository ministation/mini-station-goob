// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Input;
using Content.Shared._Trauma.Genetics.Console;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._Trauma.Genetics.UI;

[GenerateTypedNameReferences]
public sealed partial class GenePuzzle : Control
{
    public event Action<uint, GeneticsCycle>? OnSetBase;
    public event Action? OnSequence;
    public event Action? OnResetSequence;

    private bool _busy;
    private bool _sequenced;
    private bool _writable = true;
    private string _bases = string.Empty;
    private string _originalBases = string.Empty;

    public static readonly Color Blue = Color.FromHex("#2563EB");
    public static readonly Color Green = Color.FromHex("#16A34A");
    public static readonly Color Unknown = Color.FromHex("#52525B");

    private static readonly StyleBoxFlat BlueBox = MakeBaseBox(Blue);
    private static readonly StyleBoxFlat GreenBox = MakeBaseBox(Green);
    private static readonly StyleBoxFlat UnknownBox = MakeBaseBox(Unknown);

    private static StyleBoxFlat MakeBaseBox(Color color)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = color,
            BorderColor = Color.FromHex("#00000099"),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 4,
            ContentMarginRightOverride = 4,
            ContentMarginTopOverride = 2,
            ContentMarginBottomOverride = 2,
        };
    }

    public GenePuzzle()
    {
        RobustXamlLoader.Load(this);

        SequenceButton.OnPressed += _ => OnSequence?.Invoke();
        ResetSequenceButton.OnPressed += _ => OnResetSequence?.Invoke();
        OnSetBase += (_, _) => UpdateSequenceButton();
    }

    public void MakeReadonly()
    {
        _writable = false;
        Tip.Visible = false; // clicking won't do anything
        SequenceButtonContainer.Visible = false;
    }

    private void UpdateSequenceButton()
    {
        SequenceButton.Disabled = _busy || _sequenced || !IsComplete();
        ResetSequenceButton.Disabled = _busy || _bases == _originalBases;
    }

    private bool IsComplete()
    {
        foreach (var c in _bases)
        {
            if (c == 'X') return false;
        }

        return true;
    }

    public void SetBusy(bool busy)
    {
        _busy = busy;
        UpdateSequenceButton();
    }

    public void SetSequenced(bool sequenced)
    {
        _sequenced = sequenced;
        UpdateSequenceButton();
    }

    public void SetBases(string bases, string originalBases)
    {
        Visible = true;
        _bases = bases;
        _originalBases = originalBases;
        BaseButtons.RemoveAllChildren();
        for (int i = 0; i < bases.Length; i++)
        {
            AddBase((uint) i, bases[i]);
        }
        UpdateSequenceButton();
    }

    private void AddBase(uint i, char b)
    {
        var button = new Button();
        void Cycle(GeneticsCycle cycle)
        {
            if (_busy || !_writable || button.Disabled)
                return;

            b = GeneticsConsoleSystem.CycleBase(b, cycle);
            button.StyleBoxOverride = GetColor(b);

            button.Text = b.ToString();
            OnSetBase?.Invoke(i, cycle);
        }

        button.Text = b.ToString();
        button.StyleBoxOverride = GetColor(b);
        button.Disabled = _originalBases[(int) i] != 'X'; // can't cycle bases that are guaranteed known
        button.OnKeyBindDown += args =>
        {
            if (args.Function == EngineKeyFunctions.UIRightClick)
                Cycle(GeneticsCycle.Last);
            else if (args.Function == ContentKeyFunctions.TryPullObject) // Ctrl click
                Cycle(GeneticsCycle.Reset);
        };
        button.OnPressed += _ =>
        {
            Cycle(GeneticsCycle.Next);
        };
        BaseButtons.AddChild(button);
    }

    private static StyleBoxFlat GetColor(char b)
        => b switch
        {
            'A' or 'T' => GreenBox,
            'G' or 'C' => BlueBox,
            _ => UnknownBox
        };
}
