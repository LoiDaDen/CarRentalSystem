from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from urllib.parse import quote

import yaml
from dotenv import load_dotenv


class ConfigError(ValueError):
    pass


@dataclass(frozen=True)
class NvrConfig:
    host: str
    rtsp_port: int
    username: str
    password: str
    rtsp_template: str


@dataclass(frozen=True)
class ProcessingConfig:
    sample_fps: float
    min_emit_confidence: float
    dedupe_seconds: int
    ocr_gpu: bool
    save_snapshots: bool
    snapshot_public_base_url: str


@dataclass(frozen=True)
class WebhookConfig:
    enabled: bool
    url: str
    timeout_seconds: int
    send_low_confidence: bool


@dataclass(frozen=True)
class OutputConfig:
    directory: Path
    event_log: Path


@dataclass(frozen=True)
class PreviewConfig:
    enabled: bool
    host: str
    port: int
    jpeg_quality: int
    stream_fps: float


@dataclass(frozen=True)
class CameraConfig:
    code: str
    name: str
    channel: int
    direction: str
    enabled: bool
    webhook_secret: str
    rtsp_url: str


@dataclass(frozen=True)
class BridgeConfig:
    nvr: NvrConfig
    processing: ProcessingConfig
    webhook: WebhookConfig
    output: OutputConfig
    preview: PreviewConfig
    cameras: tuple[CameraConfig, ...]

    @property
    def enabled_cameras(self) -> tuple[CameraConfig, ...]:
        return tuple(camera for camera in self.cameras if camera.enabled)


def load_config(path: Path) -> BridgeConfig:
    path = path.resolve()
    load_dotenv(path.parent / ".env")

    if not path.exists():
        raise ConfigError(f"Config file not found: {path}")

    with path.open("r", encoding="utf-8") as handle:
        data = yaml.safe_load(handle) or {}

    nvr_data = _require_mapping(data, "nvr")
    password_env = str(nvr_data.get("password_env") or "NVR_PASSWORD")
    password = os.environ.get(password_env)
    if not password:
        raise ConfigError(f"Missing NVR password environment variable: {password_env}")

    nvr = NvrConfig(
        host=str(nvr_data.get("host") or "").strip(),
        rtsp_port=int(nvr_data.get("rtsp_port") or 554),
        username=str(nvr_data.get("username") or "").strip(),
        password=password,
        rtsp_template=str(nvr_data.get("rtsp_template") or "").strip(),
    )
    if not nvr.host or not nvr.username or not nvr.rtsp_template:
        raise ConfigError("nvr.host, nvr.username, and nvr.rtsp_template are required.")

    processing_data = dict(data.get("processing") or {})
    processing = ProcessingConfig(
        sample_fps=float(processing_data.get("sample_fps", 1.5)),
        min_emit_confidence=float(processing_data.get("min_emit_confidence", 0.80)),
        dedupe_seconds=int(processing_data.get("dedupe_seconds", 45)),
        ocr_gpu=bool(processing_data.get("ocr_gpu", False)),
        save_snapshots=bool(processing_data.get("save_snapshots", True)),
        snapshot_public_base_url=str(processing_data.get("snapshot_public_base_url") or ""),
    )

    webhook_data = dict(data.get("webhook") or {})
    webhook_url_env = str(webhook_data.get("url_env") or "WEBHOOK_URL")
    webhook = WebhookConfig(
        enabled=bool(webhook_data.get("enabled", False)),
        url=str(os.environ.get(webhook_url_env) or "").strip(),
        timeout_seconds=int(webhook_data.get("timeout_seconds", 10)),
        send_low_confidence=bool(webhook_data.get("send_low_confidence", False)),
    )
    if webhook.enabled and not webhook.url:
        raise ConfigError(f"webhook.enabled is true but {webhook_url_env} is empty.")

    output_data = dict(data.get("output") or {})
    output_dir = _resolve_path(path.parent, str(output_data.get("directory") or "output"))
    event_log = _resolve_path(path.parent, str(output_data.get("event_log") or "output/events.jsonl"))
    output = OutputConfig(directory=output_dir, event_log=event_log)

    preview_data = dict(data.get("preview") or {})
    preview = PreviewConfig(
        enabled=bool(preview_data.get("enabled", True)),
        host=str(preview_data.get("host") or "127.0.0.1").strip(),
        port=int(preview_data.get("port") or 8765),
        jpeg_quality=max(10, min(100, int(preview_data.get("jpeg_quality") or 80))),
        stream_fps=max(0.5, min(30.0, float(preview_data.get("stream_fps") or 6))),
    )

    cameras_data = data.get("cameras")
    if not isinstance(cameras_data, list) or not cameras_data:
        raise ConfigError("At least one camera must be configured.")

    cameras = tuple(_load_camera(item, nvr, webhook.enabled) for item in cameras_data)
    return BridgeConfig(nvr=nvr, processing=processing, webhook=webhook, output=output, preview=preview, cameras=cameras)


def _load_camera(data: dict[str, Any], nvr: NvrConfig, webhook_enabled: bool) -> CameraConfig:
    code = str(data.get("code") or "").strip().upper()
    name = str(data.get("name") or code).strip()
    channel = int(data.get("channel") or 0)
    direction = str(data.get("direction") or "").strip()
    enabled = bool(data.get("enabled", True))
    secret_env = str(data.get("webhook_secret_env") or "").strip()
    webhook_secret = os.environ.get(secret_env, "") if secret_env else ""

    if not code or channel <= 0:
        raise ConfigError("Each camera requires code and channel.")
    if direction not in {"In", "Out", "Unknown"}:
        raise ConfigError(f"{code} has invalid direction: {direction}")
    if webhook_enabled and enabled and not webhook_secret:
        raise ConfigError(f"{code} is missing webhook secret env value: {secret_env}")

    rtsp_url = nvr.rtsp_template.format(
        username=quote(nvr.username, safe=""),
        password=quote(nvr.password, safe=""),
        host=nvr.host,
        port=nvr.rtsp_port,
        channel=channel,
    )

    return CameraConfig(
        code=code,
        name=name,
        channel=channel,
        direction=direction,
        enabled=enabled,
        webhook_secret=webhook_secret,
        rtsp_url=rtsp_url,
    )


def _require_mapping(data: dict[str, Any], key: str) -> dict[str, Any]:
    value = data.get(key)
    if not isinstance(value, dict):
        raise ConfigError(f"Missing config section: {key}")
    return value


def _resolve_path(base_dir: Path, value: str) -> Path:
    path = Path(value)
    return path if path.is_absolute() else base_dir / path
