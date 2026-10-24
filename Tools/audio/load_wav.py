import numpy as np, struct
def load(path):
    b=open(path,'rb').read(); i=12; rate=44100; data=None
    while i<len(b):
        cid=b[i:i+4]; size=struct.unpack('<I',b[i+4:i+8])[0]
        if cid==b'fmt ': rate=struct.unpack('<I',b[i+12:i+16])[0]
        if cid==b'data': data=np.frombuffer(b[i+8:i+8+size],dtype=np.int16).astype(float)/32768
        i+=8+size+(size&1)
    return data,rate
