from __future__ import annotations

import logging
from statistics import mean

import numpy as np

from .models import PlateRead
from .plate_normalizer import clean_ocr_text, looks_like_vietnam_plate, normalize_plate

LOGGER = logging.getLogger(__name__)


class EasyOcrPlateReader:
    def __init__(self, gpu: bool = False) -> None:
        self.gpu = gpu
        self._reader = None

    def read(self, frame: np.ndarray) -> PlateRead | None:
        reader = self._get_reader()
        best: PlateRead | None = None

        for crop in _candidate_crops(frame):
            results = reader.readtext(
                crop,
                detail=1,
                paragraph=False,
                allowlist="ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-. ",
            )
            read = _result_to_plate_read(results)
            if read and (best is None or read.confidence > best.confidence):
                best = read

        return best

    def _get_reader(self):
        if self._reader is None:
            import easyocr

            LOGGER.info("Loading EasyOCR reader gpu=%s", self.gpu)
            self._reader = easyocr.Reader(["en"], gpu=self.gpu, verbose=False)
        return self._reader


def _candidate_crops(frame: np.ndarray) -> list[np.ndarray]:
    import cv2

    height, width = frame.shape[:2]
    scale = 1.0
    if width > 1280:
        scale = 1280.0 / width
        resized = cv2.resize(frame, (1280, int(height * scale)))
    else:
        resized = frame

    gray = cv2.cvtColor(resized, cv2.COLOR_BGR2GRAY)
    gray = cv2.equalizeHist(gray)
    blurred = cv2.GaussianBlur(gray, (5, 5), 0)
    edges = cv2.Canny(blurred, 80, 180)
    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (17, 5))
    closed = cv2.morphologyEx(edges, cv2.MORPH_CLOSE, kernel)
    contours, _ = cv2.findContours(closed, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    crops: list[np.ndarray] = []
    resized_h, resized_w = resized.shape[:2]
    min_area = resized_w * resized_h * 0.0004
    max_area = resized_w * resized_h * 0.08

    for contour in contours:
        x, y, w, h = cv2.boundingRect(contour)
        if h == 0:
            continue

        aspect = w / h
        area = w * h
        if 1.6 <= aspect <= 6.8 and min_area <= area <= max_area:
            pad_x = int(w * 0.15)
            pad_y = int(h * 0.35)
            x1 = max(x - pad_x, 0)
            y1 = max(y - pad_y, 0)
            x2 = min(x + w + pad_x, resized_w)
            y2 = min(y + h + pad_y, resized_h)
            crops.append(resized[y1:y2, x1:x2])

    # Fallback: EasyOCR can still find text on the whole frame when contour
    # detection misses reflective or low-light plates.
    crops.append(resized)
    return crops[:12]


def _result_to_plate_read(results: list[tuple[object, str, float]]) -> PlateRead | None:
    candidates: list[PlateRead] = []

    for _bbox, text, confidence in results:
        cleaned = clean_ocr_text(text)
        normalized = normalize_plate(cleaned)
        if looks_like_vietnam_plate(normalized):
            candidates.append(PlateRead(cleaned, normalized, float(confidence)))

    if len(results) > 1:
        joined_text = "".join(clean_ocr_text(text) for _bbox, text, _confidence in results)
        normalized = normalize_plate(joined_text)
        if looks_like_vietnam_plate(normalized):
            joined_confidence = mean(float(confidence) for _bbox, _text, confidence in results)
            candidates.append(PlateRead(joined_text, normalized, joined_confidence))

    if not candidates:
        return None

    return max(candidates, key=lambda item: item.confidence)
