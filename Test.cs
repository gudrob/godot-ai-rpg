using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Godot;

public partial class Test : Node
{
    public string prompt = null;

    public string text = "";

    public override void _Ready()
    {
        var architecture = RuntimeInformation.ProcessArchitecture;

        string modelPath;
        string workingDirectory;
        string fileName;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (architecture == Architecture.Arm64)
            {
                GD.Print("Detected MacOS on Arm64");

                modelPath = "./model/model.gguf";
                workingDirectory = "./apple-silicon-llama/";
                fileName = "./apple-silicon-llama/server";
            }
            else
            {
                GD.Print("MacOS on " + architecture.ToString() + " is currently unsupported");
                GetTree().Quit();
                return;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            GD.Print("Windows is currently unsupported");
            GetTree().Quit();
            return;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            GD.Print("Linux is currently unsupported");
            GetTree().Quit();
            return;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            GD.Print("FreeBSD is currently unsupported");
            GetTree().Quit();
            return;
        }
        else
        {
            GD.Print("Your Operating System is unsupported");
            GetTree().Quit();
            return;
        }

        StartServer(modelPath, workingDirectory, fileName);
    }

    public bool StartServer(string modelPath, string workingDirectory, string fileName)
    {
        var debugLabel = FindChild("Label") as Label;

        Process process = new();
        process.StartInfo.ErrorDialog = false;
        process.StartInfo.UseShellExecute = false;
        process.EnableRaisingEvents = true;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.WorkingDirectory = workingDirectory;
        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = $"-m \"{modelPath}\"";
        process.Exited += processExited;
        process.ErrorDataReceived += processErrorDataReceived;
        process.OutputDataReceived += processOutputDataReceived;

        if (!process.Start())
        {
            GD.Print("Could not start the AI process.");
            return false;
        }

        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        void processExited(object sender, EventArgs e)
        {
            text += process.ExitCode + System.Environment.NewLine;
            debugLabel.SetDeferred("text", text);
        }

        void processErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            text += e.Data + System.Environment.NewLine;
            debugLabel.SetDeferred("text", text);
        }

        void processOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            text += e.Data + System.Environment.NewLine;
            debugLabel.SetDeferred("text", text);
        }

        return true;
    }
}
