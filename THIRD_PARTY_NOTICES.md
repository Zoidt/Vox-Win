# Third-party components

Vox uses these components under their respective licenses:

- [Parakeet TDT 0.6B v3](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3), NVIDIA, licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Vox uses the INT8 ONNX conversion published by the sherpa-onnx maintainer at [csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8](https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8), pinned to revision `2bda32ec70b097a55adaa07d9a7173915b43cc78`. Conversion and quantization change the original model representation. Model files are downloaded separately and SHA-256 checked.
- [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx), Next-gen Kaldi team and contributors, Apache-2.0.
- [ONNX Runtime](https://github.com/microsoft/onnxruntime), Microsoft and contributors, MIT; included by the sherpa-onnx runtime package.
- [NAudio](https://github.com/naudio/NAudio), Mark Heath and contributors, MIT.
- [xUnit.net](https://github.com/xunit/xunit), .NET Foundation and contributors, Apache-2.0; development tests only.

Hex is the behavioral inspiration. No Hex source code or visual assets are included.
