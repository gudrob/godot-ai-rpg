# Godot Voiced AI NPCs
A prototype using meta's LLaMA 2 (7B) as NPC controller in an RPG with microphone input and text to speech output in realtime

### Conversation and Interaction Workflow
The general workflow and tools for interaction with NPCS are intended to be the following:
- Player speaks to NPC (Vosk) or types into chat
- Input is processed to AI prompt 
- AI generates response (LLaMA2.cpp Server)
- Speech is generated (piper with LibriTTS)

### Supported Platforms
Currently MacOS ARM64 and Windows X64. \
Linux X64 and Linux ARM might be added at a later time. \
For GPU acelleration LLaMA.cpp was compiled with Vulkan for win-x64 and Metal for macos-arm64. \
CUDA and OpenCL might also be added at a later time.

### Roadmap
- [x] Make LLaMA interaction async and allow partial responses, see [#1]
- [x] Add STT library for both base platforms
- [x] Implement full conversation STT -> AI -> TTS
- [x] Add humanoid example NPC
- [ ] Add TTS library for both base platforms
- [ ] Refactor code
- [ ] Introduce TTS plugin system so own solutions can be used
- [ ] Make the AI roleplay properly
- [ ] Add gameplay scenario that goes beyond dialogue
- [ ] Add dungeon master AI
- [ ] Make NPCs show emotions

### LLaMA Models
https://huggingface.co/TheBloke/WizardLM-7B-uncensored-GGUF
WizardLM-7B-uncensored.Q4_K_M.gguf has proven to work on both Windows and MacOS for this project

### Installation

#### Windows x64
Everything should work out of the box, you only need a fitting LLaMA 2 model and place it in model and rename it to model.gguf. \
Please read error messages carefully.

#### MacOS ARM64
Currently TTS is not working for MacOS as the project is moving away from bundled python.

### Issues
If you have an issue, please post as much info as possible. \
Do not post screenshots of error message, copy them. \
All backend output is written to the godot console. \
Please copy this output to your issue as well unless you are 100% sure it is unrelated.

### Contribution
Feel free to make a PR to add to or improve the project. \
Keep it at a reasonable size, though, please. \
In case you are doing heavy lifting in your code, do it on a thread or async. \
#### Possible, needed contributons
While piper is very fast and very cool for tts it depends on espeak which is GPL v3 licensed. If someone has decent knowledge in this field, it would be great if you could find a way to avoid espeak or anything that needs source disclosure. \
Also a TTS model trained on different emotions would be greatly appreciated.

### Licensing

#### Code

This project's code - MIT \
llama.cpp - MIT \
vits - MIT \
piper - MIT \
LLaMA - Llama 2 Community License Agreement \
vosk-model-small-en-us-0.15 - Apache 2.0 \
Libri TTS Medium - CC BY 4.0 - "LibriTTS: A Corpus Derived from LibriSpeech for Text-to-Speech", Heiga Zen, Viet Dang, Rob Clark, Yu Zhang, Ron J. Weiss, Ye Jia, Zhifeng Chen, and Yonghui Wu, arXiv, 2019 \
EspeakNG - GNU GPL v3 

#### Assets

hair.obj, elvs_grump_hair by Elvaerwyn, CC-BY, taken from MakeHuman Asset Pack Hair 02 https://static.makehumancommunity.org/assets/assetpacks/hair02.html \
skin_normal.png, Normalmap from Kamden Skin by Mindfront, CC-BY 4.0, taken from http://www.makehumancommunity.org/skin/kamden_skin.html \
Other NPC-related assets are public domain, big thanks for the makehuman project and contributors for existing and being generally very awesome.

