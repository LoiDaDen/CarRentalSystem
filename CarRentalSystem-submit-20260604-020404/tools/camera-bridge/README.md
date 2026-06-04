# Camera ANPR Bridge

Python bridge for the XVR/NVR at `192.168.1.200`. It reads RTSP streams from
the selected gate cameras, runs local plate OCR, writes dry-run JSONL logs and
snapshots, and can later send signed events to the web app endpoint:

`POST /integrations/camera/anpr-events`

## Camera Mapping

| Code | Channel | Direction | RTSP stream |
| --- | ---: | --- | --- |
| CAM-IN-05 | 5 | In | `/cam/realmonitor?channel=5&subtype=0` |
| CAM-IN-13 | 13 | In | `/cam/realmonitor?channel=13&subtype=0` |
| CAM-OUT-02 | 2 | Out | `/cam/realmonitor?channel=2&subtype=0` |
| CAM-OUT-03 | 3 | Out | `/cam/realmonitor?channel=3&subtype=0` |
| CAM-OUT-15 | 15 | Out | `/cam/realmonitor?channel=15&subtype=0` |

## Setup on Windows

1. Install Python 3.11 or newer.
2. In PowerShell:

```powershell
cd tools\camera-bridge
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install --upgrade pip
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
Copy-Item .env.example .env
Copy-Item config.example.yaml config.yaml
notepad .env
```

3. Put the real NVR password in `.env`.
4. Keep `webhook.enabled: false` until the web VPS has HTTPS and camera
   secrets are created in Admin > Camera.
5. Run one dry-run pass:

```powershell
.\run_bridge.ps1 -Once
```

6. Run continuously:

```powershell
.\run_bridge.ps1
```

Dry-run events are written to `output/events.jsonl`; snapshots are written under
`output/snapshots`.

## Local Web Preview

Browsers cannot play RTSP directly. The bridge can expose a local MJPEG preview:

```powershell
.\run_bridge.ps1 -PreviewOnly
```

Open:

`http://127.0.0.1:8765/`

This preview is intended for the Windows bridge machine. Keep the default host
`127.0.0.1` unless you intentionally want other LAN devices to access it.

## Local Tests

```powershell
cd tools\camera-bridge
.\.venv\Scripts\python.exe -m pip install -r requirements-dev.txt
.\.venv\Scripts\python.exe -m pytest
```

## Webhook Mode

After the web app is deployed:

1. Create these camera devices in Admin > Camera:
   `CAM-IN-05`, `CAM-IN-13`, `CAM-OUT-02`, `CAM-OUT-03`, `CAM-OUT-15`.
2. Copy each one-time webhook secret into `.env`.
3. Set `WEBHOOK_URL=https://your-domain/integrations/camera/anpr-events`.
4. Set `webhook.enabled: true` in `config.yaml`.

The bridge signs each request with:

`HMACSHA256(CAMERA_CODE.TIMESTAMP.NONCE.RAW_BODY, camera_secret)`

Headers sent:

- `X-Camera-Code`
- `X-Camera-Timestamp`
- `X-Camera-Nonce`
- `X-Camera-Signature`

## Notes

- Do not expose the NVR to the Internet.
- Do not put the NVR password or webhook secrets in committed files.
- Use `subtype=0` for plate OCR. Substream `subtype=1` is usually too small.
- If CPU is high, lower `processing.sample_fps` before lowering resolution.
