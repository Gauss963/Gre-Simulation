#!/usr/bin/env python3
"""Extract page-delimited text from authorized PDFs for content auditing."""

import argparse
from pathlib import Path

from pypdf import PdfReader


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()

    reader = PdfReader(args.input)
    pages = []
    for number, page in enumerate(reader.pages, 1):
        pages.append(f"===== PDF PAGE {number} =====\n{page.extract_text() or ''}")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text("\n\n".join(pages), encoding="utf-8")
    print(f"Extracted {len(reader.pages)} pages to {args.output}")


if __name__ == "__main__":
    main()
