# Gre-Simulation

A native Swift + SwiftUI GRE® practice simulator for macOS and iPadOS.

## What is implemented

- Full-length test that follows the current GRE General Test: Analytical Writing always first, then two Verbal sections (12/15 questions) and two Quantitative sections (12/15 questions) in one of the official orders, with no unscored section and no breaks (1 hr 58 min)
- Official section timing: 30 minutes for the Issue task, 18/23 minutes for Verbal, and 21/26 minutes for Quant
- Section-level adaptive routing: the first section of each measure is of average difficulty and the second is Easy, Medium, or Hard depending on first-section performance
- Official section composition: Verbal opens with Text Completion (1, 2, and 3 blanks), keeps Reading Comprehension passages together (about half the section), and includes Sentence Equivalence; Quant opens with Quantitative Comparison (about a third) and includes one Data Interpretation set
- Test-style navigation: Back/Next within a section, Mark, a Review screen with Answered/Incomplete/Not Answered/Not Encountered status and Go to Question, Hide/Show Time with the clock forced visible in the last five minutes, Exit Section, and automatic submission when time expires
- Text Completion, Sentence Equivalence, Reading Comprehension (select one of five, select all that apply of three, and Select-in-Passage), Quantitative Comparison, single-answer, multiple-answer, and Numeric Entry (single box or fraction boxes) interfaces
- Floating, draggable on-screen calculator laid out like the official one: order of operations, parentheses, memory (MR/MC/M+), square root, ±, C/CE, an 8-digit display with ERROR, and Transfer Display for single-box Numeric Entry
- Analytical Writing word processor like the real test (Cut, Paste, Undo, Redo; no spelling or grammar checking), with all six official Issue instruction variants
- Separate Verbal-only, Quant-only, and Writing-only practice modes
- 6,379-item core bank with an exact 1:1 scored-measure balance: 3,184 Verbal, 3,184 Quantitative, and 11 imported Analytical Writing prompts, plus the app's original Issue prompt
- 159 original 2026-format additions: 101 Reading Comprehension questions (including Select-in-Passage) and 58 Issue prompts covering all six official instruction variants
- 3,110 source-derived questions from the user's authorized GRE library, each labeled in post-test review
- 240 deterministic vocabulary-synthesis and ETS-aligned original questions, plus 2,939 Super Power Pack-aligned original Quant questions and the original 90-question bank
- 837 Data Analysis and statistics questions in the runtime pool, including 779 questions built around 201 structured data displays
- Exposure-aware Quant selection that rotates question families and remembers the most recent 800 exact questions to reduce repeat encounters across practice sessions
- Native, accessible tables, bar and grouped-bar charts, line charts, pie charts, histograms, scatterplots, box plots, normal curves, and Venn diagrams that adapt to macOS, iPadOS, and system appearance
- Data charts include high-contrast horizontal grid lines, fine trailing-axis ticks, and exact values above bars so frequencies can be read unambiguously
- 5,995 deduplicated vocabulary entries assembled from the authorized 3000, Gauss, Magoosh, Gruber, and 2026-07-20 collections; every entry has a manually reviewed part of speech, English definition, and complete Taiwan Traditional Chinese senses ordered by how often the GRE tests them, plus an English example
- Vocabulary search and filtering by source or high-frequency list
- Flash-card study sessions with fixed decks or an **Infinite loop** that brings each missed card back after about 15–40 other cards until every card is mastered, persistent mastery progress, natural offline neural pronunciation for every headword, and a V-key listening shortcut
- Practice score report, answer explanations, adaptive route details, and persistent on-device score history with reopenable per-question correct/incorrect review
- 130–170 practice score estimates that weight the second-section route and convert raw scores with tables modeled on ETS's published conversion tables, NS for a measure with no answers, and ETS percentile ranks
- Issue essays scored 0–6 in half points by averaging two independent ratings (a rubric reading and a feature model), mirroring ETS's human + e-rater design
- Direct links to official ETS test format, scoring, POWERPREP, Verbal, Quant, Math Review, and Khan Academy resources

## Platforms

- macOS 14+
- iPadOS 17+
- Xcode 26 project using native SwiftUI; no third-party runtime dependencies

Open [`Gre Simulation.xcodeproj`](Gre%20Simulation.xcodeproj) and run the `Gre Simulation` scheme on My Mac or an iPad simulator/device.

## Content provenance and scoring

This build includes source-derived items from the user's locally supplied, fully authorized GRE library, including *The Official Guide to the GRE General Test*, *Peterson's Master the GRE General Test*, both 2026-07-20 collections, Gruber's guide, and the licensed vocabulary files. It also contains original items aligned to the official content outline. The score-report review identifies the source and location of every sourced item and distinguishes it from generated practice.

ETS reports Verbal and Quant scores on a 130–170 scale and uses proprietary equating to account for edition difficulty and the adaptive second-section route. Since the computer-test conversion is not public, this app weights second-section answers by route and interpolates conversion tables modeled on ETS's published paper-test tables, and labels the result as an estimate. The Analytical Writing score averages two local ratings on the official 0–6 half-point scale; it is a practice estimate, not an ETS score or e-rater® result.

The vocabulary bank is generated from the user's authorized [`liurui39660/3000`](https://github.com/liurui39660/3000) workbook, Gauss high-frequency list and workbook, Magoosh vocabulary eBook, and contextual-vocabulary collection. Unmatched valid terms receive definitions and synonyms from [Open English WordNet 2025](https://en-word.net/), licensed CC BY 4.0. Missing Chinese glosses are supplemented by the MIT-licensed [ECDICT](https://github.com/skywind3000/ECDICT) database and localized to Taiwan Traditional Chinese. Missing examples are selected from the [Tatoeba Corpus](https://tatoeba.org/en/downloads) under CC0 1.0 or CC BY 2.0 FR, with definition-grounded original examples used only when the corpus has no suitable complete sentence. Source workbooks and PDFs are not copied into the app bundle; normalized JSON resources are bundled for offline use.

The reproducible import pipeline is in [`Tools`](Tools): the workbook exporter uses `@oai/artifact-tool`, and the deterministic Python generators validate IDs, answer keys, duplicate choices, content uniqueness, structured figures, and difficulty coverage before writing app resources.

See [SOURCES.md](SOURCES.md) for the source audit and design decisions.

## Verification

The app has been built successfully for both:

```sh
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  xcodebuild -project "Gre Simulation.xcodeproj" \
  -scheme "Gre Simulation" -destination "platform=macOS" \
  CODE_SIGNING_ALLOWED=NO build

DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer \
  xcodebuild -project "Gre Simulation.xcodeproj" \
  -scheme "Gre Simulation" \
  -destination "platform=iOS Simulator,name=iPad Pro 13-inch (M5)" \
  CODE_SIGNING_ALLOWED=NO build
```

Debug launches validate that every vocabulary entry has a reviewed gloss and a bundled pronunciation clip, plus unique question identifiers, answer keys, minimum pool sizes, per-difficulty coverage, and structured figure decoding. The content generator separately rejects malformed answer groups and empty chart/table data.

## Trademark notice

GRE®, POWERPREP®, and e-rater® are trademarks of ETS. This independent practice app is not affiliated with or endorsed by ETS.
