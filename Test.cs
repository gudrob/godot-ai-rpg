using System;
using System.Diagnostics;
using Godot;

public partial class Test : Node
{
	public string prompt = null;

	public string text = "";


	public async override void _Ready()
	{
		string modelPath = "/Users/-/godot-ai-rpg/model/model.gguf"; // change it to your own model path
		prompt = $"Loading model ${modelPath}";

		var label = FindChild("Label", true) as Label;

		// Start the child process.
		Process process = new();
		// Redirect the output stream of the child process.
		process.StartInfo.ErrorDialog = false;
		process.StartInfo.UseShellExecute = false;
		process.EnableRaisingEvents = true;
		process.StartInfo.CreateNoWindow = true;
		process.StartInfo.RedirectStandardError = true;
		process.StartInfo.RedirectStandardInput = true;
		process.StartInfo.RedirectStandardOutput = true;
		process.StartInfo.WorkingDirectory = "/Users/-/godot-ai-rpg/apple-silicon-llama/";
		process.StartInfo.FileName = "/Users/-/godot-ai-rpg/apple-silicon-llama/main";
		process.StartInfo.Arguments = $"-m \"{modelPath}\"";
		process.OutputDataReceived += process_OutputDataReceived;
		process.ErrorDataReceived += process_ErrorDataReceived;
		process.Exited += process_Exited;
		GD.Print("AI Process Start Result: " + process.Start());

		process.BeginErrorReadLine();
		process.BeginOutputReadLine();

		void process_Exited(object sender, EventArgs e)
		{
			text += process.ExitCode + System.Environment.NewLine;
			label.SetDeferred("text", text);
		}

		void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
		{
			text += e.Data + System.Environment.NewLine;
			label.SetDeferred("text", text);
		}

		void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
		{
			text += e.Data + System.Environment.NewLine;
			label.SetDeferred("text", text);
		}
	}
}
