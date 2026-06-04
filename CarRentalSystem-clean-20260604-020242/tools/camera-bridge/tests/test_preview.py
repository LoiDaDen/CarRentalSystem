import numpy as np

from camera_bridge.config import CameraConfig
from camera_bridge.preview import FrameHub


def test_frame_hub_stores_jpeg_snapshot():
    camera = CameraConfig(
        code="CAM-IN-05",
        name="Gate In",
        channel=5,
        direction="In",
        enabled=True,
        webhook_secret="",
        rtsp_url="rtsp://example",
    )
    hub = FrameHub((camera,), jpeg_quality=80)
    frame = np.zeros((32, 32, 3), dtype=np.uint8)

    hub.update(camera, frame)
    snapshot = hub.snapshot("cam-in-05")

    assert snapshot is not None
    assert snapshot.jpeg.startswith(b"\xff\xd8")
