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

        const string UNSUPPORTED_BACKEND = "none";

        public static async Task Generate(Session session, string text, int predictTokens = 192, float repeatPenalty = 1.5f, float temperature = 0.2f)
        {
            Game.Instance.processingBaseText = "Preparing Prompt";
            Game.Instance.isProcessing = true;

            var aiCharacterToken = $"{session.aiCharacterName}:";
            var playerCharacterToken = $"{session.playerCharacterName}:";

            session.fullPrompt
                .Append(System.Environment.NewLine)
                .Append(playerCharacterToken)
                .Append(' ');

            session.fullPrompt
                .Append(text)
                .Append(System.Environment.NewLine)
                .Append(aiCharacterToken)
                .Append(' ');

            var body = "{"
            + $"\"temperature\": {temperature},"
            + $"\"repeat_penalty\": {repeatPenalty},"
            + $"\"n_predict\": {predictTokens},"
            + $"\"cache_prompt\": true,"
            + $"\"prompt\": \"{HttpUtility.JavaScriptStringEncode(session.fullPrompt.ToString())}\","
            + $"\"slot_id\": -1,"
            + $"\"stream\": true,"
            + $"\"stop\": [\"</s>\", \"{aiCharacterToken}\", \"{playerCharacterToken}\"]"
            + "}";

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
                var chunkTrimmed = chunk.Trim()[5..];
                if (chunkTrimmed.Length == 0) continue;
                try
                {
                    var responseDict = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(chunkTrimmed);
                    var res = responseDict["content"].ToString();
                    if (res.Length > 0) session.fullPrompt.Append(res);
                }
                catch (Exception ex)
                {
                    Log("Exception while parsing AI response: " + ex.ToString());
                }
                tmpString += chunk;
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
    }

    public class Session
    {
        public StringBuilder fullPrompt;
        public string aiCharacterName;
        public string playerCharacterName;
    }

}
