# Godot AI RPG
A prototype using meta's LLaMA 2 (7B) as a dungeon master and NPC controller in an RPG

### Conversation and Interaction Workflow
The general workflow and tools for interaction with NPCS are intended to be the following:
#### 1. Player speaks to NPC (Vosk) or types into chat
#### 2. Input is processed to AI prompt 
#### 3. AI generates response (LLaMA2.cpp Server)
#### 4. Speech is generated (vctk/vits TTS model & Espeak)

### Supported Platforms
Currently MacOS ARM64 and Windows X64.
Linux X64 and Linux ARM might be added at a later time.
For GPU acelleration LLaMA.cpp was compiled with OpenCL BLAS for win-x64 and Metal for macos-arm64.
CUDA and Vulkan might also be added at a later time.

### Roadmap
#### [x] Add
#### [x] Make LLaMA interaction async and allow partial responses, see [#1]
#### [x] Add STT library for both base platforms
#### [ ] Add TTS library for both base platforms
#### [ ] Implement full conversation STT -> AI -> TTS
#### [ ] Refactor code
#### [ ] Make the AI roleplay properly
#### [ ] Add humanoid example NPC
#### [ ] Add dungeon master AI

### LLaMA Models
https://huggingface.co/TheBloke/WizardLM-7B-uncensored-GGUF
WizardLM-7B-uncensored.Q4_K_M.gguf has been working fine for me on both Windows and MacOS

### Issues
At the moment the project is a Franksteinish abomination and prone to breaking.
If you have an issue, please post as much info as possible.
Do not post screenshots of error message, copy them.

### Contribution
Feel free to make a PR to add to or improve the project.
Keep it at a reasonable size, though, please.
In case you are doing heavy lifting in your code, do it on a thread or async.

### Licensing

This project's code - MIT
llama.cpp - MIT
vits - MIT 
LLaMA - Llama 2 Community License Agreement 
vosk-model-small-en-us-0.15 - Apache 2.0
Coqui TTS - Mozilla Public License Version 2.0
EspeakNG - GNU GPL v3

Since GNU GPL v3 is very restrictive, you might want to select a different TTS model that does not use EspeakNG. This setting needs to be changed in the main.py of the TTS backends.
