"""Restore a task checkpoint with its backbone configuration and tokenizer."""

from pathlib import Path

import torch

from model import TransitionSpanLocator


VERSION = "transition-joint-real-1"


def load(path, backbone=None, local_files_only=False, dtype=None):
    """Build the network from the stored config and load the saved weights.

    Supported containers:
      float32  -- reference weights
      float16  -- half precision storage, upcast to float32 by default
      int8     -- dynamic int8 Linear quantization (quantization="dynamic-int8-linear")
    """
    # Only load trusted local checkpoints.
    checkpoint = torch.load(path, map_location="cpu", weights_only=False)
    if checkpoint.get("version") != VERSION:
        raise ValueError(f"Expected {VERSION}; found {checkpoint.get('version')!r}")
    config = dict(checkpoint["config"])
    if backbone:
        config["backbone"] = backbone
    model = TransitionSpanLocator(**config, init_pretrained=False, local_files_only=local_files_only)
    quantization = checkpoint.get("quantization")
    if quantization == "dynamic-int8-linear":
        from torch import nn
        from torch.ao.quantization import quantize_dynamic

        model = quantize_dynamic(model, {nn.Linear}, dtype=torch.qint8, inplace=False)
    elif quantization:
        raise ValueError(f"Unsupported quantization {quantization!r}")
    # load_state_dict copies with dtype conversion, so float16 storage lands in
    # the float32 module without an extra cast.
    model.load_state_dict(checkpoint["model"], strict=True)
    if dtype is not None:
        model = model.to(dtype)
    return model, config, checkpoint


def tokenizer_source(path, config, override=None):
    if override:
        return override
    adjacent = Path(path).resolve().parent / "tokenizer"
    return str(adjacent) if adjacent.is_dir() else config["backbone"]
