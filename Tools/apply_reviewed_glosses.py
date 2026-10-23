#!/usr/bin/env python3
"""Apply the manually reviewed GRE glosses to the bundled vocabulary JSON.

`reviewed_vocabulary_glosses.json` maps each lowercased headword to its part
of speech, English definition, and Taiwan Traditional Chinese senses (ordered
by how often the GRE tests them, separated by "；", with n./v./adj. group
labels for words with several parts of speech). Run this after regenerating
any vocabulary resource so the corrections are kept.
"""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RESOURCES = ROOT / "Gre Simulation" / "Resources"
FILES = ["ExpandedVocabulary.json", "Authorized20260720Vocabulary.json", "Authorized20260720Part2Vocabulary.json"]


def main() -> None:
    reviewed = json.loads((Path(__file__).with_name("reviewed_vocabulary_glosses.json")).read_text(encoding="utf-8"))
    missing: set[str] = set()
    for name in FILES:
        path = RESOURCES / name
        entries = json.loads(path.read_text(encoding="utf-8"))
        for entry in entries:
            key = entry["word"].strip().lower()
            review = reviewed.get(key)
            if review is None:
                missing.add(key)
                continue
            entry["partOfSpeech"] = review["partOfSpeech"]
            entry["chinese"] = review["chinese"]
            entry["definition"] = review["definition"]
        path.write_text(json.dumps(entries, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Applied {len(reviewed)} reviewed glosses; {len(missing)} headwords still need review.")


if __name__ == "__main__":
    main()
