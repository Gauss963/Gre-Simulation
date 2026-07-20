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

The source PDFs and workbooks remain outside the application repository. The app bundles compact JSON records only. Source-derived questions may be normalized or condensed for computer display; the review screen identifies them as authorized source items, not necessarily verbatim facsimiles.

## Open lexical supplement

- [Open English WordNet 2025](https://en-word.net/): current open lexical database used to provide definitions, pronunciations, examples, and synonyms for 1,525 valid Gauss/Magoosh terms not found in the 3000 workbook. OEWN is released under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
- [ECDICT](https://github.com/skywind3000/ECDICT), commit `bc015ed2e24a7abef49fc6dbbb7fe32c1dadaf8b`: MIT-licensed English-Chinese database used to fill 1,525 missing Chinese glosses. The imported general gloss is converted with OpenCC `s2twp` so the bundled text uses Taiwan Traditional Chinese and Taiwan-localized terminology.
- [Tatoeba English sentence exports](https://tatoeba.org/en/downloads), generated 2026-07-18: 358 missing examples come from the English CC0 export, and 2,428 come from the English corpus under [CC BY 2.0 FR](https://creativecommons.org/licenses/by/2.0/fr/). Every attributed record stores its Tatoeba sentence URL. Another 638 rare terms use definition-grounded original examples because no neutral, complete corpus sentence passed the quality filters.

All 4,557 bundled resource records now contain both a Chinese definition and an English example. The 45 curated app entries are also complete; after deduplication, the runtime bank contains 4,560 complete words.

Misspellings, stray worksheet annotations, unsupported phrases, and terms without a reliable 3000 or OEWN entry are excluded. This prevents incorrect column adjacency in the workbook from becoming a false definition. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for the redistribution notices.

## Generated practice and scoring

The 62 source-derived items are supplemented by 330 original questions: the original 90-item bank plus 240 deterministic vocabulary-synthesis and ETS-aligned Quant items. The runtime pool now contains 98 Data Analysis/statistics questions. Of the 60-question 2026-07-20 expansion, 45 questions use 15 original structured data displays and the remaining 15 cover standalone statistics, probability, distributions, and counting. No official question text or numerical scenario was copied for this expansion.

The generator checks IDs, keys, duplicate choices, route coverage, and nonempty chart/table data. Structured figures are rendered at runtime with native SwiftUI Charts, Grid, and Canvas rather than bundled screenshots.

The resulting score is a simulation. It must not be described as an official or predictive ETS score because the operational item parameters and equating tables are not public.
