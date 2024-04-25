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

    const string WINDOWS_X64_BACKEND = "./tts/win-x64/";

    const string MACOS_ARM64_BACKEND = "./tts/macos-arm64/";

    public static async Task<AudioStreamWav> Generate(string speaker, string text, bool deleteAfterLoading = true)
    {
        return await instance._Generate(speaker, text);
    }

    private async Task<AudioStreamWav> _Generate(string speaker, string text, bool deleteAfterLoading = true)
    {
        while (generationRunning || speechProcessing)
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        generationRunning = true;

        var generation = generationCounter++;
        var filePath = $"output/{generation}_out.wav";
        var filePathFull = Path.Join(backend, filePath);
        var filePathFullOnlyFolder = Path.GetDirectoryName(filePathFull);

        try
        {
            speechProcessing = true;
            Log($"Generating speech with speaker {speaker} and text {text}");
            if (!Directory.Exists(filePathFullOnlyFolder)) Directory.CreateDirectory(filePathFullOnlyFolder);

            while(generationProcessedCounter < generation)
            {
                await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }

            await input.WriteLineAsync(speaker + ":" + filePath + ":" + text);

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
            audioFile = await RuntimeAudioLoader.LoadFile(filePathFull);
            if (deleteAfterLoading) File.Delete(filePathFull);
        }
        catch (Exception exception)
        {
            Log("Exception while loading audio file: \n" + exception);
        }

        return audioFile;
    }

    public override void _Ready()
    {
        instance = this;
        tree = GetTree();
        StartServer();
    }

    private void SelectBackend(Architecture architecture)
    {
        backend = UNSUPPORTED_BACKEND;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && architecture == Architecture.Arm64)
        {
            Log("Detected MacOS on Arm64");
            backend = MACOS_ARM64_BACKEND;
            ttsPath = "env/bin/python";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && architecture == Architecture.X64)
        {
            Log("Detected Windows on X64");
            backend = WINDOWS_X64_BACKEND;
            ttsPath = "env/Scripts/python.exe";
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
        ttsProcess.StartInfo.Arguments = "main.py";
        ttsProcess.StartInfo.FileName = backend + ttsPath;

        if (backend == WINDOWS_X64_BACKEND)
        {
            ttsProcess.StartInfo.FileName = "cmd.exe";
        }

        ttsProcess.Start();
        input = ttsProcess.StandardInput;
        ttsProcess.BeginErrorReadLine();
        ttsProcess.BeginOutputReadLine();

        if (backend == WINDOWS_X64_BACKEND)
        {
            ttsProcess.StandardInput.WriteLine(".\\env\\Scripts\\Activate");
            ttsProcess.StandardInput.WriteLine(".\\env\\Scripts\\python.exe main.py");
            ttsProcess.StandardInput.Flush();
        }

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

    static string LogPrefix()
    {
        var time = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
        return $"[TTS][{time:hh\\:mm\\:ss\\:fff}] ";
    }
}
