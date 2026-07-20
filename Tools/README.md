# Authorized content pipeline

Install the build-time conversion dependency before regenerating vocabulary:

```sh
python3 -m pip install opencc-python-reimplemented
```

The generator uses OpenCC's `s2twp` conversion so all bundled Chinese definitions use Taiwan Traditional Chinese and Taiwan-localized terminology.

Download the open lexical and example-sentence supplements outside the repository:

```sh
mkdir -p ../tmp/open-lexicon ../tmp/open-corpus
curl -L https://raw.githubusercontent.com/skywind3000/ECDICT/master/ecdict.csv \
  -o ../tmp/open-lexicon/ecdict.csv
curl -L https://downloads.tatoeba.org/exports/per_language/eng/eng_sentences_CC0.tsv.bz2 \
  -o ../tmp/open-corpus/eng_sentences_CC0.tsv.bz2
curl -L https://downloads.tatoeba.org/exports/per_language/eng/eng_sentences.tsv.bz2 \
  -o ../tmp/open-corpus/eng_sentences.tsv.bz2
```

`enrich_vocabulary.py` can fill an existing `ExpandedVocabulary.json` directly. It selects deterministic, complete Tatoeba sentences, rejects explicit, violent, partisan, current-affairs, named-person, and metalinguistic content, and writes sentence-level attribution URLs. If neither corpus has an acceptable example, it uses a definition-grounded original sentence. The command fails if any Chinese definition or example remains empty.

These tools rebuild the app's normalized offline resources without bundling the source books or workbooks.

1. Export each authorized `.xlsx` file with `extract_authorized_workbook.mjs`. It uses `@oai/artifact-tool` from the Codex workspace runtime.
2. Extract page-delimited PDF text with `extract_authorized_pdfs.py` for page-by-page auditing.
3. Download and unzip the [Open English WordNet 2025 JSON release](https://en-word.net/static/english-wordnet-2025-json.zip).
4. Download ECDICT and the Tatoeba English CC0/full exports using the commands above.
5. Run `generate_authorized_resources.py`, passing alternate paths when the defaults do not match the local workspace.

To add or revise questions without touching the completed vocabulary resource, run:

```sh
python3 Tools/generate_authorized_resources.py --questions-only
```

The generator writes `ExpandedQuestions.json`, `ExpandedVocabulary.json`, and `ContentManifest.json` under the app's `Resources` folder. It is deterministic and fails on duplicate IDs, duplicate answer choices, invalid answer keys, empty structured figures, or incomplete vocabulary records. The original chart-based Data Analysis items live in `quantitative_expansion.py`; their tables, bar charts, line charts, pie charts, histograms, scatterplots, box plots, normal curves, and Venn diagrams are stored as data rather than screenshots.

The source-derived question records are intentionally reviewed and curated in the generator rather than scraped blindly from PDF layout. This preserves answer grouping and prevents missing equations or diagrams from becoming malformed questions.

## macOS app icon

`generate_mac_app_icon.swift` clips `Design/AppIconSource.png` to the macOS rounded shape on a genuinely transparent canvas. It also validates that the corner alpha is zero before writing the PNG. Generate the 1024-point source with:

```sh
DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer xcrun swift \
  Tools/generate_mac_app_icon.swift \
  Design/AppIconSource.png \
  "Gre Simulation/Assets.xcassets/AppIcon.appiconset/AppIcon-mac-1024.png"
```

Use `sips` to derive the 16, 32, 64, 128, 256, and 512-pixel macOS slots from that validated source.
