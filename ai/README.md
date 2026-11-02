# AJAI AI worker

Real, local PPE object detection using a pinned third-party YOLO11n ONNX model. No cloud AI API, API billing, PyTorch checkpoint loading, or training is required.

This is a replacement implementation. It is not the recovered original AJAI model. The model author is `yihong1120`; the model architecture is from Ultralytics. See [MODEL_CARD.md](MODEL_CARD.md) and [the model license](third_party/MODEL-LICENSE.txt).

## Setup

Python 3.13, Bash/zsh commands from the repository root:

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r ai/requirements.lock.txt
python ai/download_model.py
```

The downloader fetches the exact pinned revision, verifies its SHA-256, and stores it in ignored `ai/models/yolo11n.onnx`. The GitHub source ZIP intentionally excludes downloaded weights. The prepared local folder already has the verified model. Initial setup needs internet access. Inference itself is local.

## Detect objects in an image

```bash
python ai/ajai_ai.py detect /path/to/photo.jpg \
  --save-annotated ai/results/annotated.jpg
```

The CLI prints JSON with detected labels, scores, pixel coordinates, and predicted violation types. `--save-annotated` saves an actual model overlay. JPEG and PNG inputs are supported, up to 10 MB and the configured image pixel limit. The default score threshold is 0.5; use `--threshold 0.6` to change it. A score is not a calibrated probability.

Only explicit `NO-Hardhat` and `NO-Safety Vest` predictions produce `no_helmet` and `no_vest` warnings. Missing positive detections do not automatically produce an alert. No detections does not establish that an image is safe. Review predictions manually.

## Send real predictions to AJAI

Start the .NET API using the root README. Enable its Development demo accounts or supply an existing Camera account. The password below must match the camera password configured in that API. Environment variables are read directly; `.env` files are not loaded.

```bash
export AJAI_API_URL='http://localhost:5102'
export AJAI_CAMERA_EMAIL='camera1@example.test'
read -r -s -p 'Camera password: ' AJAI_CAMERA_PASSWORD; echo
export AJAI_CAMERA_PASSWORD
python ai/ajai_ai.py detect /path/to/photo.jpg --post-alerts
```

The `read -p` example is for Bash. In zsh use `read -r -s 'AJAI_CAMERA_PASSWORD?Camera password: '; echo`, then export it. Using a hidden prompt avoids putting the real password in shell history.

The adapter logs in as a camera, resizes and re-encodes the image without its original metadata, then submits one alert for each predicted violation type. Dashboard users can verify those alerts through `GET /api/Alerts`. AJAI broadcasts them through its existing authenticated SignalR hub. No alert is sent unless `--post-alerts` is selected, and no placeholder result is sent when the model finds no violation.

Remote backends must use HTTPS. The client rejects credential-bearing URLs and redirects. Passwords and JWTs are not printed. The upload is a still image, not a video stream.

## Optional local HTTP service

```bash
export AJAI_WORKER_KEY="$(openssl rand -hex 32)"
python ai/ajai_ai.py serve --port 8000
```

This local Flask development server binds to `127.0.0.1`. `GET /health` is public. `POST /detect` requires `X-Worker-Key` and a multipart `image` field. It returns real detection JSON. It sends no backend alerts unless multipart field `send_alerts=true` is provided and camera credentials are configured.

```bash
curl --fail http://127.0.0.1:8000/detect \
  -H "X-Worker-Key: $AJAI_WORKER_KEY" \
  -F 'image=@/path/to/photo.jpg'
```

Do not expose this development server to the internet. There is no video reader, camera heartbeat, frame confirmation, cooldown, or production request queue. Repeated image submissions can create repeated alerts.

## Tests

```bash
python -m unittest discover -s ai/tests -v
```

Nine tests cover image validation, coordinate restoration, class-aware suppression, worker authentication, endpoint validation, alert safeguards, and a real-model blank-image smoke test. The real-model test is skipped if weights have not been downloaded. GitHub CI downloads and verifies the pinned model before running it.

A local integration check also ran a real upstream test image through the model, logged in as a camera, saved both warning types, and retrieved those rows with a dashboard account. This verifies the plumbing. It is not a representative accuracy evaluation.
