using LLama.Common;
using LLama;
using System.Collections.Generic;
using Godot;

public partial class Test : Node
{
	public string prompt = null;


	public async override void _Ready()
	{
		string modelPath = "/Users/robertgudat/godot-ai-rpg/model/model.gguf"; // change it to your own model path
		prompt = "Transcript of a dialog, where A interacts with an Assissent named B. B answers immediately and precise. A: Hello B. What is 10 * 10? B: 100. A: What is 10 * 3? B: 30. A: ";

		// Load model
		var parameters = new ModelParams(modelPath)
		{
			ContextSize = 2048,
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

				var texts = session.ChatAsync(prompt, new InferenceParams() { Temperature = 0.6f, AntiPrompts = new List<string> { "A:" } });

				GD.Print(prompt);

				var answer = "";

				await foreach (var text in texts)
				{
					answer += text;
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				}

				GD.Print(answer);
				GD.Print("Time taken (ms): "+sw.Elapsed.TotalMilliseconds);

				prompt = "What is 10 * 10?";
			}
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}
}
