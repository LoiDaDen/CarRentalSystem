import hmac
import hashlib

from camera_bridge.webhook import compute_signature


def test_compute_signature_matches_backend_canonical_string():
    secret = "cam_secret"
    camera_code = "cam-in-05"
    timestamp = "2026-05-24T10:00:00Z"
    nonce = "nonce-1"
    body = '{"providerEventId":"evt-1","licensePlate":"51A-123.45"}'

    expected = hmac.new(
        secret.encode("utf-8"),
        f"CAM-IN-05.{timestamp}.{nonce}.{body}".encode("utf-8"),
        hashlib.sha256,
    ).hexdigest()

    assert compute_signature(secret, camera_code, timestamp, nonce, body) == expected

