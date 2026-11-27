"""Exact O(N) partition and MAP over all nonempty Unicode spans [s,e)."""
import torch


def checked(start,end):
    if start.ndim!=1 or end.shape!=start.shape or start.numel()<2:
        raise ValueError('Expected equal one-dimensional N+1 logits, N >= 1')
    # Keep doubles for gradient tests; promote low precision for stable training.
    if start.dtype in (torch.float16,torch.bfloat16):start=start.float()
    if end.dtype in (torch.float16,torch.bfloat16):end=end.float()
    return start,end


def log_partition(start,end):
    start,end=checked(start,end)
    return torch.logsumexp(torch.logcumsumexp(start[:-1],dim=0)+end[1:],dim=0)


def joint_nll(start,end,span):
    start,end=checked(start,end)
    s,e=span
    if not isinstance(s,int) or not isinstance(e,int) or not 0<=s<e<start.numel():
        raise ValueError(f'Invalid nonempty span: {span}')
    return log_partition(start,end)-start[s]-end[e]


@torch.no_grad()
def decode_span(start,end):
    start,end=checked(start,end)
    scores=torch.cummax(start[:-1],dim=0).values+end[1:]
    e=int(scores.argmax())+1
    # Stable tie rule: earliest end, then earliest start.
    s=int(start[:e].argmax())
    return s,e
