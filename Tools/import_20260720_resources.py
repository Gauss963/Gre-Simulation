#!/usr/bin/env python3
"""Import the authorized 2026-07-20 GRE archive into audited app resources.

The source PDFs are image-only. Run ``vision_ocr.swift`` first, then pass the
resulting JSON directory to this tool. Only questions with a complete prompt,
complete choices, and an answer that can be mapped without guessing are kept.
"""

from __future__ import annotations

import argparse
import collections
import csv
import json
import re
from dataclasses import dataclass
from pathlib import Path

try:
    from opencc import OpenCC
except ImportError as error:  # pragma: no cover - actionable CLI failure
    raise SystemExit(
        "Install opencc-python-reimplemented or add it to PYTHONPATH before importing."
    ) from error


ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "Gre Simulation"
RESOURCES = APP / "Resources"
TAIWAN = OpenCC("s2twp")

CHOICE_LINE = re.compile(r"^([A-I])[\.．]\s*(.*)$")
QUESTION_LINE = re.compile(r"^(10|[1-9])[\.．]\s*(.*)$")
SECTION_LINE = re.compile(r"section\s+(\d+)\s+(easy|median|medium|hard)", re.I)
BOOK_SOURCE = "GRE Contextual Vocabulary · authorized copy"
FILL_SOURCE = "GRE 填空機經 1250 題 · 授權內容"
READING_SOURCE = "老肖新 GRE 閱讀 250 篇 · 授權內容"
QUANT_SOURCE = "猴哥 GRE 數學難題 112 題 · 授權內容"
DUMMIES_SOURCE = "The GRE Test For Dummies · authorized copy"
POWERPACK_QUANT_SOURCE = "Official GRE Quantitative Reasoning Practice Questions · authorized copy"
LINDUXI_SOURCE = "麟渡兮 GRE 填空 · 授權內容"
NORTH_AMERICAN_ESSAY_SOURCE = "北美 GRE 範文精講 · 授權內容"
ORIGINAL_EXAMPLE_SOURCE = "Original app examples"

# Dense mathematical layouts were reviewed item by item. Questions outside
# this allowlist stay in the audit instead of being repaired from uncertain OCR.
POWERPACK_QA_ALLOWLIST = {
    (1, 6), (1, 8), (1, 13), (1, 14), (1, 17),
    (2, 16), (2, 17), (3, 3), (4, 8), (5, 11),
    (5, 21), (6, 6), (6, 13), (7, 19),
}
QUANT112_QA_ALLOWLIST = {3, 8, 28, 32, 38}
QUANT112_EXPLANATIONS = {
    3: "設史密斯先生與太太在 1988 年的收入分別為 X、Y，且 X > Y。1989 年合計收入為 X(1-p%)+Y(1+p%)=X+Y-p%(X-Y)，因 p>0 且 X-Y>0，所以 1989 年合計收入較低，Quantity A 較大。",
    8: "兩台機器每年都減損原始價值的 10%。三年後 X 的價值為 V-3(0.1V)=0.7V；六年後 Y 的價值為 2V-6(0.2V)=0.8V，因此 Quantity B 較大。",
    28: "連續整數形成等差數列，其平均數與中位數都等於首項與末項的平均，因此兩個量相等。",
    32: "要求包含 F 的三字母子集，只需從其餘 5 個字母選 2 個，共有 C(5,2)=10 種，因此兩個量相等。",
    38: "圓的周長與半徑成正比，而曲率是半徑的倒數。周長 35 公尺的圓半徑較小、曲率較大，因此 Quantity A 較大。",
}

CONTEXTUAL_OVERRIDES = {
    "abstained": ("戒除；避免；棄權", "Several members abstained from voting because of a conflict of interest."),
    "aristocratic": ("貴族的；有貴族氣派的", "The portrait emphasizes the subject's aristocratic bearing and formal dress."),
    "assiduously": ("勤勉地；專心仔細地", "She worked assiduously to verify every citation in the report."),
    "autonomous": ("自主的；自治的", "The laboratory remained autonomous despite its partnership with the university."),
    "brimming": ("充滿的；滿溢的", "The archive is brimming with letters that illuminate the author's early career."),
    "bulbous": ("球狀的；圓鼓的", "The plant stores water in a bulbous stem during the dry season."),
    "chastisement": ("責罵；懲戒", "Public chastisement only made the reluctant employee more defensive."),
    "coerced": ("被強迫的；受脅迫的", "The witness insisted that the confession had been coerced."),
    "credulousness": ("輕信；易受騙", "The scheme succeeded because it exploited the investors' credulousness."),
    "derogatory": ("貶抑的；侮辱的", "The editor removed the derogatory remark from the final article."),
    "despondent": ("沮喪的；失去希望的", "After repeated setbacks, the once-confident team became despondent."),
    "disconcerting": ("令人不安的；令人困惑的", "The sudden silence was more disconcerting than the earlier argument."),
    "divulged": ("已洩露；已透露", "The official divulged the findings only after the review was complete."),
    "eccentricity": ("古怪之處；反常行為", "His habit of annotating menus was a harmless eccentricity."),
    "ensued": ("隨後發生；接著出現", "A lengthy debate ensued after the committee announced its proposal."),
    "entails": ("需要；使成為必要", "Restoring the bridge entails years of careful engineering work."),
    "entreated": ("已懇求；已乞求", "Residents entreated the council to preserve the historic market."),
    "epitaph": ("墓誌銘；紀念短文", "The brief epitaph captures the poet's wit and humility."),
    "furtively": ("偷偷地；祕密地", "The student glanced furtively at the clock during the lecture."),
    "gingerly": ("小心翼翼地；謹慎地", "The conservator gingerly lifted the fragile manuscript from its case."),
    "gouge": ("敲竹槓；挖出", "Reputable vendors do not gouge customers during an emergency."),
    "hereditary": ("遺傳的；世襲的", "Researchers found that the disorder was hereditary rather than contagious."),
    "hiatus": ("中斷；間隙", "The journal returned after a two-year publication hiatus."),
    "histrionics": ("誇張的情緒表現；裝腔作勢", "Her calm rebuttal ended the debate without unnecessary histrionics."),
    "incisively": ("敏銳而有力地", "The reviewer incisively identified the argument's central weakness."),
    "inconspicuous": ("不顯眼的；不引人注意的", "The sensor is small enough to remain inconspicuous on the wall."),
    "indignation": ("憤慨；義憤", "The unfair accusation provoked widespread indignation among the researchers."),
    "infamy": ("惡名；聲名狼藉", "The failed expedition passed into infamy because its leaders ignored repeated warnings."),
    "jovially": ("愉快地；快活地", "The host jovially welcomed every guest at the door."),
    "liberty": ("自由；自主權", "The constitution protects individual liberty while permitting reasonable regulation."),
    "lucidity": ("清晰；明澈", "The essay's lucidity makes a difficult theory accessible."),
    "morass": ("困境；泥淖", "The simple dispute became a legal morass after several appeals."),
    "prevarication": ("支吾其詞；閃爍其辭", "Repeated prevarication damaged the spokesperson's credibility."),
    "proclaimed": ("已宣告；已正式宣布", "The panel proclaimed the experiment a success after reviewing the data."),
    "prone": ("容易有某種傾向的；俯臥的", "Coastal roads are prone to flooding during severe storms."),
    "protracting": ("拖延；延長", "Further procedural objections were merely protracting the negotiations."),
    "quibbles": ("吹毛求疵的異議；遁詞", "Minor quibbles should not obscure the proposal's broad merits."),
    "rejoinder": ("機敏的回答；反駁", "Her concise rejoinder exposed the weakness of the criticism."),
    "resilient": ("有韌性的；能迅速復原的", "The resilient ecosystem recovered quickly after the drought."),
    "sentient": ("有知覺的；能感受的", "The novel asks whether an artificial intelligence could become sentient."),
    "stammered": ("已結巴地說；已口吃", "Surprised by the accusation, he stammered an incomplete reply."),
    "stolidity": ("冷漠；遲鈍；不動聲色", "The guard's stolidity concealed considerable anxiety."),
    "stymied": ("受阻的；被妨礙的", "The investigation was stymied by incomplete and contradictory records."),
    "subsided": ("已減弱；已平息", "The crowd resumed its discussion after the noise subsided."),
    "tome": ("厚重的大書；學術鉅著", "The historian's new tome examines three centuries of regional trade."),
    "unabated": ("未減弱的；持續強烈的", "Public interest in the discovery continued unabated for months."),
    "unassuming": ("謙遜的；不張揚的", "The unassuming scientist rarely mentioned her many awards."),
    "undaunted": ("不畏懼的；勇敢堅定的", "Undaunted by the initial rejection, the team revised its proposal."),
    "vacated": ("已離開；已空出", "The tenants vacated the building before repairs began."),
    "vacillated": ("已猶豫不決；已搖擺", "The committee vacillated between caution and rapid reform."),
    "venerated": ("受敬重的；受崇敬的", "The venerated scholar continued teaching well into retirement."),
    "vicariously": ("間接地；設身處地地", "Readers experience the explorer's discoveries vicariously through her journals."),
    "vying": ("競爭中的；爭取中的", "Several laboratories are vying to reproduce the surprising result."),
}


@dataclass
class ParsedBlock:
    section: int
    number: int
    page: int
    difficulty: str
    lines: list[str]


def read_json(path: Path):
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)


def write_json(path: Path, value) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def normalized_space(text: str) -> str:
    text = text.replace("—", " ").replace("–", " ")
    text = re.sub(r"\s+", " ", text)
    return text.strip(" -\t\n")


def taiwan_text(text: str) -> str:
    converted = TAIWAN.convert(normalized_space(text))
    return re.sub(r"(?<=[\u3400-\u9fff])\s+(?=[\u3400-\u9fff])", "", converted)


def clean_taiwan_explanation(text: str) -> str:
    lines = [
        line for line in text.splitlines()
        if not re.search(r"ShareWith|sharewithu|Generated by|Foxit|www\.|本文章由", line, re.I)
    ]
    return taiwan_text("\n".join(lines))


def content_signature(question: dict) -> str:
    fields = [question.get("stimulus") or "", question.get("prompt") or ""]
    return re.sub(r"[^a-z0-9]+", "", " ".join(fields).lower())


def parse_answer_key(path: Path) -> dict[tuple[int, int], str]:
    text = path.read_text(encoding="utf-8", errors="ignore")
    sections = list(re.finditer(r"section\s+(\d+)\s+参考答案", text, re.I))
    answers: dict[tuple[int, int], str] = {}
    for index, match in enumerate(sections):
        section = int(match.group(1))
        end = sections[index + 1].start() if index + 1 < len(sections) else len(text)
        body = text[match.end():end]
        ranges = re.findall(r"(?:1-5|6-10)\s+([A-I/\s]+)", body)
        values: list[str] = []
        for group in ranges:
            values.extend(re.findall(r"[A-I]+", group))
        if len(values) == 10:
            for number, value in enumerate(values, 1):
                answers[(section, number)] = value
    return answers


def fill_difficulty(section: int) -> str:
    if section in set(range(1, 16)) | set(range(51, 63)) | {91, 92, 93, 101, 121}:
        return "easy"
    if section in set(range(36, 51)) | set(range(79, 91)) | {98, 99, 100, 104, 105, 106, 108, 113, 115, 118, 119, 123}:
        return "hard"
    return "medium"


def extract_fill_blocks(pages: list[dict]) -> list[ParsedBlock]:
    blocks: list[ParsedBlock] = []
    current_section: int | None = None
    current_difficulty = "medium"
    for page in sorted(pages, key=lambda item: item["page"]):
        lines = [normalized_space(line) for line in page["text"].splitlines()]
        section_match = next((SECTION_LINE.search(line) for line in lines if SECTION_LINE.search(line)), None)
        if section_match:
            current_section = int(section_match.group(1))
            current_difficulty = fill_difficulty(current_section)
        if current_section is None:
            continue

        starts = [(index, QUESTION_LINE.match(line)) for index, line in enumerate(lines)]
        starts = [(index, match) for index, match in starts if match]
        for position, (start, match) in enumerate(starts):
            end = starts[position + 1][0] if position + 1 < len(starts) else len(lines)
            block_lines = [match.group(2)] + lines[start + 1:end]
            blocks.append(
                ParsedBlock(
                    section=current_section,
                    number=int(match.group(1)),
                    page=int(page["page"]),
                    difficulty=current_difficulty,
                    lines=block_lines,
                )
            )
    return blocks


def clean_fill_block(block: ParsedBlock) -> tuple[str, dict[str, str]]:
    prompt_lines: list[str] = []
    choices: dict[str, str] = {}
    active_choice: str | None = None
    for raw_line in block.lines:
        line = normalized_space(raw_line)
        if not line or re.search(r"(?:GREt|HIZGRE|HEGRE|HIGRE|414:|\$\s*\d+\s*d$)", line, re.I):
            continue
        choice_match = CHOICE_LINE.match(line)
        if choice_match:
            active_choice = choice_match.group(1)
            choices[active_choice] = normalized_space(choice_match.group(2))
            continue
        if active_choice and len(line.split()) <= 7 and line[0].islower():
            choices[active_choice] = normalized_space(f"{choices[active_choice]} {line}")
        else:
            prompt_lines.append(line)

    prompt = normalized_space(" ".join(prompt_lines))
    prompt = re.sub(r"\((i{1,3})\)[\._\s-]*", lambda match: f"({match.group(1)}) ______ ", prompt)
    prompt = re.sub(r"(?<!\w)[_—-]{1,}(?!\w)", "______", prompt)
    prompt = re.sub(r"(?:______\s*){2,}", "______ ", prompt)
    return normalized_space(prompt), choices


def repair_fill_prompt(block: ParsedBlock, prompt: str) -> str:
    key = (block.section, block.number)
    if key == (4, 10):
        prompt = prompt.replace(
            "suffer from subject matter",
            "suffer from ______ subject matter",
        )
        prompt = re.sub(r"\s+IGRE\s+______.*$", "", prompt)
    elif key == (6, 10):
        prompt = prompt.replace("widespread appear in China", "widespread appeal in China")
        prompt = prompt.replace("communication researches", "communication researchers")
    elif key == (41, 10):
        prompt = prompt.replace("all of its practitioners ______", "all of its practitioners are ______")
    elif key == (49, 10):
        prompt = prompt.replace("Chuang existence", "Chuang ______ the existence")
        prompt = re.sub(r"\s+44:\s*3GRE.*$", "", prompt)
    elif key == (71, 5):
        prompt = prompt.replace("Hong Kong's,", "Hong Kong,")
        prompt = prompt.replace("Given So's _many", "Given So's (iii) ______ many")
        prompt = re.sub(r"\s+GRE\s+______.*$", "", prompt)
    elif key == (86, 6):
        prompt = prompt.replace(
            "were characteristically the literary form",
            "were characteristically (i) ______ the literary form",
        )
        prompt = prompt.replace("interpreted as (i) ______", "interpreted as (ii) ______")
        prompt = prompt.replace("authorship (ii) ______", "authorship (iii) ______")
        prompt = re.sub(r"\s+IGRE\s+______.*$", "", prompt)
    elif key == (111, 6):
        prompt = (
            "There is no sense trying to rehabilitate the reputation of the mosquito; nobody loves such a creature. "
            "But it's (i) ______ to (ii) ______ all 2,600 described species of mosquito when it's just 80 or so, "
            "about 3 percent, that drink human blood. Among those 2,520 relatively (iii) ______ mosquitoes, there's even "
            "one we'd like to see in greater numbers: Taxorhynchites, the mosquito that eats other mosquitoes."
        )
    elif key == (22, 6):
        prompt = prompt.replace(": (iT). (iii)", ": (iii)")

    prompt = re.sub(r"(?<![\w(])i\)\s*", "(i) ", prompt)
    prompt = re.sub(r"\s+44:\s*3GRE.*$", "", prompt)
    prompt = re.sub(
        r"\s+(?:(?:[#\$]|[A-Za-z\u0400-\u04ff])\s*)?\d+(?:\s+\d+)?(?:\s+[A-Za-z0-9\u0400-\u04ff])?\s*$",
        "",
        prompt,
    )
    prompt = re.sub(r"\bI?GRE\b\s*", "", prompt)
    prompt = prompt.replace("@ ______", "______")
    if key == (49, 5):
        prompt = re.sub(r"field conditions are\s+______\s*$", "field conditions are (iii) ______", prompt)
        prompt = prompt.replace("(i) ______ One reason", "(i) ______. One reason")
    if key == (111, 6):
        prompt = prompt.replace("80 or so 3 percent that", "80 or so, about 3 percent, that")
    # Remove a thin remnant of the printed underline that Vision sometimes
    # leaves immediately beside an otherwise recovered blank.
    prompt = prompt.replace("______ _", "______ ").replace("_ ______", " ______")
    prompt = re.sub(
        r"(?:\([iItT*\".]{1,5}\)?|(?<!\w)(?:i{1,3}|0))[)._*]*\s*(?=\((?:i|ii|iii)\)\s*______)",
        "",
        prompt,
        flags=re.I,
    )
    prompt = re.sub(r"\s+([,.;:?!])", r"\1", prompt)
    return normalized_space(prompt)


def make_fill_question(block: ParsedBlock, answer: str) -> dict | None:
    prompt, choices = clean_fill_block(block)
    prompt = repair_fill_prompt(block, prompt)
    if len(prompt.split()) < 7 or not re.search(r"_|\(i", prompt, re.I):
        return None
    if re.search(r"[#@]|44:|\bGRE\b|[\u0400-\u04ff]", prompt):
        return None
    if not choices:
        return None
    highest = max(choices)
    expected = list("ABCDEFGHI"[: ord(highest) - ord("A") + 1])
    if list(sorted(choices)) != expected or any(not choices[letter] for letter in expected):
        return None
    if any(letter not in choices for letter in answer):
        return None

    blank_markers = set(re.findall(r"\((i{1,3})\)", prompt, re.I))
    if len(choices) == 9 and len(answer) == 3:
        group_count = 3
    elif len(choices) == 6 and len(answer) == 2 and blank_markers:
        group_count = 2
    elif len(blank_markers) >= 2:
        group_count = len(blank_markers)
    else:
        group_count = 1
    if group_count > 1:
        marker_names = ["i", "ii", "iii"]
        parts = prompt.split("______")
        if len(parts) - 1 == group_count:
            rebuilt = parts[0]
            for marker_number, tail in enumerate(parts[1:]):
                # OCR commonly turns (ii)/(iii) into 0), (il), (ili), or drops
                # the label altogether. Since the answer-table width establishes
                # the blank count, number each blank deterministically by order.
                rebuilt = re.sub(
                    r"\s*\([iIlLtT01*\".]{1,5}\)?[\"'.:]*\s*$",
                    " ",
                    rebuilt,
                    flags=re.I,
                )
                rebuilt = re.sub(
                    r"\s+(?:(?:i{1,3}|il|ili|li|l{1,3}|1{1,3}|0))[\)\"'.:]?\s*$",
                    " ",
                    rebuilt,
                    flags=re.I,
                )
                rebuilt += f"({marker_names[marker_number]}) ______"
                rebuilt += tail
            prompt = normalized_space(rebuilt)
    if prompt.count("______") != group_count:
        return None
    if group_count > 1:
        expected_markers = [f"({name})" for name in marker_names[:group_count]]
        marker_positions = [prompt.find(marker) for marker in expected_markers]
        if any(position < 0 for position in marker_positions) or marker_positions != sorted(marker_positions):
            return None
        if any(prompt.count(marker) != 1 for marker in expected_markers):
            return None
    groups: list[dict] = []
    correct: dict[str, list[str]] = {}
    selected_words: list[str] = []

    if group_count >= 2:
        if len(choices) != group_count * 3 or len(answer) != group_count:
            return None
        for group_index in range(group_count):
            letters = expected[group_index * 3:(group_index + 1) * 3]
            answer_letter = answer[group_index]
            if answer_letter not in letters:
                return None
            group_id = f"blank{group_index}"
            options = [
                {"id": f"b{group_index}o{option_index}", "text": choices[letter]}
                for option_index, letter in enumerate(letters)
            ]
            groups.append({
                "id": group_id,
                "title": f"Blank ({['i', 'ii', 'iii'][group_index]})",
                "options": options,
                "maximumSelections": 1,
            })
            option_index = letters.index(answer_letter)
            correct[group_id] = [f"b{group_index}o{option_index}"]
            selected_words.append(choices[answer_letter])
        question_format = "textCompletion"
    elif len(answer) == 2 and len(choices) == 6:
        groups = [{
            "id": "main",
            "title": None,
            "options": [{"id": f"o{index}", "text": choices[letter]} for index, letter in enumerate(expected)],
            "maximumSelections": 2,
        }]
        correct = {"main": [f"o{expected.index(letter)}" for letter in answer]}
        selected_words = [choices[letter] for letter in answer]
        question_format = "sentenceEquivalence"
    elif len(answer) == 1 and len(choices) == 5:
        groups = [{
            "id": "blank0",
            "title": None,
            "options": [{"id": f"b0o{index}", "text": choices[letter]} for index, letter in enumerate(expected)],
            "maximumSelections": 1,
        }]
        correct = {"blank0": [f"b0o{expected.index(answer)}"]}
        selected_words = [choices[answer]]
        question_format = "textCompletion"
    else:
        return None

    quoted = "、".join(f"“{word}”" for word in selected_words)
    return {
        "id": f"20260720-fill-s{block.section:03d}-q{block.number:02d}",
        "measure": "verbal",
        "difficulty": block.difficulty,
        "format": question_format,
        "stimulus": None,
        "prompt": prompt,
        "quantityA": None,
        "quantityB": None,
        "groups": groups,
        "correctSelections": correct,
        "acceptedNumericAnswers": [],
        "explanation": f"正確答案為 {quoted}。這些選項能使句子的轉折、因果與語氣關係一致，並符合各空格的文法功能。",
        "contentArea": "Sentence Equivalence" if question_format == "sentenceEquivalence" else "Text Completion",
        "source": {
            "title": FILL_SOURCE,
            "detail": f"第 {block.section} 節 · 第 {block.number} 題 · PDF 第 {block.page} 頁",
            "isAuthorizedSourceItem": True,
        },
    }


def import_fill_questions(ocr_path: Path, answer_path: Path, existing: list[dict]) -> tuple[list[dict], dict]:
    answers = parse_answer_key(answer_path)
    blocks = extract_fill_blocks(read_json(ocr_path))
    existing_signatures = {content_signature(question) for question in existing}
    imported: list[dict] = []
    rejected = 0
    duplicate = 0
    seen: set[str] = set()
    for block in blocks:
        answer = answers.get((block.section, block.number))
        question = make_fill_question(block, answer) if answer else None
        if not question:
            rejected += 1
            continue
        signature = content_signature(question)
        if not signature or signature in existing_signatures or signature in seen:
            duplicate += 1
            continue
        seen.add(signature)
        imported.append(question)
    return imported, {
        "answerKeys": len(answers),
        "blocksDetected": len(blocks),
        "imported": len(imported),
        "rejectedAmbiguous": rejected,
        "duplicatesSkipped": duplicate,
    }


def passage_blocks(pages: list[dict]) -> list[dict]:
    blocks: list[dict] = []
    current: dict | None = None
    heading = re.compile(r"^Passage\s+(\d+)\s*$", re.I)
    for page in sorted(pages, key=lambda item: item["page"]):
        for line in page["text"].splitlines():
            match = heading.match(line.strip())
            if match:
                if current:
                    blocks.append(current)
                current = {"number": int(match.group(1)), "page": int(page["page"]), "lines": []}
            elif current:
                current["lines"].append(line)
    if current:
        blocks.append(current)
    return blocks


def parse_inline_choices(text: str) -> tuple[str, dict[str, str]]:
    matches = list(re.finditer(r"(?:(?<=\s)|^)([A-E])[\.．]\s*", text))
    if not matches:
        return normalized_space(text), {}
    prompt = normalized_space(text[:matches[0].start()])
    choices: dict[str, str] = {}
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        choices[match.group(1)] = normalized_space(text[match.end():end])
    return prompt, choices


def source_reading_questions(block: dict) -> list[dict]:
    lines = [normalized_space(line) for line in block["lines"] if normalized_space(line)]
    anchors = [
        (index, int(match.group(1)))
        for index, line in enumerate(lines)
        if (match := QUESTION_LINE.match(line))
    ]
    questions: list[dict] = []

    if anchors:
        stimulus = normalized_space(" ".join(lines[:anchors[0][0]]))
        for position, (start, number) in enumerate(anchors):
            end = anchors[position + 1][0] if position + 1 < len(anchors) else len(lines)
            first = QUESTION_LINE.match(lines[start])
            text = normalized_space(" ".join([first.group(2)] + lines[start + 1:end]))
            prompt, choices = parse_inline_choices(text)
            if choices:
                questions.append({"number": number, "stimulus": stimulus, "prompt": prompt, "choices": choices})
        return questions

    text = normalized_space(" ".join(lines))
    prompt_with_stimulus, choices = parse_inline_choices(text)
    if not choices:
        return []
    question_starts = list(re.finditer(
        r"(?i)(?:Which of the following|In the argument|The passage suggests|According to the passage|"
        r"It can be inferred|Consider each of the choices|Select the sentence)",
        prompt_with_stimulus,
    ))
    if not question_starts:
        return []
    start = question_starts[-1].start()
    return [{
        "number": 1,
        "stimulus": normalized_space(prompt_with_stimulus[:start]),
        "prompt": normalized_space(prompt_with_stimulus[start:]),
        "choices": choices,
    }]


def solution_question_groups(block: dict) -> dict[int, dict]:
    lines = [normalized_space(line) for line in block["lines"] if normalized_space(line)]
    anchors: list[tuple[int, int]] = []
    for index, line in enumerate(lines):
        match = re.match(r"^(10|[1-9])[\.、．]\s*(?!句)", line)
        if match:
            anchors.append((index, int(match.group(1))))
        elif re.match(r"^(?:問|问)[：:]", line):
            anchors.append((index, 1))
    groups: dict[int, dict] = {}
    ambiguous: set[int] = set()
    for position, (start, number) in enumerate(anchors):
        end = anchors[position + 1][0] if position + 1 < len(anchors) else len(lines)
        text = "\n".join(lines[start:end])
        answers = re.findall(
            r"(?im)^\s*([A-E])\s*[\.：:]\s*[^\n]{0,220}?(?:正確|正确)", text
        )
        answers = list(dict.fromkeys(answers))
        if not answers:
            continue
        explanation_match = re.search(r"(?:解析|說明|说明)[：:]\s*(.+?)(?=\n[A-E][\.：:]|$)", text, re.S)
        explanation = explanation_match.group(1) if explanation_match else ""
        if number in groups:
            ambiguous.add(number)
            continue
        groups[number] = {
            "answers": answers,
            "explanation": taiwan_text(explanation)[:1_200],
        }
    for number in ambiguous:
        groups.pop(number, None)
    return groups


def make_reading_question(
    ordinal: int,
    source_block: dict,
    parsed: dict,
    solution: dict,
) -> dict | None:
    choices = parsed["choices"]
    expected = list("ABCDE"[: len(choices)])
    if list(sorted(choices)) != expected or len(choices) not in {3, 5}:
        return None
    dangling_words = {"a", "an", "and", "any", "for", "from", "in", "of", "on", "or", "other", "than", "the", "to", "with"}
    if any(option.split()[-1].lower().strip(".,;:()") in dangling_words for option in choices.values() if option.split()):
        return None
    answers = solution["answers"]
    if not answers or any(answer not in choices for answer in answers):
        return None
    prompt = parsed["prompt"]
    stimulus = parsed["stimulus"]
    if len(prompt.split()) < 4 or len(stimulus.split()) < 20:
        return None

    maximum = len(answers) if len(answers) > 1 else 1
    question_format = "multipleChoice" if maximum > 1 else "singleChoice"
    correct_text = "、".join(f"{answer}（{choices[answer]}）" for answer in answers)
    explanation = solution["explanation"]
    if explanation:
        explanation = f"正確答案為 {correct_text}。{explanation}"
    else:
        explanation = f"正確答案為 {correct_text}。答案可由文章中的明示資訊、語意關係或作者立場推出。"

    level = ["easy", "medium", "hard"][(ordinal - 1) % 3]
    number = parsed["number"]
    return {
        "id": f"20260720-read-p{ordinal:03d}-q{number:02d}",
        "measure": "verbal",
        "difficulty": level,
        "format": question_format,
        "stimulus": stimulus,
        "prompt": prompt,
        "quantityA": None,
        "quantityB": None,
        "groups": [{
            "id": "main",
            "title": None,
            "options": [{"id": f"o{index}", "text": choices[letter]} for index, letter in enumerate(expected)],
            "maximumSelections": maximum,
        }],
        "correctSelections": {"main": [f"o{expected.index(answer)}" for answer in answers]},
        "acceptedNumericAnswers": [],
        "explanation": explanation,
        "contentArea": "Reading Comprehension",
        "source": {
            "title": READING_SOURCE,
            "detail": f"篇章 {source_block['number']}（匯入序號 {ordinal}）· 題目 PDF 第 {source_block['page']} 頁",
            "isAuthorizedSourceItem": True,
        },
    }


def import_reading_questions(
    question_ocr: Path,
    solution_ocr: Path,
    existing: list[dict],
) -> tuple[list[dict], dict]:
    sources = passage_blocks(read_json(question_ocr))
    solutions = passage_blocks(read_json(solution_ocr))
    existing_signatures = {content_signature(question) for question in existing}
    seen: set[str] = set()
    imported: list[dict] = []
    detected = 0
    rejected = 0
    duplicates = 0

    for ordinal, (source_block, solution_block) in enumerate(zip(sources, solutions), 1):
        parsed_questions = source_reading_questions(source_block)
        solution_groups = solution_question_groups(solution_block)
        detected += len(parsed_questions)
        for parsed in parsed_questions:
            solution = solution_groups.get(parsed["number"])
            question = make_reading_question(ordinal, source_block, parsed, solution) if solution else None
            if not question:
                rejected += 1
                continue
            signature = content_signature(question)
            if signature in existing_signatures or signature in seen:
                duplicates += 1
                continue
            seen.add(signature)
            imported.append(question)
    return imported, {
        "sourcePassagesDetected": len(sources),
        "solutionPassagesDetected": len(solutions),
        "questionBlocksDetected": detected,
        "imported": len(imported),
        "rejectedAmbiguous": rejected,
        "duplicatesSkipped": duplicates,
    }


def quant_numbered_blocks(pages: list[dict]) -> list[dict]:
    combined_parts: list[str] = []
    for page in sorted(pages, key=lambda item: item["page"]):
        combined_parts.append(f"\n[[PAGE {page['page']}]]\n{page['text']}")
    combined = "\n".join(combined_parts)
    endings = list(re.finditer(r"(?m)^\s*(\d{1,3})\s*\n\s*@([A-E])\s*$", combined))
    blocks: list[dict] = []
    previous_end = 0
    for match in endings:
        number = int(match.group(1))
        if not 1 <= number <= 112:
            previous_end = match.end()
            continue
        body = combined[previous_end:match.start()]
        page_matches = re.findall(r"\[\[PAGE (\d+)\]\]", body)
        blocks.append({
            "number": number,
            "answer": match.group(2),
            "page": int(page_matches[-1]) if page_matches else 0,
            "text": re.sub(r"\[\[PAGE \d+\]\]", "", body),
        })
        previous_end = match.end()
    unique: dict[int, dict] = {}
    for block in blocks:
        if block["number"] not in unique:
            unique[block["number"]] = block
    return [unique[number] for number in sorted(unique)]


def quant_solution_map(directory: Path) -> dict[int, str]:
    solutions: dict[int, str] = {}
    for path in sorted(directory.glob("quant112sol-*.json")):
        text = "\n".join(page["text"] for page in read_json(path))
        matches = list(re.finditer(r"(?im)^NO\s*(\d+)\s*$", text))
        for index, match in enumerate(matches):
            number = int(match.group(1))
            end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
            body = text[match.end():end]
            explanation = re.search(r"解答[：:]\s*(.+?)(?=\n考點[：:]|$)", body, re.S)
            if explanation:
                solutions[number] = clean_taiwan_explanation(explanation.group(1))[:1_200]
    return solutions


def quant_solution_blocks(directory: Path) -> list[dict]:
    blocks: list[dict] = []
    for path in sorted(directory.glob("quant112sol-*.json")):
        pages = read_json(path)
        combined = "\n".join(f"[[PAGE {page['page']}]]\n{page['text']}" for page in pages)
        matches = list(re.finditer(r"(?im)^NO\s*(\d+)\s*$", combined))
        for index, match in enumerate(matches):
            number = int(match.group(1))
            end = matches[index + 1].start() if index + 1 < len(matches) else len(combined)
            body = combined[match.end():end]
            english = re.split(r"(?m)^翻[譯译][：:]", body, maxsplit=1)[0]
            answer_match = re.search(r"@([A-D])", english)
            if not answer_match:
                continue
            english = english[:answer_match.start()]
            page_matches = re.findall(r"\[\[PAGE (\d+)\]\]", english)
            english = re.sub(r"\[\[PAGE \d+\]\]", "", english)
            blocks.append({
                "number": number,
                "answer": answer_match.group(1),
                "page": int(page_matches[-1]) if page_matches else 0,
                "text": english,
            })
    return blocks


def quant_area(text: str) -> str:
    lowered = text.lower()
    if re.search(r"triangle|circle|angle|coordinate|line |area|perimeter|volume", lowered):
        return "Geometry"
    if re.search(r"mean|median|probability|percent|data|standard deviation|range", lowered):
        return "Data Analysis"
    if re.search(r"equation|integer|factor|multiple|remainder|variable|function", lowered):
        return "Algebra"
    return "Arithmetic"


def make_quant_comparison(block: dict, explanation: str | None) -> dict | None:
    if block["answer"] not in "ABCD":
        return None
    text = block["text"]
    if not re.search(r"Column\s*A", text, re.I) or not re.search(r"Column\s*[BE]", text, re.I):
        return None
    if re.search(r"following\s+(?:graph|table|information)|distribution of|drawn to scale|figure represents", text, re.I):
        return None

    text = re.sub(r"(?im)^.*(?:H[EI] ?HF|ShareWith|www\.).*$", "", text)
    heading = re.search(r"Column\s*A.*?Column\s*[BE]", text, re.I | re.S)
    if not heading:
        return None
    stimulus = normalized_space(text[:heading.start()])
    comparison_text = text[heading.end():]
    quantity_a: list[str] = []
    quantity_b: list[str] = []
    unpaired_lines: list[str] = []
    for raw_line in comparison_text.splitlines():
        line = normalized_space(raw_line)
        if not line or line == str(block["number"]):
            continue
        parts = re.split(r"\s+______\s+", line, maxsplit=1)
        if len(parts) == 2 and parts[0] and parts[1]:
            quantity_a.append(parts[0])
            quantity_b.append(parts[1])
        else:
            unpaired_lines.append(line)
    if not quantity_a and len(unpaired_lines) >= 2:
        quantity_a = unpaired_lines[0::2]
        quantity_b = unpaired_lines[1::2]
    a_text = normalized_space(" ".join(quantity_a))
    b_text = normalized_space(" ".join(quantity_b))
    a_text = re.sub(r"^\d+\.\s*", "", a_text).lstrip("_ ")
    b_text = re.sub(r"^\d+\.\s*", "", b_text).lstrip("_ ")
    if a_text == "S" and re.search(r"\bs\b", stimulus):
        a_text = "s"
    if not a_text or not b_text or len(a_text) > 220 or len(b_text) > 220:
        return None
    if stimulus and len(stimulus.split()) < 3:
        stimulus = ""

    choices = [
        "Quantity A is greater.",
        "Quantity B is greater.",
        "The two quantities are equal.",
        "The relationship cannot be determined from the information given.",
    ]
    answer_index = "ABCD".index(block["answer"])
    detail = explanation or (
        f"正確答案為 {block['answer']}（{choices[answer_index]}）。比較時應檢查題目條件允許的所有數值，"
        "並確認關係是否在每一種合法情況下都保持不變。"
    )
    return {
        "id": f"20260720-quant112-q{block['number']:03d}",
        "measure": "quantitative",
        "difficulty": ["easy", "medium", "hard"][(block["number"] - 1) % 3],
        "format": "quantitativeComparison",
        "stimulus": stimulus or None,
        "prompt": "Compare Quantity A and Quantity B.",
        "quantityA": a_text,
        "quantityB": b_text,
        "groups": [{
            "id": "main",
            "title": None,
            "options": [{"id": f"o{index}", "text": choice} for index, choice in enumerate(choices)],
            "maximumSelections": 1,
        }],
        "correctSelections": {"main": [f"o{answer_index}"]},
        "acceptedNumericAnswers": [],
        "explanation": detail,
        "contentArea": quant_area(" ".join([stimulus, a_text, b_text])),
        "source": {
            "title": QUANT_SOURCE,
            "detail": f"第 {block['number']} 題 · PDF 第 {block['page']} 頁",
            "isAuthorizedSourceItem": True,
        },
    }


def import_quant_questions(directory: Path, existing: list[dict]) -> tuple[list[dict], dict]:
    source_blocks = quant_numbered_blocks(read_json(directory / "quant112.json"))
    preferred = {block["number"]: block for block in source_blocks}
    for block in quant_solution_blocks(directory):
        preferred[block["number"]] = block
    blocks = [preferred[number] for number in sorted(preferred)]
    solutions = quant_solution_map(directory)
    existing_signatures = {content_signature(question) for question in existing}
    imported: list[dict] = []
    rejected = 0
    duplicates = 0
    for block in blocks:
        if block["number"] not in QUANT112_QA_ALLOWLIST:
            rejected += 1
            continue
        if block["number"] == 28:
            block["text"] = block["text"].replace("consecufive", "consecutive").replace("mean of s", "mean of S")
        question = make_quant_comparison(block, QUANT112_EXPLANATIONS[block["number"]])
        if question and block["number"] == 3:
            question["stimulus"] = (
                "In 1988 Mr. Smith's annual income was greater than Mrs. Smith's annual income. "
                "In 1989 Mr. Smith's annual income decreased by p percent, whereas Mrs. Smith's annual income "
                "increased by p percent (p > 0)."
            )
            question["quantityA"] = "Mr. and Mrs. Smith's combined annual income in 1988"
        elif question and block["number"] == 32:
            question["stimulus"] = (
                "From the set of 6 letters A, B, C, D, E, and F, there are 20 different 3-letter subsets "
                "that could be selected."
            )
            question["quantityA"] = "The number of 3-letter subsets that include the letter F"
            question["quantityB"] = "10"
        elif question and block["number"] == 38:
            question["quantityA"] = "The curvature of a circle with circumference 35 meters"
            question["quantityB"] = "The curvature of a circle with circumference 36 meters"
        if not question:
            rejected += 1
            continue
        signature = content_signature(question)
        if signature in existing_signatures:
            duplicates += 1
            continue
        existing_signatures.add(signature)
        imported.append(question)
    return imported, {
        "numberedBlocksDetected": len(blocks),
        "sourceBlocksDetected": len(source_blocks),
        "solutionBlocksDetected": len(quant_solution_blocks(directory)),
        "detailedSolutionsDetected": len(solutions),
        "importedComparisons": len(imported),
        "rejectedMissingLayout": rejected,
        "duplicatesSkipped": duplicates,
    }


def import_dummies_writing(directory: Path, existing: list[dict]) -> tuple[list[dict], dict]:
    pages = read_json(directory / "dummies.json")
    prompts: list[tuple[str, int]] = []
    for page in pages:
        if "Analytical Writing: Present Your Perspective" not in page["text"]:
            continue
        matches = re.findall(
            r"Topic\s+[12]\s+[\"“](.*?)[\"”](?=\s*Topic|\s*Choose)",
            page["text"],
            re.I | re.S,
        )
        prompts.extend((normalized_space(prompt), int(page["page"])) for prompt in matches)

    signatures = {content_signature(question) for question in existing}
    imported: list[dict] = []
    for index, (prompt, page) in enumerate(prompts, 1):
        question = {
            "id": f"20260720-dummies-aw-{index:02d}",
            "measure": "analyticalWriting",
            "difficulty": "medium",
            "format": "essay",
            "stimulus": None,
            "prompt": prompt,
            "quantityA": None,
            "quantityB": None,
            "groups": [],
            "correctSelections": {},
            "acceptedNumericAnswers": [],
            "explanation": "先界定主張中的關鍵概念，再提出清楚立場，以具體理由與例子支持論點，並回應至少一項重要的限制或反方觀點。",
            "contentArea": "Analyze an Issue",
            "source": {
                "title": DUMMIES_SOURCE,
                "detail": f"Issue prompt · PDF 第 {page} 頁",
                "isAuthorizedSourceItem": True,
            },
        }
        signature = content_signature(question)
        if signature not in signatures:
            signatures.add(signature)
            imported.append(question)
    return imported, {"promptsDetected": len(prompts), "imported": len(imported)}


def writing_index_numbers(path: Path) -> set[int]:
    issue_section = path.read_text(encoding="utf-8", errors="ignore").split("Argument", 1)[0]
    numbers: set[int] = set()
    for line in issue_section.splitlines():
        if "：" in line:
            numbers.update(int(value) for value in re.findall(r"\d+", line.split("：", 1)[0]))
    return numbers


def import_north_american_writing(
    directory: Path,
    index_path: Path,
    existing: list[dict],
) -> tuple[list[dict], dict]:
    requested_numbers = writing_index_numbers(index_path)
    detected_numbers: set[int] = set()
    prompt_by_number: dict[int, str] = {}

    for path in sorted(directory.glob("aw-[1-4].json")):
        text = "\n".join(page["text"] for page in read_json(path))
        matches = list(re.finditer(r"(?im)^\s*Issue\s*(\d{1,3})\b", text))
        for position, match in enumerate(matches):
            number = int(match.group(1))
            if number not in requested_numbers:
                continue
            detected_numbers.add(number)
            end = matches[position + 1].start() if position + 1 < len(matches) else len(text)
            lines = text[match.end():end][:2_200].splitlines()
            english_only = "\n".join(
                line for line in lines
                if not re.search(r"[\u3400-\u9fff]", line)
                and not re.fullmatch(r"\s*\d+\s*", line)
            )
            quoted = re.search(r"“(.{20,1200}?)”", english_only, re.S)
            if not quoted:
                continue
            prompt = re.sub(r"(?<=[A-Za-z])-\s+(?=[a-z])", "", quoted.group(1))
            prompt = re.sub(r",(?=\S)", ", ", prompt)
            prompt = re.sub(r"’(?=[A-Za-z])", "’ ", prompt)
            prompt = normalized_space(prompt)
            prompt = prompt.replace("signifcance", "significance")
            if (
                8 <= len(prompt.split()) <= 100
                and not re.search(r"[\u3400-\u9fff]", prompt)
                and prompt.count("“") == prompt.count("”")
            ):
                prompt_by_number.setdefault(number, prompt)

    signatures = {content_signature(question) for question in existing}
    imported: list[dict] = []
    duplicates = 0
    for number, prompt in sorted(prompt_by_number.items()):
        question = {
            "id": f"20260720-north-american-aw-{number:03d}",
            "measure": "analyticalWriting",
            "difficulty": "medium",
            "format": "essay",
            "stimulus": None,
            "prompt": prompt,
            "quantityA": None,
            "quantityB": None,
            "groups": [],
            "correctSelections": {},
            "acceptedNumericAnswers": [],
            "explanation": "先界定命題中的關鍵概念，再提出清楚立場，以具體理由與例子支持論點，並回應至少一項重要的限制或反方觀點。",
            "contentArea": "Analyze an Issue",
            "source": {
                "title": NORTH_AMERICAN_ESSAY_SOURCE,
                "detail": f"新題庫對照 Issue {number}",
                "isAuthorizedSourceItem": True,
            },
        }
        signature = content_signature(question)
        if signature in signatures:
            duplicates += 1
            continue
        signatures.add(signature)
        imported.append(question)

    return imported, {
        "currentIssueNumbersIndexed": len(requested_numbers),
        "indexedNumbersDetectedInOCR": len(detected_numbers),
        "completeQuotedPromptsDetected": len(prompt_by_number),
        "imported": len(imported),
        "duplicatesSkipped": duplicates,
        "rejectedWithoutCompleteQuotedPrompt": len(requested_numbers - set(prompt_by_number)),
    }


def linduxi_answers(lines: list[str], source_index: int, block_text: str) -> list[str]:
    answer: list[str] = []
    next_source = next(
        (
            index for index, line in enumerate(lines[source_index + 1:], source_index + 1)
            if re.search(r"\b(?:OG|0G|PPII|PBT)\b", line, re.I)
        ),
        min(len(lines), source_index + 30),
    )
    for line in lines[source_index + 1:next_source]:
        if ":" not in line:
            continue
        tail = line.split(":", 1)[1]
        matches = re.findall(r"\(([A-I])\)|\b([A-I])\)", tail)
        letters = [left or right for left, right in matches]
        if 1 <= len(letters) <= 3:
            answer = letters
    if answer:
        return list(dict.fromkeys(answer))

    explicit: list[str] = []
    for sentence in re.findall(r"(?i)(?:correct answer|correct response).{0,320}", block_text):
        explicit.extend(re.findall(r"Choice\s+([A-I])", sentence, re.I))
    return list(dict.fromkeys(explicit))


def normalize_linduxi_prompt(prompt: str, group_count: int) -> str:
    prompt = re.sub(r"^\s*\d{1,3}\.\s*", "", prompt)
    prompt = prompt.split("Blank", 1)[0]
    prompt = re.sub(r"(?:_+\s*)+", "______ ", prompt)
    if group_count > 1:
        marker = re.compile(r"(?<!\w)(?:\((?P<inner>[il1t]{1,3})\)|(?P<bare>i{1,3})\))\s*(?:______\s*)?", re.I)
        marker_number = 0

        def replace_marker(_: re.Match) -> str:
            nonlocal marker_number
            names = ["i", "ii", "iii"]
            index = min(marker_number, group_count - 1, len(names) - 1)
            marker_number += 1
            return f"({names[index]}) ______ "

        prompt = marker.sub(replace_marker, prompt)
    prompt = re.sub(r"\s+([,.;:?!])", r"\1", prompt)
    prompt = prompt.replace("______ -", "______")
    return normalized_space(prompt)


def repair_linduxi_prompt(number: int, prompt: str) -> str:
    replacements = {
        13: [("more. ______", "more ______")],
        18: [("chlorofluorocarbons-implicated", "chlorofluorocarbons—implicated")],
        23: [("represent all sides no matter how marginal equally", "represent all sides—even marginal ones—equally")],
        25: [("valuation results", "valuation resulted")],
        36: [("______ uS:", "______ us:")],
        48: [("interpretations- not", "interpretations—not")],
        73: [("(i) ______ Indeed", "(i) ______. Indeed")],
        81: [("long- running", "long-running")],
        112: [("(iii) ______ Yet", "(iii) ______. Yet")],
        113: [("folio pages a fortune", "folio pages—a fortune")],
        121: [("that Romeo at", "that Romero at")],
        128: [("(i) ______ At a moment", "(i) ______. At a moment")],
    }
    for old, new in replacements.get(number, []):
        prompt = prompt.replace(old, new)
    prompt = prompt.lstrip(". ")
    if prompt and prompt[-1] not in ".?!’\"”":
        prompt += "."
    return prompt


def make_linduxi_question(number: int, block_text: str) -> dict | None:
    lines = block_text.splitlines()
    source_index = next(
        (index for index, line in enumerate(lines) if re.search(r"\b(?:OG|0G|PPII|PBT)\b", line, re.I)),
        None,
    )
    if source_index is None:
        return None
    question_text = "\n".join(lines[:source_index])
    option_matches = list(re.finditer(r"\(([A-I])\)\s*", question_text))
    if not option_matches:
        return None
    choices: dict[str, str] = {}
    for position, match in enumerate(option_matches):
        end = option_matches[position + 1].start() if position + 1 < len(option_matches) else len(question_text)
        value = normalized_space(question_text[match.end():end])
        value = re.sub(r"(?:\s*______\s*)+$", "", value).strip()
        choices[match.group(1)] = value

    expected = list("ABCDEFGHI"[: ord(max(choices)) - ord("A") + 1])
    if list(sorted(choices)) != expected or len(choices) not in {5, 6, 9}:
        return None
    if any(not value or len(value.split()) > 12 for value in choices.values()):
        return None

    answers = linduxi_answers(lines, source_index, block_text)
    if not answers or any(answer not in choices for answer in answers):
        return None
    raw_prompt = question_text[:option_matches[0].start()]
    prompt_body = raw_prompt.split("Blank", 1)[0]
    marker_count = len(re.findall(r"(?<!\w)(?:\([il1t]{1,3}\)|i{1,3}\))", prompt_body, re.I))

    if len(choices) == 9 and len(answers) == 3 and marker_count >= 3:
        group_count = 3
        question_format = "textCompletion"
    elif len(choices) == 6 and len(answers) == 2 and marker_count >= 2:
        group_count = 2
        question_format = "textCompletion"
    elif len(choices) == 6 and len(answers) == 2:
        group_count = 1
        question_format = "sentenceEquivalence"
    elif len(choices) == 5 and len(answers) == 1:
        group_count = 1
        question_format = "textCompletion"
    else:
        return None

    prompt = repair_linduxi_prompt(number, normalize_linduxi_prompt(raw_prompt, group_count))
    if prompt.count("______") != group_count or len(prompt.split()) < 7:
        return None

    groups: list[dict] = []
    correct: dict[str, list[str]] = {}
    if group_count > 1:
        for group_index in range(group_count):
            letters = expected[group_index * 3:(group_index + 1) * 3]
            answer = answers[group_index]
            if answer not in letters:
                return None
            group_id = f"blank{group_index}"
            groups.append({
                "id": group_id,
                "title": f"Blank ({['i', 'ii', 'iii'][group_index]})",
                "options": [
                    {"id": f"b{group_index}o{option_index}", "text": choices[letter]}
                    for option_index, letter in enumerate(letters)
                ],
                "maximumSelections": 1,
            })
            correct[group_id] = [f"b{group_index}o{letters.index(answer)}"]
    else:
        groups = [{
            "id": "main" if question_format == "sentenceEquivalence" else "blank0",
            "title": None,
            "options": [
                {"id": f"o{index}", "text": choices[letter]}
                for index, letter in enumerate(expected)
            ],
            "maximumSelections": len(answers),
        }]
        group_id = groups[0]["id"]
        correct[group_id] = [f"o{expected.index(answer)}" for answer in answers]

    correct_text = "、".join(f"{answer}（{choices[answer]}）" for answer in answers)
    explanation_match = re.search(
        r"(?is)(?:The correct|Looking at|The key phrase|The sentence|Since |Because |Although ).{80,1300}",
        "\n".join(lines[source_index + 1:]),
    )
    detail = normalized_space(explanation_match.group(0)) if explanation_match else ""
    if re.search(r"[•#@†]|\b(?:PPII|OG|PBT)\b", detail):
        detail = ""
    explanation = f"正確答案為 {correct_text}。"
    explanation += detail or "這些詞能使上下文的轉折、因果與語氣保持一致，且符合空格的文法功能。"

    difficulty_match = re.search(r"\b(Easy|Medium|Hard)\b", lines[source_index], re.I)
    difficulty = difficulty_match.group(1).lower() if difficulty_match else ["easy", "medium", "hard"][(number - 1) % 3]
    return {
        "id": f"20260720-linduxi-q{number:03d}",
        "measure": "verbal",
        "difficulty": difficulty,
        "format": question_format,
        "stimulus": None,
        "prompt": prompt,
        "quantityA": None,
        "quantityB": None,
        "groups": groups,
        "correctSelections": correct,
        "acceptedNumericAnswers": [],
        "explanation": explanation[:1_600],
        "contentArea": "Sentence Equivalence" if question_format == "sentenceEquivalence" else "Text Completion",
        "source": {
            "title": LINDUXI_SOURCE,
            "detail": f"解答本第 {number} 題 · {normalized_space(lines[source_index])}",
            "isAuthorizedSourceItem": True,
        },
    }


def import_linduxi_fill(ocr_path: Path, existing: list[dict]) -> tuple[list[dict], dict]:
    text = "\n".join(page["text"] for page in read_json(ocr_path))
    matches = list(re.finditer(r"(?m)^\s*(\d{1,3})\.\s+", text))
    existing_signatures = {content_signature(question) for question in existing}
    seen: set[str] = set()
    imported: list[dict] = []
    rejected = 0
    duplicates = 0
    for position, match in enumerate(matches):
        end = matches[position + 1].start() if position + 1 < len(matches) else len(text)
        question = make_linduxi_question(int(match.group(1)), text[match.start():end])
        if not question:
            rejected += 1
            continue
        signature = content_signature(question)
        if signature in existing_signatures or signature in seen:
            duplicates += 1
            continue
        seen.add(signature)
        imported.append(question)
    return imported, {
        "numberedBlocksDetected": len(matches),
        "imported": len(imported),
        "rejectedAmbiguous": rejected,
        "duplicatesSkipped": duplicates,
    }


def official_quant_stimulus(prefix: str) -> str:
    heading_matches = list(re.finditer(r"(?m)^Quantity\s+A\s+_+\s+Quantity\s+B\s*$", prefix, re.I))
    if not heading_matches:
        return ""
    text = prefix[:heading_matches[-1].start()]
    strategy_matches = list(re.finditer(r"(?m)^Strategy\s+\d+:[^\n]*$", text))
    if strategy_matches:
        text = text[strategy_matches[-1].end():]
    lines = []
    for line in text.splitlines():
        cleaned = normalized_space(line)
        if not cleaned or re.fullmatch(r"\d{1,3}", cleaned):
            continue
        if cleaned in {"Arithmetic", "Algebra", "Geometry", "Data Analysis", "Mixed Practice Sets"}:
            continue
        if "Answers and Explanations" in cleaned:
            continue
        lines.append(cleaned)
    return normalized_space(" ".join(lines))


def official_correct_answer(explanation: str) -> tuple[list[str], str | None]:
    positions = list(re.finditer(r"correct answer", explanation, re.I))
    if not positions:
        return [], None
    sentence = explanation[positions[-1].start():positions[-1].start() + 300]
    letters = list(dict.fromkeys(re.findall(r"\b[A-I]\b", sentence)))
    if "Choice" in sentence and letters:
        return letters, None
    numeric_match = re.search(r"(?:is|are)\s+(-?[\d,]+(?:\.\d+)?(?:/\d+)?)", sentence, re.I)
    return [], numeric_match.group(1).replace(",", "") if numeric_match else None


def official_options(question_text: str) -> tuple[str, dict[str, str]]:
    text = re.sub(r"^\s*\d{1,2}\.\s*", "", question_text, count=1)
    lines = [normalized_space(line) for line in text.splitlines() if normalized_space(line)]
    labels: list[tuple[int, str, str]] = []
    glyph_labels = {"©": "C", "Æ": "E", "€": "E"}
    for index, line in enumerate(lines):
        match = re.match(r"^\(?([A-I])\)?[\.)]?\s+(.+)$", line)
        if match:
            labels.append((index, match.group(1), match.group(2)))
        elif line[0] in glyph_labels and len(line) > 1:
            labels.append((index, glyph_labels[line[0]], normalized_space(line[1:])))
    if not labels:
        return normalized_space(text), {}
    start = labels[0][0]
    choices: dict[str, str] = {}
    for position, (line_index, label, value) in enumerate(labels):
        end = labels[position + 1][0] if position + 1 < len(labels) else len(lines)
        continuation = " ".join(lines[line_index + 1:end])
        choices[label] = normalized_space(f"{value} {continuation}")
    return normalized_space(" ".join(lines[:start])), choices


def make_official_noncomparison(
    set_index: int,
    set_name: str,
    difficulty: str,
    number: int,
    question_text: str,
    explanation_text: str,
    page_range: tuple[int, int],
) -> dict | None:
    letters, numeric_answer = official_correct_answer(explanation_text)
    explanation = taiwan_text(explanation_text)[:1_500]
    if numeric_answer:
        prompt = normalized_space(re.sub(r"^\s*\d{1,2}\.\s*", "", question_text, count=1))
        prompt = re.sub(r"\b(?:employees|miles|dollars|minutes|hours)\s*$", "", prompt, flags=re.I)
        if len(prompt.split()) < 7 or re.search(r"figure|graph|table|above", prompt, re.I):
            return None
        return {
            "id": f"20260720-powerpack-q-{set_index:02d}-{number:02d}",
            "measure": "quantitative",
            "difficulty": difficulty,
            "format": "numericEntry",
            "stimulus": None,
            "prompt": prompt,
            "quantityA": None,
            "quantityB": None,
            "groups": [],
            "correctSelections": {},
            "acceptedNumericAnswers": [numeric_answer],
            "explanation": explanation,
            "contentArea": set_name if not set_name.startswith("Mixed") else quant_area(prompt),
            "source": {
                "title": POWERPACK_QUANT_SOURCE,
                "detail": f"{set_name} · 第 {number} 題 · 詳解 PDF 第 {page_range[0]}–{page_range[1]} 頁",
                "isAuthorizedSourceItem": True,
            },
        }

    if not letters:
        return None
    prompt, choices = official_options(question_text)
    if len(prompt.split()) < 5 or re.search(r"figure|graph|table|above", prompt, re.I):
        return None
    if not choices or any(letter not in choices for letter in letters):
        return None
    if any(re.search(r"[©Æ€]", choice) for choice in choices.values()):
        return None
    expected = list("ABCDEFGHI"[: ord(max(choices)) - ord("A") + 1])
    if list(sorted(choices)) != expected or len(expected) < 3:
        return None
    maximum = len(letters)
    return {
        "id": f"20260720-powerpack-q-{set_index:02d}-{number:02d}",
        "measure": "quantitative",
        "difficulty": difficulty,
        "format": "multipleChoice" if maximum > 1 else "singleChoice",
        "stimulus": None,
        "prompt": prompt,
        "quantityA": None,
        "quantityB": None,
        "groups": [{
            "id": "main",
            "title": None,
            "options": [{"id": f"o{index}", "text": choices[letter]} for index, letter in enumerate(expected)],
            "maximumSelections": maximum,
        }],
        "correctSelections": {"main": [f"o{expected.index(letter)}" for letter in letters]},
        "acceptedNumericAnswers": [],
        "explanation": explanation,
        "contentArea": set_name if not set_name.startswith("Mixed") else quant_area(prompt),
        "source": {
            "title": POWERPACK_QUANT_SOURCE,
            "detail": f"{set_name} · 第 {number} 題 · 詳解 PDF 第 {page_range[0]}–{page_range[1]} 頁",
            "isAuthorizedSourceItem": True,
        },
    }


def import_powerpack_quant(directory: Path, existing: list[dict]) -> tuple[list[dict], dict]:
    pages = {int(page["page"]): page["text"] for page in read_json(directory / "powerpack.json")}
    configurations = [
        ("Arithmetic", "easy", 673, 685),
        ("Algebra", "medium", 695, 705),
        ("Geometry", "medium", 715, 725),
        ("Data Analysis", "hard", 740, 759),
        ("Mixed Practice Set 1", "easy", 773, 793),
        ("Mixed Practice Set 2", "medium", 805, 819),
        ("Mixed Practice Set 3", "hard", 831, 849),
    ]
    signatures = {content_signature(question) for question in existing}
    imported: list[dict] = []
    detected = 0
    rejected = 0
    duplicates = 0

    for set_index, (set_name, difficulty, first_page, last_page) in enumerate(configurations, 1):
        text = "\n".join(pages.get(page, "") for page in range(first_page, last_page + 1))
        matches = list(re.finditer(r"(?m)^\s*(\d{1,2})\.\s+", text))
        for position, match in enumerate(matches):
            end = matches[position + 1].start() if position + 1 < len(matches) else len(text)
            block_text = text[match.start():end]
            if "Explanation" not in block_text:
                continue
            detected += 1
            number = int(match.group(1))
            if (set_index, number) not in POWERPACK_QA_ALLOWLIST:
                rejected += 1
                continue
            question_text, explanation_text = block_text.split("Explanation", 1)
            candidate = None
            if "______" in question_text:
                correct_matches = re.findall(r"correct answer is\s+Choice\s+([A-D])", explanation_text, re.I)
                stimulus = official_quant_stimulus(text[:match.start()])
                body = QUESTION_LINE.sub(r"\2", question_text, count=1)
                body = re.sub(r"\s+______\s+[A-D]\s*$", "", body, flags=re.M)
                comparison_lines = [
                    normalized_space(line)
                    for line in body.splitlines()
                    if normalized_space(line) and not re.fullmatch(r"\d{1,3}", normalized_space(line))
                ]
                if correct_matches and len(stimulus.split()) <= 80 and all("______" in line for line in comparison_lines):
                    candidate = make_quant_comparison({
                        "number": number,
                        "answer": correct_matches[-1],
                        "page": first_page,
                        "text": f"{stimulus}\nColumn A ______ Column B\n" + "\n".join(comparison_lines),
                    }, taiwan_text(explanation_text)[:1_500])
            if candidate is None:
                candidate = make_official_noncomparison(
                    set_index,
                    set_name,
                    difficulty,
                    number,
                    question_text,
                    explanation_text,
                    (first_page, last_page),
                )
            if not candidate:
                rejected += 1
                continue
            if candidate["format"] == "quantitativeComparison":
                candidate["id"] = f"20260720-powerpack-q-{set_index:02d}-{number:02d}"
                candidate["difficulty"] = difficulty
                candidate["contentArea"] = set_name if not set_name.startswith("Mixed") else quant_area(
                    " ".join([candidate.get("stimulus") or "", candidate["quantityA"], candidate["quantityB"]])
                )
                candidate["source"] = {
                    "title": POWERPACK_QUANT_SOURCE,
                    "detail": f"{set_name} · 第 {number} 題 · 詳解 PDF 第 {first_page}–{last_page} 頁",
                    "isAuthorizedSourceItem": True,
                }
            signature = content_signature(candidate)
            if signature in signatures:
                duplicates += 1
                continue
            signatures.add(signature)
            imported.append(candidate)
    return imported, {
        "explanationBlocksDetected": detected,
        "importedQuestions": len(imported),
        "formatCounts": dict(sorted(collections.Counter(question["format"] for question in imported).items())),
        "rejectedIncompleteLayout": rejected,
        "duplicatesSkipped": duplicates,
    }


def load_ecdict(path: Path, targets: set[str]) -> dict[str, dict]:
    found: dict[str, dict] = {}
    with path.open(encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            word = row.get("word", "").strip().lower()
            if word in targets:
                found[word] = row
    return found


def contextual_definitions(pages: list[dict]) -> dict[str, tuple[str, int]]:
    ignored = {"instructions", "definitions", "your guesses"}
    definitions: dict[str, tuple[str, int]] = {}
    for page in pages:
        text = page["text"]
        if "Definitions" not in text:
            continue
        body = text.split("Definitions", 1)[1]
        for line in body.splitlines():
            match = re.match(r"^([A-Z][A-Za-z '-]{1,30}):\s*(.{2,120})$", normalized_space(line))
            if not match:
                continue
            word = match.group(1).strip()
            if word.lower() in ignored or match.group(2).lower().startswith(("draw ", "fill ")):
                continue
            definitions.setdefault(word.lower(), (match.group(2).strip(), int(page["page"])))
    return definitions


def sentence_candidates(pages: list[dict], word: str) -> list[str]:
    pattern = re.compile(rf"\b{re.escape(word)}\b", re.I)
    candidates: list[str] = []
    for page in pages:
        text = normalized_space(page["text"])
        for sentence in re.split(r"(?<=[.!?])\s+", text):
            count = len(re.findall(r"[A-Za-z]+", sentence))
            if pattern.search(sentence) and 6 <= count <= 36 and not re.search(r"Instructions:|Definitions|Your guesses", sentence, re.I):
                candidates.append(sentence.strip())
    return sorted(candidates, key=lambda sentence: abs(len(sentence.split()) - 16))


def import_contextual_vocabulary(ocr_path: Path, ecdict_path: Path, existing: list[dict]) -> tuple[list[dict], dict]:
    pages = read_json(ocr_path)
    definitions = contextual_definitions(pages)
    dictionary = load_ecdict(ecdict_path, set(definitions))
    existing_by_word = {item["word"].strip().lower(): item for item in existing}
    imported: list[dict] = []
    unresolved: list[str] = []

    for key, (book_definition, page) in sorted(definitions.items()):
        current = existing_by_word.get(key)
        row = dictionary.get(key, {})
        override = CONTEXTUAL_OVERRIDES.get(key)
        chinese = taiwan_text(override[0] if override else ((current or {}).get("chinese", "") or row.get("translation", "")))
        example = override[1] if override else (current or {}).get("example", "").strip()
        if not example:
            candidates = sentence_candidates(pages, key)
            example = candidates[0] if candidates else ""
        if not chinese or not example:
            unresolved.append(key)
            continue
        sources = sorted(set((current or {}).get("sources", []) + [BOOK_SOURCE]))
        imported.append({
            "word": (current or {}).get("word", key),
            "pronunciation": (current or {}).get("pronunciation", "") or row.get("phonetic", "").strip(),
            "definition": (current or {}).get("definition", "") or book_definition,
            "chinese": chinese,
            "synonyms": (current or {}).get("synonyms", []),
            "example": example,
            "exampleSource": ORIGINAL_EXAMPLE_SOURCE if override else ((current or {}).get("exampleSource") or BOOK_SOURCE),
            "exampleSourceURL": (current or {}).get("exampleSourceURL"),
            "sources": sources,
            "isHighFrequency": True,
        })
    return imported, {
        "definitionsDetected": len(definitions),
        "importedOrSourceMerged": len(imported),
        "unresolved": unresolved,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--ocr-dir", type=Path, required=True)
    parser.add_argument("--answer-key", type=Path, required=True)
    parser.add_argument("--ecdict", type=Path, required=True)
    parser.add_argument("--books-ocr-dir", type=Path)
    parser.add_argument("--supplemental-ocr-dir", type=Path)
    parser.add_argument("--writing-index", type=Path)
    parser.add_argument("--output-dir", type=Path, default=RESOURCES)
    arguments = parser.parse_args()

    existing_questions = read_json(RESOURCES / "ExpandedQuestions.json")
    existing_vocabulary = read_json(RESOURCES / "ExpandedVocabulary.json")
    questions, question_audit = import_fill_questions(
        arguments.ocr_dir / "fill1250.json", arguments.answer_key, existing_questions
    )
    reading_questions, reading_audit = import_reading_questions(
        arguments.ocr_dir / "read250.json",
        arguments.ocr_dir / "read250sol.json",
        existing_questions + questions,
    )
    questions.extend(reading_questions)
    linduxi_audit = None
    if arguments.supplemental_ocr_dir:
        linduxi_questions, linduxi_audit = import_linduxi_fill(
            arguments.supplemental_ocr_dir / "linduxisol-gaps.json",
            existing_questions + questions,
        )
        questions.extend(linduxi_questions)
    quant_audit = None
    if arguments.books_ocr_dir:
        quant_questions, quant_audit = import_quant_questions(
            arguments.books_ocr_dir, existing_questions + questions
        )
        questions.extend(quant_questions)
        powerpack_quant_questions, powerpack_quant_audit = import_powerpack_quant(
            arguments.books_ocr_dir, existing_questions + questions
        )
        questions.extend(powerpack_quant_questions)
        writing_questions, writing_audit = import_dummies_writing(
            arguments.books_ocr_dir, existing_questions + questions
        )
        questions.extend(writing_questions)
        north_american_writing_audit = None
        if arguments.supplemental_ocr_dir and arguments.writing_index:
            additional_writing, north_american_writing_audit = import_north_american_writing(
                arguments.supplemental_ocr_dir,
                arguments.writing_index,
                existing_questions + questions,
            )
            questions.extend(additional_writing)
    else:
        writing_audit = None
        powerpack_quant_audit = None
        north_american_writing_audit = None
    vocabulary, vocabulary_audit = import_contextual_vocabulary(
        arguments.ocr_dir / "contextual-vocab.json", arguments.ecdict, existing_vocabulary
    )

    arguments.output_dir.mkdir(parents=True, exist_ok=True)
    write_json(arguments.output_dir / "Authorized20260720Questions.json", questions)
    write_json(arguments.output_dir / "Authorized20260720Vocabulary.json", vocabulary)
    write_json(arguments.output_dir / "Authorized20260720Manifest.json", {
        "questionCount": len(questions),
        "questionAudit": {
            "textCompletion": question_audit,
            "reading": reading_audit,
            "supplementalTextCompletion": linduxi_audit,
            "quantitative": quant_audit,
            "officialQuantitative": powerpack_quant_audit,
            "analyticalWriting": writing_audit,
            "supplementalAnalyticalWriting": north_american_writing_audit,
        },
        "vocabularyCount": len(vocabulary),
        "vocabularyAudit": vocabulary_audit,
        "privacy": "Order confirmations and personal purchase data are intentionally excluded.",
        "sourceAudit": [
            {
                "collection": "GRE 填空 1000／1100／1250 與答案表",
                "status": "The 1250-question superset and its answer key were imported; overlapping 1000/1100 files are represented through the superset and deduplicated.",
            },
            {
                "collection": "麟渡兮 GRE 填空題目與解答",
                "status": "Complete numbered items with recoverable blanks, choices, and answer keys were imported; damaged layouts were rejected.",
            },
            {
                "collection": "老肖閱讀 250 篇、閱讀 36 套、邏輯 10 套與長難句資料",
                "status": "The 250-passage bank was imported. Two-column layouts or reference-only material without a reliably paired answer remain audit-only to prevent cross-column corruption.",
            },
            {
                "collection": "猴哥數學 112 題、Official Quantitative Reasoning 與數學必備知識",
                "status": "Scorable text-complete quantitative items and detailed solutions were imported. Figure-dependent or formula-damaged scans and the reference-only DOCX remain audit-only.",
            },
            {
                "collection": "Official Super Power Pack／Official Guide／Practicing to Take／For Dummies",
                "status": "Current-format, nonduplicate scored items and Issue prompts were imported. Superseded analogies, antonyms, and obsolete Argument tasks were excluded from the current GRE simulation.",
            },
            {
                "collection": "北美 GRE 範文與 Issue 論證素材",
                "status": "Current Issue numbers were cross-checked against the supplied index. Complete quoted prompts were imported; essay/reference prose remains study-source audit material.",
            },
            {
                "collection": "myPrep ZIP archive",
                "status": "Audited as the compressed mirror of the expanded 資料 directory; duplicate files were not imported twice.",
            },
        ],
    })

    print(json.dumps({
        "questions": {
            "textCompletion": question_audit,
            "reading": reading_audit,
            "supplementalTextCompletion": linduxi_audit,
            "quantitative": quant_audit,
            "officialQuantitative": powerpack_quant_audit,
            "analyticalWriting": writing_audit,
            "supplementalAnalyticalWriting": north_american_writing_audit,
        },
        "vocabulary": vocabulary_audit,
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
