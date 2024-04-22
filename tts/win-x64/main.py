import torch
from TTS.api import TTS

# Get device
device = "cuda" if torch.cuda.is_available() else "cpu"

# Init TTS
tts = TTS("tts_models/en/vctk/vits").to(device)

while True:
    print("-READY-")
    data = input().split(":",2)
    tts.tts_to_file(text=data[2], file_path=data[1], speaker=data[0])
