"""Load the checkpoint once and locate the body span of a reply."""

from __future__ import annotations

import json
import time

import torch
from transformers import AutoTokenizer

from checkpoint import load, tokenizer_source
from data import model_inputs
from encode import encode_document
from span import decode_span


class TokenLimitError(ValueError):
    def __init__(self, count: int, cap: int):
        super().__init__(f"document has {count} tokens, over the {cap} token budget")
        self.count = count
        self.cap = cap


class BodyLocator:
    """Predicts the body span [start, end) of a reply in Unicode codepoints.

    The span is a single contiguous range over the ORIGINAL string. Nothing is
    truncated or rewritten: end - start == len(text[start:end]).
    """

    def __init__(self, checkpoint: str = "best.pt", device: str | None = None,
                 backbone: str | None = None, local_files_only: bool = True,
                 dtype: str | None = None):
        self.device = device or ("cuda" if torch.cuda.is_available() else "cpu")
        # dtype only pays off on GPU: float16/bfloat16 weights are upcast on CPU.
        torch_dtype = getattr(torch, dtype) if dtype else None
        if torch_dtype is not None and str(self.device).startswith("cpu"):
            raise ValueError("float16/bfloat16 inference needs a CUDA device; omit dtype on CPU")
        self.model, self.config, self.saved = load(checkpoint, backbone, local_files_only, torch_dtype)
        source = tokenizer_source(checkpoint, self.config, backbone)
        self.tokenizer = AutoTokenizer.from_pretrained(source, use_fast=True,
                                                      local_files_only=local_files_only)
        settings = self.saved.get("train_settings", {})
        self.window = int(settings.get("window", 512))
        self.overlap = int(settings.get("overlap", 128))
        self.max_total_tokens = int(settings.get("max_total_tokens", 8192))
        self.model.to(self.device).eval()

    @torch.inference_mode()
    def locate(self, text: str, target_span: tuple[int, int] | None = None,
               max_total_tokens: int | None = None) -> dict:
        if not isinstance(text, str) or not text:
            raise ValueError("text must be a nonempty str")
        cap = self.max_total_tokens if max_total_tokens is None else int(max_total_tokens)
        offset, stop = (0, len(text)) if target_span is None else (int(target_span[0]), int(target_span[1]))
        if not 0 <= offset < stop <= len(text):
            raise ValueError("target_span must be a nonempty range inside text")
        selected = text[offset:stop]
        started = time.perf_counter()
        enc = encode_document(self.tokenizer, selected, window=self.window, overlap=self.overlap)
        count = enc["token_count"]
        if cap and count > cap:
            raise TokenLimitError(count, cap)
        out = self.model(**model_inputs(enc, selected, self.device))
        start, end = decode_span(out["start_logits"], out["end_logits"])
        start += offset
        end += offset
        return {"span": [start, end], "body": text[start:end], "text": text,
                "char_count": len(text), "token_count": count,
                "chunk_count": enc["chunk_count"],
                "time_ms": round(1000 * (time.perf_counter() - started))}


_DEFAULT = None


def locate(text: str, checkpoint: str = "best.pt", **kwargs) -> dict:
    """Module-level convenience wrapper that caches one loaded model."""
    global _DEFAULT
    device, backbone = kwargs.pop("device", None), kwargs.pop("backbone", None)
    local_files_only, dtype = kwargs.pop("local_files_only", True), kwargs.pop("dtype", None)
    if _DEFAULT is None or getattr(_DEFAULT, "_checkpoint", None) != checkpoint:
        _DEFAULT = BodyLocator(checkpoint, device, backbone, local_files_only, dtype)
        _DEFAULT._checkpoint = checkpoint
    return _DEFAULT.locate(text, **kwargs)


if __name__ == "__main__":
    import argparse

    p = argparse.ArgumentParser(description="Locate the body span of one UTF-8 file")
    p.add_argument("--checkpoint", default="best.pt")
    p.add_argument("--input", required=True, help="UTF-8 text file, newline='' is preserved")
    p.add_argument("--output", help="optional file receiving the exact body substring")
    p.add_argument("--device", default=None)
    p.add_argument("--backbone", default=None)
    p.add_argument("--dtype", choices=["float16", "bfloat16"], default=None,
                   help="GPU only; on CPU the weights stay float32")
    p.add_argument("--target-span", nargs=2, type=int, metavar=("START", "END"))
    a = p.parse_args()
    with open(a.input, encoding="utf-8", newline="") as f:
        text = f.read()
    result = BodyLocator(a.checkpoint, a.device, a.backbone, dtype=a.dtype).locate(
        text, tuple(a.target_span) if a.target_span else None)
    if a.output:
        with open(a.output, "w", encoding="utf-8", newline="") as f:
            f.write(result["body"])
    print(json.dumps({k: v for k, v in result.items() if k not in ("body", "text")},
                     ensure_ascii=False))