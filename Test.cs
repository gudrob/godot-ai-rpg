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
		prompt = @"<s>[INST] <<SYS>>
You voice video game characters.
Information about the character you voice will be provided between the tags <CHAR> and </CHAR>.
The character will be told something between the tags <INPUT> and </INPUT>.
You will write what the character says between the tags <OUTPUT> and </OUTPUT>
and the emotion the character feels between the tags <EMO> and </EMO>.
Possible emotions are happy, angry, sad, neutral and surprised.
Example output: <OUTPUT> Hello, how are you? </OUTPUT> <EMO> happy </EMO>.
<</SYS>>

<CHAR> You are a humble blacksmith from a village close to a river. You are 25 years old. </CHAR> <INPUT> What is 10 times 10? </INPUT> [/INST] ";

		// Load model
		var parameters = new ModelParams(modelPath)
		{
			ContextSize = 4096,
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

				var texts = session.ChatAsync(prompt, new InferenceParams() { Temperature = 0.5f, AntiPrompts = new List<string> { "[END]" } });

				GD.Print(prompt);

				var answer = "";

				await foreach (var text in texts)
				{
					answer += text;
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
				}

				GD.Print(answer);
				GD.Print("Time taken (ms): "+sw.Elapsed.TotalMilliseconds);

				prompt = "</s><s>[INST] <INPUT> Have you heard any rumors? </INPUT> [/INST]";
			}
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}
}
