# Authorized content pipeline

Install the build-time conversion dependency before regenerating vocabulary:

```sh
python3 -m pip install opencc-python-reimplemented
```

The generator uses OpenCC's `s2twp` conversion so all bundled Chinese definitions use Taiwan Traditional Chinese and Taiwan-localized terminology.

These tools rebuild the app's normalized offline resources without bundling the source books or workbooks.

1. Export each authorized `.xlsx` file with `extract_authorized_workbook.mjs`. It uses `@oai/artifact-tool` from the Codex workspace runtime.
2. Extract page-delimited PDF text with `extract_authorized_pdfs.py` for page-by-page auditing.
3. Download and unzip the [Open English WordNet 2025 JSON release](https://en-word.net/static/english-wordnet-2025-json.zip).
4. Run `generate_authorized_resources.py`, passing alternate paths when the defaults do not match the local workspace.

The generator writes `ExpandedQuestions.json`, `ExpandedVocabulary.json`, and `ContentManifest.json` under the app's `Resources` folder. It is deterministic and fails on duplicate IDs, duplicate answer choices, or invalid answer keys.

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
