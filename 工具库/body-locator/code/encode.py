"""Fast-tokenizer overlapping chunks, exact character coverage and span labels.

Boundary positions are Unicode codepoint offsets in the ORIGINAL string, 0..N.
This differs from token classification and has no whitespace restoration rules.
"""
from __future__ import annotations
import torch


def _best_token_per_char(offsets, specials, textlen, seq_lengths):
    # Returns flattened token position for each covered original character.
    # When windows overlap, select a central-window occurrence for stability.
    pos=[-1]*textlen
    q=[-1]*textlen
    width=max(seq_lengths)
    for row,(offs,sp,len_row) in enumerate(zip(offsets,specials,seq_lengths)):
        for j,((a,b),special) in enumerate(zip(offs,sp)):
            a=int(a);b=int(b)
            if special or a==b:continue
            if a<0 or b>textlen:raise ValueError('Invalid tokenizer offset')
            quality=min(j,len_row-j-1)
            for c in range(a,b):
                if quality>q[c]:
                    pos[c]=row*width+j;q[c]=quality
    # Carry token embeddings to adjacent unmapped characters but preserve both
    # directions separately: a whitespace gap knows its left & right neighbors.
    left=[-1]*(textlen+1)
    right=[-1]*(textlen+1)
    last=-1
    for i in range(textlen+1):
        if i>0 and pos[i-1]>=0:last=pos[i-1]
        left[i]=last
    last=-1
    for i in range(textlen,-1,-1):
        if i<textlen and pos[i]>=0:last=pos[i]
        right[i]=last
    return left,right


def encode_document(tokenizer,text,span=None,window=512,overlap=128):
    if not tokenizer.is_fast:raise ValueError('A fast tokenizer is required')
    if not text:raise ValueError('Empty string is not a supported document')
    budget=window-tokenizer.num_special_tokens_to_add(pair=False)
    if window>tokenizer.model_max_length and tokenizer.model_max_length<100000:
        raise ValueError(f'Local window {window} exceeds backbone max {tokenizer.model_max_length}')
    if not 0<=overlap<budget:raise ValueError('Invalid overlap/window')
    enc=tokenizer(text,add_special_tokens=True,truncation=True,max_length=window,
                  stride=overlap,return_overflowing_tokens=True,
                  return_offsets_mapping=True,return_special_tokens_mask=True,
                  return_attention_mask=True,padding=False)
    ids=enc['input_ids'];attn=enc['attention_mask'];special=enc['special_tokens_mask']
    offsets=enc['offset_mapping'];lens=[len(x) for x in ids]
    width=max(lens)
    if width>window:raise ValueError('Overflow failed')
    pad=tokenizer.pad_token_id
    if pad is None:raise ValueError('Missing tokenizer pad token')
    def to_padded(rows,pv):return torch.tensor([list(row)+[pv]*(width-len(row)) for row in rows],dtype=torch.long)
    left,right=_best_token_per_char(offsets,special,len(text),lens)
    # Non-special raw tokens in overlapping windows are not counted twice.
    unique_offsets={(int(a),int(b)) for row,sm in zip(offsets,special)
                    for (a,b),m in zip(row,sm) if not m and b>a}
    if not unique_offsets:raise ValueError('No non-special tokens in document')
    # Character identities and categories are deliberately local features: the
    # model can choose an offset inside a WordPiece or whitespace run.
    char_code=lambda c:((ord(c)*2654435761)%8191)+1 if c else 0
    chars=[0]+[char_code(c) for c in text]
    next_chars=[char_code(c) for c in text]+[0]
    flags=[_char_flags(text[i-1] if i>0 else '',text[i] if i<len(text) else '')
           for i in range(len(text)+1)]
    if span is not None:
        a,b=map(int,span)
        if not 0<=a<b<=len(text):raise ValueError(f'Invalid body span {span}')
    else:a=b=None
    return {'input_ids':to_padded(ids,pad),'attention_mask':to_padded(attn,0),
            'left_indices':torch.tensor(left,dtype=torch.long),
            'right_indices':torch.tensor(right,dtype=torch.long),
            'left_chars':torch.tensor(chars,dtype=torch.long),
            'right_chars':torch.tensor(next_chars,dtype=torch.long),
            'char_flags':torch.tensor(flags,dtype=torch.float32),
            'span':(a,b) if span is not None else None,
            'token_count':len(unique_offsets),'chunk_count':len(ids),
            'char_count':len(text)}


def _char_flags(l,r):
    def props(c):
        if not c:return (0,0,0,0,0)
        return (int(c.isspace()),int(c in '，。！？：；、,.!?;:'),
                int(c in '<>[]{}/\\"\'“”「」()（）#*-_=|'),
                int('0'<=c<='9'),int(c=='\n'))
    return [*props(l),*props(r)]


def batch_to_device(encoded,device):
    return {key:value.to(device,non_blocking=True) for key,value in encoded.items()
            if isinstance(value,torch.Tensor)}
