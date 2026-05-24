from __future__ import annotations

import logging

LOGGER = logging.getLogger(__name__)


class RtspFrameSource:
    def __init__(self, url: str) -> None:
        self.url = url
        self._capture = None
        self._open()

    def read(self):
        if self._capture is None:
            self._open()

        ok, frame = self._capture.read()
        if not ok:
            return None

        return frame

    def reconnect(self) -> None:
        self.close()
        self._open()

    def close(self) -> None:
        if self._capture is not None:
            self._capture.release()
            self._capture = None

    def _open(self) -> None:
        import cv2

        LOGGER.info("Opening RTSP stream %s", _redact_rtsp(self.url))
        self._capture = cv2.VideoCapture(self.url, cv2.CAP_FFMPEG)
        self._capture.set(cv2.CAP_PROP_BUFFERSIZE, 1)


def _redact_rtsp(url: str) -> str:
    if "@" not in url or "://" not in url:
        return url

    scheme, rest = url.split("://", 1)
    return f"{scheme}://***:***@{rest.split('@', 1)[1]}"

