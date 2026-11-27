"""BGE local windows -> small bidirectional RoPE global Transformer -> Unicode span heads.

The RoPE Transformer operates on chunk summaries, not on the BGE token stream.
This preserves pretrained BGE absolute positional embeddings without conversion.
"""
from __future__ import annotations
import torch
from torch import nn
from torch.nn import functional as F
from transformers import AutoConfig, AutoModel
from span import joint_nll
from transition import TransitionScorer


def apply_rope(q,k,base=10000.0):
    # q,k: [batch, heads, n_chunks, head_dim]. No causal mask.
    dim=q.size(-1)
    if dim%2:raise ValueError('RoPE head width must be even')
    inv_freq=1.0/(base**(torch.arange(0,dim,2,device=q.device,dtype=torch.float32)/dim))
    theta=torch.outer(torch.arange(q.size(-2),device=q.device,dtype=torch.float32),inv_freq)
    c=theta.cos()[None,None,:,:];s=theta.sin()[None,None,:,:]
    def rotate(x):
        a=x.float()[...,0::2];b=x.float()[...,1::2]
        return torch.stack((a*c-b*s,a*s+b*c),dim=-1).flatten(-2).to(x.dtype)
    return rotate(q),rotate(k)


class RoPESelfAttention(nn.Module):
    def __init__(self,width=256,heads=4):
        super().__init__()
        if width%heads or (width//heads)%2:raise ValueError('Incompatible width/heads')
        self.heads=heads
        self.qkv=nn.Linear(width,3*width)
        self.proj=nn.Linear(width,width)
    def forward(self,x):
        n,c=x.shape
        q,k,v=self.qkv(x).reshape(n,3,self.heads,c//self.heads).permute(1,2,0,3).unbind(0)
        q,k=apply_rope(q.unsqueeze(0),k.unsqueeze(0))
        v=v.unsqueeze(0)
        # FULL bidirectional attention across all summaries. Unlike a causal
        # LLM there is no triangular mask.
        h=F.scaled_dot_product_attention(q,k,v,dropout_p=0.0,is_causal=False)
        return self.proj(h.squeeze(0).transpose(0,1).reshape(n,c))


class GlobalRoPEBlock(nn.Module):
    def __init__(self,width=256,heads=4,dropout=.1):
        super().__init__()
        self.ln1=nn.LayerNorm(width)
        self.attn=RoPESelfAttention(width,heads)
        self.ln2=nn.LayerNorm(width)
        self.mlp=nn.Sequential(nn.Linear(width,width*4),nn.GELU(),nn.Dropout(dropout),nn.Linear(width*4,width))
        self.drop=nn.Dropout(dropout)
    def forward(self,x):
        x=x+self.drop(self.attn(self.ln1(x)))
        return x+self.drop(self.mlp(self.ln2(x)))


class TransitionSpanLocator(nn.Module):
    def __init__(self,backbone='BAAI/bge-small-zh-v1.5',global_dim=256,
                 global_layers=2,global_heads=4,dropout=.1,
                 chunks_per_forward=4,init_pretrained=True,encoder_config=None,local_files_only=False):
        super().__init__()
        self.backbone=backbone
        if encoder_config is not None:
            ec=dict(encoder_config)
            model_type=ec.pop('model_type')
            self.encoder=AutoModel.from_config(AutoConfig.for_model(model_type,**ec))
        elif init_pretrained:
            self.encoder=AutoModel.from_pretrained(backbone,local_files_only=local_files_only)
        else:
            self.encoder=AutoModel.from_config(AutoConfig.from_pretrained(backbone,local_files_only=local_files_only))
        h=self.encoder.config.hidden_size
        self.global_dim=global_dim
        self.chunks_per_forward=chunks_per_forward
        self.summary=nn.Sequential(nn.Linear(h*2,global_dim),nn.LayerNorm(global_dim),nn.Tanh())
        self.global_layers=nn.ModuleList([GlobalRoPEBlock(global_dim,global_heads,dropout)
                                          for _ in range(global_layers)])
        self.global_norm=nn.LayerNorm(global_dim)
        self.fusion=nn.Sequential(nn.Linear(h+global_dim,h),nn.GELU(),nn.Dropout(dropout),nn.Linear(h,h))
        self.fusion_norm=nn.LayerNorm(h)
        self.left_proj=nn.Linear(h,128)
        self.right_proj=nn.Linear(h,128)
        self.char_embedding=nn.Embedding(8192,24,padding_idx=0)
        self.gap_proj=nn.Sequential(nn.Linear(128*2+48+10,128),nn.GELU(),nn.LayerNorm(128))
        self.gap_conv1=nn.Conv1d(128,128,kernel_size=5,padding=2)
        self.gap_conv2=nn.Conv1d(128,128,kernel_size=5,padding=2)
        self.dropout=nn.Dropout(dropout)
        self.start_head=nn.Linear(128,1)
        self.end_head=nn.Linear(128,1)
        self.transition=TransitionScorer(dropout=dropout)

    def forward(self,input_ids,attention_mask,left_indices,right_indices,
                left_chars,right_chars,char_flags,structure_features,span=None):
        local=[]
        step=max(1,self.chunks_per_forward)
        for i in range(0,input_ids.size(0),step):
            out=self.encoder(input_ids=input_ids[i:i+step],
                             attention_mask=attention_mask[i:i+step]).last_hidden_state
            local.append(out)
        h=torch.cat(local,dim=0)
        m=attention_mask.unsqueeze(-1).to(dtype=h.dtype)
        pooled=(h*m).sum(dim=1)/m.sum(dim=1).clamp_min(1.)
        summaries=self.summary(torch.cat((h[:,0],pooled),dim=-1))
        for block in self.global_layers:summaries=block(summaries)
        global_context=self.global_norm(summaries)
        global_tok=global_context[:,None,:].expand(-1,h.size(1),-1)
        tok=self.fusion_norm(h+self.fusion(torch.cat((h,global_tok),dim=-1)))
        flat=tok.reshape(-1,tok.size(-1))
        # left_indices/right_indices address flattened [chunk,token] positions.
        # -1 denotes missing local context, not a real token.
        def look(idx):
            valid=idx>=0
            vec=flat[idx.clamp(min=0)]
            return vec*valid.unsqueeze(-1).to(vec.dtype)
        features=torch.cat((self.left_proj(look(left_indices)),
                            self.right_proj(look(right_indices)),
                            self.char_embedding(left_chars),
                            self.char_embedding(right_chars),
                            char_flags.to(dtype=tok.dtype)),dim=-1)
        gaps=self.gap_proj(features)
        x=gaps.transpose(0,1)[None,:,:]
        x=x+self.dropout(F.gelu(self.gap_conv1(x)))
        x=x+self.dropout(F.gelu(self.gap_conv2(x)))
        gaps=x[0].transpose(0,1)
        adjustment = self.transition(gaps, structure_features)
        start = self.start_head(gaps).squeeze(-1).float() + adjustment[:, 0]
        end = self.end_head(gaps).squeeze(-1).float() + adjustment[:, 1]
        result={'start_logits':start,'end_logits':end}
        if span is not None:
            result['loss']=joint_nll(start,end,span)
        return result
