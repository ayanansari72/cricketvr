"""Cut the bat impact clips used by the game from the owner's recording.

Usage (from the repository root):
    afconvert -f WAVE -d LEI16@44100 "Sector 66.m4a" /tmp/sector66.wav
    python3 Tools/audio/derive_bat_sounds.py /tmp/sector66.wav Assets/CricketVR/Audio

Writes BatMiddle_{0,1,2}.wav (the recorded hits, trimmed and level-matched), BatOffCentre_{0,1,2}.wav
(the same hits low-passed, pitched down ~15% and with a shorter ring: a mistimed shot) and BatEdge_{0,1,2}.wav
(only the bright attack, high-passed and short: a nick). The three hits used are the least clipped in the
recording (onsets at 1.148 s, 3.045 s and 4.488 s).
"""
import os
import sys
import wave

import numpy as np

from load_wav import load

HIT_ONSETS = [1.148, 3.045, 4.488]


def main(source, out_dir):
    x, rate = load(source)

    def cut(t, length=0.30, pre=0.002):
        s = int((t - pre) * rate)
        seg = x[s:s + int(length * rate)].copy()
        fade = np.ones(len(seg))
        k = int(len(seg) * 0.35)
        fade[-k:] = np.linspace(1, 0, k) ** 2  # fade out so the clip never ends with a pop
        return seg * fade

    def lowpass(y, fc):
        a = np.exp(-2 * np.pi * fc / rate)
        out = np.zeros_like(y)
        prev = 0.0
        for i, v in enumerate(y):
            prev = (1 - a) * v + a * prev
            out[i] = prev
        return out

    def highpass(y, fc):
        return y - lowpass(y, fc)

    def resample(y, factor):  # factor < 1 lowers the pitch
        return np.interp(np.arange(0, len(y) - 1, factor), np.arange(len(y)), y)

    def decay(y, per_second):
        return y * np.exp(-np.arange(len(y)) / rate * per_second)

    def norm(y, peak):
        return y / (np.abs(y).max() + 1e-9) * peak

    def write(name, y):
        pcm = (np.clip(y, -1, 1) * 32767).astype(np.int16)
        with wave.open(os.path.join(out_dir, name), 'wb') as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(rate)
            w.writeframes(pcm.tobytes())

    os.makedirs(out_dir, exist_ok=True)
    for i, t in enumerate(HIT_ONSETS):
        middle = norm(cut(t), 0.95)
        dull = resample(lowpass(lowpass(middle, 1300), 1300), 0.85)[:len(middle)]
        dull = norm(decay(dull, 18), 0.85)
        edge = norm(decay(highpass(highpass(cut(t, length=0.06), 2500), 2500), 45), 0.75)
        write(f'BatMiddle_{i}.wav', middle)
        write(f'BatOffCentre_{i}.wav', dull)
        write(f'BatEdge_{i}.wav', edge)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
