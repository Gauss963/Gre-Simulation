#!/usr/bin/env python3
"""Apply the written GRE Verbal explanations to the bundled question JSON.

Many imported Verbal items had only a boilerplate explanation that stated the answer.
`reviewed_verbal_explanations.json` maps question ids to a real explanation in Taiwan
Traditional Chinese (the clue words and logic for Text Completion and Sentence Equivalence;
the evidence and the tempting distractor for Reading). Run this after regenerating any
question resource so the explanations are kept.
"""

from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RESOURCES = ROOT / "Gre Simulation" / "Resources"
FILES = ["ExpandedQuestions.json", "Authorized20260720Questions.json", "Authorized20260720Part2Questions.json", "GRE2026Additions.json"]


def main() -> None:
    reviewed = json.loads(Path(__file__).with_name("reviewed_verbal_explanations.json").read_text(encoding="utf-8"))
    applied = 0
    for name in FILES:
        path = RESOURCES / name
        raw = path.read_text(encoding="utf-8")
        questions = json.loads(raw)
        changed = False
        for question in questions:
            explanation = reviewed.get(question["id"])
            if explanation and question.get("explanation") != explanation:
                question["explanation"] = explanation
                applied += 1
                changed = True
        if changed:
            indent = 1 if raw.startswith("[\n {") else 2
            path.write_text(json.dumps(questions, ensure_ascii=False, indent=indent) + "\n", encoding="utf-8")
    print(f"Applied {applied} of {len(reviewed)} reviewed Verbal explanations.")


if __name__ == "__main__":
    main()
