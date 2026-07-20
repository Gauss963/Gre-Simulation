# Gre-Simulation

A native Swift + SwiftUI GRE® practice simulator for macOS and iPadOS.

## What is implemented

- Current shorter GRE structure: one 30-minute Analytical Writing task, two Verbal sections (12/15 questions), and two Quantitative sections (12/15 questions)
- Official section timing: 18/23 minutes for Verbal and 21/26 minutes for Quant
- Section-level adaptive routing for the second Verbal and Quant sections
- Test-style navigation with Back, Next, Mark, Review, hidden/visible time, and automatic section submission when time expires
- Text Completion, Sentence Equivalence, Reading Comprehension, Quantitative Comparison, single-answer, multiple-answer, and Numeric Entry interfaces
- Basic on-screen Quant calculator with memory operations
- 3,212-item offline bank with an exact 1:1 scored-measure balance: 1,602 Verbal, 1,602 Quantitative, and 8 imported Analytical Writing prompts, plus the app's original Issue prompt
- 62 source-derived questions from the user's authorized ETS Official Guide and Peterson's book, each labeled in post-test review
- 240 deterministic vocabulary-synthesis and ETS-aligned original questions, plus 1,357 new Super Power Pack-aligned original Quant questions and the original 90-question bank
- 442 Data Analysis and statistics questions in the runtime pool, including 384 questions built around 123 structured data displays
- Exposure-aware Quant selection that rotates question families and remembers the most recent 800 exact questions to reduce repeat encounters across practice sessions
- Native, accessible tables, bar and grouped-bar charts, line charts, pie charts, histograms, scatterplots, box plots, normal curves, and Venn diagrams that adapt to macOS, iPadOS, and system appearance
- 4,613 deduplicated vocabulary entries assembled from the authorized 3000, Gauss, Magoosh, and 2026-07-20 contextual-vocabulary sources; every entry includes a Taiwan Traditional Chinese definition and an English example
- Vocabulary search and filtering by source or high-frequency list
- Practice score report, answer explanations, adaptive route details, and persistent on-device score history
- A transparent 130–170 practice score estimator and a clearly labeled local writing-structure signal
- Direct links to official ETS test format, scoring, POWERPREP, Verbal, Quant, Math Review, and Khan Academy resources

## Platforms

- macOS 14+
- iPadOS 17+
- Xcode 26 project using native SwiftUI; no third-party runtime dependencies

Open [`Gre Simulation.xcodeproj`](Gre%20Simulation.xcodeproj) and run the `Gre Simulation` scheme on My Mac or an iPad simulator/device.

## Content provenance and scoring

This build includes source-derived items from the user's locally supplied, fully authorized GRE library, including *The Official Guide to the GRE General Test*, *Peterson's Master the GRE General Test*, the 2026-07-20 Verbal/Quant/Writing collection, and the licensed vocabulary files. It also contains original items aligned to the official content outline. The score-report review identifies the source and location of every sourced item and distinguishes it from generated practice.

ETS reports Verbal and Quant scores on a 130–170 scale and uses proprietary equating to account for edition difficulty and the adaptive second-section route. Since the conversion table is not public, this app maps raw accuracy linearly onto 130–170 and labels the result as an estimate. The writing signal is a local heuristic, not an official score or e-rater® result.

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

Debug launches validate unique question identifiers, answer keys, minimum pool sizes, per-difficulty coverage, and structured figure decoding. The content generator separately rejects malformed answer groups and empty chart/table data.

## Trademark notice

GRE®, POWERPREP®, and e-rater® are trademarks of ETS. This independent practice app is not affiliated with or endorsed by ETS.
