#!/usr/bin/env python3
"""Build deterministic offline resources from authorized study material."""

from __future__ import annotations

import argparse
import json
import math
import random
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "Gre Simulation"


def source(title: str, detail: str, original: bool) -> dict:
    return {"title": title, "detail": detail, "isAuthorizedSourceItem": original}


def ets(detail: str) -> dict:
    return source("ETS Official Guide · authorized item", detail, True)


def petersons(detail: str) -> dict:
    return source("Peterson's Master the GRE · authorized item", detail, True)


SYNTHETIC_VERBAL = source(
    "Authorized vocabulary synthesis",
    "Original item built from the authorized 3000/Gauss/Magoosh word resources",
    False,
)
SYNTHETIC_QUANT = source(
    "Original parameterized Quant item",
    "Generated locally and audited against ETS Math Review and Peterson's topic coverage",
    False,
)


def option_group(choices, maximum=1, group_id="main", title=None):
    return {
        "id": group_id,
        "title": title,
        "options": [{"id": f"o{i}", "text": text} for i, text in enumerate(choices)],
        "maximumSelections": maximum,
    }


def single(qid, measure, difficulty, prompt, choices, correct, explanation, area, src, stimulus=None):
    return {
        "id": qid,
        "measure": measure,
        "difficulty": difficulty,
        "format": "singleChoice",
        "stimulus": stimulus,
        "prompt": prompt,
        "quantityA": None,
        "quantityB": None,
        "groups": [option_group(choices)],
        "correctSelections": {"main": [f"o{correct}"]},
        "acceptedNumericAnswers": [],
        "explanation": explanation,
        "contentArea": area,
        "source": src,
    }


def multiple(qid, measure, difficulty, prompt, choices, correct, maximum, explanation, area, src, stimulus=None, fmt="multipleChoice"):
    question = single(qid, measure, difficulty, prompt, choices, 0, explanation, area, src, stimulus)
    question["format"] = fmt
    question["groups"] = [option_group(choices, maximum)]
    question["correctSelections"] = {"main": [f"o{i}" for i in sorted(correct)]}
    return question


def completion(qid, difficulty, prompt, columns, correct, explanation, src):
    groups = []
    answers = {}
    for column, choices in enumerate(columns):
        group_id = f"blank{column}"
        groups.append({
            "id": group_id,
            "title": None if len(columns) == 1 else f"Blank ({['i', 'ii', 'iii'][column]})",
            "options": [{"id": f"b{column}o{i}", "text": text} for i, text in enumerate(choices)],
            "maximumSelections": 1,
        })
        answers[group_id] = [f"b{column}o{correct[column]}"]
    return {
        "id": qid,
        "measure": "verbal",
        "difficulty": difficulty,
        "format": "textCompletion",
        "stimulus": None,
        "prompt": prompt,
        "quantityA": None,
        "quantityB": None,
        "groups": groups,
        "correctSelections": answers,
        "acceptedNumericAnswers": [],
        "explanation": explanation,
        "contentArea": "Text Completion",
        "source": src,
    }


def numeric(qid, difficulty, prompt, answers, explanation, area, src, stimulus=None):
    return {
        "id": qid,
        "measure": "quantitative",
        "difficulty": difficulty,
        "format": "numericEntry",
        "stimulus": stimulus,
        "prompt": prompt,
        "quantityA": None,
        "quantityB": None,
        "groups": [],
        "correctSelections": {},
        "acceptedNumericAnswers": list(map(str, answers)),
        "explanation": explanation,
        "contentArea": area,
        "source": src,
    }


def comparison(qid, difficulty, stimulus, quantity_a, quantity_b, correct, explanation, area, src):
    choices = [
        "Quantity A is greater.",
        "Quantity B is greater.",
        "The two quantities are equal.",
        "The relationship cannot be determined from the information given.",
    ]
    question = single(qid, "quantitative", difficulty, "Compare Quantity A and Quantity B.", choices, correct, explanation, area, src, stimulus)
    question["format"] = "quantitativeComparison"
    question["quantityA"] = quantity_a
    question["quantityB"] = quantity_b
    return question


def official_verbal_questions():
    q = []
    q.append(completion("ets-v-e-01", "easy", "Dominant interests often benefit most from ______ of governmental interference in business, since they are able to take care of themselves if left alone.", [["intensification", "authorization", "centralization", "improvisation", "elimination"]], [4], "The logic calls for the removal of interference.", ets("Chapter 4 · Set 1 · Q1 · PDF p. 88")))
    q.append(completion("ets-v-e-02", "easy", "Kagan maintains that an infant’s reactions to its first stressful experiences are part of a natural process of development, not harbingers of childhood unhappiness or ______ signs of adolescent anxiety.", [["prophetic", "normal", "monotonous", "virtual", "typical"]], [0], "Harbingers predict later events, so prophetic is parallel.", ets("Chapter 4 · Set 1 · Q2 · PDF p. 88")))
    q.append(completion("ets-v-e-03", "easy", "An investigation that is ______ can occasionally yield new facts, even notable ones, but typically the appearance of such facts is the result of a search in a definite direction.", [["timely", "unguided", "consistent", "uncomplicated", "subjective"]], [1], "The contrast with a definite direction requires unguided.", ets("Chapter 4 · Set 1 · Q3 · PDF p. 88")))
    q.append(completion("ets-v-e-04", "easy", "It is (i) ______ that so many portrait paintings hang in art museums, since the subject matter seems closer to family photographs than to high art. But perhaps artistic skill (ii) ______ their presence in museums.", [["surprising", "understandable", "irrelevant"], ["challenges", "justifies", "changes"]], [0, 1], "The apparent mismatch is surprising, while artistic skill justifies the portraits' presence.", ets("Chapter 4 · Set 1 · Q4 · PDF pp. 88–89")))
    q.append(completion("ets-v-e-05", "easy", "In stark contrast to his later (i) ______, Simpson was largely (ii) ______ politics during college, despite attending a campus rife with political activity.", [["activism", "apathy", "affability"], ["devoted to", "indifferent to", "shaped by"]], [0, 1], "Later activism contrasts with earlier indifference.", ets("Chapter 4 · Set 1 · Q5 · PDF p. 89")))
    easy_eq = [
        ("As my eyesight began to ______, I spent a lot of time writing about what I saw through damaged eyes.", ["deteriorate", "sharpen", "improve", "decline", "recover", "adjust"], {0, 3}),
        ("The judge’s standing in the legal community, though shaken by phony allegations, emerged at long last ______.", ["unqualified", "undiminished", "undecided", "undamaged", "unresolved", "unprincipled"], {1, 3}),
        ("Modern agriculture has increased crop productivity, yet despite heavy pesticide use, ______ losses to diseases and pests occur each year.", ["incongruous", "reasonable", "significant", "considerable", "equitable", "fortuitous"], {2, 3}),
    ]
    for index, (prompt, choices, answers) in enumerate(easy_eq, 6):
        q.append(multiple(f"ets-v-e-{index:02d}", "verbal", "easy", prompt, choices, answers, 2, "The two keyed words fit the context and produce equivalent meanings.", "Sentence Equivalence", ets(f"Chapter 4 · Set 1 · Q{index} · PDF pp. 89–90"), fmt="sentenceEquivalence"))
    q.append(completion("ets-v-m-01", "medium", "It comes as no surprise that societies have codes of behavior; the character of the codes, on the other hand, can often be ______.", [["predictable", "unexpected", "admirable", "explicit", "confusing"]], [1], "The phrase on the other hand contrasts expected existence with unexpected character.", ets("Chapter 4 · Set 3 · Q1 · PDF p. 95")))
    q.append(completion("ets-v-m-02", "medium", "Like Béla Bartók, Ruth Crawford brought a composer’s acumen to folk-music notation and had a marked (i) ______ the task. Her concern with minute details shows a (ii) ______ that makes her work a landmark.", [["reverence for", "detachment from", "curiosity about"], ["fastidiousness", "didacticism", "iconoclasm"]], [0, 0], "Concern over exact detail demonstrates reverence and fastidiousness.", ets("Chapter 4 · Set 3 · Q2 · PDF p. 95")))
    q.append(completion("ets-v-m-03", "medium", "Political advertising may be the most (i) ______ kind of advertising: candidates are usually quite (ii) ______, yet their ads hide important differences behind smiles and slogans.", [["polemical", "effective", "deceptive"], ["interchangeable", "dissimilar", "vocal"]], [2, 1], "The candidates differ, but advertising deceptively conceals those differences.", ets("Chapter 4 · Set 3 · Q3 · PDF pp. 95–96")))
    q.append(completion("ets-v-m-04", "medium", "Russell said 52 percent of national growth had (i) ______ invention. He said, (ii) ______ research, government's greatest role is a strong patent office: unless original ideas can be (iii) ______, invention will suffer.", [["been at the expense of", "no bearing on", "come through"], ["in addition to restricting", "aside from supporting", "far from"], ["evaluate", "protect", "disseminate"]], [2, 1, 1], "Growth came through invention; beyond supporting research, government must protect ideas.", ets("Chapter 4 · Set 3 · Q4 · PDF pp. 96–97")))
    q.append(completion("ets-v-m-05", "medium", "Statements presented as fact in a patent application are (i) ______ unless reason for doubt is found. No incentive exists to expend effort (ii) ______ the science of an erroneous patent, so a stream of (iii) ______ devices can yield patents.", [["presumed verifiable", "carefully scrutinized", "considered capricious"], ["corroborating", "advancing", "debunking"], ["novel", "bogus", "obsolete"]], [0, 2, 1], "The permissive standard leaves presumed claims untested and bogus devices undebunked.", ets("Chapter 4 · Set 3 · Q5 · PDF p. 97")))
    medium_eq = [
        ("Ever a demanding reader of others’ fiction, the novelist Chase was likewise often the object of ______ analyses by contemporaries.", ["exacting", "copious", "respectful", "acerbic", "scathing", "meticulous"], {0, 5}),
        ("Her ______ should not be confused with miserliness; she has always been willing to assist those in need.", ["stinginess", "diffidence", "frugality", "illiberality", "intolerance", "thrift"], {2, 5}),
        ("A misconception held by novice writers is that sentence structure mirrors thought: the more convoluted the structure, the more ______ the ideas.", ["complicated", "engaged", "essential", "fanciful", "inconsequential", "involved"], {0, 5}),
    ]
    for index, (prompt, choices, answers) in enumerate(medium_eq, 6):
        q.append(multiple(f"ets-v-m-{index:02d}", "verbal", "medium", prompt, choices, answers, 2, "The keyed pair preserves both context and equivalent meaning.", "Sentence Equivalence", ets(f"Chapter 4 · Set 3 · Q{index} · PDF pp. 97–98"), fmt="sentenceEquivalence"))
    q.append(completion("ets-v-h-01", "hard", "For some time now, ______ has been presumed not to exist: the cynical conviction that everybody has an angle is considered wisdom.", [["rationality", "flexibility", "diffidence", "disinterestedness", "insincerity"]], [3], "If everyone has an angle, impartial disinterestedness is presumed absent.", ets("Chapter 4 · Set 5 · Q1 · PDF p. 104")))
    q.append(completion("ets-v-h-02", "hard", "Human nature and long distances have made exceeding the speed limit a (i) ______ in the state, so legislators surprised no one when, acceding to public demand, they (ii) ______ increased penalties.", [["controversial habit", "cherished tradition", "disquieting ritual"], ["endorsed", "considered", "rejected"]], [1, 2], "A cherished practice explains public pressure to reject higher penalties.", ets("Chapter 4 · Set 5 · Q2 · PDF p. 104")))
    q.append(completion("ets-v-h-03", "hard", "Serling’s account of his employer’s reckless decision making (i) ______ that company’s image as (ii) ______ bureaucracy full of wary managers.", [["belies", "exposes", "overshadows"], ["an injudicious", "a disorganized", "a cautious"]], [0, 2], "Recklessness contradicts, or belies, an image of caution.", ets("Chapter 4 · Set 5 · Q3 · PDF pp. 104–105")))
    q.append(completion("ets-v-h-04", "hard", "No contemporary poet has such a reputation for (i) ______. Yet this fourth book in six years is ample output for one of such (ii) ______ over the previous 50 years. For all his newfound (iii) ______, the poetry remains thorny.", [["patent accessibility", "intrinsic frivolity", "near impenetrability"], ["penitential austerity", "intractable severity", "impetuous prodigality"], ["taciturnity", "volubility", "pellucidity"]], [2, 0, 1], "Impenetrable, austere work contrasts with a newly voluble output that is still difficult.", ets("Chapter 4 · Set 5 · Q4 · PDF p. 105")))
    q.append(completion("ets-v-h-05", "hard", "Managers who think environmental performance will (i) ______ financial performance often (ii) ______ claims that management systems are valuable. Those who see it as (iii) ______ to financial success may view such systems as extraneous.", [["eclipse", "bolster", "degrade"], ["uncritically accept", "appropriately acknowledge", "hotly dispute"], ["complementary", "intrinsic", "peripheral"]], [1, 0, 2], "Belief in a financial benefit invites acceptance; seeing the issue as peripheral does not.", ets("Chapter 4 · Set 5 · Q5 · PDF pp. 105–106")))
    q.append(completion("ets-v-h-06", "hard", "Philosophy deepens understanding through (i) ______ what is closest to us but usually unnoticed. It begins by finding (ii) ______ the things that are (iii) ______.", [["attainment of", "rumination on", "detachment from"], ["essentially irrelevant", "utterly mysterious", "thoroughly commonplace"], ["most prosaic", "somewhat hackneyed", "refreshingly novel"]], [1, 1, 0], "Philosophy reflects on familiar experience and discovers mystery in the most ordinary things.", ets("Chapter 4 · Set 5 · Q6 · PDF pp. 106–107")))
    hard_eq = [
        ("The new ethics code appeared intended to shore up the ruling party’s standing with an increasingly ______ electorate while the party faced corruption charges.", ["aloof", "placid", "restive", "skittish", "tranquil", "vociferous"], {2, 3}),
        ("Overlarge and disappointing, the retrospective seems like special pleading for a forgotten painter of real but ______ talents.", ["limited", "partial", "undiscovered", "circumscribed", "prosaic", "hidden"], {0, 3}),
        ("Newspapers report that the former executive has tried to keep a low profile since his ______ exit from the company.", ["celebrated", "mysterious", "long-awaited", "fortuitous", "indecorous", "unseemly"], {4, 5}),
    ]
    for index, (prompt, choices, answers) in enumerate(hard_eq, 7):
        q.append(multiple(f"ets-v-h-{index:02d}", "verbal", "hard", prompt, choices, answers, 2, "The keyed pair fits the contextual contrast and yields equivalent sentences.", "Sentence Equivalence", ets(f"Chapter 4 · Set 5 · Q{index} · PDF pp. 107–108"), fmt="sentenceEquivalence"))
    return q


def petersons_verbal_questions():
    q = []
    cars = "The New York Times declared the end of car culture, citing declining miles driven, ownership, and driving among young people. Yet car culture may merely be in the slow lane. Road building peaked decades ago, public transportation use has grown faster than population, people drive less after age 45, and vehicle costs have risen. These data may show the end of a driving boom, not the end of cars."
    q.append(multiple("pet-v-05", "verbal", "medium", "The passage suggests that the decline in driving is authenticated by which statements?", ["A 23-percent decline in car ownership among young people", "The end of massive road-building projects", "The steady drop in miles driven"], {1, 2}, 3, "The passage supports the latter two; the 23-percent figure concerns driving, not ownership.", "Reading Comprehension", petersons("Diagnostic Test · Section 2 · Q5 · PDF p. 65"), cars))
    q.append(single("pet-v-06", "verbal", "medium", "Why did the Times writer most likely use the phrase ‘the end of car culture’?", ["To contradict automobility", "To generalize trends exactly", "To create a neutral technical term", "To summarize every study accurately", "To reflect data while using attention-getting hyperbole"], 4, "The author treats the phrase as an overstatement grounded in real trends.", "Reading Comprehension", petersons("Diagnostic Test · Section 2 · Q6 · PDF p. 65"), cars))
    q.append(single("pet-v-07", "verbal", "medium", "The second paragraph is primarily concerned with", ["mitigating the claim that car culture is at an end", "explaining only the recent mileage decline", "presenting a new demographic", "denying any ownership decline", "proving car culture is over"], 0, "It qualifies the bold claim rather than fully accepting or rejecting the evidence.", "Reading Comprehension", petersons("Diagnostic Test · Section 2 · Q7 · PDF p. 65"), cars))
    obesity = "Obesity results when calorie intake exceeds energy burned over time, though no single cause is known. Genetic factors influence fat metabolism and appetite. Digestion raises blood glucose and triggers insulin, which helps change glucose into energy and determines which nutrients are burned or stored. Studies find that the faster a cell processes insulin, the more fat it stores."
    q.append(multiple("pet-v-08", "verbal", "medium", "What function might a medication perform to decrease a user’s obesity?", ["Help cells process insulin faster", "Cause more insulin production", "Slow the rate at which cells process insulin"], {2}, 3, "The passage associates faster processing with more fat storage, so slowing it could help.", "Reading Comprehension", petersons("Diagnostic Test · Section 2 · Q8 · PDF p. 66"), obesity))
    q.append(single("pet-v-09", "verbal", "medium", "What is the function of the sentence explaining that carbohydrates break down into sugar molecules, including glucose?", ["It provides evidence on which a theory is based", "It summarizes the author's favored theory", "It merely restates an earlier point", "It disproves an accepted theory", "It presents a specific application of a general concept"], 0, "The biochemical detail supplies evidence for the insulin-based account that follows.", "Reading Comprehension", petersons("Diagnostic Test · Section 2 · Q9 · PDF p. 66"), obesity))
    escher = "M. C. Escher is best known for works drawn from unusual perspectives that produce enigmatic effects. He used a mathematical approach and personal systems for categorizing shapes, colors, and symmetries, but did not call himself a mathematician. He thought mathematicians opened the gate to a vast domain yet cared more about how the gate opened than the garden beyond it."
    q.append(single("pet-v-10", "verbal", "easy", "The passage suggests that the enigmatic effects of Escher’s work are caused by", ["his range of subjects", "drawing instead of painting", "mathematics alone", "his categorization system", "his unusual perspectives"], 4, "The opening sentence directly links unusual perspectives to enigmatic effects.", "Reading Comprehension", petersons("Diagnostic Test · Section 2 · Q10 · PDF p. 67"), escher))
    q.append(single("pet-v-11", "verbal", "medium", "Which statement best reflects Escher’s view of mathematicians?", ["They miss links between math and art", "They cannot appreciate his art", "They cannot invent notation", "They can never turn theories into art", "They fail to see the beauty inherent in their theories"], 4, "The gate-and-garden metaphor says they focus on mechanism rather than the domain's beauty.", "Reading Comprehension", petersons("Diagnostic Test · Section 2 · Q11 · PDF p. 67"), escher))
    q.append(single("pet-v-12", "verbal", "easy", "In context, ‘distinctive’ most nearly means", ["disturbing", "honorable", "characteristic", "maladjusted", "macabre"], 2, "Distinctive style means a characteristic or recognizable style.", "Vocabulary in Context", petersons("Diagnostic Test · Section 2 · Q12 · PDF p. 67"), escher))
    triangle = "The 1911 Triangle Shirtwaist Factory fire killed 146 people, many young immigrant women. Workers were trapped because supervisors had locked stairwell and exit doors. The tragedy exposed inhumane sweatshop conditions, strengthened the garment workers’ union, prompted a state investigation, and led to new worker-safety laws."
    q.append(multiple("pet-v-13", "verbal", "easy", "What evidently caused the fire to have such a great impact on public opinion?", ["The large death toll, including many young women", "Workers were unable to escape because doors had been locked", "The fire later strengthened labor and safety laws"], {0, 1}, 3, "The first two facts explain public reaction; the third is a consequence of that reaction.", "Reading Comprehension", petersons("Diagnostic Test · Section 2 · Q13 · PDF pp. 67–68"), triangle))
    q.append(single("pet-v-14", "verbal", "easy", "Which detail adds the least support to the passage’s central idea?", ["The tragedy exposed sweatshop conditions", "The fire started near closing time on March 25, 1911", "Many trapped women died or jumped because ladders could not reach them"], 1, "The exact time and date do not develop the central claim about causes and impact.", "Reading Comprehension", petersons("Diagnostic Test · Section 2 · Q14 · PDF p. 68"), triangle))
    q.append(single("pet-v-15", "verbal", "easy", "In context, ‘galvanized’ most nearly means", ["impeded", "increased", "hurtled", "angered", "incited"], 4, "The tragedy incited people to push for reform.", "Vocabulary in Context", petersons("Diagnostic Test · Section 2 · Q15 · PDF p. 68"), triangle))
    q.append(multiple("pet-v-16", "verbal", "medium", "In shuttering programs to reduce costs, the new CFO was ______ toward employees and refused to listen to their concerns and alternatives.", ["arrogant", "unkind", "uncharitable", "dismissive", "contentious", "confrontational"], {0, 3}, 2, "Arrogant and dismissive both capture the refusal to consider employees' views.", "Sentence Equivalence", petersons("Diagnostic Test · Section 2 · Q16 · PDF p. 68"), fmt="sentenceEquivalence"))
    q.append(multiple("pet-v-17", "verbal", "medium", "Green building is a fast-growing industry segment and one that ALLIED Builders hopes to ______ according to its five-year plan.", ["promote", "advance", "capitalize on", "upgrade", "exploit", "endorse"], {2, 4}, 2, "Capitalize on and exploit both mean take advantage of the opportunity.", "Sentence Equivalence", petersons("Diagnostic Test · Section 2 · Q17 · PDF p. 68"), fmt="sentenceEquivalence"))
    q.append(multiple("pet-v-18", "verbal", "medium", "Products such as cordless tools and polarized sunglasses resulted from ______ research that NASA conducted for the space program.", ["far-reaching", "wide-ranging", "innovative", "unusual", "cutting-edge", "conventional"], {2, 4}, 2, "Innovative and cutting-edge match both the context and each other.", "Sentence Equivalence", petersons("Diagnostic Test · Section 2 · Q18 · PDF p. 69"), fmt="sentenceEquivalence"))
    q.append(multiple("pet-v-19", "verbal", "easy", "Researchers believe that ______ bacteria keep harmful bacteria from invading humans by using the material the harmful bacteria need.", ["helpful", "malignant", "pathogenic", "benign", "benevolent", "beneficial"], {0, 5}, 2, "Helpful and beneficial describe bacteria with a protective effect.", "Sentence Equivalence", petersons("Diagnostic Test · Section 2 · Q19 · PDF p. 69"), fmt="sentenceEquivalence"))
    dickinson = "Emily Dickinson wrote memorable, brief poems marked by unusual punctuation and figurative language. Her syntax, imagery, metaphor, and personification can seem puzzling to an uninitiated reader. Present-day readers would do well to renounce a literal way of reading in order to appreciate the poetry."
    q.append(single("pet-v-20", "verbal", "medium", "What does the final statement imply?", ["Readers should seek no literal meaning at all", "Readers today are unaccustomed to so much figurative language", "Readers need only identify Dickinson's themes", "Interpreting figurative language is a disservice", "Readers need only reconstruct historical context"], 1, "The advice to abandon a strictly literal reading implies modern readers need to adjust to dense figurative language.", "Reading Comprehension", petersons("Diagnostic Test · Section 2 · Q20 · PDF p. 69"), dickinson))
    return q


def official_quant_questions():
    q = []
    q.append(comparison("ets-q-e-01", "easy", "Emma spent $75 on a bicycle and $27 repairing it, then sold it for 40 percent more than her total cost.", "The selling price", "$140", 0, "Total cost is $102; 1.4 × 102 = $142.80, greater than $140.", "Arithmetic", ets("Chapter 6 · Set 1 · Q1 · PDF p. 224")))
    q.append(comparison("ets-q-e-03", "easy", "A town charged a constant property-tax rate. A $125,000 home was taxed $2,500.", "Tax on a $160,000 home", "$3,000", 0, "The rate is 2 percent, so the tax is $3,200.", "Percent", ets("Chapter 6 · Set 1 · Q3 · PDF p. 225")))
    q.append(comparison("ets-q-e-04", "easy", "x + y = −1", "x", "y", 3, "The sum alone does not establish which addend is larger.", "Algebra", ets("Chapter 6 · Set 1 · Q4 · PDF pp. 225–226")))
    q.append(comparison("ets-q-e-05", "easy", "r, s, and t are consecutive odd integers with r < s < t.", "r + s + 1", "s + t − 1", 1, "Since t = r + 4, Quantity B exceeds Quantity A by 2.", "Number Properties", ets("Chapter 6 · Set 1 · Q5 · PDF p. 226")))
    q.append(single("ets-q-e-08", "quantitative", "easy", "A store sells pens for either $2 or $3 each. With at most $25 and no tax, what is the greatest number of pens a customer can buy?", ["9", "10", "11", "12", "20"], 3, "Buying twelve $2 pens costs $24; thirteen would cost at least $26.", "Arithmetic", ets("Chapter 6 · Set 1 · Q8 · PDF p. 227")))
    q.append(single("ets-q-e-09", "quantitative", "easy", "If y = 3x and z = 2y, what is x + y + z in terms of x?", ["10x", "9x", "8x", "6x", "5x"], 0, "z = 6x, so x + 3x + 6x = 10x.", "Algebra", ets("Chapter 6 · Set 1 · Q9 · PDF p. 227")))
    q.append(single("ets-q-e-10", "quantitative", "easy", "Insurance costs $0.75 for contents worth $25 or less and $1.00 for contents over $25. What is the total fee for packages worth $18.25, $25.00, and $127.50?", ["$1.75", "$2.25", "$2.50", "$2.75", "$3.00"], 2, "$0.75 + $0.75 + $1.00 = $2.50.", "Arithmetic", ets("Chapter 6 · Set 1 · Q10 · PDF pp. 227–228")))
    q.append(single("ets-q-e-11", "quantitative", "easy", "If 55 percent of purchases were online and the rest were in a store, what was the ratio of online to in-store purchases?", ["11 to 9", "11 to 5", "10 to 9", "9 to 11", "9 to 10"], 0, "The ratio 55:45 reduces to 11:9.", "Ratio", ets("Chapter 6 · Set 1 · Q11 · PDF p. 228")))
    q.append(numeric("ets-q-e-13", "easy", "A list has mean 8 and standard deviation 2.5. What value is 2 standard deviations above the mean?", ["13"], "8 + 2(2.5) = 13.", "Data Analysis", ets("Chapter 6 · Set 1 · Q13 · PDF p. 229")))
    q.append(comparison("ets-q-m-01", "medium", "Machine R makes x units in 30 minutes; machine S makes x units in 48 minutes. Both rates are constant and x is positive.", "Units R makes in 3 hours", "Units S makes in 4 hours", 0, "R makes 6x units; S makes 5x units.", "Rates", ets("Chapter 6 · Set 2 · Q1 · PDF pp. 230–231")))
    q.append(comparison("ets-q-m-02", "medium", "Lists X and Y each contain 60 values and have means 2.7 and 7.1. Combined list Z contains all 120 values. Its 60th and 61st ordered values are 5 and 6.", "The mean of Z", "The median of Z", 1, "The mean is (2.7 + 7.1)/2 = 4.9, while the median is (5 + 6)/2 = 5.5.", "Data Analysis", ets("Chapter 6 · Set 2 · Q2 · PDF p. 231")))
    q.append(comparison("ets-q-m-05", "medium", "Among 9,000 attendees, x are College C students and y are students from elsewhere.", "The number of attendees who are not students", "9,000 − x − y", 2, "Subtracting all students from total attendance gives the number of nonstudents.", "Algebra", ets("Chapter 6 · Set 2 · Q5 · PDF pp. 232–233")))
    q.append(single("ets-q-m-09", "quantitative", "medium", "Liquid A is 8 percent of solution R and 18 percent of solution S by weight. If 3 grams of R are mixed with 7 grams of S, what percent of the result is liquid A?", ["10%", "13%", "15%", "19%", "26%"], 2, "The mixture contains 0.08(3) + 0.18(7) = 1.5 grams out of 10 grams, or 15 percent.", "Percent", ets("Chapter 6 · Set 2 · Q9 · PDF p. 234")))
    q.append(single("ets-q-m-10", "quantitative", "medium", "Of 700 members, 120 are lawyers. Two members are selected at random. Which is closest to the probability that neither is a lawyer?", ["0.5", "0.6", "0.7", "0.8", "0.9"], 2, "(580/700)(579/699) is about 0.686, closest to 0.7.", "Probability", ets("Chapter 6 · Set 2 · Q10 · PDF p. 234")))
    q.append(numeric("ets-q-m-11", "medium", "A rectangular garden is 18 feet by 12 feet and has a uniformly 3-foot-wide walkway outside it. What is the walkway’s area in square feet?", ["216"], "The outer rectangle is 24 by 18. Subtracting the garden gives 432 − 216 = 216.", "Geometry", ets("Chapter 6 · Set 2 · Q11 · PDF p. 235")))
    q.append(multiple("ets-q-m-13", "quantitative", "medium", "Two sides of a triangle have lengths 5 and 9. Which values could be the third side?", ["3", "5", "8", "15"], {1, 2}, 4, "The triangle inequality requires 4 < side < 14, so 5 and 8 work.", "Geometry", ets("Chapter 6 · Set 2 · Q13 · PDF p. 236")))
    q.append(comparison("ets-q-h-04", "hard", "Rectangle A has length 10% greater and width 10% less than rectangle C. Rectangle B has length 20% greater and width 20% less than C.", "Area of rectangle A", "Area of rectangle B", 0, "Their area factors are 1.1×0.9 = 0.99 and 1.2×0.8 = 0.96.", "Percent", ets("Chapter 6 · Set 3 · Q4 · PDF p. 238")))
    q.append(comparison("ets-q-h-05", "hard", "X is normally distributed. Values 650 and 850 are at the 60th and 90th percentiles.", "The value at the 75th percentile", "750", 1, "Normal-distribution percentile spacing is nonlinear; the 75th-percentile value is less than 750.", "Data Analysis", ets("Chapter 6 · Set 3 · Q5 · PDF pp. 238–239")))
    q.append(comparison("ets-q-h-06", "hard", "S contains all positive integers less than 81 that are not perfect squares.", "The number of integers in S", "72", 2, "There are 80 positive integers below 81 and 8 perfect squares from 1² through 8², leaving 72.", "Number Properties", ets("Chapter 6 · Set 3 · Q6 · PDF p. 239")))
    q.append(single("ets-q-h-07", "quantitative", "hard", "A manager needs 3 more people for a team after choosing 3 of 11 candidates. How many combinations of 3 can be selected from the remaining candidates?", ["6", "24", "56", "120", "462"], 2, "There are 8 remaining candidates, and C(8,3) = 56.", "Combinatorics", ets("Chapter 6 · Set 3 · Q7 · PDF p. 239")))
    q.append(multiple("ets-q-h-15", "quantitative", "hard", "Two penguin species have height ranges 13.2 cm and 15.4 cm. Which statement alone is sufficient to determine the range across both species?", ["The tallest of one species is 5.8 cm taller than the tallest of the other", "One median is 1.1 cm greater", "One mean is 4.6 cm greater"], {0}, 3, "The two ranges plus the difference between maxima determine both minima relative to one another; medians and means do not.", "Data Analysis", ets("Chapter 6 · Set 3 · Q15 · PDF p. 243")))
    return q


def generated_quant_questions():
    q = []
    for i in range(10):
        cost = 40 + 20 * i
        pct = [10, 15, 20, 25, 30][i % 5]
        answer = cost * (100 + pct) // 100
        choices = [answer - 8, answer - 4, answer, answer + 4, answer + 8]
        q.append(single(f"gen-q-percent-{i:02d}", "quantitative", "easy", f"A shop buys a study lamp for ${cost} and marks it up by {pct} percent. What is the marked price?", [f"${x}" for x in choices], 2, f"${cost} × {1 + pct / 100:g} = ${answer}.", "Percent", SYNTHETIC_QUANT))

        start = 2 * i + 3
        values = [start + 2 * k for k in range(5)]
        mean = sum(values) // 5
        q.append(numeric(f"gen-q-mean-{i:02d}", "easy", f"What is the arithmetic mean of the five consecutive odd integers {', '.join(map(str, values))}?", [mean], "For equally spaced values, the mean is the middle value.", "Data Analysis", SYNTHETIC_QUANT))

        online = 50 + 5 * i
        store = 100 - online
        divisor = math.gcd(online, store)
        ratio = f"{online // divisor} to {store // divisor}"
        ratios = [f"{online} to {store}", ratio, f"{store // divisor} to {online // divisor}", f"{online // 5} to {store // 5}", "1 to 1"]
        unique = []
        for item in ratios:
            if item not in unique:
                unique.append(item)
        while len(unique) < 5:
            unique.append(f"{len(unique) + 2} to {len(unique) + 1}")
        correct = unique.index(ratio)
        q.append(single(f"gen-q-ratio-{i:02d}", "quantitative", "easy", f"In a survey, {online} percent responded online and the rest responded by mail. What is the ratio of online responses to mail responses in lowest terms?", unique, correct, f"The ratio {online}:{store} reduces by {divisor} to {ratio}.", "Ratio", SYNTHETIC_QUANT))

        a = 3 + i % 5
        x = 4 + i
        b = 2 * i - 5
        total = a * x + b
        q.append(numeric(f"gen-q-linear-{i:02d}", "medium", f"If {a}x {b:+d} = {total}, what is x?", [x], f"Isolate {a}x, then divide by {a}, giving x = {x}.", "Algebra", SYNTHETIC_QUANT))

        known = [6 + i, 9 + i, 12 + i, 15 + i]
        target_mean = 11 + i
        missing = target_mean * 5 - sum(known)
        q.append(numeric(f"gen-q-missing-mean-{i:02d}", "medium", f"Four values are {', '.join(map(str, known))}. If these and a fifth value have mean {target_mean}, what is the fifth value?", [missing], f"The required total is 5({target_mean}) = {5 * target_mean}; subtracting the four known values gives {missing}.", "Data Analysis", SYNTHETIC_QUANT))

        total_items = 20 + 5 * i
        special = 5 + i
        numerator = total_items - special
        probability = numerator / total_items
        distractors = [max(0, probability - .15), max(0, probability - .05), probability, min(1, probability + .05), min(1, probability + .15)]
        q.append(single(f"gen-q-probability-{i:02d}", "quantitative", "medium", f"A box contains {total_items} cards, of which {special} are marked. One card is selected at random. What is the probability it is not marked?", [f"{v:.2f}" for v in distractors], 2, f"There are {numerator} unmarked cards, so the probability is {numerator}/{total_items} = {probability:.2f}.", "Probability", SYNTHETIC_QUANT))

        low = 10 + i
        high = low + 20
        grams_low = 3
        grams_high = 7
        mix = (low * grams_low + high * grams_high) / 10
        mix_choices = [mix - 6, mix - 3, mix, mix + 3, mix + 6]
        q.append(single(f"gen-q-mixture-{i:02d}", "quantitative", "medium", f"A {low}% solution weighing {grams_low} grams is mixed with a {high}% solution weighing {grams_high} grams. What percent of the mixture is the dissolved substance?", [f"{v:g}%" for v in mix_choices], 2, f"The weighted percentage is ({low}×{grams_low} + {high}×{grams_high})/10 = {mix:g}%.", "Percent", SYNTHETIC_QUANT))

        length = 12 + 2 * i
        width = 8 + i
        border = 2 + i % 3
        walkway = (length + 2 * border) * (width + 2 * border) - length * width
        q.append(numeric(f"gen-q-walkway-{i:02d}", "easy", f"A {length}-by-{width} rectangular garden has a uniform walkway {border} feet wide outside every side. What is the walkway area?", [walkway], f"Outer area minus garden area is ({length + 2 * border})({width + 2 * border}) − ({length})({width}) = {walkway}.", "Geometry", SYNTHETIC_QUANT))

        p = 5 + i
        q_pct = p + 3 if i % 2 == 0 else max(1, p - 2)
        correct_qc = 0 if p < q_pct else 1
        q.append(comparison(f"gen-q-area-change-{i:02d}", "hard", f"Rectangle A changes a reference rectangle’s length by +{p}% and width by −{p}%. Rectangle B changes the same reference length by +{q_pct}% and width by −{q_pct}%.", "Area of rectangle A", "Area of rectangle B", correct_qc, f"The factors are 1 − ({p}/100)² and 1 − ({q_pct}/100)²; the smaller absolute percentage loses less area.", "Percent", SYNTHETIC_QUANT))

        remaining = 7 + i
        answer_combinations = math.comb(remaining, 3)
        combo_choices = [answer_combinations - remaining, answer_combinations - 3, answer_combinations, answer_combinations + remaining, answer_combinations * 2]
        q.append(single(f"gen-q-combinations-{i:02d}", "quantitative", "hard", f"A committee needs 3 more members and {remaining} eligible people remain. How many different groups of 3 can be selected?", [str(value) for value in combo_choices], 2, f"Order does not matter, so the count is C({remaining},3) = {answer_combinations}.", "Combinatorics", SYNTHETIC_QUANT))
    return q


def clean_word(value):
    if not isinstance(value, str):
        return None
    value = value.strip().lower().replace("’", "'")
    if re.fullmatch(r"[a-z][a-z' -]{1,40}", value):
        return re.sub(r"\s+", " ", value)
    return None


def load_gauss_workbook(path: Path):
    sheets = json.loads(path.read_text())
    words = set()
    chinese = {}
    synonym_map = {}
    for sheet in sheets:
        rows = sheet.get("values", [])
        for row in rows:
            english = [clean_word(cell) for cell in row]
            english = [word for word in english if word]
            note = next((str(cell).strip() for cell in reversed(row) if isinstance(cell, str) and re.search(r"[\u3400-\u9fff]", cell)), "")
            for word in english:
                words.add(word)
                if note and word not in chinese:
                    chinese[word] = note[:180]
        if sheet.get("name") == "synonym":
            group = []
            for row in rows:
                row_words = [clean_word(cell) for cell in row[:3]]
                row_words = [word for word in row_words if word]
                if not row_words:
                    if 1 < len(group) <= 15:
                        for word in group:
                            synonym_map.setdefault(word, set()).update(set(group) - {word})
                    group = []
                else:
                    group.extend(row_words)
            if 1 < len(group) <= 15:
                for word in group:
                    synonym_map.setdefault(word, set()).update(set(group) - {word})
    return words, chinese, synonym_map


def load_magoosh(path: Path):
    text = path.read_text(errors="ignore")
    pattern = re.compile(r"(?m)^\s{0,20}([A-Z][A-Za-z’'-]+(?: [A-Z]?[A-Za-z’'-]+){0,3})\s*\((?:n\.|v\.|adj\.|adv\.|n\./v\.|adj\./v\.|adj\./n\.)\)\s*$")
    matches = list(pattern.finditer(text))
    words = set()
    examples = {}
    for index, match in enumerate(matches):
        word = clean_word(match.group(1))
        if not word:
            continue
        words.add(word)
        block_end = matches[index + 1].start() if index + 1 < len(matches) else min(len(text), match.end() + 1800)
        block = text[match.end():block_end]
        paragraphs = [re.sub(r"\s+", " ", part).strip() for part in re.split(r"\n\s*\n", block)]
        candidates = [part for part in paragraphs if 25 < len(part) < 280 and word in part.lower() and "Suggestions for" not in part]
        if candidates:
            examples[word] = candidates[-1]
    return words, examples


def load_high_frequency(path: Path):
    words = set()
    for line in path.read_text(errors="ignore").splitlines():
        word = clean_word(line)
        if word:
            words.add(word)
    return words


def load_oewn(directory: Path):
    synsets = {}
    for path in directory.glob("*.json"):
        if path.name.startswith("entries-") or path.name == "frames.json":
            continue
        synsets.update(json.loads(path.read_text()))
    entries = {}
    for path in directory.glob("entries-*.json"):
        for lemma, value in json.loads(path.read_text()).items():
            entries.setdefault(lemma.lower(), value)

    def lookup(word):
        entry = entries.get(word.lower())
        if not entry:
            return None
        for part in ["a", "s", "r", "v", "n"]:
            if part not in entry:
                continue
            senses = entry[part].get("sense", [])
            if not senses:
                continue
            synset = synsets.get(senses[0].get("synset"), {})
            definitions = synset.get("definition", [])
            if not definitions:
                continue
            pronunciation = next((item.get("value", "") for item in entry[part].get("pronunciation", []) if item.get("variety") == "US"), "")
            if not pronunciation:
                pronunciation = next((item.get("value", "") for item in entry[part].get("pronunciation", [])), "")
            synonyms = [item.replace("_", " ") for item in synset.get("members", []) if item.lower().replace("_", " ") != word.lower()]
            examples = synset.get("example", [])
            example = examples[0] if examples else ""
            if isinstance(example, dict):
                example = example.get("text", "")
            return {"definition": definitions[0], "pronunciation": pronunciation, "synonyms": synonyms[:6], "example": example}
        return None
    return lookup


def build_vocabulary(word3000_path, gauss_path, hf_path, magoosh_path, oewn_dir):
    sheets = json.loads(word3000_path.read_text())
    main = next(sheet for sheet in sheets if sheet.get("name") == "3000")
    rows = main["values"]
    header = {name: index for index, name in enumerate(rows[0])}
    gauss_words, gauss_chinese, synonym_map = load_gauss_workbook(gauss_path)
    high_frequency = load_high_frequency(hf_path)
    magoosh_words, magoosh_examples = load_magoosh(magoosh_path)
    oewn = load_oewn(oewn_dir)

    entries = {}
    for row in rows[1:]:
        word = clean_word(row[header["Word"]] if len(row) > header["Word"] else None)
        if not word:
            continue
        def cell(name):
            index = header[name]
            return str(row[index]).strip() if len(row) > index and row[index] is not None else ""
        sources = ["liurui39660/3000"]
        if word in gauss_words:
            sources.append("Gauss workbook")
        if word in high_frequency:
            sources.append("Gauss high-frequency list")
        if word in magoosh_words:
            sources.append("Magoosh vocabulary eBook")
        lexical = oewn(word)
        synonyms = sorted(synonym_map.get(word, set()))[:6]
        if not synonyms and lexical:
            synonyms = lexical["synonyms"]
        entries[word] = {
            "word": word,
            "pronunciation": cell("US Phonetics") or cell("UK Phonetics"),
            "definition": cell("Paraphrase (English)") or cell("Paraphrase"),
            "chinese": cell("Paraphrase") or gauss_chinese.get(word, ""),
            "synonyms": synonyms,
            "example": magoosh_examples.get(word, ""),
            "sources": sources,
            "isHighFrequency": word in high_frequency,
        }
    for word in sorted(gauss_words | high_frequency | magoosh_words):
        if word in entries:
            continue
        lexical = oewn(word)
        if not lexical:
            continue
        sources = []
        if word in gauss_words:
            sources.append("Gauss workbook")
        if word in high_frequency:
            sources.append("Gauss high-frequency list")
        if word in magoosh_words:
            sources.append("Magoosh vocabulary eBook")
        sources.append("Open English WordNet 2025")
        entries[word] = {
            "word": word,
            "pronunciation": lexical["pronunciation"],
            "definition": lexical["definition"],
            "chinese": "",
            "synonyms": sorted(synonym_map.get(word, set()))[:6] or lexical["synonyms"],
            "example": magoosh_examples.get(word, "") or lexical["example"],
            "sources": sources,
            "isHighFrequency": word in high_frequency,
        }
    return sorted(entries.values(), key=lambda item: item["word"])


def generated_verbal_questions(swift_path: Path):
    text = swift_path.read_text()
    pattern = re.compile(r'word\("([^"]+)",\s*"[^"]*",\s*"([^"]+)",\s*"[^"]*",\s*\[([^\]]*)\],\s*"([^"]+)"\)')
    entries = []
    for word, definition, synonyms_text, example in pattern.findall(text):
        synonyms = re.findall(r'"([^"]+)"', synonyms_text)
        if synonyms and re.search(rf"\b{re.escape(word)}\b", example, flags=re.IGNORECASE):
            entries.append((word, definition, synonyms, example))

    rng = random.Random(963)
    pool = [entry[0] for entry in entries]
    difficulties = ["easy", "medium", "hard"]
    questions = []
    for index, (word, definition, synonyms, example) in enumerate(entries):
        blanked = re.sub(rf"\b{re.escape(word)}\b", "______", example, count=1, flags=re.IGNORECASE)
        distractors = [candidate for candidate in pool if candidate != word and candidate not in synonyms]
        rng.shuffle(distractors)
        choices = [word] + distractors[:4]
        rng.shuffle(choices)
        difficulty = difficulties[index % 3]
        questions.append(completion(
            f"synth-v-tc-{index:03d}", difficulty, blanked, [choices], [choices.index(word)],
            f"{word} means {definition}; that meaning fits the sentence's logic and grammar.", SYNTHETIC_VERBAL,
        ))

        pair = [word, synonyms[0]]
        eq_distractors = [candidate for candidate in distractors if candidate.lower() not in {item.lower() for item in pair}][:4]
        eq_choices = pair + eq_distractors
        rng.shuffle(eq_choices)
        correct = {eq_choices.index(pair[0]), eq_choices.index(pair[1])}
        questions.append(multiple(
            f"synth-v-se-{index:03d}", "verbal", difficulty,
            f"Select two equivalent completions for this sentence: {blanked}", eq_choices, correct, 2,
            f"{word} and {synonyms[0]} are equivalent here; both express {definition}.",
            "Sentence Equivalence", SYNTHETIC_VERBAL, fmt="sentenceEquivalence",
        ))
    return questions


def main():
    source_root = ROOT.parent
    parser = argparse.ArgumentParser()
    parser.add_argument("--word3000-json", type=Path, default=source_root / "tmp/source-audit/word3000/values.json")
    parser.add_argument("--gauss-json", type=Path, default=source_root / "tmp/source-audit/workbook/values.json")
    parser.add_argument("--high-frequency", type=Path, default=source_root / "Gauss-HF-words.txt")
    parser.add_argument("--magoosh-text", type=Path, default=source_root / "tmp/pdfs/magoosh-vocab.txt")
    parser.add_argument("--oewn-dir", type=Path, default=source_root / "tmp/oewn/json")
    args = parser.parse_args()

    questions = (
        official_verbal_questions()
        + petersons_verbal_questions()
        + official_quant_questions()
        + generated_quant_questions()
        + generated_verbal_questions(APP / "VocabularyBank.swift")
    )
    vocabulary = build_vocabulary(args.word3000_json, args.gauss_json, args.high_frequency, args.magoosh_text, args.oewn_dir)

    ids = [item["id"] for item in questions]
    if len(ids) != len(set(ids)):
        raise ValueError("Generated question IDs are not unique")
    if any(not item["acceptedNumericAnswers"] and not item["correctSelections"] for item in questions):
        raise ValueError("A generated question is missing its answer key")
    for item in questions:
        for group in item["groups"]:
            texts = [choice["text"].strip().lower() for choice in group["options"]]
            if len(texts) != len(set(texts)):
                raise ValueError(f"{item['id']} contains duplicate choices")
            keyed = item["correctSelections"].get(group["id"], [])
            if not keyed or len(keyed) > group["maximumSelections"]:
                raise ValueError(f"{item['id']} contains an invalid answer key")

    resources = APP / "Resources"
    resources.mkdir(parents=True, exist_ok=True)
    (resources / "ExpandedQuestions.json").write_text(json.dumps(questions, ensure_ascii=False, indent=2) + "\n")
    (resources / "ExpandedVocabulary.json").write_text(json.dumps(vocabulary, ensure_ascii=False, indent=2) + "\n")
    manifest = {
        "expandedQuestionCount": len(questions),
        "expandedVocabularyCount": len(vocabulary),
        "questionCounts": {
            f"{measure}-{difficulty}": sum(1 for item in questions if item["measure"] == measure and item["difficulty"] == difficulty)
            for measure in ["verbal", "quantitative"]
            for difficulty in ["easy", "medium", "hard"]
        },
        "authorizedSourceQuestionCount": sum(1 for item in questions if item["source"]["isAuthorizedSourceItem"]),
    }
    (resources / "ContentManifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps(manifest, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
