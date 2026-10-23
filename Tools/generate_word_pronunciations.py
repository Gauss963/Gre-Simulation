#!/usr/bin/env python3
"""Pre-render natural word pronunciations for offline flash cards.

Build-time only: the app never links Kokoro, PyTorch, or any other library.
Each headword is synthesized once with the Apache-2.0 Kokoro-82M neural voice,
trimmed, encoded as mono AAC with macOS `afconvert`, and concatenated into one
resource pack plus a JSON index of byte ranges:

    WordPronunciations.pack   concatenated .m4a clips
    WordPronunciations.json   {"voice": ..., "clips": {"<lowercased word>": [offset, length]}}

Usage (inside a virtualenv with `pip install kokoro soundfile`):

    python Tools/generate_word_pronunciations.py \
        --words words.json --output-dir "App/Resources" --cache tmp/pronunciation-cache

`words.json` is a JSON array of headwords. Clips are cached by headword, so a
re-run only renders new words.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import tempfile
from pathlib import Path

VOICE = "af_heart"
MODEL = "hexgrad/Kokoro-82M"
SAMPLE_RATE = 24_000
SPEED = 0.92
BITRATE = 32_000


def speech_text(word: str) -> str:
    """Turn dictionary notation into something a speaker would actually say."""
    text = word.strip()
    text = re.sub(r"\bsth\b", "something", text)
    text = re.sub(r"\bsb\b", "somebody", text)
    text = text.replace("/", " or ")
    text = re.sub(r"\s*(?:\.\.\.|…)\s*", ", ", text).strip(" ,")
    text = re.sub(r"\s+", " ", text)
    return text


def cache_name(key: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", key).strip("-")[:40] or "word"
    return f"{slug}-{hashlib.sha1(key.encode('utf-8')).hexdigest()[:10]}.m4a"


def trim(audio, threshold: float = 0.008, pad: int = int(0.06 * SAMPLE_RATE)):
    import numpy as np

    loud = np.flatnonzero(np.abs(audio) > threshold)
    if loud.size == 0:
        return audio
    return audio[max(0, loud[0] - pad) : min(len(audio), loud[-1] + pad)]


def render(words: list[str], cache: Path) -> None:
    import numpy as np
    import soundfile as sf
    import torch
    from kokoro import KPipeline

    torch.set_num_threads(4)
    pipeline = KPipeline(lang_code="a", repo_id=MODEL)
    cache.mkdir(parents=True, exist_ok=True)
    pending = [word for word in words if not (cache / cache_name(word.lower().strip())).exists()]
    print(f"{len(words) - len(pending)} cached, {len(pending)} to render", flush=True)
    with tempfile.TemporaryDirectory() as scratch:
        wav = Path(scratch) / "clip.wav"
        for index, word in enumerate(pending, 1):
            chunks = [audio.numpy() for _, _, audio in pipeline(speech_text(word), voice=VOICE, speed=SPEED)]
            if not chunks:
                print(f"skipped (no audio): {word}", flush=True)
                continue
            sf.write(wav, trim(np.concatenate(chunks)), SAMPLE_RATE)
            target = cache / cache_name(word.lower().strip())
            subprocess.run(
                ["afconvert", "-f", "m4af", "-d", "aac", "-b", str(BITRATE), "-c", "1", str(wav), str(target)],
                check=True,
            )
            if index % 200 == 0:
                print(f"rendered {index}/{len(pending)}", flush=True)


def pack(words: list[str], cache: Path, output_dir: Path) -> None:
    clips: dict[str, list[int]] = {}
    blob = bytearray()
    for key in sorted({word.lower().strip() for word in words}):
        path = cache / cache_name(key)
        if not path.exists():
            continue
        data = path.read_bytes()
        clips[key] = [len(blob), len(data)]
        blob.extend(data)
    output_dir.mkdir(parents=True, exist_ok=True)
    (output_dir / "WordPronunciations.pack").write_bytes(bytes(blob))
    index = {"voice": f"Kokoro-82M {VOICE}", "license": "Apache-2.0", "format": "m4a", "clips": clips}
    (output_dir / "WordPronunciations.json").write_text(
        json.dumps(index, ensure_ascii=False, separators=(",", ":"), sort_keys=True) + "\n", encoding="utf-8"
    )
    print(f"packed {len(clips)} clips, {len(blob) / 1_048_576:.1f} MB", flush=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--words", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--cache", type=Path, required=True)
    parser.add_argument("--pack-only", action="store_true")
    args = parser.parse_args()
    words = list(dict.fromkeys(w for w in json.loads(args.words.read_text(encoding="utf-8")) if w.strip()))
    if not args.pack_only:
        render(words, args.cache)
    pack(words, args.cache, args.output_dir)


if __name__ == "__main__":
    main()
