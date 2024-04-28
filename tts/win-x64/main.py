import torch
from TTS.api import TTS


print("-ALLOW CUDA-")
allow_cuda = input()
if allow_cuda.lower() == 'y' :
    device = "cuda" if torch.cuda.is_available() else "cpu" 
else:
    device = "cpu"

tts = TTS("tts_models/en/vctk/vits").to(device)

while True:
    print("-READY-")
    data = input().split(":",2)
    print("RECEIVED: " + ','.join(data))
    tts.tts_to_file(text=data[2], file_path=data[1], speaker=data[0])
