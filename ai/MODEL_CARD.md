# Replacement model provenance

## Identity

| Field | Value |
| --- | --- |
| Publisher | `yihong1120` |
| Source | [Construction-Hazard-Detection on Hugging Face](https://huggingface.co/yihong1120/Construction-Hazard-Detection) |
| Upstream code | [Construction-Hazard-Detection on GitHub](https://github.com/yihong1120/Construction-Hazard-Detection) |
| Architecture | Ultralytics YOLO11n |
| Artifact | `models/yolo11/onnx/yolo11n.onnx` |
| Revision | `212ee245136b4e330f84b409f67f3d35eff59f42` |
| SHA-256 | `1eb7911cbfcae972da752a7cd9d3939c8e060f0bf0f7e8b4d0ab6cd979703165` |
| File size | 10,590,715 bytes |
| Input | RGB float32, `[1, 3, 640, 640]`, normalized to 0–1 |
| Output | Raw detections, `[1, 14, 8400]`; class-aware NMS runs in the adapter |
| Runtime | ONNX Runtime CPU |
| Upstream license | AGPL-3.0, declared by model metadata and model repository |

The download script pins both revision and digest. It never substitutes another model. The model binary is kept out of Git; a manifest and verified downloader make setup reproducible. The model is a third-party dependency, not original training work by the AJAI team.

## Exact labels

The exact downloaded artifact contains ten labels in this order:

```text
Hardhat, Mask, NO-Hardhat, NO-Mask, NO-Safety Vest,
Person, Safety Cone, Safety Vest, machinery, vehicle
```

The current upstream model page also discusses newer artifacts with a different class list. This integration reads and validates the ONNX file's embedded metadata. It does not assume the newer class list applies to this legacy file.

`NO-Hardhat` maps to `no_helmet`; `NO-Safety Vest` maps to `no_vest`. Other classes are returned as detections but do not trigger backend alerts. This adapter does not infer a violation merely because it failed to detect PPE.

## Verification and limits

The file checksum, metadata, tensor layout, and actual CPU execution were checked locally. A source-image smoke test produced both mapped warning types, which were saved and retrieved through AJAI's real authenticated API. No synthetic predictions were substituted in that integration check. Unit tests also use synthetic tensors to verify coordinate conversion and suppression; those are tests, not model performance evidence.

The smoke-test image came from the upstream repository at commit `83a5cfb80eabc1d86a899253452320aea3876c5f`, path `tests/cv_dataset/images/-_jpeg.rf.3e98d2f5b90e0b1459e15f570a433459.jpg`. It is not redistributed in this repository.

No independent accuracy, recall, calibration, latency target, or Saudi workplace evaluation has been established. The sample checks included missed objects. Small people, distance, lighting, occlusion, camera angle, and unfamiliar workwear may reduce reliability. A detection score is not proof of a violation. A missing detection is not proof of compliance. Require human review.

This implementation currently supports still-image inference. It does not recreate the lost NCNN model, recover the original training dataset, or implement continuous video capture. It has no per-person tracking, multi-frame confirmation, or cooldown.

## Credits and license

Model publication and training artifacts: `yihong1120/Construction-Hazard-Detection`. YOLO architecture: Ultralytics. CPU inference runtime: Microsoft ONNX Runtime. AJAI integration code was added during repository preparation.

The complete upstream model license is preserved in [third_party/MODEL-LICENSE.txt](third_party/MODEL-LICENSE.txt). The upstream project source is linked above. Preserve its license and attribution. Adoption of the model requires following its AGPL-3.0 terms; do not describe it as an unrestricted proprietary asset. A separate license for the AJAI team's code has not been selected. Review the applicable distribution terms with the team before choosing a license for a combined release.
