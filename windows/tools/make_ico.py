#!/usr/bin/env python3
"""Builds a Windows .ico from the Mac app icon PNGs (PNG-compressed entries, Vista and later).

Usage: make_ico.py <AppIcon.appiconset dir> <output.ico>
"""
import struct
import sys
from pathlib import Path

SIZES = [16, 32, 64, 128, 256]


def main() -> None:
    src, out = Path(sys.argv[1]), Path(sys.argv[2])
    images = []
    for size in SIZES:
        data = (src / f"AppIcon-{size}.png").read_bytes()
        width, height = struct.unpack(">II", data[16:24])
        assert (width, height) == (size, size), (size, width, height)
        images.append((size, data))
    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries, blobs = b"", b""
    for size, data in images:
        dim = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(data), offset + len(blobs))
        blobs += data
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(header + entries + blobs)


if __name__ == "__main__":
    main()
