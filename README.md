# Godot Voiced AI NPCs
A prototype using meta's LLaMA 2 (7B) as NPC controller / diaglogue manager with microphone input and text to speech output in realtime

### Conversation and Interaction Workflow
The general workflow and tools for interaction with NPCS are intended to be the following:
- Player speaks to NPC (whisper.cpp) or types into chat
- Input is processed to AI prompt 
- AI generates response (llama.cpp Server)
- Speech is generated (piper-without-espeak with LibriTTS)

### Supported Platforms
Currently MacOS ARM64 and Windows X64. \
For GPU acelleration LLaMA.cpp was compiled with Vulkan for win-x64 and Metal for macos-arm64. \

### LLaMA.cpp compatible Models
https://huggingface.co/TheBloke/WizardLM-7B-uncensored-GGUF \
WizardLM-7B-uncensored.Q4_K_M.gguf has proven to work on both Windows and MacOS for this project. \
This model is licensed under Apache 2.0 and this specific training dataset is CC-BY.
Disclaimer (04.08.2024): In case you want to use this project as a base for your game, I would recommend you switch to LLaMA 3.1 or Gemma 2 9B as they yield far superior results to the "old" WizardLM. Especially Instruct-tuned models adhere to prompts in a way that is needed for proper control over generation.

### Installation

Everything should work out of the box, you only need a fitting LLaMA 2 model and place it in model and rename it to model.gguf. \
Please read error messages carefully.

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
- A TTS model trained on different emotions would be greatly appreciated
- Someone needs to take a look at all speakers and determine the best ones

### Licensing

#### Code

This project's code - MIT \
llama.cpp - MIT \
vits - MIT \
piper-without-espeak - MIT \
whisper AI - MIT \
whisper.cpp - MIT \
whisper-godot - MIT \
LLaMA - Llama 2 Community License Agreement \
Libri TTS Medium - CC BY 4.0 - "LibriTTS: A Corpus Derived from LibriSpeech for Text-to-Speech", Heiga Zen, Viet Dang, Rob Clark, Yu Zhang, Ron J. Weiss, Ye Jia, Zhifeng Chen, and Yonghui Wu, arXiv, 2019 \

#### Assets

hair.obj, elvs_grump_hair by Elvaerwyn, CC-BY, taken from MakeHuman Asset Pack Hair 02 https://static.makehumancommunity.org/assets/assetpacks/hair02.html \
skin_normal.png, Normalmap from Kamden Skin by Mindfront, CC-BY 4.0, taken from http://www.makehumancommunity.org/skin/kamden_skin.html \
Other NPC-related assets are public domain, big thanks for the makehuman project and contributors for existing and being generally very awesome.

