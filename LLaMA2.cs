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

        public static async Task Prompt(Session session, string prompt, int predictTokens = 192, float repeatPenalty = 1.1764705882352942f, float temperature = 0.25f)
        {
            var aiCharacterToken = $"{session.aiCharacterName}:";
            var playerCharacterToken = $"{session.playerCharacterName}:";

            session.fullPrompt
                .Append(System.Environment.NewLine)
                .Append(playerCharacterToken)
                .Append(' ');

            session.fullPrompt
                .Append(prompt)
                .Append(System.Environment.NewLine)
                .Append(aiCharacterToken)
                .Append(' ');

            var body = "{"
            + $"\"temperature\": {temperature},"
            + $"\"repeat_penalty\": {repeatPenalty},"
            + $"\"n_predict\": {predictTokens},"
            + $"\"cache_promt\": true,"
            + $"\"prompt\": \"{HttpUtility.JavaScriptStringEncode(session.fullPrompt.ToString())}\","
            + $"\"slot_id\": -1,"
            + $"\"stream\": true,"
            + $"\"stop\": [\"</s>\", \"{aiCharacterToken}\", \"{playerCharacterToken}\"]"
            + "}";

            var response = await Instance.httpService.PostAsync(Instance.completionAddress, body, "application/json");

            StringBuilder sb = new();
            byte[] buf = new byte[8192];
            var resStream = await response.Content.ReadAsStreamAsync();
            string tmpString = null;
            int count;
            do
            {
                count = resStream.Read(buf, 0, buf.Length);
                if (count != 0)
                {
                    tmpString = Encoding.ASCII.GetString(buf, 0, count);
                    sb.Append(tmpString);
                }
            } while (count > 0);

            GD.Print(tmpString);

            var chunks = tmpString.Split("data: ");

            foreach (var chunk in chunks)
            {
                var chunkTrimmed = chunk.Trim();

                if (chunkTrimmed.Length == 0) continue;

                try
                {
                    var responseDict = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(chunkTrimmed);
                    var res = responseDict["content"].ToString();
                    if (res.Length > 0) session.fullPrompt.Append(res);
                }
                catch (Exception ex)
                {
                    GD.Print(ex);
                }
            }
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

        public static void Initialize(int gpuLayers = 33, int cpuThreads = 2, int maximumSessions = 2, string host = "127.0.0.1", short port = 8080, int contextSize = 1024, int maxWaitTime = 600, bool allowMemoryMap = true, bool alwaysKeepInMemory = true)
        {
            Instance.InitializeServer(gpuLayers, cpuThreads, maximumSessions, host, port, contextSize, maxWaitTime, allowMemoryMap, alwaysKeepInMemory);
        }

        private void InitializeServer(int gpuLayers, int cpuThreads, int maximumSessions, string host, short port, int contextSize, int maxWaitTime, bool allowMemoryMapping, bool alwaysKeepInMemory)
        {
            var architecture = RuntimeInformation.ProcessArchitecture;

            string modelPath = "./../model/model.gguf";
            string workingDirectory;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (architecture == Architecture.Arm64)
                {
                    GD.Print("Detected MacOS on Arm64");
                    workingDirectory = "./apple-silicon-llama/";
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
                if (architecture == Architecture.X64)
                {
                    GD.Print("Detected Windows on X64");
                    workingDirectory = "./win-x64-llama/";
                }
                else
                {
                    GD.Print("Windows on " + architecture.ToString() + " is currently unsupported");
                    GetTree().Quit();
                    return;
                }
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

            StartServer(modelPath, workingDirectory, gpuLayers, cpuThreads, maximumSessions, host, port, contextSize, maxWaitTime, allowMemoryMapping, alwaysKeepInMemory);

            completionAddress = $"http://{host}:{port}/completion";
        }

        private bool StartServer(string modelPath, string workingDirectory, int gpuLayers, int cpuThreads, int maximumSessions, string host, short port, int contextSize, int maxWaitTime, bool allowMemoryMapping, bool alwaysKeepInMemory)
        {
            LLaMAProcess = new();
            LLaMAProcess.StartInfo.ErrorDialog = false;
            LLaMAProcess.StartInfo.UseShellExecute = false;
            LLaMAProcess.EnableRaisingEvents = true;
            LLaMAProcess.StartInfo.CreateNoWindow = true;
            LLaMAProcess.StartInfo.RedirectStandardError = true;
            LLaMAProcess.StartInfo.RedirectStandardInput = true;
            LLaMAProcess.StartInfo.RedirectStandardOutput = true;
            LLaMAProcess.StartInfo.WorkingDirectory = workingDirectory;
            LLaMAProcess.StartInfo.FileName = workingDirectory + "server";
            LLaMAProcess.StartInfo.Arguments = $"-m \"{modelPath}\" --n-gpu-layers {gpuLayers} -t {cpuThreads} --host \"{host}\" --port {port} -c {contextSize} --timeout {maxWaitTime} {(allowMemoryMapping ? "" : "--no-mmap")} {(alwaysKeepInMemory ? "--mlock" : "")} --parallel {maximumSessions} ";
            LLaMAProcess.Exited += processExited;
            LLaMAProcess.ErrorDataReceived += processErrorDataReceived;
            LLaMAProcess.OutputDataReceived += processOutputDataReceived;

            if (!LLaMAProcess.Start())
            {
                GD.Print("Could not start the AI process.");
                return false;
            }

            LLaMAProcess.BeginErrorReadLine();
            LLaMAProcess.BeginOutputReadLine();

            void processExited(object sender, EventArgs e)
            {
                GD.PrintRaw(LLaMAProcess.ExitCode + System.Environment.NewLine);
            }

            void processErrorDataReceived(object sender, DataReceivedEventArgs e)
            {
                GD.Print(e.Data);
            }

            void processOutputDataReceived(object sender, DataReceivedEventArgs e)
            {
                GD.Print(e.Data);
            }

            return true;
        }
    }

    public class Session
    {
        public StringBuilder fullPrompt;
        public string aiCharacterName;
        public string playerCharacterName;
    }

}
