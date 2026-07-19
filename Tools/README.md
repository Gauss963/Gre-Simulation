# Authorized content pipeline

These tools rebuild the app's normalized offline resources without bundling the source books or workbooks.

1. Export each authorized `.xlsx` file with `extract_authorized_workbook.mjs`. It uses `@oai/artifact-tool` from the Codex workspace runtime.
2. Extract page-delimited PDF text with `extract_authorized_pdfs.py` for page-by-page auditing.
3. Download and unzip the [Open English WordNet 2025 JSON release](https://en-word.net/static/english-wordnet-2025-json.zip).
4. Run `generate_authorized_resources.py`, passing alternate paths when the defaults do not match the local workspace.

The generator writes `ExpandedQuestions.json`, `ExpandedVocabulary.json`, and `ContentManifest.json` under the app's `Resources` folder. It is deterministic and fails on duplicate IDs, duplicate answer choices, or invalid answer keys.

The source-derived question records are intentionally reviewed and curated in the generator rather than scraped blindly from PDF layout. This preserves answer grouping and prevents missing equations or diagrams from becoming malformed questions.
