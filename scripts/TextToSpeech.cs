using Godot;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;

public partial class TextToSpeech : Node
{
    static TextToSpeech instance;

    private int generationCounter = 0;

    private int generationProcessedCounter = 0;

    private bool generationRunning = false;

    private bool speechProcessing = false;

    private SceneTree tree;

    string outputFilePath = "";

    [Export]
    public bool allowCuda = false;

#if GODOT_MACOS
    [LibraryImport("ttslib-macos-arm64.dylib", SetLastError = true)]
#elif GODOT_PC
    [LibraryImport("ttslib-win-x64.dll", SetLastError = true)]
#endif
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial void LoadVoice(int modelDataLength, byte[] modelData);

#if GODOT_MACOS
    [LibraryImport("ttslib-macos-arm64.dylib", SetLastError = true, StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
#elif GODOT_PC
    [LibraryImport("ttslib-win-x64.dll", SetLastError = true, StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
#endif
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial void LoadIPAData(string path);

#if GODOT_MACOS
    [LibraryImport("ttslib-macos-arm64.dylib", SetLastError = true)]
#elif GODOT_PC
    [LibraryImport("ttslib-win-x64.dll", SetLastError = true)]
#endif
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial void ApplySynthesisConfig(float lengthScale, float noiseScale, float noiseW, int speakerId, float sentenceSilenceSeconds, float fadeTimeSeconds, [MarshalAs(UnmanagedType.Bool)] bool useCuda);

#if GODOT_MACOS
    [LibraryImport("ttslib-macos-arm64.dylib", SetLastError = true)]
#elif GODOT_PC
    [LibraryImport("ttslib-win-x64.dll", SetLastError = true)]
#endif
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial void SetWriteToFile([MarshalAs(UnmanagedType.Bool)] bool path);

#if GODOT_MACOS
    [LibraryImport("ttslib-macos-arm64.dylib", SetLastError = true, StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
#elif GODOT_PC
    [LibraryImport("ttslib-win-x64.dll", SetLastError = true, StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
#endif
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial void SetOutputDirectory(string path);

#if GODOT_MACOS
    [LibraryImport("ttslib-macos-arm64.dylib", SetLastError = true, StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
#elif GODOT_PC
    [LibraryImport("ttslib-win-x64.dll", SetLastError = true, StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
#endif
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial IntPtr GenerateVoiceData(out int dataLength, string text);

#if GODOT_MACOS
    [LibraryImport("ttslib-macos-arm64.dylib", SetLastError = true)]
#elif GODOT_PC
    [LibraryImport("ttslib-win-x64.dll", SetLastError = true)]
#endif
    [UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
    private static partial void DiscardVoiceData(IntPtr data);


    public static async Task<AudioStreamWav> Generate(string speaker, string text)
    {
        return await instance._Generate(speaker, text);
    }

    private async Task<AudioStreamWav> _Generate(string speaker, string text)
    {
        Log($"Preparing speech with speaker {speaker} and text {text}");

        while (generationRunning || speechProcessing)
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        generationRunning = true;

        var generation = generationCounter++;

        AudioStreamWav audioFile = null;

        while (generationProcessedCounter < generation)
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        speechProcessing = true;

        int.TryParse(speaker, out var res);

        res = Mathf.Clamp(res, 0, 903);

        Log($"Generating speech with speaker {res} and text {text}");

        ThreadPool.QueueUserWorkItem((_) =>
        {
            try
            {
                ApplySynthesisConfig(1.3f, 0.33f, 0.66f, res, 0.4f, 0.1f, allowCuda);
                var data = GenerateVoiceData(out var dataLength, text);
                byte[] byteData = new byte[dataLength];
                Marshal.Copy(data, byteData, 0, dataLength);
                DiscardVoiceData(data);

                audioFile = new()
                {
                    MixRate = 22050,
                    Data = byteData,
                    Format = AudioStreamWav.FormatEnum.Format16Bits,
                    LoopEnd = -1,
                    LoopBegin = 0,
                    LoopMode = AudioStreamWav.LoopModeEnum.Disabled
                };
            }
            finally
            {
                generationProcessedCounter++;
                generationRunning = false;
                speechProcessing = false;
            }
        });

        while (speechProcessing)
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        return audioFile;
    }

    public override async void _Ready()
    {
#if GODOT_MACOS
        NativeLibrary.Load(ProjectSettings.GlobalizePath("res://tts/macos-arm64/libonnxruntime.1.18.0.dylib"));
#elif GODOT_PC
        NativeLibrary.Load(ProjectSettings.GlobalizePath("res://tts/win-x64/onnxruntime.dll"));
        NativeLibrary.Load(ProjectSettings.GlobalizePath("res://tts/win-x64/onnxruntime_providers_shared.dll"));
#endif
        instance = this;
        tree = GetTree();
        LoadIPAData(ProjectSettings.GlobalizePath("res://tts/ipa.data"));
        var modelByteData = await File.ReadAllBytesAsync(ProjectSettings.GlobalizePath("res://tts/libri_medium.onnx"));
        LoadVoice(modelByteData.Length, modelByteData);
        SetWriteToFile(false);
        ApplySynthesisConfig(1.3f, 0.3f, 0.6f, 0, 0.4f, 0.1f, false);
    }

    static void Log(string info)
    {
        GD.Print(LogPrefix() + info);
    }

    private static DateTime startTime = Process.GetCurrentProcess().StartTime.ToUniversalTime();
    static string LogPrefix()
    {
        var time = DateTime.UtcNow - startTime;
        return $"[TTS][{time:hh\\:mm\\:ss\\:fff}] ";
    }
}
