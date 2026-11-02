"""Python 3.13: unit checks and a real-model blank-image smoke test."""
import io
import os
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import numpy as np
from PIL import Image
from ajai_ai import Detector, create_app, decode, letterbox, read_image, send_alerts
from download_model import verify


def png():
    buffer = io.BytesIO()
    Image.new('RGB', (64, 32), 'white').save(buffer, 'PNG')
    return buffer.getvalue()


class WorkerTests(unittest.TestCase):
    def test_image_validation(self):
        self.assertEqual(read_image(png()).size, (64, 32))
        for data in [b'', b'not an image']:
            with self.assertRaises(ValueError):
                read_image(data)

    def test_letterbox_geometry(self):
        tensor, scale, offset = letterbox(Image.new('RGB', (1280, 640)))
        self.assertEqual(tensor.shape, (1, 3, 640, 640))
        self.assertEqual(scale, 0.5)
        self.assertEqual(offset, (0, 160))
        self.assertEqual(tensor.dtype, np.float32)

    def test_nms_keeps_distinct_classes_and_restores_coordinates(self):
        output = np.zeros((1, 14, 8400), dtype=np.float32)
        for index, (class_id, score) in enumerate([(2, 0.9), (2, 0.8), (4, 0.7)]):
            output[0, :4, index] = [320, 320, 100, 100]
            output[0, class_id + 4, index] = score
        result = decode(output, (1280, 640), 0.5, (0, 160))
        self.assertEqual([d['label'] for d in result], ['NO-Hardhat', 'NO-Safety Vest'])
        self.assertEqual(result[0]['box'], [540, 220, 740, 420])

    def test_wrong_output_layout_rejected(self):
        with self.assertRaises(ValueError):
            decode(np.zeros((1, 15, 8400)), (640, 640), 1, (0, 0))

    def test_no_alerts_without_explicit_violation_class(self):
        self.assertEqual(send_alerts(Image.new('RGB', (10, 10)), {'violationTypes': []}), [])

    def test_remote_plain_http_rejected(self):
        with patch.dict(os.environ, {'AJAI_API_URL': 'http://example.com'}):
            with self.assertRaises(ValueError):
                send_alerts(Image.new('RGB', (10, 10)), {'violationTypes': ['no_helmet']})

    def test_service_requires_secret(self):
        with patch.dict(os.environ, {'AJAI_WORKER_KEY': ''}):
            with self.assertRaises(ValueError):
                create_app(object())

    def test_api_auth_and_invalid_upload(self):
        with patch.dict(os.environ, {'AJAI_WORKER_KEY': 'test-only-worker-secret-123456789012345'}):
            client = create_app(object()).test_client()
        self.assertEqual(client.get('/health').status_code, 200)
        self.assertEqual(client.post('/detect').status_code, 401)
        headers = {'X-Worker-Key': 'test-only-worker-secret-123456789012345'}
        self.assertEqual(client.post('/detect', headers=headers).status_code, 400)
        self.assertEqual(client.post('/detect', headers=headers,
                                    data={'image': (io.BytesIO(b'invalid'), 'x.jpg')}).status_code, 400)

    @unittest.skipUnless(verify(), 'Run download_model.py to enable real-model smoke tests.')
    def test_real_onnx_model_blank_image_and_threshold(self):
        detector = Detector()
        image = Image.new('RGB', (640, 640), (114, 114, 114))
        result = detector.detect(image)
        self.assertEqual(result['detections'], [])
        self.assertEqual(result['violationTypes'], [])
        with patch.dict(os.environ, {'AJAI_WORKER_KEY': 'test-only-worker-secret-123456789012345'}):
            client = create_app(detector).test_client()
        response = client.post('/detect',
                               headers={'X-Worker-Key': 'test-only-worker-secret-123456789012345'},
                               data={'image': (io.BytesIO(png()), 'blank.png')})
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json['modelSha256'], result['modelSha256'])
        self.assertEqual(response.json['alertsSent'], [])
        for threshold in [float('nan'), 0, 1]:
            with self.assertRaises(ValueError):
                detector.detect(image, threshold)


if __name__ == '__main__':
    unittest.main()
