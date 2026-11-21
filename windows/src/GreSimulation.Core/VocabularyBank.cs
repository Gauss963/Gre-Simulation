using System.Globalization;

namespace GreSimulation.Core;

/// <summary>
/// Port of VocabularyBank.swift: merges ExpandedVocabulary, Authorized20260720Vocabulary,
/// Authorized20260720Part2Vocabulary, AnswerChoiceVocabulary and the curated Swift entries (embedded), in that order, so a
/// later complete entry (curated last) overrides earlier ones while synonyms and sources are unioned.
/// </summary>
public sealed class VocabularyBank
{
    public const string AllSources = "All sources";

    public IReadOnlyList<VocabularyWord> CuratedWords { get; }
    /// <summary>Deduplicated words sorted case-insensitively.</summary>
    public IReadOnlyList<VocabularyWord> Words { get; }
    /// <summary>Every source tag, sorted (the Source pickers add "All sources" first).</summary>
    public IReadOnlyList<string> SourceOptions { get; }

    public VocabularyBank(string resourceDirectory)
    {
        CuratedWords = BundledResource.DecodeEmbedded<List<VocabularyWord>>("CuratedVocabulary.json");
        List<VocabularyWord> Load(string name) => BundledResource.Decode<List<VocabularyWord>>(resourceDirectory, name) ?? [];
        var all = Load("ExpandedVocabulary").Concat(Load("Authorized20260720Vocabulary"))
            .Concat(Load("Authorized20260720Part2Vocabulary")).Concat(Load("AnswerChoiceVocabulary")).Concat(CuratedWords);

        var merged = new Dictionary<string, VocabularyWord>(StringComparer.Ordinal);
        foreach (var item in all)
        {
            var key = SwiftText.TrimWhitespaceAndNewlines(item.Word.ToLowerInvariant());
            if (key.Length == 0) continue;
            if (merged.TryGetValue(key, out var existing))
            {
                var preferCurated = item.Example.Length > 0 && item.Definition.Length > 0;
                var preferred = preferCurated ? item : existing;
                merged[key] = new VocabularyWord
                {
                    Word = preferred.Word,
                    Pronunciation = preferred.Pronunciation.Length == 0 ? existing.Pronunciation : preferred.Pronunciation,
                    Definition = preferred.Definition.Length == 0 ? existing.Definition : preferred.Definition,
                    Chinese = preferred.Chinese.Length == 0 ? existing.Chinese : preferred.Chinese,
                    Synonyms = existing.Synonyms.Concat(item.Synonyms).Distinct().Order(StringComparer.Ordinal).ToList(),
                    Example = preferred.Example.Length == 0 ? existing.Example : preferred.Example,
                    ExampleSource = preferred.ExampleSource,
                    ExampleSourceURL = preferred.ExampleSourceURL,
                    Sources = existing.Sources.Concat(item.Sources).Distinct().Order(StringComparer.Ordinal).ToList(),
                    IsHighFrequency = existing.IsHighFrequency || item.IsHighFrequency,
                    PartOfSpeech = preferred.PartOfSpeech ?? existing.PartOfSpeech,
                };
            }
            else
            {
                merged[key] = item;
            }
        }
        var comparer = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);
        Words = merged.Values.OrderBy(w => w.Word, comparer).ToList();
        SourceOptions = Words.SelectMany(w => w.Sources).Distinct().Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>Port of <c>VocabularyBank.validate()</c> (needs the pronunciation library for the audio check).</summary>
    public IReadOnlyList<string> Validate(WordPronunciationLibrary pronunciations)
    {
        var issues = new List<string>();
        if (Words.Select(w => w.Id).Distinct().Count() != Words.Count) issues.Add("Vocabulary bank has duplicate IDs.");
        var unreviewed = Words.Where(w => string.IsNullOrEmpty(w.PartOfSpeech) || w.Chinese.Contains('、') || w.Chinese.Contains('/')).ToList();
        if (unreviewed.Count > 0)
            issues.Add($"{unreviewed.Count} words lack a reviewed gloss, e.g. {SwiftArray(unreviewed.Take(3).Select(w => w.Word))}.");
        var missingAudio = Words.Where(w => !pronunciations.HasClip(w.Word)).ToList();
        if (missingAudio.Count > 0)
            issues.Add($"{missingAudio.Count} words have no bundled pronunciation, e.g. {SwiftArray(missingAudio.Take(3).Select(w => w.Word))}.");
        return issues;
    }

    private static string SwiftArray(IEnumerable<string> items) => "[" + string.Join(", ", items.Select(i => $"\"{i}\"")) + "]";
}
