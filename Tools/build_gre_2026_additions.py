#!/usr/bin/env python3
"""Build GRE2026Additions.json: Analyze an Issue prompts and Select-in-Passage reading sets.

Inputs (original content written for this app) live in Tools/content2026/:
  issue_prompt_*.json       {"id", "instruction": 1-6, "statement"}
  select_in_passage_*.json  {"id", "difficulty", "passage", "questions": [...]}
"""

from __future__ import annotations

import glob
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CONTENT = ROOT / "Tools" / "content2026"
OUTPUT = ROOT / "Gre Simulation" / "Resources" / "GRE2026Additions.json"
SOURCE = {"title": "Original app question bank", "detail": "Written for this app in the current GRE General Test format; not an ETS question.", "isAuthorizedSourceItem": False}

# The six official Analyze an Issue instruction sets (ETS GRE "Analyze an Issue" page).
OFFICIAL = {
    "statement": "Write a response in which you discuss the extent to which you agree or disagree with the statement and explain your reasoning for the position you take. In developing and supporting your position, you should consider ways in which the statement might or might not hold true and explain how these considerations shape your position.",
    "recommendation": "Write a response in which you discuss the extent to which you agree or disagree with the recommendation and explain your reasoning for the position you take. In developing and supporting your position, describe specific circumstances in which adopting the recommendation would or would not be advantageous and explain how these examples shape your position.",
    "claim": "Write a response in which you discuss the extent to which you agree or disagree with the claim. In developing and supporting your position, be sure to address the most compelling reasons and/or examples that could be used to challenge your position.",
    "views": "Write a response in which you discuss which view more closely aligns with your own position and explain your reasoning for the position you take. In developing and supporting your position, you should address both of the views presented.",
    "claim_reason": "Write a response in which you discuss the extent to which you agree or disagree with the claim and the reason on which that claim is based.",
    "policy": "Write a response in which you discuss your views on the policy and explain your reasoning for the position you take. In developing and supporting your position, you should consider the possible consequences of implementing the policy and explain how these consequences shape your position.",
}
# Writer instruction codes -> official instruction set.
INSTRUCTIONS = {1: OFFICIAL["statement"], 2: OFFICIAL["recommendation"], 3: OFFICIAL["statement"], 4: OFFICIAL["views"],
                5: OFFICIAL["claim"], 6: OFFICIAL["claim_reason"], 7: OFFICIAL["policy"]}


def load(kind: str) -> list[dict]:
    rows: list[dict] = []
    for path in sorted(glob.glob(str(CONTENT / f"{kind}_*.json"))):
        rows += json.loads(Path(path).read_text(encoding="utf-8"))
    return rows


def question(id, difficulty, fmt, prompt, *, stimulus=None, groups=(), correct=None, explanation="", area="Reading Comprehension", **extra):
    row = {
        "id": id, "measure": "verbal", "difficulty": difficulty, "format": fmt, "stimulus": stimulus,
        "prompt": prompt, "quantityA": None, "quantityB": None, "groups": list(groups),
        "correctSelections": correct or {}, "acceptedNumericAnswers": [], "explanation": explanation,
        "contentArea": area, "source": SOURCE,
    }
    row.update(extra)
    return row


items: list[dict] = []
for row in load("issue_prompt"):
    items.append({
        "id": f"aw-{row['id']}", "measure": "analyticalWriting", "difficulty": "medium", "format": "essay",
        "stimulus": None, "prompt": row["statement"], "quantityA": None, "quantityB": None, "groups": [],
        "correctSelections": {}, "acceptedNumericAnswers": [],
        "explanation": "Scored 0–6 with the official Analyze an Issue scoring guide: position, development, support, organization, and language control.",
        "contentArea": "Analyze an Issue", "source": SOURCE, "instructions": INSTRUCTIONS[row["instruction"]],
    })

for row in load("select_in_passage"):
    passage = row["passage"].strip()
    sentences = [s.strip() for s in re.split(r"(?<=[.?!])\s+", passage) if s.strip()]
    for index, q in enumerate(row["questions"]):
        id = f"{row['id']}-{index + 1}"
        common = dict(stimulus=passage, explanation=q["explanation"], setID=row["id"])
        if q["type"] == "select_sentence":
            options = [{"id": f"s{i}", "text": s} for i, s in enumerate(sentences)]
            key = sentences.index(q["sentence"].strip())
            items.append(question(id, row["difficulty"], "selectInPassage", q["prompt"],
                                  groups=[{"id": "main", "title": None, "options": options, "maximumSelections": 1}],
                                  correct={"main": [f"s{key}"]}, **common))
        elif q["type"] == "single":
            options = [{"id": f"o{i}", "text": t} for i, t in enumerate(q["options"])]
            items.append(question(id, row["difficulty"], "singleChoice", q["prompt"],
                                  groups=[{"id": "main", "title": None, "options": options, "maximumSelections": 1}],
                                  correct={"main": [f"o{q['answer']}"]}, **common))
        else:
            options = [{"id": f"o{i}", "text": t} for i, t in enumerate(q["options"])]
            items.append(question(id, row["difficulty"], "multipleChoice", q["prompt"],
                                  groups=[{"id": "main", "title": None, "options": options, "maximumSelections": 3}],
                                  correct={"main": [f"o{a}" for a in q["answers"]]}, **common))

OUTPUT.write_text(json.dumps(items, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
print(f"{len(items)} items")
