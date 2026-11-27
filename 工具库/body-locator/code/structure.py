"""Generic, vocabulary-free structural evidence at Unicode gap positions.

Positions 0..len(text) are places between codepoints.  Structural hints are
Soft input features are available at every character boundary.
"""
from __future__ import annotations
import re
import torch
import numpy as np

DIM = 20
PUNCT = set('，。！？：；、,.!?;:…—·')
OPEN = set('<[{（(「『“\"\'【《')
CLOSE = set('>]}）)」』”\"\'】》')
SEPS = set('\\/*_=|#-`~')

# The rules intentionally have no vocabulary of specific metadata tag names.
TAG = re.compile(r'<[^<>\n]{1,110}>')
FENCE = re.compile(r'(?m)^\s{0,3}(?:```|~~~)')
HEADING = re.compile(r'(?m)^\s{0,3}#{1,6}\s')
RULE = re.compile(r'(?m)^\s{0,3}(?:-{3,}|\*{3,}|_{3,})\s*$')


def make_structure(text: str):
    """Return float32 [N+1, 20] features."""
    n = len(text)
    if n < 1:
        raise ValueError('Empty documents are not supported')
    x = np.zeros((n+1, DIM), dtype=np.float32)
    x[0, 0] = 1
    x[n, 1] = 1
    for i in range(1, n):
        l, r = text[i-1], text[i]
        vals = (
            l.isspace(), r.isspace(), l == '\n', r == '\n',
            l in PUNCT, r in PUNCT, l in OPEN, r in OPEN,
            l in CLOSE, r in CLOSE, l in SEPS, r in SEPS,
            (i>=2 and text[i-2:i]=='\n\n'),
            (i+1<n and text[i:i+2]=='\n\n'),
        )
        for k, v in enumerate(vals, start=2):
            if v: x[i, k] = 1
    for match in TAG.finditer(text):
        a, b = match.span()
        x[a, 16] = 1; x[b, 17] = 1
        # Opening versus closing tags are syntactic, not tag-name specific.
        if text[a:a+2] == '</':
            x[a, 18] = 1; x[b, 18] = 1
    for pat in (FENCE, HEADING, RULE):
        for m in pat.finditer(text):
            a, b = m.span()
            x[a, 19] = 1; x[b, 19] = 1
    return torch.from_numpy(x)
