from pathlib import Path
import numpy as np
import wave

# Original 16-bar swing/lounge composition; no sampled or third-party audio.
rate=22050;beat=60/96;beats=64
song=np.zeros(round(rate*beat*beats),dtype=np.float64)
random=np.random.default_rng(170402)
def add(start,note,duration,volume,voice='keys'):
    length=round(duration*rate);t=np.arange(length)/rate;frequency=440*2**((note-69)/12)
    attack=np.minimum(1,t/.007);release=np.minimum(1,(duration-t)/.08)
    if voice=='bass':
        y=(np.sin(2*np.pi*frequency*t)+.15*np.sin(4*np.pi*frequency*t))*np.exp(-t*2.2)*attack*release
    else:
        y=(np.sin(2*np.pi*frequency*t+1.1*np.sin(4*np.pi*frequency*t)*np.exp(-t*4))+.10*np.sin(6*np.pi*frequency*t))*np.exp(-t*1.5)*attack*release
    index=round(start*rate);end=min(len(song),index+length)
    song[index:end]+=y[:end-index]*volume
def brush(start,length,volume):
    n=round(length*rate);t=np.arange(n)/rate;noise=random.normal(0,1,n)
    noise=np.r_[0,np.diff(noise)]*.5
    y=noise*np.exp(-t*28)*np.minimum(1,t/.002)
    index=round(start*rate);end=min(len(song),index+n);song[index:end]+=y[:end-index]*volume
chords=[[60,63,67,70],[65,68,72,75],[68,72,75,79],[67,71,74,77]]
roots=[36,41,44,43]
melody=[79,77,75,74,72,70,72,75,77,75,72,70,74,77,75,71]
for bar in range(16):
    chord=chords[(bar//2)%4];root=roots[(bar//2)%4]
    for q,interval in enumerate([0,7,10,12] if bar%2==0 else [12,10,7,2]):
        start=(bar*4+q)*beat
        add(start,root+interval,beat*.82,.23,'bass')
        brush(start,.09,.035)
        brush(start+beat*2/3,.07,.018)
    for offset in [2/3,2,3+2/3]:
        for i,note in enumerate(chord):add((bar*4+offset)*beat+i*.008,note,beat*1.3,.045)
    if bar%4!=3:
        add((bar*4+1+2/3)*beat,melody[bar],beat*.75,.07)
        add((bar*4+3)*beat,melody[bar]+(-2 if bar%2 else 3),beat*.68,.05)
# Wrap instrument tails at the phrase boundary with a short equal-power join.
n=round(.012*rate);song[:n]*=np.linspace(0,1,n);song[-n:]*=np.linspace(1,0,n)
song=np.tanh(song*1.5);song*=.82/max(abs(song))
path=Path(__file__).resolve().parents[1] / 'src/BazaarCasino/Assets/casino-lounge.wav'
with wave.open(str(path),'wb') as file:
    file.setnchannels(1);file.setsampwidth(2);file.setframerate(rate)
    file.writeframes(np.round(song*32767).astype('<i2').tobytes())
print(f'Original casino loop: {len(song)/rate:.1f}s, {rate}Hz mono, peak {max(abs(song)):.2f}, RMS {np.sqrt(np.mean(song**2)):.3f}; {path}')
