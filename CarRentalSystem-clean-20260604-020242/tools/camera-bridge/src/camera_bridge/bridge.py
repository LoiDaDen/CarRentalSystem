from __future__ import annotations

import argparse
import json
import logging
import signal
import time
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from pathlib import Path
from threading import Event
from uuid import uuid4

from .config import BridgeConfig, CameraConfig, load_config
from .dedupe import DedupeCache
from .frame_source import RtspFrameSource
from .models import BridgeEvent, PlateRead
from .ocr import EasyOcrPlateReader
from .preview import FrameHub, PreviewServer
from .webhook import WebhookClient

LOGGER = logging.getLogger("camera_bridge")


def main() -> int:
    parser = argparse.ArgumentParser(description="Read XVR RTSP streams and emit ANPR webhook events.")
    parser.add_argument("--config", default="config.yaml", help="Path to bridge config YAML.")
    parser.add_argument("--once", action="store_true", help="Read and process one frame per enabled camera.")
    parser.add_argument("--preview-only", action="store_true", help="Serve camera preview without OCR/webhook processing.")
    parser.add_argument("--log-level", default="INFO", help="Python logging level.")
    args = parser.parse_args()

    logging.basicConfig(
        level=getattr(logging, args.log_level.upper(), logging.INFO),
        format="%(asctime)s %(levelname)s %(name)s %(message)s",
    )

    config = load_config(Path(args.config))
    runner = BridgeRunner(config, preview_only=args.preview_only)
    return runner.run_once() if args.once else runner.run_forever()


class BridgeRunner:
    def __init__(self, config: BridgeConfig, preview_only: bool = False) -> None:
        self.config = config
        self.preview_only = preview_only
        self.stop_event = Event()
        self.dedupe = DedupeCache(config.processing.dedupe_seconds)
        self.reader = EasyOcrPlateReader(gpu=config.processing.ocr_gpu)
        self.webhook = WebhookClient(config.webhook)
        self.frame_hub = FrameHub(config.enabled_cameras, jpeg_quality=config.preview.jpeg_quality)
        self.preview_server = PreviewServer(config.preview, self.frame_hub, self.stop_event) if config.preview.enabled else None
        self.output_dir = config.output.directory
        self.event_log = config.output.event_log
        self.snapshot_dir = self.output_dir / "snapshots"
        self.output_dir.mkdir(parents=True, exist_ok=True)
        self.snapshot_dir.mkdir(parents=True, exist_ok=True)

    def run_once(self) -> int:
        for camera in self.config.enabled_cameras:
            self._process_one_camera(camera, once=True)
        return 0

    def run_forever(self) -> int:
        signal.signal(signal.SIGINT, self._request_stop)
        signal.signal(signal.SIGTERM, self._request_stop)

        if self.preview_server:
            self.preview_server.start()

        try:
            with ThreadPoolExecutor(max_workers=len(self.config.enabled_cameras)) as executor:
                for camera in self.config.enabled_cameras:
                    executor.submit(self._process_one_camera, camera, False)

                while not self.stop_event.is_set():
                    time.sleep(1)
        finally:
            if self.preview_server:
                self.preview_server.stop()

        return 0

    def _request_stop(self, signum: int, _frame: object) -> None:
        LOGGER.info("Stopping bridge after signal %s", signum)
        self.stop_event.set()

    def _process_one_camera(self, camera: CameraConfig, once: bool) -> None:
        interval_seconds = 1.0 / max(self.config.processing.sample_fps, 0.1)
        source = RtspFrameSource(camera.rtsp_url)
        LOGGER.info("Starting %s channel=%s direction=%s", camera.code, camera.channel, camera.direction)

        try:
            while not self.stop_event.is_set():
                frame = source.read()
                if frame is None:
                    LOGGER.warning("%s has no frame, reconnecting", camera.code)
                    source.reconnect()
                    time.sleep(2)
                    if once:
                        return
                    continue

                if once:
                    self._save_probe_frame(camera, frame)

                self.frame_hub.update(camera, frame)
                if self.preview_only:
                    if once:
                        return
                    time.sleep(interval_seconds)
                    continue

                read = self.reader.read(frame)
                if read:
                    self._handle_plate(camera, read, frame)

                if once:
                    return

                time.sleep(interval_seconds)
        finally:
            source.close()

    def _handle_plate(self, camera: CameraConfig, read: PlateRead, frame: object) -> None:
        dedupe_key = f"{camera.code}:{camera.direction}:{read.normalized_text}"
        if self.dedupe.seen(dedupe_key):
            LOGGER.debug("Duplicate plate skipped: %s", dedupe_key)
            return

        captured_at = datetime.now(timezone.utc)
        provider_event_id = f"{camera.code}-{captured_at.strftime('%Y%m%dT%H%M%S%fZ')}-{uuid4().hex[:8]}"
        snapshot_path = self._save_snapshot(camera, provider_event_id, frame)
        event = BridgeEvent(
            provider_event_id=provider_event_id,
            camera_code=camera.code,
            license_plate=read.text,
            license_plate_normalized=read.normalized_text,
            direction=camera.direction,
            captured_at=captured_at,
            confidence=read.confidence,
            snapshot_path=snapshot_path,
            snapshot_url=self._snapshot_url(snapshot_path),
        )

        self._append_event_log(event)

        if read.confidence < self.config.processing.min_emit_confidence and not self.config.webhook.send_low_confidence:
            LOGGER.info(
                "%s low confidence plate=%s confidence=%.3f, webhook skipped",
                camera.code,
                read.text,
                read.confidence,
            )
            return

        if self.config.webhook.enabled:
            self.webhook.send(camera, event)
        else:
            LOGGER.info(
                "%s dry-run plate=%s normalized=%s confidence=%.3f",
                camera.code,
                read.text,
                read.normalized_text,
                read.confidence,
            )

    def _save_snapshot(self, camera: CameraConfig, provider_event_id: str, frame: object) -> Path | None:
        if not self.config.processing.save_snapshots:
            return None

        import cv2

        camera_dir = self.snapshot_dir / camera.code
        camera_dir.mkdir(parents=True, exist_ok=True)
        path = camera_dir / f"{provider_event_id}.jpg"
        cv2.imwrite(str(path), frame)
        return path

    def _save_probe_frame(self, camera: CameraConfig, frame: object) -> None:
        import cv2

        probe_dir = self.output_dir / "probe"
        probe_dir.mkdir(parents=True, exist_ok=True)
        cv2.imwrite(str(probe_dir / f"{camera.code}.jpg"), frame)

    def _snapshot_url(self, snapshot_path: Path | None) -> str | None:
        if snapshot_path is None:
            return None

        base_url = self.config.processing.snapshot_public_base_url.strip().rstrip("/")
        if not base_url:
            return str(snapshot_path)

        relative = snapshot_path.relative_to(self.output_dir).as_posix()
        return f"{base_url}/{relative}"

    def _append_event_log(self, event: BridgeEvent) -> None:
        self.event_log.parent.mkdir(parents=True, exist_ok=True)
        with self.event_log.open("a", encoding="utf-8") as handle:
            handle.write(json.dumps(event.to_log_dict(), ensure_ascii=False) + "\n")
