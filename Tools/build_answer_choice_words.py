#!/usr/bin/env python3
"""Build the Answer-choice words flash-card deck from its reviewed source files.

`answer_choice_words/words.json` lists each word that appears often as a Text Completion or
Sentence Equivalence option (inflected forms counted with the base word, basic words left out),
plus a few opposites from the word bank. Words missing from the bank carry their own gloss.
`answer_choice_words/groups.json` sorts the words into meaning groups of one or two opposite poles.

Writes `Resources/AnswerChoiceWords.json` (groups + counts) and
`Resources/AnswerChoiceVocabulary.json` (the new words as vocabulary entries). After adding
words, render their pronunciations with `generate_word_pronunciations.py`.
"""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RESOURCES = ROOT / "Gre Simulation" / "Resources"
SOURCE = Path(__file__).with_name("answer_choice_words")


def write(path: Path, value) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")


def main() -> None:
    groups = json.loads((SOURCE / "groups.json").read_text(encoding="utf-8"))
    words = json.loads((SOURCE / "words.json").read_text(encoding="utf-8"))
    counts = {w["word"].lower(): w["count"] for w in words}
    write(RESOURCES / "AnswerChoiceWords.json", {"groups": groups, "counts": dict(sorted(counts.items()))})
    new = [
        {
            "word": w["word"], "pronunciation": w["pronunciation"], "definition": w["definition"],
            "chinese": w["chinese"], "synonyms": [], "example": w["example"],
            "exampleSource": "Original app example", "sources": ["GRE answer choices"],
            "isHighFrequency": False, "partOfSpeech": w["partOfSpeech"],
        }
        for w in sorted((w for w in words if "chinese" in w), key=lambda w: w["word"].lower())
    ]
    write(RESOURCES / "AnswerChoiceVocabulary.json", new)
    print(f"{len(groups)} groups, {len(words)} words, {len(new)} new vocabulary entries")


if __name__ == "__main__":
    main()
