# Gre-Simulation

A native Swift + SwiftUI GRE® practice simulator for macOS and iPadOS.

## What is implemented

- Current shorter GRE structure: one 30-minute Analytical Writing task, two Verbal sections (12/15 questions), and two Quantitative sections (12/15 questions)
- Official section timing: 18/23 minutes for Verbal and 21/26 minutes for Quant
- Section-level adaptive routing for the second Verbal and Quant sections
- Test-style navigation with Back, Next, Mark, Review, hidden/visible time, and automatic section submission when time expires
- Text Completion, Sentence Equivalence, Reading Comprehension, Quantitative Comparison, single-answer, multiple-answer, and Numeric Entry interfaces
- Basic on-screen Quant calculator with memory operations
- 90 independently authored practice questions aligned to the ETS content outline
- 45-word vocabulary recall lab with original English/Chinese notes, synonyms, pronunciation, and examples
- Practice score report, answer explanations, adaptive route details, and persistent on-device score history
- A transparent 130–170 practice score estimator and a clearly labeled local writing-structure signal
- Direct links to official ETS test format, scoring, POWERPREP, Verbal, Quant, Math Review, and Khan Academy resources

## Platforms

- macOS 14+
- iPadOS 17+
- Xcode 26 project using native SwiftUI; no third-party runtime dependencies

Open [`Gre Simulation.xcodeproj`](Gre%20Simulation.xcodeproj) and run the `Gre Simulation` scheme on My Mac or an iPad simulator/device.

## Content and scoring policy

The built-in questions are independently authored. They follow the current ETS-published content areas and question formats but do not copy official paid questions or POWERPREP items.

ETS reports Verbal and Quant scores on a 130–170 scale and uses proprietary equating to account for edition difficulty and the adaptive second-section route. Since the conversion table is not public, this app maps raw accuracy linearly onto 130–170 and labels the result as an estimate. The writing signal is a local heuristic, not an official score or e-rater® result.

The community [`liurui39660/3000`](https://github.com/liurui39660/3000) spreadsheet was reviewed as a vocabulary reference. Its repository states that it currently has no selected license and permits personal downloads while requesting attribution for redistribution. For that reason, its spreadsheet and definitions are linked but are not bundled or republished here.

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
