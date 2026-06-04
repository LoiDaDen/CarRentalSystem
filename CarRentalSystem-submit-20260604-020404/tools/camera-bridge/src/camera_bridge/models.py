from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path


@dataclass(frozen=True)
class PlateRead:
    text: str
    normalized_text: str
    confidence: float


@dataclass(frozen=True)
class BridgeEvent:
    provider_event_id: str
    camera_code: str
    license_plate: str
    license_plate_normalized: str
    direction: str
    captured_at: datetime
    confidence: float
    snapshot_path: Path | None
    snapshot_url: str | None

    def to_payload(self) -> dict[str, object]:
        return {
            "providerEventId": self.provider_event_id,
            "licensePlate": self.license_plate,
            "direction": self.direction,
            "capturedAt": _iso_utc(self.captured_at),
            "confidence": round(self.confidence, 4),
            "snapshotUrl": self.snapshot_url,
        }

    def to_log_dict(self) -> dict[str, object]:
        data = self.to_payload()
        data["cameraCode"] = self.camera_code
        data["licensePlateNormalized"] = self.license_plate_normalized
        data["snapshotPath"] = str(self.snapshot_path) if self.snapshot_path else None
        return data


def _iso_utc(value: datetime) -> str:
    if value.tzinfo is None:
        value = value.replace(tzinfo=timezone.utc)
    return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")

