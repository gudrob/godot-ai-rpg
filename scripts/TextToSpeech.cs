using Godot;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;

public partial class TextToSpeech : Node
{
    static TextToSpeech instance;

    private Process ttsProcess;

    private int generationCounter = 0;

    private bool generationRunning = false;

    private bool speechProcessing = false;

    private StreamWriter input;

    private SceneTree tree;

    private string workingDirectory;

    private string ttsPath;

    public static async Task<AudioStreamWav> Generate(string speaker, string text)
    {
        return await instance._Generate(speaker, text);
    }

    private async Task<AudioStreamWav> _Generate(string speaker, string text)
    {
        while (generationRunning)
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        generationRunning = true;
        var generationId = ++generationCounter;
        var filePath = generationId+"_out.wav";

        try
        {
            speechProcessing = true;

            await input.WriteAsync(filePath+":"+speaker+":"+text);

            while (speechProcessing)
            {
                await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }

        }
        catch (Exception exception)
        {
            GD.Print("EXCEPTION WHILE GENERATING TTS: " + "\n" + exception);
        }
        finally
        {
            generationRunning = false;
        }

        filePath = Path.Join(workingDirectory, filePath);

        AudioStreamWav audioFile = null;

        try
        {
            audioFile = ResourceLoader.Load<AudioStreamWav>(filePath);

            File.Delete(filePath);
        }
        catch (Exception exception)
        {
            GD.Print("EXCEPTION WHILE LOADING AUDIO FILE: " + "\n" + exception);
        }

        return audioFile;
    }

    public override void _Ready()
    {
        instance = this;
        tree = GetTree();
    }

    private bool StartServer(string workingDirectory, string ttsPath)
    {
        GD.Print($"[TTS] Starting. Working directory: {workingDirectory}, tts path: {ttsPath}");

        ttsProcess = new();
        ttsProcess.StartInfo.ErrorDialog = false;
        ttsProcess.StartInfo.UseShellExecute = false;
        ttsProcess.EnableRaisingEvents = true;
        ttsProcess.StartInfo.CreateNoWindow = true;
        ttsProcess.StartInfo.RedirectStandardError = true;
        ttsProcess.StartInfo.RedirectStandardInput = true;
        ttsProcess.StartInfo.RedirectStandardOutput = true;
        ttsProcess.StartInfo.WorkingDirectory = workingDirectory;
        ttsProcess.StartInfo.FileName = ttsPath;
        ttsProcess.StartInfo.Arguments = "";
        ttsProcess.Exited += processExited;
        ttsProcess.ErrorDataReceived += processErrorDataReceived;
        ttsProcess.OutputDataReceived += processOutputDataReceived;
        input = ttsProcess.StandardInput;

        if (!ttsProcess.Start())
        {
            GD.Print("Could not start the TTS process.");
            return false;
        }

        ttsProcess.BeginErrorReadLine();
        ttsProcess.BeginOutputReadLine();

        void processExited(object sender, EventArgs e)
        {
            GD.Print("[TTS] Exited with code "+ttsProcess.ExitCode);

            StartServer(workingDirectory, ttsPath);
        }

        void processErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            GD.Print(e.Data);
        }

        void processOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            var data = e.Data ?? "";

            if (data.Contains("-GENERATION FINISHED-"))
            {
                speechProcessing = false;
            }
        }

        return true;
    }
}
