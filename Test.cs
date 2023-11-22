using LLama.Common;
using LLama;
using System.Collections.Generic;
using Godot;

public partial class Test : Node
{
	public string prompt = null;


	public async override void _Ready()
	{
		string modelPath = "model"; // change it to your own model path
		prompt = "<s>[INST] <<SYS>>You are a digital assistant. You answer immediately, precise and as short as possible <</SYS>> What is 10 times 10? [/INST] ";

		// Load model
		var parameters = new ModelParams(modelPath)
		{
			ContextSize = 8192,
			Threads = 1,
		};
		using var model = LLamaWeights.LoadFromFile(parameters);

		// Initialize a chat session
		using var context = model.CreateContext(parameters);
		var ex = new InteractiveExecutor(context);
		ChatSession session = new ChatSession(ex);

		// run the inference in a loop to chat with LLM
		while (true)
		{
			if(prompt != null)
			{
				var sw = System.Diagnostics.Stopwatch.StartNew();

				var texts = session.ChatAsync(prompt, new InferenceParams() { Temperature = 0.1f, AntiPrompts = new List<string> { "[END]" } });

				GD.Print(prompt);

				var answer = "";

				await foreach (var text in texts)
				{
					answer += text;
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				}

				GD.Print(answer);
				GD.Print("Time taken (ms): "+sw.Elapsed.TotalMilliseconds);

				prompt = "</s><s>[INST] What is 10 times 10 ? [/INST]";
			}
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}
}
