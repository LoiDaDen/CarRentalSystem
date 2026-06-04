from __future__ import annotations

import html
import logging
import threading
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from threading import Event, RLock
from urllib.parse import unquote, urlparse

from .config import CameraConfig, PreviewConfig

LOGGER = logging.getLogger(__name__)


@dataclass(frozen=True)
class StoredFrame:
    jpeg: bytes
    updated_at: datetime


class FrameHub:
    def __init__(self, cameras: tuple[CameraConfig, ...], jpeg_quality: int) -> None:
        self._cameras = {camera.code: camera for camera in cameras}
        self._jpeg_quality = jpeg_quality
        self._frames: dict[str, StoredFrame] = {}
        self._lock = RLock()

    @property
    def cameras(self) -> tuple[CameraConfig, ...]:
        return tuple(self._cameras.values())

    def update(self, camera: CameraConfig, frame: object) -> None:
        import cv2

        ok, encoded = cv2.imencode(".jpg", frame, [int(cv2.IMWRITE_JPEG_QUALITY), self._jpeg_quality])
        if not ok:
            return

        with self._lock:
            self._frames[camera.code] = StoredFrame(encoded.tobytes(), datetime.now(timezone.utc))

    def snapshot(self, code: str) -> StoredFrame | None:
        with self._lock:
            return self._frames.get(code.upper())


class PreviewServer:
    def __init__(self, config: PreviewConfig, frame_hub: FrameHub, stop_event: Event) -> None:
        self.config = config
        self.frame_hub = frame_hub
        self.stop_event = stop_event
        self._server: ThreadingHTTPServer | None = None
        self._thread: threading.Thread | None = None

    def start(self) -> None:
        handler = _make_handler(self.config, self.frame_hub, self.stop_event)
        self._server = ThreadingHTTPServer((self.config.host, self.config.port), handler)
        self._thread = threading.Thread(target=self._server.serve_forever, name="camera-preview", daemon=True)
        self._thread.start()
        LOGGER.info("Preview web is available at http://%s:%s/", self.config.host, self.config.port)

    def stop(self) -> None:
        if self._server:
            self._server.shutdown()
            self._server.server_close()
            self._server = None
        if self._thread:
            self._thread.join(timeout=5)
            self._thread = None


def _make_handler(config: PreviewConfig, frame_hub: FrameHub, stop_event: Event):
    class PreviewHandler(BaseHTTPRequestHandler):
        server_version = "CameraBridgePreview/0.1"

        def do_GET(self) -> None:
            parsed = urlparse(self.path)
            path = parsed.path.rstrip("/") or "/"

            if path == "/":
                self._send_html(_render_index(frame_hub))
                return

            if path == "/health":
                self._send_text("ok")
                return

            if path.startswith("/snapshot/") and path.endswith(".jpg"):
                code = unquote(path.removeprefix("/snapshot/").removesuffix(".jpg")).upper()
                self._send_snapshot(code)
                return

            if path.startswith("/stream/") and path.endswith(".mjpg"):
                code = unquote(path.removeprefix("/stream/").removesuffix(".mjpg")).upper()
                self._send_stream(code)
                return

            self.send_error(HTTPStatus.NOT_FOUND)

        def log_message(self, format: str, *args: object) -> None:
            LOGGER.debug("preview %s", format % args)

        def _send_html(self, body: str) -> None:
            data = body.encode("utf-8")
            self.send_response(HTTPStatus.OK)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(data)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(data)

        def _send_text(self, body: str) -> None:
            data = body.encode("utf-8")
            self.send_response(HTTPStatus.OK)
            self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)

        def _send_snapshot(self, code: str) -> None:
            frame = frame_hub.snapshot(code)
            if frame is None:
                self.send_error(HTTPStatus.NOT_FOUND, f"No frame yet for {code}")
                return

            self.send_response(HTTPStatus.OK)
            self.send_header("Content-Type", "image/jpeg")
            self.send_header("Content-Length", str(len(frame.jpeg)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(frame.jpeg)

        def _send_stream(self, code: str) -> None:
            self.send_response(HTTPStatus.OK)
            self.send_header("Content-Type", "multipart/x-mixed-replace; boundary=frame")
            self.send_header("Cache-Control", "no-store")
            self.end_headers()

            interval = 1.0 / config.stream_fps
            while not stop_event.is_set():
                frame = frame_hub.snapshot(code)
                if frame is not None:
                    try:
                        self.wfile.write(b"--frame\r\n")
                        self.wfile.write(b"Content-Type: image/jpeg\r\n")
                        self.wfile.write(f"Content-Length: {len(frame.jpeg)}\r\n\r\n".encode("ascii"))
                        self.wfile.write(frame.jpeg)
                        self.wfile.write(b"\r\n")
                        self.wfile.flush()
                    except (BrokenPipeError, ConnectionResetError):
                        return
                time.sleep(interval)

    return PreviewHandler


def _render_index(frame_hub: FrameHub) -> str:
    tiles = []
    for camera in frame_hub.cameras:
        code = html.escape(camera.code)
        name = html.escape(camera.name)
        direction = html.escape(camera.direction)
        tiles.append(
            f"""
            <section class="tile">
                <header>
                    <strong>{code}</strong>
                    <span>{name} - {direction}</span>
                </header>
                <img src="/stream/{code}.mjpg" alt="{code}" />
                <footer>
                    <a href="/snapshot/{code}.jpg" target="_blank" rel="noopener">Snapshot</a>
                </footer>
            </section>
            """
        )

    return f"""<!doctype html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Camera Bridge Preview</title>
    <style>
        body {{ margin: 0; font-family: Arial, sans-serif; background: #101418; color: #f4f6f8; }}
        header.page {{ padding: 16px 20px; background: #171d24; border-bottom: 1px solid #2b3440; }}
        h1 {{ margin: 0; font-size: 20px; }}
        main {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(360px, 1fr)); gap: 14px; padding: 14px; }}
        .tile {{ background: #171d24; border: 1px solid #2b3440; border-radius: 6px; overflow: hidden; }}
        .tile header, .tile footer {{ display: flex; justify-content: space-between; gap: 10px; padding: 10px 12px; }}
        .tile span {{ color: #aeb8c4; }}
        img {{ display: block; width: 100%; aspect-ratio: 16 / 9; object-fit: contain; background: #050607; }}
        a {{ color: #82c7ff; }}
    </style>
</head>
<body>
    <header class="page">
        <h1>Camera Bridge Preview</h1>
    </header>
    <main>{''.join(tiles)}</main>
</body>
</html>"""

