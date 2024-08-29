using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using System.Text.RegularExpressions;

namespace AIRPG
{
	public partial class LLaMA : Node
	{

#if GODOT_MACOS
		[LibraryImport("llama-server", SetLastError = true)]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		private static partial IntPtr main(int argc, IntPtr argv);

		[LibraryImport("llama-server", SetLastError = true)]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static partial bool LLAMA_IsActive(UInt32 slotIndex);

		[LibraryImport("llama-server", SetLastError = true)]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static partial void LLAMA_Terminate();

		[LibraryImport("llama-server", SetLastError = true)]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static partial bool LLAMA_Initialized();

		[LibraryImport("llama-server", SetLastError = true)]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		private static partial void LLAMA_DiscardData(IntPtr dataPointer);

		[LibraryImport("llama-server", SetLastError = true)]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		private static partial int LLAMA_CheckLoops();

		[LibraryImport("llama-server", SetLastError = true)]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		private static partial IntPtr LLAMA_GetData(UInt32 slotIndex, out int dataLength);

		[LibraryImport("llama-server", SetLastError = true, StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
		[UnmanagedCallConv(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvCdecl) })]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static partial bool LLAMA_Prompt(UInt32 slotIndex, string text);
#endif

		private static LLaMA Instance;

		[Export]
		public NPC character;

		[Export]
		public LineEdit speakerInput;

		private static int seed = (int)(GD.Randi() / 2);

		public void LoadLLaMALibrary()
		{
			NativeLibrary.Load("./llama-server");
		}

		public async Task Generate(Session session, string text, int predictTokens = 256, float repeatPenalty = 1.2f, float temperature = 1f)
		{
			Game.SetProcessingInfo("Preparing Prompt");

			if (!session.fullPrompt.ToString().EndsWith("\n")) session.fullPrompt.Append("\n");

			session.fullPrompt.Append("<|start_header_id|>user<|end_header_id|>\n" + text + "<|eot_id|>\n<|start_header_id|>assistant<|end_header_id|>\n");

			Game.SetProcessingInfo("Sending Prompt");

			GD.Print(LLAMA_Prompt(0, @"{
            ""cache_prompt"": true,
            ""n_predict"": 256,
            ""prompt"": """ + System.Web.HttpUtility.JavaScriptStringEncode(session.fullPrompt.ToString()) + @""",
			""repeat_last_n"": 256,
            ""repeat_penalty"": 1.14,
			""seed"": " + seed + @",
            ""stop"": [""<|eot_id|>""],
            ""stream"": true,
            ""temperature"": 1.2,
			""top_k"": 50
			}"));

			var sw = Stopwatch.StartNew();

			bool firstByte = false;

			Game.SetProcessingInfo("Tokenizing Prompt");

			while (LLAMA_IsActive(0))
			{
				var data = LLAMA_GetData(0, out int dataLength);

				if (dataLength == 0)
				{
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
					continue;
				}

				Game.SetProcessingInfo("Processing AI Response");

				if (firstByte == false)
				{
					firstByte = true;
					Log("Time until first byte: " + sw.Elapsed.TotalMilliseconds + " ms");
				}

				byte[] byteData = new byte[dataLength];
				Marshal.Copy(data, byteData, 0, dataLength);
				LLAMA_DiscardData(data);
				if (byteData.Length > 0)
				{
					var str = Encoding.UTF8.GetString(byteData);
					Instance.TrackLineForTTS(session, str);
				}
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			}

			Instance.TrackLineForTTS(session, "", true);
			Log("Total prompt time: " + sw.Elapsed.TotalMilliseconds + " ms");
			Game.SetProcessingInfo("AI Response finished", false);
		}

		public Session StartSession(string basePrompt)
		{
			return new Session()
			{
				fullPrompt = new("<|begin_of_text|><|start_header_id|>system<|end_header_id|>\n" + basePrompt + "<|eot_id|>")
			};
		}

		int delayPos = 0;
		int delayPosProcessed = 0;

		private async void AddToSessionPromptDelayed(Session session, string text)
		{
			var pos = delayPos++;
			var tree = GetTree();
			try
			{
				await ToSignal(tree.CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);

				while (delayPosProcessed != pos)
				{
					await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
				}

				foreach (var t in text)
				{
					session.fullPrompt.Append(t);
					await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
				}
			}
			finally
			{
				delayPosProcessed++;
			}
		}

		private static readonly char[] sentenceTerminationTokens = ['!', '?', ':', '.', ',', ';'];


		private int speechIndex = 0;
		private int speechCounterIndex = 0;

		private async void TrackLineForTTS(Session session, string chunk, bool hasEnded = false)
		{
			chunk = Regex.Replace(chunk, @"[^\u0000-\u007F]+", string.Empty);
			Log("Chunk received: " + chunk);
			AddToSessionPromptDelayed(session, chunk);
			bool chunkAdded = false;

			//Need to check for Numbers
			if (session.parsingNumber)
			{
				if (session.parsingNumberDecimalDetected)
				{
					if (chunk == ".")
					{
						//Second decimal point. It is probably a ellipsis or something, so we generate speech.
						hasEnded = true;
					}
					else if (!decimal.TryParse(chunk, out _))
					{
						session.parsingNumberDecimalDetected = false;
						session.parsingNumber = false;
						hasEnded = true;
					}
					else if (!hasEnded)
					{
						session.lastSentence.Append(chunk);
						return;
					}
				}
				else
				{
					if (chunk == ".")
					{
						session.parsingNumberDecimalDetected = true;
						chunkAdded = true;
						session.lastSentence.Append(chunk);
						if (!hasEnded)
						{
							return;
						}
					}
					else if (decimal.TryParse(chunk, out _))
					{
						chunkAdded = true;
						session.lastSentence.Append(chunk);
						if (!hasEnded)
						{
							return;
						}
					}
					else
					{
						session.parsingNumberDecimalDetected = false;
						session.parsingNumber = false;
					}
				}
			}
			else if (decimal.TryParse(chunk, out _))
			{
				session.parsingNumber = true;
				chunkAdded = true;
				session.lastSentence.Append(chunk);
				if (!hasEnded)
				{
					return;
				}
			}
			else
			{
				chunkAdded = true;
				session.lastSentence.Append(chunk);
			}


			if (chunk.IndexOfAny(sentenceTerminationTokens) != -1 || hasEnded)
			{
				if (session.lastSentence.Length > 2)
				{
					try
					{
						session.parsingNumber = false;
						session.parsingNumberDecimalDetected = false;
						var thisSpeechIndex = speechIndex++;
						var text = session.lastSentence.ToString().Trim();
						if (text.Length == 0)
						{
							if (!chunkAdded)
							{
								session.lastSentence.Append(chunk);
							}
							return;
						}
						session.lastSentence.Clear();

						if (!chunkAdded)
						{
							chunkAdded = true;
							session.lastSentence.Append(chunk);
						}

						var output = await TextToSpeech.Generate(speakerInput.Text, text);

						while (character.IsSpeaking() || speechCounterIndex < thisSpeechIndex)
						{
							await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
						}

						character?.Speak(output, true);
					}
					finally
					{
						speechCounterIndex++;
					}
				}
				else
				{
					session.lastSentence.Clear();
				}
			}

			if (!chunkAdded)
			{
				session.lastSentence.Append(chunk);
			}
		}

		public override void _Ready()
		{
			if (Instance != null) return;

			Instance = this;

			Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");
		}

		public void StartLLaMA(int gpuLayers, int cpuThreads, int maximumSessions, int contextSize)
		{
			ThreadPool.QueueUserWorkItem((_) =>
			{
				try
				{
					string[] args = ["server", "--model", ProjectSettings.GlobalizePath("res://model.gguf"), "--n-gpu-layers", gpuLayers.ToString(), "--threads", cpuThreads.ToString(), "--flash-attn", "-ctk", "q8_0", "-ctv", "q8_0", "-c", contextSize.ToString(), "--parallel", maximumSessions.ToString()];

					int argc = args.Length;
					var argv = new IntPtr[argc];
					for (int i = 0; i < argc; i++)
					{
						argv[i] = Marshal.StringToHGlobalAnsi(args[i]);
					}

					IntPtr argvPtr = Marshal.AllocHGlobal(IntPtr.Size * argc);
					Marshal.Copy(argv, 0, argvPtr, argc);

					var a = main(argc, argvPtr);

					Log("LLAMA Exit Code: " + a);
				}
				catch (Exception e)
				{
					GD.PrintErr(e);
				}
			});
		}

		static void Log(string info)
		{
			GD.Print(LogPrefix() + info);
		}

		private static DateTime startTime = Process.GetCurrentProcess().StartTime.ToUniversalTime();
		static string LogPrefix()
		{
			var time = DateTime.UtcNow - startTime;
			return $"[LLAMA][{time:hh\\:mm\\:ss\\:fff}] ";
		}
	}


	public class Session
	{
		public StringBuilder fullPrompt;
		public StringBuilder lastSentence = new();
		public bool parsingNumber = false;
		public bool parsingNumberDecimalDetected = false;
	}

}
