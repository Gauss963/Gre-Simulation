#!/usr/bin/env python3
"""Fill missing vocabulary translations and examples from open offline sources."""

from __future__ import annotations

import argparse
import bz2
import csv
import json
import re
from dataclasses import dataclass
from pathlib import Path

try:
    from opencc import OpenCC
except ImportError:
    OpenCC = None


ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "Gre Simulation"

ECDICT_SOURCE = "ECDICT English-Chinese Dictionary"
TATOEBA_SOURCE = "Tatoeba Corpus"
ORIGINAL_EXAMPLE_SOURCE = "Original app examples"

_TAIWAN_CONVERTER = None

_COMMON_NAMES = {
    "alice", "bill", "bob", "carlos", "dan", "fadil", "frank", "jack", "jane",
    "john", "ken", "layla", "linda", "maria", "mary", "peter", "sami", "tom", "tony",
}
_ACADEMIC_WORDS = {
    "analysis", "argument", "committee", "data", "evidence", "experiment", "historian",
    "policy", "proposal", "report", "research", "researchers", "scholars", "scientists",
    "study", "theory",
}
_UNSUITABLE_PATTERN = re.compile(
    r"\b(?:fuck(?:ed|ing)?|motherfucker|shit(?:ty)?|bitch(?:es)?|cunt|porn(?:ography)?|"
    r"masturbat(?:e|ed|es|ing|ion)|rapist|racial\s+slur)\b",
    re.IGNORECASE,
)
_SENSITIVE_PATTERN = re.compile(
    r"\b(?:sex(?:ual|ually|uality)?|same-sex|rape(?:d|s)?|rapist|prostitut(?:e|ed|es|ion)|"
    r"genital(?:s)?|nazi(?:s)?|hitler|antisemit(?:e|ic|ism)|kill(?:ed|ing|s)?|"
    r"murder(?:ed|er|ers|ing|s)?|suicid(?:e|al)|gun(?:s)?|weapon(?:s)?|tortur(?:e|ed|es|ing)|"
    r"slav(?:e|ery|es)|"
    r"transgender|\btrans\b|\bcis\b|homosexual(?:ity)?|same-sex|\bgay\b|lgbt|hijab|"
    r"cannabis|rectum|bomb(?:s|ed|ing|ardment)?|human\s+trafficking|"
    r"bastard|damn|hell|death|die[sd]?|dying|violent|violence|blood|"
    r"crime(?:s)?|criminal(?:s)?|prison|police|"
    r"racis(?:m|t)|xenophobia|anti-?semit(?:e|es|ic|ism)|drug(?:s)?|coronavirus|"
    r"women|woman|men|man|girls?|boys?|vagina|penis|"
    r"orgasm(?:s)?|erect(?:ed|ion|ions)?|naked|nude)\b",
    re.IGNORECASE,
)
_META_EXAMPLE_PATTERN = re.compile(
    r"\b(?:aforesaid|definition|define[sd]?|the word .{0,24} means?)\b",
    re.IGNORECASE,
)

# Handwritten sentences cover every high-frequency term for which neither open
# corpus produced a neutral, complete sentence under the quality filters.
_ORIGINAL_EXAMPLE_OVERRIDES = {
    "annotate": "Students should annotate the passage before comparing its competing claims.",
    "appall": "The report's evidence of deliberate neglect will appall many readers.",
    "aspersion": "The unsupported accusation cast an aspersion on the researcher's integrity.",
    "assail": "Several reviewers assail the theory for ignoring contrary evidence.",
    "asseverate": "The witness continued to asseverate that the document was authentic.",
    "autocracy": "Under the autocracy, public criticism of the ruler was rarely tolerated.",
    "bon voyage": "The travelers exchanged a cheerful bon voyage before boarding the train.",
    "bromidic": "The speech sounded bromidic because it repeated familiar slogans.",
    "calumnious": "The editor removed the calumnious claim before publication.",
    "capitulate": "The negotiators refused to capitulate despite mounting pressure.",
    "caterwaul": "Stray cats began to caterwaul beneath the window after midnight.",
    "celerity": "The rescue team responded with remarkable celerity.",
    "chantey": "The sailors sang a chantey to keep time as they worked.",
    "circumlocution": "Her circumlocution concealed a simple answer beneath many unnecessary words.",
    "circumscribe": "The new rules circumscribe the agency's authority to collect personal data.",
    "cliquish": "The committee became so cliquish that newcomers felt unwelcome.",
    "comity": "Years of cooperation restored comity between the neighboring communities.",
    "conflate": "The article should not conflate correlation with causation.",
    "consign": "The curator would not consign the fragile manuscript to an unsafe display case.",
    "crabby": "After the sleepless flight, even the patient traveler became crabby.",
    "debilitate": "A prolonged drought can debilitate an already fragile ecosystem.",
    "decorous": "The normally heated debate remained decorous throughout the evening.",
    "demarcate": "Painted lines demarcate the protected habitat from the public trail.",
    "deprecate": "Good mentors correct errors without seeking to deprecate their students.",
    "descry": "From the ridge, the hikers could descry a cabin through the fog.",
    "dilatory": "The court criticized the agency's dilatory response to the complaint.",
    "discomfit": "The unexpected question seemed to discomfit the otherwise confident speaker.",
    "discontinuity": "The abrupt change in tone creates a discontinuity in the argument.",
    "disjunctive": "The essay's disjunctive structure makes its central claim difficult to follow.",
    "dissimulate": "The diplomat tried to dissimulate her concern during the meeting.",
    "distend": "The gas caused the sealed container to distend visibly.",
    "doctrinaire": "His doctrinaire approach left no room for evidence that challenged the theory.",
    "dolorous": "A dolorous melody accompanied the final scene of the play.",
    "drollness": "The narrator's quiet drollness keeps the serious story from becoming grim.",
    "duplicitous": "The audit exposed a duplicitous effort to conceal the missing funds.",
    "efficacious": "The treatment proved efficacious in reducing the symptoms.",
    "elucidate": "The diagram helps elucidate the relationship between the two variables.",
    "emollient": "The mediator's emollient remarks eased the tension in the room.",
    "engender": "Transparent procedures can engender greater trust in public institutions.",
    "estrange": "Years of bitter disagreement began to estrange the former friends.",
    "evince": "The early sketches evince the artist's careful attention to proportion.",
    "excavate": "Archaeologists plan to excavate the site without disturbing the lower layers.",
    "exigent": "The hospital reserved its limited supplies for the most exigent cases.",
    "exonerate": "New laboratory evidence may exonerate the wrongly accused technician.",
    "extemporize": "When the projector failed, the lecturer had to extemporize.",
    "extenuate": "The judge found no evidence that could extenuate the deliberate offense.",
    "factotum": "As the director's factotum, she handled schedules, correspondence, and travel.",
    "fallibility": "Scientific review acknowledges human fallibility by requiring results to be checked.",
    "felicitate": "The dean rose to felicitate the recipients of the research award.",
    "flummery": "The proposal offered rhetorical flummery instead of a workable plan.",
    "foreordain": "Initial advantages do not foreordain the outcome of a close competition.",
    "foreshadow": "The opening image appears to foreshadow the novel's final reversal.",
    "forthright": "Her forthright explanation restored the committee's confidence.",
    "hauteur": "His hauteur alienated colleagues who might otherwise have supported him.",
    "heretical": "The scientist's once-heretical hypothesis later gained broad acceptance.",
    "heterodox": "The journal welcomed heterodox arguments that were supported by evidence.",
    "hieroglyph": "The archaeologist identified a hieroglyph carved beside the entrance.",
    "hortative": "The editorial adopts a hortative tone and urges readers to act.",
    "hortatory": "Her hortatory speech encouraged the volunteers to continue their work.",
    "iconoclastic": "The critic's iconoclastic essay challenged several revered traditions.",
    "idiosyncrasy": "The unusual pause was an idiosyncrasy of the conductor's style.",
    "idolatrize": "Biographers should not idolatrize their subjects at the expense of accuracy.",
    "illiberal": "The university rejected the illiberal policy because it restricted open inquiry.",
    "impel": "A desire for greater accuracy should impel researchers to replicate the study.",
    "imperious": "The manager's imperious tone discouraged the staff from raising concerns.",
    "imperturbable": "The pilot remained imperturbable as the weather deteriorated.",
    "inadvertent": "An inadvertent omission changed the apparent meaning of the paragraph.",
    "incinerate": "The facility can incinerate hazardous waste at extremely high temperatures.",
    "incisiveness": "The review's incisiveness exposed weaknesses that others had overlooked.",
    "incommensurate": "The minor error drew a penalty incommensurate with its consequences.",
    "infuse": "The director used humor to infuse the somber drama with moments of warmth.",
    "ingrate": "He seemed an ingrate when he dismissed years of generous assistance.",
    "invigorate": "A vigorous public debate can invigorate a stagnant institution.",
    "lambaste": "Editorials continued to lambaste officials for ignoring the warnings.",
    "laudatory": "The biography is generally laudatory but still acknowledges its subject's errors.",
    "leaven": "Brief moments of humor leaven the novel's otherwise somber mood.",
    "lucubration": "The scholar's dense lucubration rewards careful and patient reading.",
    "malodorous": "A malodorous vapor escaped when the container was opened.",
    "mawkish": "The film's mawkish ending undermined its earlier restraint.",
    "mendacity": "Repeated contradictions eventually revealed the witness's mendacity.",
    "metamorphose": "Over several decades, the industrial district began to metamorphose into a residential area.",
    "minatory": "The letter's minatory language warned of severe consequences.",
    "misapprehension": "The dispute arose from a misapprehension about the study's purpose.",
    "miscreant": "The story's miscreant secretly altered the records for personal gain.",
    "monomania": "His monomania about efficiency caused him to ignore every other goal.",
    "moralistic": "The novel avoids a moralistic conclusion and leaves judgment to the reader.",
    "mordant": "Her mordant wit exposed the hypocrisy behind the ceremony.",
    "mortify": "The public correction seemed to mortify the young speaker.",
    "munificent": "A munificent donation allowed the library to preserve the collection.",
    "naysayer": "Even the most persistent naysayer eventually accepted the new evidence.",
    "neonate": "The physician monitored the neonate closely during its first week.",
    "nonviable": "Without additional funding, the ambitious proposal is nonviable.",
    "numinous": "Visitors described a numinous atmosphere inside the ancient sanctuary.",
    "obsolescent": "The once-essential device became obsolescent as digital alternatives spread.",
    "oscillate": "Public opinion may oscillate as new evidence emerges.",
    "outgrowth": "The reform was an outgrowth of years of public dissatisfaction.",
    "peccadillo": "The committee treated the minor bookkeeping error as a peccadillo.",
    "pediatrics": "She chose pediatrics because she wanted to care for children.",
    "pellucid": "His pellucid explanation made the complex theory accessible.",
    "peripatetic": "The peripatetic lecturer taught in five cities during the spring.",
    "perquisite": "Free housing was a valuable perquisite of the overseas appointment.",
    "petroglyph": "Researchers photographed the petroglyph without touching the rock surface.",
    "pilferage": "Improved inventory controls reduced pilferage from the warehouse.",
    "platitude": "The audience wanted a concrete plan rather than another platitude.",
    "polemical": "The author's polemical tone sometimes obscures the strength of her evidence.",
    "preempt": "The urgent announcement will preempt the scheduled lecture.",
    "premeditate": "Investigators sought evidence that the defendant had time to premeditate the act.",
    "proliferate": "Small errors can proliferate when copied from one report to another.",
    "protract": "Further procedural disputes could protract the negotiations for months.",
    "providential": "A providential change in the weather allowed the rescue to proceed.",
    "pusillanimous": "The general condemned the retreat as a pusillanimous response to a manageable risk.",
    "recant": "The witness refused to recant her earlier statement.",
    "refulgent": "The refulgent dome was visible from across the valley.",
    "refutable": "A scientific claim must be refutable by contrary evidence.",
    "remonstrance": "Despite the residents' remonstrance, the council approved the project.",
    "scissor": "The fault could scissor the rock layers in opposite directions.",
    "somatic": "The study distinguishes somatic symptoms from purely emotional responses.",
    "spurn": "The committee chose to spurn the compromise despite its practical benefits.",
    "stratification": "The study examines social stratification across several generations.",
    "stymie": "A lack of reliable data could stymie the investigation.",
    "tantalize": "The fragmentary inscription will tantalize historians for years.",
    "tepid": "The proposal received only tepid support from the board.",
    "timorous": "A timorous response allowed the problem to grow more serious.",
    "univocal": "The statute offers no univocal answer to the unusual case.",
    "vacillate": "The council continued to vacillate between the two proposals.",
    "valorize": "The museum's new exhibition seeks to valorize overlooked local artists.",
    "veracious": "The panel considered her a careful and veracious witness.",
    "xerophyte": "A xerophyte can survive where water is scarce for long periods.",
}


@dataclass(frozen=True)
class LexicalSupplement:
    translation: str
    part_of_speech: str
    english_definition: str


@dataclass(frozen=True)
class ExampleCandidate:
    score: float
    sentence_id: str
    sentence: str
    is_cc0: bool


def taiwan_traditional(text: str) -> str:
    global _TAIWAN_CONVERTER
    if not text:
        return ""
    if OpenCC is None:
        raise RuntimeError(
            "Taiwan Traditional Chinese conversion requires opencc-python-reimplemented. "
            "Install it with: python3 -m pip install opencc-python-reimplemented"
        )
    if _TAIWAN_CONVERTER is None:
        _TAIWAN_CONVERTER = OpenCC("s2twp")
    return _TAIWAN_CONVERTER.convert(text)


def compact_translation(raw: str) -> str:
    """Keep the general dictionary gloss and remove specialist-domain duplicates."""
    first_line = raw.replace("\\n", "\n").splitlines()[0].strip()
    first_line = re.sub(r"^(?:adj|adv|vt|vi|v|a|ad|n|prep|pron|conj)\.\s*", "", first_line, flags=re.I)
    first_line = re.sub(r"^\[[^]]+]\s*", "", first_line)
    first_line = first_line.replace(", ", "、").replace("; ", "；")
    result = taiwan_traditional(first_line)
    result = result.replace("遊標、游標", "游標")
    result = result.replace("做為", "作為")
    result = result.replace("占主導", "佔主導")
    return result.strip(" ；、")


def load_ecdict(path: Path, target_words: set[str]) -> dict[str, LexicalSupplement]:
    if not path.exists():
        raise FileNotFoundError(f"ECDICT CSV not found: {path}")
    found: dict[str, LexicalSupplement] = {}
    with path.open(encoding="utf-8", newline="") as handle:
        for row in csv.DictReader(handle):
            word = row.get("word", "").strip().lower()
            translation = row.get("translation", "").strip()
            if word not in target_words or word in found or not translation:
                continue
            found[word] = LexicalSupplement(
                translation=translation,
                part_of_speech=row.get("pos", "").strip(),
                english_definition=row.get("definition", "").strip(),
            )
    return found


def sentence_score(sentence: str, target: str, is_cc0: bool) -> float | None:
    tokens = re.findall(r"[A-Za-z]+(?:[-'][A-Za-z]+)*", sentence.lower())
    case_preserved_tokens = re.findall(r"[A-Za-z]+(?:[-'][A-Za-z]+)*", sentence)
    token_set = set(tokens)
    word_count = len(tokens)
    stripped = sentence.strip()

    if not 5 <= word_count <= 28 or not 20 <= len(stripped) <= 180:
        return None
    if not re.search(r"[.!?][\"']?$", stripped):
        return None
    if len(re.findall(r"[.!?](?:\s|$)", stripped)) > 1:
        return None
    if (
        _UNSUITABLE_PATTERN.search(stripped)
        or _SENSITIVE_PATTERN.search(stripped)
        or _META_EXAMPLE_PATTERN.search(stripped)
    ):
        return None
    name_tokens = set(re.findall(r"[A-Za-z]+", stripped.lower()))
    if name_tokens & _COMMON_NAMES:
        return None
    internal_capitals = [
        token
        for index, token in enumerate(case_preserved_tokens)
        if index > 0 and token[0].isupper() and token != "I"
    ]
    if internal_capitals:
        return None

    score = abs(word_count - 14) * 1.5
    score -= len(token_set & _ACADEMIC_WORDS) * 2
    score += stripped.count("!") * 4 + stripped.count("?") * 2
    score += stripped.count('"') * 2
    score += sum(character.isdigit() for character in stripped) * 0.5
    score += len(token_set & {"i", "me", "mine", "my", "our", "ours", "we"}) * (1 if is_cc0 else 3)
    score += len(token_set & {"you", "your", "yours"}) * (1 if is_cc0 else 2)
    if stripped.lower().startswith(f"{target} "):
        score += 1
    return score


def select_examples(path: Path, target_words: set[str], is_cc0: bool) -> dict[str, ExampleCandidate]:
    if not target_words:
        return {}
    if not path.exists():
        raise FileNotFoundError(f"Tatoeba export not found: {path}")

    simple_words = {word for word in target_words if re.fullmatch(r"[a-z]+", word)}
    complex_words = target_words - simple_words
    complex_patterns = {
        word: re.compile(rf"(?<![A-Za-z]){re.escape(word)}(?![A-Za-z])", re.I)
        for word in complex_words
    }
    selected: dict[str, ExampleCandidate] = {}

    with bz2.open(path, "rt", encoding="utf-8") as handle:
        for line in handle:
            fields = line.rstrip("\n").split("\t")
            if len(fields) < 3:
                continue
            sentence_id, sentence = fields[0], fields[2].strip()
            tokens = set(re.findall(r"[A-Za-z]+", sentence.lower()))
            matches = tokens & simple_words
            lowered = sentence.lower()
            for word, pattern in complex_patterns.items():
                if word in lowered and pattern.search(sentence):
                    matches.add(word)

            for word in matches:
                score = sentence_score(sentence, word, is_cc0)
                if score is None:
                    continue
                candidate = ExampleCandidate(score, sentence_id, sentence, is_cc0)
                if word not in selected or candidate.score < selected[word].score:
                    selected[word] = candidate
    return selected


def inferred_part_of_speech(supplement: LexicalSupplement | None, definition: str) -> str:
    if supplement:
        source = f"{supplement.translation} {supplement.part_of_speech}"
        match = re.match(r"\s*(vt|vi|v|adj|adv|a|ad|n)\.", source, re.I)
        if match:
            value = match.group(1).lower()
            if value in {"adj", "a"}:
                return "adjective"
            if value in {"adv", "ad"}:
                return "adverb"
            if value == "n":
                return "noun"
            if value == "vt":
                return "transitiveVerb"
            if value == "vi":
                return "intransitiveVerb"
            return "verb"

    lowered = definition.lower()
    if lowered.startswith(("a person", "a state", "a feeling", "a quality", "an act", "one who")):
        return "noun"
    if lowered.startswith("to "):
        return "verb"
    return "noun"


def original_contextual_example(
    word: str,
    definition: str,
    supplement: LexicalSupplement | None,
) -> str:
    """Create a definition-grounded fallback when neither corpus has a suitable sentence."""
    if word.lower() in _ORIGINAL_EXAMPLE_OVERRIDES:
        return _ORIGINAL_EXAMPLE_OVERRIDES[word.lower()]

    part_of_speech = inferred_part_of_speech(supplement, definition)
    source_definition = supplement.english_definition if supplement and supplement.english_definition else definition
    meaning = source_definition.replace("\\n", "\n").splitlines()[0]
    meaning = re.sub(r"^(?:adj|adv|vt|vi|v|s|a|r|n)\.?\s+", "", meaning, flags=re.I)
    meaning = re.sub(r"\s+", " ", meaning).strip().rstrip(".;")
    meaning = meaning.replace('"', "'")

    if part_of_speech == "adjective":
        return f"The passage describes its subject as {word}, meaning {meaning}."

    if part_of_speech == "adverb":
        return f"The action occurs {word}, meaning {meaning}."

    if part_of_speech in {"transitiveVerb", "intransitiveVerb", "verb"}:
        return f"In this context, “{word}” means {meaning}."

    return f"In this context, “{word}” refers to {meaning}."


def example_uses_word(entry: dict) -> bool:
    """Return whether an example actually demonstrates the headword."""
    word = entry.get("word", "").strip()
    example = entry.get("example", "").strip()
    if not word or not example:
        return False
    return bool(re.search(rf"(?<![A-Za-z]){re.escape(word)}(?![A-Za-z])", example, re.I))


def append_source(entry: dict, source: str) -> None:
    sources = entry.setdefault("sources", [])
    if source not in sources:
        sources.append(source)
        sources.sort()


def enrich_vocabulary_entries(
    entries: list[dict],
    ecdict_csv: Path,
    tatoeba_cc0: Path,
    tatoeba_english: Path,
    refresh_original_examples: bool = False,
    refresh_invalid_examples: bool = False,
) -> dict[str, int]:
    if refresh_original_examples:
        for entry in entries:
            if entry.get("exampleSource", "").startswith("Original"):
                entry["example"] = ""
                entry.pop("exampleSource", None)
                entry.pop("exampleSourceURL", None)
                entry["sources"] = [
                    source for source in entry.get("sources", [])
                    if source != ORIGINAL_EXAMPLE_SOURCE
                ]

    invalid_examples_cleared = 0
    if refresh_invalid_examples:
        for entry in entries:
            if entry.get("example", "").strip() and not example_uses_word(entry):
                entry["example"] = ""
                entry.pop("exampleSource", None)
                entry.pop("exampleSourceURL", None)
                invalid_examples_cleared += 1

    target_words = {
        entry["word"].strip().lower()
        for entry in entries
        if not entry.get("chinese", "").strip() or not entry.get("example", "").strip()
    }
    supplements = load_ecdict(ecdict_csv, target_words)

    missing_examples = {
        entry["word"].strip().lower()
        for entry in entries
        if not entry.get("example", "").strip()
    }
    cc0_examples = select_examples(tatoeba_cc0, missing_examples, is_cc0=True)
    attributed_examples = select_examples(tatoeba_english, missing_examples, is_cc0=False)

    translations_added = 0
    cc0_added = 0
    attributed_added = 0
    original_added = 0

    for entry in entries:
        word = entry["word"].strip().lower()
        supplement = supplements.get(word)
        if not entry.get("chinese", "").strip():
            if supplement is None:
                raise ValueError(f"ECDICT has no Chinese translation for {entry['word']}")
            entry["chinese"] = compact_translation(supplement.translation)
            if not entry["chinese"]:
                raise ValueError(f"ECDICT produced an empty Chinese translation for {entry['word']}")
            append_source(entry, ECDICT_SOURCE)
            translations_added += 1

        if entry.get("example", "").strip():
            continue

        candidates = [
            candidate
            for candidate in (cc0_examples.get(word), attributed_examples.get(word))
            if candidate is not None
        ]
        candidate = min(candidates, key=lambda value: (value.score, not value.is_cc0)) if candidates else None
        if candidate:
            entry["example"] = candidate.sentence
            entry["exampleSource"] = (
                "Tatoeba Corpus · CC0 1.0"
                if candidate.is_cc0
                else "Tatoeba Corpus · CC BY 2.0 FR"
            )
            entry["exampleSourceURL"] = f"https://tatoeba.org/en/sentences/show/{candidate.sentence_id}"
            append_source(entry, TATOEBA_SOURCE)
            if candidate.is_cc0:
                cc0_added += 1
            else:
                attributed_added += 1
        else:
            entry["example"] = original_contextual_example(entry["word"], entry["definition"], supplement)
            entry["exampleSource"] = "Original app example"
            append_source(entry, ORIGINAL_EXAMPLE_SOURCE)
            original_added += 1

    if any(not entry.get("chinese", "").strip() for entry in entries):
        raise ValueError("Vocabulary enrichment left a Chinese definition empty")
    if any(not entry.get("example", "").strip() for entry in entries):
        raise ValueError("Vocabulary enrichment left an example sentence empty")
    if refresh_invalid_examples and any(not example_uses_word(entry) for entry in entries):
        raise ValueError("Vocabulary enrichment left an example that does not use its headword")

    return {
        "translationsAdded": translations_added,
        "cc0ExamplesAdded": cc0_added,
        "attributedExamplesAdded": attributed_added,
        "originalExamplesAdded": original_added,
        "invalidExamplesReplaced": invalid_examples_cleared,
    }


def vocabulary_inventory(entries: list[dict]) -> dict[str, int]:
    return {
        "recordCount": len(entries),
        "withChineseDefinition": sum(bool(entry.get("chinese", "").strip()) for entry in entries),
        "withEnglishExample": sum(bool(entry.get("example", "").strip()) for entry in entries),
        "chineseFromECDICT": sum(ECDICT_SOURCE in entry.get("sources", []) for entry in entries),
        "examplesFromTatoebaCC0": sum(
            entry.get("exampleSource") == "Tatoeba Corpus · CC0 1.0" for entry in entries
        ),
        "examplesFromTatoebaCCBY": sum(
            entry.get("exampleSource") == "Tatoeba Corpus · CC BY 2.0 FR" for entry in entries
        ),
        "originalExamples": sum(entry.get("exampleSource") == "Original app example" for entry in entries),
    }


def main() -> None:
    source_root = ROOT.parent
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--vocabulary",
        type=Path,
        default=APP / "Resources/ExpandedVocabulary.json",
    )
    parser.add_argument(
        "--ecdict-csv",
        type=Path,
        default=source_root / "tmp/open-lexicon/ecdict.csv",
    )
    parser.add_argument(
        "--tatoeba-cc0",
        type=Path,
        default=source_root / "tmp/open-corpus/eng_sentences_CC0.tsv.bz2",
    )
    parser.add_argument(
        "--tatoeba-english",
        type=Path,
        default=source_root / "tmp/open-corpus/eng_sentences.tsv.bz2",
    )
    parser.add_argument(
        "--refresh-original-examples",
        action="store_true",
        help="Regenerate entries previously labeled as original app examples.",
    )
    parser.add_argument(
        "--refresh-invalid-examples",
        action="store_true",
        help="Replace examples that do not contain their vocabulary headword.",
    )
    args = parser.parse_args()

    entries = json.loads(args.vocabulary.read_text(encoding="utf-8"))
    summary = enrich_vocabulary_entries(
        entries,
        args.ecdict_csv,
        args.tatoeba_cc0,
        args.tatoeba_english,
        refresh_original_examples=args.refresh_original_examples,
        refresh_invalid_examples=args.refresh_invalid_examples,
    )
    args.vocabulary.write_text(json.dumps(entries, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    manifest_path = args.vocabulary.parent / "ContentManifest.json"
    if manifest_path.exists():
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        manifest["vocabularyCompleteness"] = vocabulary_inventory(entries)
        manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
