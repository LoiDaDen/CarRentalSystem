from __future__ import annotations

import hashlib
import hmac
import json
import logging
from datetime import datetime, timezone
from uuid import uuid4

from .config import CameraConfig, WebhookConfig
from .models import BridgeEvent

LOGGER = logging.getLogger(__name__)


class WebhookClient:
    def __init__(self, config: WebhookConfig) -> None:
        self.config = config

    def send(self, camera: CameraConfig, event: BridgeEvent) -> None:
        import requests

        raw_body = json.dumps(event.to_payload(), ensure_ascii=False, separators=(",", ":"))
        timestamp = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
        nonce = uuid4().hex
        signature = compute_signature(camera.webhook_secret, camera.code, timestamp, nonce, raw_body)

        response = requests.post(
            self.config.url,
            data=raw_body.encode("utf-8"),
            headers={
                "Content-Type": "application/json",
                "X-Camera-Code": camera.code,
                "X-Camera-Timestamp": timestamp,
                "X-Camera-Nonce": nonce,
                "X-Camera-Signature": f"sha256={signature}",
            },
            timeout=self.config.timeout_seconds,
        )
        response.raise_for_status()
        LOGGER.info("%s webhook accepted event=%s", camera.code, event.provider_event_id)


def compute_signature(secret: str, camera_code: str, timestamp: str, nonce: str, raw_body: str) -> str:
    canonical = f"{camera_code.strip().upper()}.{timestamp.strip()}.{nonce.strip()}.{raw_body}"
    digest = hmac.new(secret.strip().encode("utf-8"), canonical.encode("utf-8"), hashlib.sha256).hexdigest()
    return digest.lower()

