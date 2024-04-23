using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Godot;
using System.Text.Json;

namespace AIRPG
{
    public partial class LLaMA2 : Node
    {
        private static LLaMA2 Instance;

        private static Process LLaMAProcess;

        private string completionAddress;

        private HttpService httpService;

        [Export]
        public AudioStreamPlayer speechPlayer;

        const string UNSUPPORTED_BACKEND = "none";

        public static async Task Generate(Session session, string text, int predictTokens = 160, float repeatPenalty = 1.18f, float temperature = 0.7f)
        {
            Game.Instance.processingBaseText = "Preparing Prompt";
            Game.Instance.isProcessing = true;

            var aiCharacterToken = $"{session.aiCharacterName}:";
            var playerCharacterToken = $"{session.playerCharacterName}:";

            if (!session.fullPrompt.ToString().EndsWith("\n")) session.fullPrompt.Append("\n");

            session.fullPrompt
                .Append(playerCharacterToken)
                .Append(' ');

            session.fullPrompt
                .Append(text)
                .Append(System.Environment.NewLine)
                .Append(aiCharacterToken);

            var body = "{"
            + $"\"cache_prompt\": true,"
            + $"\"frequency_penalty\": 0,"
            + $"\"grammar\": \"\","
            + $"\"image_data\": [],"
            + $"\"min_p\": 0.05,"
            + $"\"mirostat\": 0,"
            + $"\"mirostat_eta\": 0.1,"
            + $"\"mirostat_tau\": 5,"
            + $"\"n_predict\": {predictTokens},"
            + $"\"n_probs\": 0,"
            + $"\"presence_penalty\": 0,"
            + $"\"prompt\": \"{HttpUtility.JavaScriptStringEncode(session.fullPrompt.ToString())}\","
            + $"\"repeat_last_n\": 256,"
            + $"\"repeat_penalty\": {repeatPenalty},"
            + $"\"slot_id\": -1,"
            + $"\"stop\": [\"</s>\", \"<|eot_id|>\", \"{aiCharacterToken}\", \"{playerCharacterToken}\"],"
            + $"\"stream\": true,"
            + $"\"temperature\": {temperature},"
            + $"\"tfs_z\": 1,"
            + $"\"top_k\": 5,"
            + $"\"top_p\": 0.5,"
            + $"\"typical_p\": 1"
            + "}";

            GD.Print(body);

            var sw = Stopwatch.StartNew();
            var firstByte = false;

            var response = await Instance.httpService.PostAsync(Instance.completionAddress, body, "application/json");

            byte[] buf = new byte[8192];
            int bytesRead;

            var stream = await response.Content.ReadAsStreamAsync();
            Game.Instance.processingBaseText = "Sending Prompt";
            Game.Instance.isProcessing = true;

            string tmpString = "";

            while ((bytesRead = await stream.ReadAsync(buf, 0, buf.Length)) > 0)
            {
                Game.Instance.processingBaseText = "Processing AI Response";
                Game.Instance.isProcessing = true;
                if (firstByte == false)
                {
                    firstByte = true;
                    Log("Time until first byte: " + sw.Elapsed.TotalMilliseconds + " ms");
                }
                string chunk = Encoding.UTF8.GetString(buf, 0, bytesRead);
                var chunkTrimmed = chunk.Trim()[5..].Replace("<|eot_id|>", ""); //LLaMA 3 token when it feels like it

                var chunks = chunkTrimmed.Split("data: ");

                foreach (var chunkToProcess in chunks)
                {
                    try
                    {
                        Log("Data received: " + chunkToProcess);
                        var responseDict = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(chunkTrimmed);
                        var res = responseDict["content"].ToString();
                        if (res.Length == 0) continue;

                        Instance.TrackLineForTTS(session, res);
                        session.fullPrompt.Append(res);
                    }
                    catch (Exception ex)
                    {
                        Log("Exception while parsing AI response: " + ex.ToString());
                    }
                    tmpString += chunk;
                }
            }
            Log("Total prompt time: " + sw.Elapsed.TotalMilliseconds + " ms");
            Game.Instance.processingBaseText = "AI Response finished";
            Game.Instance.isProcessing = false;
        }

        public static Session StartSession(string aiCharacterName, string playerCharacterName, string basePrompt)
        {
            return new Session()
            {
                aiCharacterName = aiCharacterName,
                playerCharacterName = playerCharacterName,
                fullPrompt = new(basePrompt)
            };
        }

        private static char[] sentenceTerminationTokens = new char[] { '!', '?', ':', '.' };

        private async void TrackLineForTTS(Session session, string chunk)
        {
            session.lastSentence.Append(chunk);

            if (chunk.IndexOfAny(sentenceTerminationTokens) != -1)
            {
                if (session.lastSentence.Length > 2 && speechPlayer != null)
                {
                    var output = await TextToSpeech.Generate(TextToSpeechSpeakers.Male, session.lastSentence.ToString());

                    while (speechPlayer.Playing)
                    {
                        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    }

                    speechPlayer.Stream = output;
                    speechPlayer.Play();
                }
                session.lastSentence.Clear();
            }
        }

        public override void _Ready()
        {
            if (Instance != null) return;

            Instance = this;

            httpService = new();

            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");

            foreach (var process in Process.GetProcessesByName("server"))
            {
                process.Kill();
            }
        }

        public static void Initialize(int gpuLayers = 33, int cpuThreads = 3, int maximumSessions = 3, string host = "127.0.0.1", short port = 8080, int contextSize = 2048, int maxWaitTime = 600, bool allowMemoryMapping = true, bool alwaysKeepInMemory = true)
        {
            Instance.StartServer(gpuLayers, cpuThreads, maximumSessions, host, port, contextSize, maxWaitTime, allowMemoryMapping, alwaysKeepInMemory);
        }

        private static string SelectBackend(Architecture architecture)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && architecture == Architecture.Arm64)
            {
                Log("Detected MacOS on Arm64");
                return "./llama/macos-arm64/";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && architecture == Architecture.X64)
            {
                Log("Detected Windows on X64");
                return "./llama/win-x64/";
            }

            return UNSUPPORTED_BACKEND;
        }

        private bool StartServer(int gpuLayers, int cpuThreads, int maximumSessions, string host, short port, int contextSize, int maxWaitTime, bool allowMemoryMapping, bool alwaysKeepInMemory)
        {
            completionAddress = $"http://{host}:{port}/completion";

            var architecture = RuntimeInformation.ProcessArchitecture;

            string modelPath = "./../../model/model.gguf";
            string backend = SelectBackend(architecture);

            if (backend == UNSUPPORTED_BACKEND)
            {
                Log($"Architecture {architecture} on this platform is currently unsupported");
                GetTree().Quit();
            }

            LLaMAProcess = new();
            LLaMAProcess.StartInfo.ErrorDialog = false;
            LLaMAProcess.StartInfo.UseShellExecute = false;
            LLaMAProcess.EnableRaisingEvents = true;
            LLaMAProcess.StartInfo.CreateNoWindow = true;
            LLaMAProcess.StartInfo.RedirectStandardError = true;
            LLaMAProcess.StartInfo.RedirectStandardInput = true;
            LLaMAProcess.StartInfo.RedirectStandardOutput = true;
            LLaMAProcess.StartInfo.WorkingDirectory = backend;
            LLaMAProcess.StartInfo.FileName = backend + "server";
            LLaMAProcess.StartInfo.Arguments = $"-m \"{modelPath}\" --log-format text --n-gpu-layers {gpuLayers} -t {cpuThreads} --host \"{host}\" --port {port} -c {contextSize} --timeout {maxWaitTime} {(allowMemoryMapping ? "" : "--no-mmap")} {(alwaysKeepInMemory ? "--mlock" : "")} --parallel {maximumSessions} ";
            LLaMAProcess.Exited += processExited;
            LLaMAProcess.ErrorDataReceived += processErrorDataReceived;
            LLaMAProcess.OutputDataReceived += processOutputDataReceived;

            if (!LLaMAProcess.Start())
            {
                Log("Could not start the AI process.");
                return false;
            }

            LLaMAProcess.BeginErrorReadLine();
            LLaMAProcess.BeginOutputReadLine();

            void processExited(object sender, EventArgs e)
            {
                Log("Exited with code " + LLaMAProcess.ExitCode);
            }

            void processErrorDataReceived(object sender, DataReceivedEventArgs e)
            {
                Log(e.Data);
            }

            void processOutputDataReceived(object sender, DataReceivedEventArgs e)
            {
                //GD.Print(e.Data);
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
            return $"[LLAMA][{time:hh\\:mm\\:ss\\:fff}] ";
        }

        public override void _ExitTree()
        {
            LLaMAProcess?.Kill();
        }
    }


    public class Session
    {
        public StringBuilder fullPrompt;
        public string aiCharacterName;
        public string playerCharacterName;
        public StringBuilder lastSentence = new();
    }

}
