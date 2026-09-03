using System.Text.RegularExpressions;

namespace Vox.Core;

public sealed record TextReplacement(string From, string To);

/// <summary>Literal, whole-phrase replacements in one pass; generated output is never matched again.</summary>
public sealed class TextRewriter
{
    private readonly Dictionary<string, string> _replacements = new(StringComparer.OrdinalIgnoreCase);
    private readonly Regex? _pattern;

    public TextRewriter(IEnumerable<TextReplacement> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var rule in rules)
        {
            if (rule is null || string.IsNullOrWhiteSpace(rule.From) || rule.To is null)
                throw new ArgumentException("Each rule needs a word or phrase to replace. The replacement can be empty.");
            var phrase = Normalize(rule.From);
            if (!_replacements.TryAdd(phrase, rule.To))
                throw new ArgumentException($"There is more than one rule for ‘{phrase}’. Keep just one.");
        }
        if (_replacements.Count == 0) return;

        var alternatives = _replacements.Keys.OrderByDescending(phrase => phrase.Length)
            .Select(phrase => string.Join(@"\s+", phrase.Split(' ').Select(Regex.Escape)));
        _pattern = new Regex(@"(?<![\p{L}\p{M}\p{N}_])(?:" + string.Join('|', alternatives) + @")(?![\p{L}\p{M}\p{N}_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
    }

    public string Apply(string text) => _pattern?.Replace(text, match => _replacements[Normalize(match.Value)]) ?? text;

    private static string Normalize(string phrase) => Regex.Replace(phrase.Trim(), @"\s+", " ");
}
