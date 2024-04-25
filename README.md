# Godot AI RPG
A prototype using meta's LLaMA 2 (7B) as a dungeon master and NPC controller in an RPG

### Conversation and Interaction Workflow
The general workflow and tools for interaction with NPCS are intended to be the following:
#### 1. Player speaks to NPC (Vosk) or types into chat
#### 2. Input is processed to AI prompt 
#### 3. AI generates response (LLaMA2.cpp Server)
#### 4. Speech is generated (vctk/vits TTS model & Espeak)

### Supported Platforms
Currently MacOS ARM64 and Windows X64. \
Linux X64 and Linux ARM might be added at a later time. \
For GPU acelleration LLaMA.cpp was compiled with Vulkan for win-x64 and Metal for macos-arm64. \
CUDA and OpenCL might also be added at a later time.

### Roadmap
- [x] Make LLaMA interaction async and allow partial responses, see [#1]
- [x] Add STT library for both base platforms
- [ ] Add TTS library for both base platforms
- [ ] Implement full conversation STT -> AI -> TTS
- [ ] Refactor code
- [ ] Make python builds bundled and standalone
- [ ] Make the AI roleplay properly
- [ ] Add humanoid example NPC
- [ ] Add dungeon master AI
- [ ] Make GPL dependencies optional
- [ ] Add alternative non-GPL TTS

### LLaMA Models
https://huggingface.co/TheBloke/WizardLM-7B-uncensored-GGUF
WizardLM-7B-uncensored.Q4_K_M.gguf has been working fine for me on both Windows and MacOS

### Installation

#### Windows x64
Everything except for the TTS backend is bundled with the project and should work out of the box.\
To install the TTS backend execute the following on cmd in the project's directory.\
```
cd tts\win-x64
.\python\Scripts\virtualenv env
.\env\Scripts\Activate
pip install tts
```
Then you need to replace the code in \tts\win-x64\env\Lib\site-packages\TTS\tts\utils\text\phonemizers\espeak_wrapper.py \
with the code from \tts\win-x64\custom_espeak_wrapper.py, so the bundled espeak can work. \
Alternatively you will have to have espeak or espeak-ng installed and available on path so the cmd can call it. \
Please read error messages carefully.

### Issues
At the moment the project is a Franksteinish abomination and prone to breaking. \
If you have an issue, please post as much info as possible. \ 
Do not post screenshots of error message, copy them. \
All backend output is written to the godot console. \
Please copy this output to your issue as well unless you are 100% sure it is unrelated.

### Contribution
Feel free to make a PR to add to or improve the project. \
Keep it at a reasonable size, though, please. \
In case you are doing heavy lifting in your code, do it on a thread or async.

### Licensing

This project's code - MIT \
llama.cpp - MIT \
vits - MIT \
LLaMA - Llama 2 Community License Agreement \
vosk-model-small-en-us-0.15 - Apache 2.0 \
Coqui TTS - Mozilla Public License Version 2.0 \
EspeakNG - GNU GPL v3

Since GNU GPL v3 is very restrictive, you might want to select a different TTS model that does not use EspeakNG. This setting needs to be changed in the main.py of the TTS backends.
