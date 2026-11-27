"""V5-style boundary context from short and medium character windows."""

import torch
from torch import nn

from structure import DIM


class TransitionScorer(nn.Module):
    def __init__(self, width=128, hidden=128, dropout=0.1):
        super().__init__()
        self.project = nn.Sequential(
            nn.Linear(5 * width + DIM, hidden),
            nn.LayerNorm(hidden),
            nn.GELU(),
            nn.Dropout(dropout),
        )
        self.output = nn.Linear(hidden, 2)
        nn.init.zeros_(self.output.weight)
        nn.init.zeros_(self.output.bias)

    def forward(self, gaps, features):
        count, width = gaps.shape
        if features.shape != (count, DIM):
            raise ValueError("Structural features must align with Unicode gaps")

        # Keep prefix sums in FP32 for short subtractions on long documents.
        gaps_fp32 = gaps.float()
        prefix = torch.cat((gaps_fp32.new_zeros((1, width)), gaps_fp32.cumsum(dim=0)), dim=0)
        indices = torch.arange(count, device=gaps.device)
        windows = [gaps_fp32]
        for radius in (8, 64):
            lo = (indices - radius).clamp_min(0)
            hi = (indices + radius).clamp_max(count)
            windows.append((prefix[indices] - prefix[lo]) / (indices - lo).clamp_min(1)[:, None])
            windows.append((prefix[hi] - prefix[indices]) / (hi - indices).clamp_min(1)[:, None])
        windows.append(features.float())
        return self.output(self.project(torch.cat(windows, dim=-1))).float()
