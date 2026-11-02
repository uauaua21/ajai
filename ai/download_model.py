"""Python 3.13: fetch the pinned public model and verify its SHA-256."""
import hashlib
import json
from pathlib import Path
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parent
MANIFEST = json.loads((ROOT / 'model-manifest.json').read_text())
MODEL_PATH = ROOT / 'models' / 'yolo11n.onnx'


def verify(path=MODEL_PATH):
    if not path.is_file() or path.stat().st_size != MANIFEST['size_bytes']:
        return False
    with path.open('rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest() == MANIFEST['sha256']



def download():
    if verify():
        return MODEL_PATH
    MODEL_PATH.parent.mkdir(parents=True, exist_ok=True)
    url = (f"https://huggingface.co/{MANIFEST['repository']}/resolve/"
           f"{MANIFEST['revision']}/{MANIFEST['file']}?download=true")
    temporary = MODEL_PATH.with_suffix('.download')
    try:
        with urlopen(url, timeout=60) as response, temporary.open('wb') as output:
            remaining = MANIFEST['size_bytes']
            while chunk := response.read(min(1024 * 1024, remaining + 1)):
                remaining -= len(chunk)
                if remaining < 0:
                    raise ValueError('Model download exceeds the pinned file size.')
                output.write(chunk)
        if not verify(temporary):
            raise ValueError('Model checksum verification failed.')
        temporary.replace(MODEL_PATH)
    finally:
        temporary.unlink(missing_ok=True)
    return MODEL_PATH


if __name__ == '__main__':
    print(download())
