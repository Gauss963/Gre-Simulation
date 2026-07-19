# Gre-Simulation

A native Swift + SwiftUI GRE® practice simulator for macOS and iPadOS.

## What is implemented

- Current shorter GRE structure: one 30-minute Analytical Writing task, two Verbal sections (12/15 questions), and two Quantitative sections (12/15 questions)
- Official section timing: 18/23 minutes for Verbal and 21/26 minutes for Quant
- Section-level adaptive routing for the second Verbal and Quant sections
- Test-style navigation with Back, Next, Mark, Review, hidden/visible time, and automatic section submission when time expires
- Text Completion, Sentence Equivalence, Reading Comprehension, Quantitative Comparison, single-answer, multiple-answer, and Numeric Entry interfaces
- Basic on-screen Quant calculator with memory operations
- 332-question offline bank: 166 Verbal and 166 Quant questions, balanced across three difficulty routes
- 62 source-derived questions from the user's authorized ETS Official Guide and Peterson's book, each labeled in post-test review
- 180 additional deterministic vocabulary-synthesis and parameterized Quant questions, plus the original 90-question bank
- 4,560 deduplicated vocabulary entries assembled from the authorized 3000, Gauss, and Magoosh sources, with Open English WordNet 2025 used to fill reliable definitions for unmatched terms
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

This build includes source-derived items from the user's locally supplied, fully authorized copies of *The Official Guide to the GRE General Test* and *Peterson's Master the GRE General Test*. It also contains original items aligned to the official content outline. The score-report review identifies the source and location of every sourced item and distinguishes it from generated practice.

ETS reports Verbal and Quant scores on a 130–170 scale and uses proprietary equating to account for edition difficulty and the adaptive second-section route. Since the conversion table is not public, this app maps raw accuracy linearly onto 130–170 and labels the result as an estimate. The writing signal is a local heuristic, not an official score or e-rater® result.

The vocabulary bank is generated from the user's authorized [`liurui39660/3000`](https://github.com/liurui39660/3000) workbook, Gauss high-frequency list and workbook, and Magoosh vocabulary eBook. Unmatched valid terms receive definitions and synonyms from [Open English WordNet 2025](https://en-word.net/), licensed CC BY 4.0. Source workbooks and PDFs are not copied into the app bundle; normalized JSON resources are bundled for offline use.

The reproducible import pipeline is in [`Tools`](Tools): the workbook exporter uses `@oai/artifact-tool`, and the deterministic Python generator validates IDs, answer keys, duplicate choices, and difficulty coverage before writing app resources.

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

Debug launches validate unique question identifiers, answer keys, minimum pool sizes, and per-difficulty coverage.

## Trademark notice

GRE®, POWERPREP®, and e-rater® are trademarks of ETS. This independent practice app is not affiliated with or endorsed by ETS.
