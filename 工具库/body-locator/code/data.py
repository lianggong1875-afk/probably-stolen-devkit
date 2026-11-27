import json,random
from encode import encode_document, batch_to_device
from structure import make_structure


def rows(path,seed=None,limit=0):
    offsets=[]
    with open(path,'rb') as f:
        while True:
            pos=f.tell()
            if not f.readline():break
            offsets.append(pos)
    if seed is not None:random.Random(seed).shuffle(offsets)
    with open(path,'rb') as f:
        for i,pos in enumerate(offsets):
            if limit and i>=limit:break
            f.seek(pos)
            row=json.loads(f.readline())
            s,e=row['body_span'];text=row['text']
            if not isinstance(s,int) or not isinstance(e,int) or not 0<=s<e<=len(text):
                raise ValueError(f'Invalid span in {path}: {row.get("id",i)}')
            yield row


def encode(tok,row,settings):
    return encode_document(tok,row['text'],span=tuple(row['body_span']),
                           window=settings['window'],overlap=settings['overlap'])


def model_inputs(encoded, text, device):
    tensors = batch_to_device(encoded, device)
    tensors["structure_features"] = make_structure(text).to(device)
    return tensors


def coverage(enc,settings):
    cap=settings['max_total_tokens']
    return not cap or enc['token_count']<=cap
