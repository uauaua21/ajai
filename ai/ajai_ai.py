"""Python 3.13: real YOLO11 ONNX inference and an AJAI alert adapter."""
import argparse
import ast
import base64
import hmac
import io
import json
import os
import threading
import time
import warnings
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urlparse
from urllib.request import Request, build_opener, HTTPRedirectHandler

import numpy as np
import onnxruntime as ort
from flask import Flask, jsonify, request
from PIL import Image, ImageDraw, ImageOps, UnidentifiedImageError

from download_model import MANIFEST, MODEL_PATH, verify

ort.disable_telemetry_events()

VIOLATIONS = {'NO-Hardhat': 'no_helmet', 'NO-Safety Vest': 'no_vest'}
MAX_IMAGE_BYTES = 10 * 1024 * 1024
Image.MAX_IMAGE_PIXELS = 16_000_000


def read_image(content):
    if not content or len(content) > MAX_IMAGE_BYTES:
        raise ValueError('Provide a JPEG or PNG image no larger than 10 MB.')
    try:
        with warnings.catch_warnings():
            warnings.simplefilter('error', Image.DecompressionBombWarning)
            with Image.open(io.BytesIO(content)) as image:
                if image.format not in {'JPEG', 'PNG'}:
                    raise ValueError('Only JPEG and PNG images are supported.')
                return ImageOps.exif_transpose(image).convert('RGB')
    except (UnidentifiedImageError, OSError, Image.DecompressionBombError,
            Image.DecompressionBombWarning) as error:
        raise ValueError('Invalid image or image dimensions exceed the limit.') from error


def letterbox(image):
    scale = min(640 / image.width, 640 / image.height)
    size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
    offset = ((640 - size[0]) // 2, (640 - size[1]) // 2)
    canvas = Image.new('RGB', (640, 640), (114, 114, 114))
    canvas.paste(image.resize(size, Image.Resampling.BILINEAR), offset)
    tensor = np.asarray(canvas, dtype=np.float32).transpose(2, 0, 1)[None] / 255.0
    return np.ascontiguousarray(tensor), scale, offset


def decode(output, image_size, scale, offset, threshold=0.5, iou_threshold=0.45):
    """Decode this pinned YOLO11 artifact's raw [1, 4 + classes, 8400] output."""
    if output.shape != (1, 4 + len(MANIFEST['labels']), 8400):
        raise ValueError('Unexpected model output layout.')
    rows = output[0].T
    labels = rows[:, 4:].argmax(axis=1)
    scores = rows[np.arange(len(rows)), labels + 4]
    valid = (scores >= threshold) & np.isfinite(rows).all(axis=1)
    rows, labels, scores = rows[valid], labels[valid], scores[valid]
    order = scores.argsort()[::-1][:3000]
    rows, labels, scores = rows[order], labels[order], scores[order]
    boxes = np.column_stack((rows[:, 0] - rows[:, 2] / 2, rows[:, 1] - rows[:, 3] / 2,
                             rows[:, 0] + rows[:, 2] / 2, rows[:, 1] + rows[:, 3] / 2))
    boxes[:, [0, 2]] = np.clip((boxes[:, [0, 2]] - offset[0]) / scale, 0, image_size[0])
    boxes[:, [1, 3]] = np.clip((boxes[:, [1, 3]] - offset[1]) / scale, 0, image_size[1])
    remaining = np.where((boxes[:, 2] > boxes[:, 0]) & (boxes[:, 3] > boxes[:, 1]))[0]
    kept = []
    while remaining.size and len(kept) < 100:
        current = remaining[0]
        kept.append(current)
        rest = remaining[1:]
        top_left = np.maximum(boxes[current, :2], boxes[rest, :2])
        bottom_right = np.minimum(boxes[current, 2:], boxes[rest, 2:])
        intersection = np.maximum(0, bottom_right - top_left).prod(axis=1)
        area = (boxes[:, 2] - boxes[:, 0]) * (boxes[:, 3] - boxes[:, 1])
        iou = intersection / np.maximum(area[current] + area[rest] - intersection, 1e-9)
        remaining = rest[(iou <= iou_threshold) | (labels[rest] != labels[current])]
    return [{'label': MANIFEST['labels'][int(labels[i])], 'score': round(float(scores[i]), 4),
             'box': [round(float(v), 1) for v in boxes[i]]} for i in kept]


class Detector:
    def __init__(self):
        if not verify():
            raise ValueError('Model is missing or changed. Run python ai/download_model.py.')
        options = ort.SessionOptions()
        options.intra_op_num_threads = 2
        self.session = ort.InferenceSession(str(MODEL_PATH), options, providers=['CPUExecutionProvider'])
        metadata = self.session.get_modelmeta().custom_metadata_map
        names = ast.literal_eval(metadata.get('names', '{}'))
        if [names.get(i) for i in range(len(names))] != MANIFEST['labels']:
            raise ValueError('Model labels do not match the pinned manifest.')
        if self.session.get_inputs()[0].shape != [1, 3, 640, 640]:
            raise ValueError('Unexpected model input layout.')
        self.lock = threading.Lock()

    def detect(self, image, threshold=0.5):
        if not np.isfinite(threshold) or not 0.05 <= threshold <= 0.95:
            raise ValueError('Threshold must be between 0.05 and 0.95.')
        tensor, scale, offset = letterbox(image)
        start = time.perf_counter()
        with self.lock:
            output = self.session.run(None, {'images': tensor})[0]
        detections = decode(output, image.size, scale, offset, threshold)
        return {'model': 'external-yolo11n-ppe', 'modelSha256': MANIFEST['sha256'],
                'threshold': threshold, 'imageSize': list(image.size),
                'elapsedMs': round((time.perf_counter() - start) * 1000, 1),
                'detections': detections,
                'violationTypes': sorted({VIOLATIONS[d['label']] for d in detections if d['label'] in VIOLATIONS}),
                'notice': 'Pretrained third-party model. Predictions require human review; no detection is not proof of safety.'}


def annotate(image, detections):
    annotated = image.copy()
    draw = ImageDraw.Draw(annotated)
    for item in detections:
        color = '#ef4444' if item['label'] in VIOLATIONS else '#14b8a6'
        draw.rectangle(item['box'], outline=color, width=max(2, image.width // 300))
        draw.text((item['box'][0] + 3, max(0, item['box'][1] - 14)),
                  f"{item['label']} {item['score']:.2f}", fill=color)
    return annotated


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def post_json(url, data, token=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = f'Bearer {token}'
    request_data = Request(url, json.dumps(data).encode(), headers=headers, method='POST')
    try:
        with build_opener(NoRedirect).open(request_data, timeout=20) as response:
            return json.load(response)
    except HTTPError as error:
        raise RuntimeError(f'AJAI API returned HTTP {error.code}. Check camera credentials and API settings.') from None
    except URLError:
        raise RuntimeError('AJAI API could not be reached. Check AJAI_API_URL and start the backend.') from None


def send_alerts(image, result):
    if not result['violationTypes']:
        return []
    base = os.environ.get('AJAI_API_URL', 'http://localhost:5102').rstrip('/')
    parsed = urlparse(base)
    if parsed.username or parsed.password or parsed.query or parsed.fragment or parsed.scheme not in {'https', 'http'}:
        raise ValueError('AJAI_API_URL must be a plain HTTP(S) base URL without credentials or query parameters.')
    if not parsed.hostname or (parsed.scheme == 'http' and parsed.hostname not in {'localhost', '127.0.0.1', '::1'}):
        raise ValueError('Remote AJAI backends must use HTTPS.')
    email = os.environ.get('AJAI_CAMERA_EMAIL', 'camera1@example.test')
    password = os.environ.get('AJAI_CAMERA_PASSWORD')
    if not password:
        raise ValueError('Set AJAI_CAMERA_PASSWORD to the password of your backend camera account.')
    login = post_json(base + '/api/Auth/login', {'email': email, 'password': password})
    if login.get('role') != 'Camera':
        raise ValueError('Alert delivery requires a Camera account.')
    # Re-encode without original metadata and keep the payload within the backend DTO limit.
    snapshot = image.copy()
    snapshot.thumbnail((1280, 1280))
    buffer = io.BytesIO()
    snapshot.save(buffer, format='JPEG', quality=75)
    encoded = base64.b64encode(buffer.getvalue()).decode('ascii')
    if len(encoded) > 1_400_000:
        raise ValueError('Encoded snapshot exceeds the AJAI alert image limit.')
    sent = []
    for alert_type in result['violationTypes']:
        post_json(base + '/api/Alerts', {'cameraEmail': email, 'alertType': alert_type,
                                      'base64Image': encoded}, login['token'])
        sent.append(alert_type)
    return sent


def create_app(detector=None):
    key = os.environ.get('AJAI_WORKER_KEY', '')
    if len(key) < 32:
        raise ValueError('Set AJAI_WORKER_KEY to a random secret of at least 32 characters.')
    detector = detector or Detector()
    app = Flask(__name__)
    app.config['MAX_CONTENT_LENGTH'] = MAX_IMAGE_BYTES + 65536

    @app.get('/health')
    def health():
        return jsonify(status='ok', inference='onnx-cpu')

    @app.post('/detect')
    def detect():
        if not hmac.compare_digest(request.headers.get('X-Worker-Key', '').encode(), key.encode()):
            return jsonify(error='Invalid worker key.'), 401
        upload = request.files.get('image')
        if upload is None:
            return jsonify(error='Supply an image multipart field.'), 400
        try:
            image = read_image(upload.read(MAX_IMAGE_BYTES + 1))
            result = detector.detect(image, float(request.form.get('threshold', 0.5)))
            result['alertsSent'] = send_alerts(image, result) if request.form.get('send_alerts') == 'true' else []
            return jsonify(result)
        except (ValueError, TypeError) as error:
            return jsonify(error=str(error)), 400
        except RuntimeError as error:
            return jsonify(error=str(error)), 502

    return app


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    detect = commands.add_parser('detect')
    detect.add_argument('image', type=Path)
    detect.add_argument('--threshold', type=float, default=0.5)
    detect.add_argument('--save-annotated', type=Path)
    detect.add_argument('--post-alerts', action='store_true')
    serve = commands.add_parser('serve')
    serve.add_argument('--port', type=int, default=8000)
    args = parser.parse_args()
    try:
        if args.command == 'serve':
            create_app().run(host='127.0.0.1', port=args.port, debug=False)
            return
        if args.image.stat().st_size > MAX_IMAGE_BYTES:
            raise ValueError('Input image exceeds 10 MB.')
        image = read_image(args.image.read_bytes())
        result = Detector().detect(image, args.threshold)
        if args.save_annotated:
            args.save_annotated.parent.mkdir(parents=True, exist_ok=True)
            annotate(image, result['detections']).save(args.save_annotated)
        result['alertsSent'] = send_alerts(image, result) if args.post_alerts else []
        print(json.dumps(result, indent=2))
    except (ValueError, RuntimeError, OSError) as error:
        parser.exit(1, f'{error}\n')


if __name__ == '__main__':
    main()
