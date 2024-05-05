using Godot;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;

public partial class TextToSpeech : Node
{
    static TextToSpeech instance;

    private Process ttsProcess;

    private int generationCounter = 0;

    private int generationProcessedCounter = 0;

    private bool generationRunning = false;

    private bool speechProcessing = true;

    private StreamWriter input;

    private SceneTree tree;

    private string backend;

    private string ttsPath;

    const string UNSUPPORTED_BACKEND = "none";

    string WINDOWS_X64_BACKEND = System.Environment.CurrentDirectory + "\\tts\\win-x64\\";

    string MACOS_ARM64_BACKEND = "./tts/macos-arm64/";

    string outputFilePath = "";

    [Export]
    public bool allowCuda = false;

    private Stopwatch keepaliveStopwatch = new();

    public static async Task<AudioStreamWav> Generate(string speaker, string text, bool deleteAfterLoading = true, string rename = null)
    {
        return await instance._Generate(speaker, text, deleteAfterLoading, rename);
    }

    private async Task<AudioStreamWav> _Generate(string speaker, string text, bool deleteAfterLoading = true, string rename = null)
    {
        while (generationRunning || speechProcessing)
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        generationRunning = true;
        keepaliveStopwatch.Restart();

        var generation = generationCounter++;

        try
        {
            speechProcessing = true;
            Log($"Generating speech with speaker {speaker} and text {text}");
            Directory.CreateDirectory(Path.Join(backend, "/output"));

            while (generationProcessedCounter < generation)
            {
                await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }

            await input.WriteLineAsync(speaker[..3] + text);

            while (speechProcessing)
            {
                await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }

        }
        catch (Exception exception)
        {
            Log("Exception while generating speech: \n" + exception);
        }
        finally
        {
            generationProcessedCounter++;
            generationRunning = false;
        }

        AudioStreamWav audioFile = null;

        try
        {
            audioFile = await RuntimeAudioLoader.LoadFile(outputFilePath);
            if (deleteAfterLoading)
            {
                _ = Task.Run(() =>
                {
                    try { File.Delete(outputFilePath); } catch (Exception exception) { Log("Exception while deleting audio file: \n" + exception); }
                });
            }
            else if (rename != null)
            {
                if (!rename.EndsWith(".wav")) rename += ".wav";
                File.Move(outputFilePath, Path.Join(Path.GetDirectoryName(outputFilePath), rename));
            }
        }
        catch (Exception exception)
        {
            Log("Exception while loading audio file: \n" + exception);
        }

        return audioFile;
    }

    public override async void _Ready()
    {
        GD.Print(System.Environment.CurrentDirectory);
        instance = this;
        tree = GetTree();
        StartServer();
        keepaliveStopwatch.Start();

        /*for(var i = 0; i < 900; i++)
        {
            await Generate(string.Format("{0:000}", i), "Hello there. This a test to evaluate speaker quality.", false, "SAMPLE_" + i);
        }*/
    }

    public override async void _Process(double delta)
    {
        if (keepaliveStopwatch.Elapsed.TotalMilliseconds > 25000 && !(generationRunning || speechProcessing))
        {
            await input.WriteLineAsync("-KEEPALIVE-");
            keepaliveStopwatch.Restart();
        }
    }

    private void SelectBackend(Architecture architecture)
    {
        backend = UNSUPPORTED_BACKEND;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && architecture == Architecture.Arm64)
        {
            Log("Detected MacOS on Arm64");
            backend = MACOS_ARM64_BACKEND;
            ttsPath = "tts";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && architecture == Architecture.X64)
        {
            Log("Detected Windows on X64");
            backend = WINDOWS_X64_BACKEND;
            ttsPath = "tts.exe";
        }
    }

    private bool StartServer()
    {
        var architecture = RuntimeInformation.ProcessArchitecture;

        SelectBackend(architecture);

        if (backend == UNSUPPORTED_BACKEND)
        {
            Log($"Architecture {architecture} on this platform is currently unsupported");
            tree.Quit();
        }

        Log($"Starting. Working directory: {backend}, tts path: {ttsPath}");

        ttsProcess = new();
        ttsProcess.StartInfo.ErrorDialog = false;
        ttsProcess.StartInfo.UseShellExecute = false;
        ttsProcess.EnableRaisingEvents = true;
        ttsProcess.StartInfo.CreateNoWindow = true;
        ttsProcess.StartInfo.RedirectStandardError = true;
        ttsProcess.StartInfo.RedirectStandardInput = true;
        ttsProcess.StartInfo.RedirectStandardOutput = true;
        ttsProcess.Exited += processExited;
        ttsProcess.ErrorDataReceived += processErrorDataReceived;
        ttsProcess.OutputDataReceived += processOutputDataReceived;
        ttsProcess.StartInfo.WorkingDirectory = backend;
        ttsProcess.StartInfo.FileName = backend + ttsPath;
        ttsProcess.StartInfo.Arguments = "--model \"./libri_medium.onnx\" --output_dir \"./output\" --config \"./libri_medium.json\" --length_scale 1.45";


        ttsProcess.Start();
        input = ttsProcess.StandardInput;
        ttsProcess.BeginErrorReadLine();
        ttsProcess.BeginOutputReadLine();

        void processExited(object sender, EventArgs e)
        {
            Log("Exited with code " + ttsProcess.ExitCode);
            StartServer();
        }

        void processErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Log(e.Data);
        }

        void processOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            var data = (e.Data ?? "").Trim();

            if (data.Contains("-READY-"))
            {
                Log("Ready to generate");
                speechProcessing = false;
            }
            else if (data.StartsWith("-FILEPATH-"))
            {
                outputFilePath = data["-FILEPATH-".Length..];
                speechProcessing = false;
                Log(data);
            }
            else if (data.Length > 0)
            {
                Log(data);
            }
        }

        return true;
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
