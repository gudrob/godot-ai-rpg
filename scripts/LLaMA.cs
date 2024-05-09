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
using System.Text.RegularExpressions;

namespace AIRPG
{
    public partial class LLaMA : Node
    {
        private static LLaMA Instance;

        private static Process LLaMAProcess;

        private string completionAddress;

        private HttpService httpService;

        [Export]
        public NPC character;

        const string UNSUPPORTED_BACKEND = "none";

        LLaMAVersion version = LLaMAVersion.Version2;


        public static async Task Generate(Session session, string text, int predictTokens = 160, float repeatPenalty = 1.18f, float temperature = 0.6f)
        {
            Game.SetProcessingInfo("Preparing Prompt");

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

            string body;

            if (Instance.version == LLaMAVersion.Version2)
            {
                body = "{"
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
                + $"\"stop\": [\"</s>\", \"{aiCharacterToken}\", \"{playerCharacterToken}\"],"
                + $"\"stream\": true,"
                + $"\"temperature\": {temperature},"
                + $"\"tfs_z\": 1,"
                + $"\"top_k\": 15,"
                + $"\"top_p\": 0.5,"
                + $"\"typical_p\": 1"
                + "}";
            }
            else
            {
                body = "{"
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
                + $"\"stop\": [\"<|start_header_id|>\", \"<|end_header_id|>\", \"<|eot_id|>\", \"{aiCharacterToken}\", \"{playerCharacterToken}\"],"
                + $"\"stream\": true,"
                + $"\"temperature\": {temperature},"
                + $"\"tfs_z\": 1,"
                + $"\"top_k\": 15,"
                + $"\"top_p\": 0.5,"
                + $"\"typical_p\": 1"
                + "}";
            }

            GD.Print(body);

            var sw = Stopwatch.StartNew();
            var firstByte = false;

            var response = await Instance.httpService.PostAsync(Instance.completionAddress, body, "application/json");

            byte[] buf = new byte[32768];
            int bytesRead;

            var stream = await response.Content.ReadAsStreamAsync();

            Game.SetProcessingInfo("Sending Prompt");

            string tmpString = "";

            while ((bytesRead = await stream.ReadAsync(buf, 0, buf.Length)) > 0)
            {
                Game.SetProcessingInfo("Processing AI Response");

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
                        if (chunkToProcess.Contains("{\"content\":\"\",\"id_slot\"") && chunkToProcess.Contains("\"stop\":true,\"model\":\""))
                        {
                            Log("End of data");
                            goto SkipDataParsing;
                        }
                        Log("Data received: " + chunkToProcess);
                        var responseDict = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(chunkTrimmed);
                        var res = responseDict["content"].ToString();
                        if (res.Length == 0) continue;

                        Instance.TrackLineForTTS(session, res);
                    }
                    catch (Exception ex)
                    {
                        Log("Exception while parsing AI response: " + ex.ToString());
                    }
                    tmpString += chunk;
                }
            }

        SkipDataParsing:
            Instance.TrackLineForTTS(session, "", true);
            Log("Total prompt time: " + sw.Elapsed.TotalMilliseconds + " ms");
            Game.SetProcessingInfo("AI Response finished", false);
        }

        public static Session StartSession(string aiCharacterName, string playerCharacterName, string basePrompt)
        {
            if (Instance.version == LLaMAVersion.Version2)
            {
                return new Session()
                {
                    aiCharacterName = aiCharacterName,
                    playerCharacterName = playerCharacterName,
                    fullPrompt = new(basePrompt)
                };
            }
            else
            {
                return new Session()
                {
                    aiCharacterName = aiCharacterName,
                    playerCharacterName = playerCharacterName,
                    fullPrompt = new(basePrompt)
                };
            }
        }

        private async void AddToSessionPromptDelayed(Session session, string text)
        {
            await ToSignal(GetTree().CreateTimer(0.333), SceneTreeTimer.SignalName.Timeout);
            session.fullPrompt.Append(text);
        }

        private static readonly char[] sentenceTerminationTokens = new char[] { '!', '?', ':', '.', ',' };


        private int speechIndex = 0;
        private int speechCounterIndex = 0;

        private async void TrackLineForTTS(Session session, string chunk, bool hasEnded = false)
        {
            chunk = Regex.Replace(chunk, @"[^\u0000-\u007F]+", string.Empty);
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

                        var output = await TextToSpeech.Generate(TextToSpeechSpeakers.Female, text);

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

            httpService = new();

            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-US");

            foreach (var process in Process.GetProcessesByName("server"))
            {
                process.Kill();
            }

        }


        public static void Initialize(int gpuLayers = 23, int cpuThreads = 2, int maximumSessions = 2, string host = "127.0.0.1", short port = 8080, int contextSize = 2048, int maxWaitTime = 600, bool allowMemoryMapping = true, bool alwaysKeepInMemory = false, LLaMAVersion version = LLaMAVersion.Version2)
        {
            Instance.StartServer(gpuLayers, cpuThreads, maximumSessions, host, port, contextSize, maxWaitTime, allowMemoryMapping, alwaysKeepInMemory, version);
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

        private bool StartServer(int gpuLayers, int cpuThreads, int maximumSessions, string host, short port, int contextSize, int maxWaitTime, bool allowMemoryMapping, bool alwaysKeepInMemory, LLaMAVersion version)
        {
            this.version = version;

            completionAddress = $"http://{host}:{port}/completion";

            var architecture = RuntimeInformation.ProcessArchitecture;

            string modelPath = "./../../llama/model/WizardLM2-7B-High.gguf";
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

        private static DateTime startTime = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        static string LogPrefix()
        {
            var time = DateTime.UtcNow - startTime;
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
        public bool parsingNumber = false;
        public bool parsingNumberDecimalDetected = false;
    }

}
