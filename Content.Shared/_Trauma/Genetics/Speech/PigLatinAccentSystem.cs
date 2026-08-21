using Content.Shared.Speech;
using Robust.Shared.Random;
using System.Linq;
using System.Text;

namespace Content.Shared._Trauma.Genetics.Speech;

public sealed partial class PigLatinAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    private static readonly char[] Punctuation = { '!', '?', '.', '-' };
    private static readonly char[] Vowels = { 'a', 'e', 'i', 'o', 'u' };
    private static readonly string[] VowelSuffix = { "yay", "way", "hay" };

    private readonly StringBuilder _builder = new();
    private readonly StringBuilder _punctuation = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PigLatinAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<PigLatinAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = ChangeMessage(args.Message);
    }

    public string ChangeMessage(string message)
    {
        message = message.ToLower();

        _builder.Clear();
        _punctuation.Clear();
        foreach (var c in message)
        {
            if (Punctuation.Contains(c))
                _punctuation.Append(c);
            else
                _builder.Append(c);
        }

        message = _builder.ToString();
        _builder.Clear();
        var words = message.Split(' ');
        var end = words.Length - 1;
        for (var i = 0; i <= end; i++)
        {
            AppendWord(words[i]);
            if (i != end)
                _builder.Append(' ');
        }

        if (_builder.Length > 0)
            _builder[0] = char.ToUpper(_builder[0]);
        _builder.Append(_punctuation);
        return _builder.ToString();
    }

    private void AppendWord(string word)
    {
        if (word.Length < 2)
        {
            _builder.Append(word);
            return;
        }

        var first = word[0];
        var second = word[1];
        var firstVowel = Vowels.Contains(first);
        var secondVowel = Vowels.Contains(second);

        if (firstVowel && !secondVowel)
        {
            _builder.Append(word);
            _builder.Append(_random.Pick(VowelSuffix));
            return;
        }

        if (!firstVowel && secondVowel)
        {
            _builder.Append(word, 1, word.Length - 1);
            _builder.Append(first);
            _builder.Append("ay");
            return;
        }

        if (!firstVowel && !secondVowel)
        {
            _builder.Append(word, 2, word.Length - 2);
            _builder.Append(first);
            _builder.Append(second);
            _builder.Append("ay");
            return;
        }

        _builder.Append(word);
    }
}
