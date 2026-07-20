# Source audit

Checked on 2026-07-20.

## Primary specifications

- [ETS — GRE General Test Content & Structure](https://www.ets.org/gre/score-users/about/general-test/content-structure.html): one 30-minute Analyze an Issue task; Verbal sections of 12/15 questions in 18/23 minutes; Quant sections of 12/15 questions in 21/26 minutes.
- [ETS — Understanding GRE General Test Scores](https://www.ets.org/gre/test-takers/general-test/scores/understand-scores.html): raw score is the number correct; all questions within a measure contribute equally; the second section is selected based on first-section performance; ETS converts raw scores using equating.
- [ETS — Verbal Reasoning Overview](https://www.ets.org/gre/test-takers/general-test/prepare/content/verbal-reasoning.html): Reading Comprehension, Text Completion, and Sentence Equivalence.
- [ETS — Quantitative Reasoning Overview](https://www.ets.org/gre/test-takers/general-test/prepare/content/quantitative-reasoning.html): Arithmetic, Algebra, Geometry, and Data Analysis; Quantitative Comparison, single choice, multiple choice, and Numeric Entry. The Data Analysis audit covers descriptive statistics, percentiles, standard deviation and interquartile range; tables, line/bar/circle/box/scatter plots and frequency distributions; probability distributions, normal distributions, conditional probability, combinations, permutations, and Venn diagrams. Inferential statistics is intentionally excluded because ETS states that it is not tested.
- [ETS — GRE Math Review](https://www.ets.org/content/dam/ets-org/pdfs/gre/gre-math-review.pdf): official topic and display review used to audit the scope and level of original Quant questions. The Data Analysis chapter's table, bar, line, circle, histogram, scatterplot, box-plot, and normal-distribution examples were visually inspected before designing the app's structured figures.
- [ETS — POWERPREP Practice Tests](https://www.ets.org/gre/test-takers/general-test/prepare/powerprep.html): official reference for timed behavior, calculator availability, and moving, changing, marking, and reviewing answers within a section.
- [ETS — Khan Academy Quant Mapping](https://www.ets.org/gre/test-takers/general-test/prepare/khan-prep-videos.html): free instructional links mapped by ETS to Math Review topics.

## User-authorized local material

The user explicitly confirmed full rights to incorporate and remix the following supplied material in this project:

- *The Official Guide to the GRE General Test*, Third Edition (ETS): Chapter 4 Verbal practice and Chapter 6 Quant practice items, keys, and explanations were audited. The build includes 46 source-derived items with book/page metadata.
- *Peterson's Master the GRE General Test 2020*: the diagnostic Verbal sections and answer explanations were audited. The build includes 16 source-derived items with location metadata.
- *Magoosh Complete Guide to GRE Vocabulary*: 225 entry headings were identified; 215 valid entries overlap the normalized final bank and retain a Magoosh source tag.
- `Gauss-HF-words.txt`: 1,923 valid high-frequency entries survive normalization and authoritative-definition matching.
- `Gauss-GRE單字.xlsx`: all 12 sheets were inspected and the valid terms/synonym groupings were merged and tagged.
- [liurui39660/3000](https://github.com/liurui39660/3000): all 3,032 valid rows from the authorized workbook are normalized into the app resource.
- The `20260720` authorized collection: the 1,250-item Text Completion/Sentence Equivalence superset and answer workbook, 麟渡兮 fill-in collection, 老肖 250 Reading collection, 猴哥 Quant collection, Official Super Power Pack, *The GRE Test For Dummies*, North American Issue materials, and contextual-vocabulary book were audited together. The importer added 1,436 Verbal questions, 19 fully recoverable Quantitative questions with detailed explanations, 8 current-format Issue prompts, and 129 vocabulary records. Overlapping 1,000/1,100 fill-in files and the mirrored ZIP were deduplicated.
- The `20260720-2` authorized collection: all 39 PDFs were extracted with page markers and audited. The deterministic import adds 337 questions from the 36-set reading collection, 1,199 from *GRE 閱讀 300 篇*, 46 current Text Completion questions and 3 current Issue prompts from *Gruber's Complete GRE Guide*. Gruber's 3,400-word list contributes 1,383 words that were not already present and adds a Gruber source tag to 1,997 existing words. The 34 per-set analysis PDFs were used to cross-check answer keys and explanations.

The 2026-07-20 importers are deliberately conservative. They reject items whose scanned columns, figures, formulas, choices, or answer keys cannot be paired unambiguously; obsolete analogy/antonym and Analyze-an-Argument formats are not placed into the current GRE simulator. Reference-only prose, order confirmations, and purchase details are not bundled. [`Authorized20260720Manifest.json`](Gre%20Simulation/Resources/Authorized20260720Manifest.json) and [`Authorized20260720Part2Manifest.json`](Gre%20Simulation/Resources/Authorized20260720Part2Manifest.json) record detected, imported, duplicate, and rejected counts. For the 300-reading collection, the lowest accepted title-to-answer-table match is 90.24%; passages with a nonunique answer partition are excluded.

The source PDFs and workbooks remain outside the application repository. The app bundles compact JSON records only. Source-derived questions may be normalized or condensed for computer display; the review screen identifies them as authorized source items, not necessarily verbatim facsimiles.

## Open lexical supplement

- [Open English WordNet 2025](https://en-word.net/): current open lexical database used to provide definitions, pronunciations, examples, and synonyms for 1,525 valid Gauss/Magoosh terms not found in the 3000 workbook. OEWN is released under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
- [ECDICT](https://github.com/skywind3000/ECDICT), commit `bc015ed2e24a7abef49fc6dbbb7fe32c1dadaf8b`: MIT-licensed English-Chinese database used by 2,908 runtime records. The imported general gloss is converted with OpenCC `s2twp` so bundled text uses Taiwan Traditional Chinese and Taiwan-localized terminology.
- [Tatoeba English sentence exports](https://tatoeba.org/en/downloads), generated 2026-07-18: after deduplication, 565 runtime examples come from the English CC0 export and 3,727 from the English corpus under [CC BY 2.0 FR](https://creativecommons.org/licenses/by/2.0/fr/). Every attributed record stores its Tatoeba sentence URL. Another 1,129 terms use definition-grounded original examples; the remaining reviewed source examples are preserved. Examples that did not actually demonstrate their headword were replaced during the 1.2.0 audit.

All 8,066 bundled vocabulary records contain both a Taiwan Traditional Chinese definition and an English example. After merging duplicate source records and the curated app entries, the runtime bank contains 5,993 complete words.

Misspellings, stray worksheet annotations, unsupported phrases, and terms without a reliable 3000 or OEWN entry are excluded. This prevents incorrect column adjacency in the workbook from becoming a false definition. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for the redistribution notices.

## Generated practice and scoring

The earlier 62 source-derived items are supplemented by the original 90-item bank, 240 deterministic vocabulary-synthesis and ETS-aligned items, and 2,939 original Quant items. The first authorized 2026-07-20 import adds 1,463 unique items after content-level deduplication; `20260720-2` adds another 1,585. The runtime bank therefore contains 6,379 bundled items: 3,184 Verbal, 3,184 Quantitative, and 11 Analytical Writing prompts. The app's original Issue prompt is selected separately for writing practice.

The balanced expansion contributes 735 Arithmetic, 735 Algebra, 735 Geometry, and 734 Data Analysis questions. Its difficulty distribution is 980 easy, 980 medium, and 979 hard; it includes Quantitative Comparison, single-answer, multiple-answer, and Numeric Entry formats. The generator contains 143 normalized stem families, with no more than 31 variants in a family, and every item has a Taiwan Traditional Chinese explanation. These are original mathematical scenarios based only on the Super Power Pack's content boundaries, conventions, and format rules; no official item text is copied.

The runtime pool now includes 837 Data Analysis/statistics questions. Across the earlier and balanced expansions, 779 questions use 201 original structured datasets and the remainder cover standalone statistics, probability, distributions, and counting. No official question text or numerical scenario was copied for these original expansions; authorized source items are labeled separately in review.

To reduce perceived repetition in actual sessions, Quant selection first rotates across distinct structural families, prefers families not used recently, and retains up to 800 recently delivered exact question IDs before recycling them. This exposure history is stored locally in `UserDefaults`; it does not affect scoring or require an account.

The generators check IDs, keys, duplicate choices, content uniqueness, route coverage, and nonempty chart/table data. [`BalancedQuantManifest.json`](Gre%20Simulation/Resources/BalancedQuantManifest.json) records the exact area, difficulty, format, and normalized-family counts. Structured figures are rendered at runtime with native SwiftUI Charts, Grid, and Canvas rather than bundled screenshots.

The resulting score is a simulation. It must not be described as an official or predictive ETS score because the operational item parameters and equating tables are not public.
