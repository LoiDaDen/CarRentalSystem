from __future__ import annotations

import re

_NON_ALNUM = re.compile(r"[^A-Z0-9]")


def clean_ocr_text(value: str) -> str:
    text = value.upper().strip()
    replacements = {
        "|": "1",
    }
    for source, target in replacements.items():
        text = text.replace(source, target)
    return text


def normalize_plate(value: str | None) -> str:
    if not value:
        return ""
    return _NON_ALNUM.sub("", value.upper())


def looks_like_vietnam_plate(value: str | None) -> bool:
    plate = normalize_plate(value)
    if len(plate) < 7 or len(plate) > 10:
        return False

    digit_count = sum(char.isdigit() for char in plate)
    letter_count = sum(char.isalpha() for char in plate)
    if digit_count < 5 or letter_count < 1:
        return False

    # Common Vietnamese plates start with a two-digit province code.
    return plate[:2].isdigit()
