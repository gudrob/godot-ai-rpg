using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Godot;
using Godot.Collections;

namespace AIRPG
{
    public partial class LLaMA2 : Node
    {
        private static readonly string[] defaultHeaders = new string[]
            {
                "Content-Type: application/json"
            };

        private static LLaMA2 Instance;

        private static Process LLaMAProcess;

        private string host;

        private int port;

        private string debugText = "";

        public static async Task<string> Prompt(Session session, string prompt, int predictTokens = 256, float repeatPenalty = 1f, float temperature = 0.5f)
        {
            var aiCharacterToken = $"{session.aiCharacterName}:";
            var playerCharacterToken = $"{session.playerCharacterName}:";

            if (session.client.GetStatus() != HttpClient.Status.Body && session.client.GetStatus() != HttpClient.Status.Connected)
            {
                throw new Exception("Session is not ready yet to be used. Please check if its status is either  HttpClient.Status.Body or HttpClient.Status.Connected");
            }

            if (!session.fullPrompt.ToString().EndsWith(playerCharacterToken))
            {
                session.fullPrompt.Append(playerCharacterToken);
            }

            session.fullPrompt.Append(prompt).Append(aiCharacterToken);

            var connection = session.client.Request(HttpClient.Method.Post, "/completion", defaultHeaders, "{"
            + $"\"temperature\": {temperature},"
            + $"\"repeat_penalty\": {repeatPenalty},"
            + $"\"n_predict\": {predictTokens},"
            + $"\"cache_promt\": true,"
            + $"\"prompt\": \"{HttpUtility.JavaScriptStringEncode(session.fullPrompt.ToString())}\","
            + $"\"slot_id\": -1,"
            + $"\"stream\": false,"
            + $"\"stop\": [\"</s>\", \"{aiCharacterToken}\", \"{playerCharacterToken}\"],"
            + "}");

            if (connection != Error.Ok)
            {
                throw new Exception("Error trying to prompt: " + connection);
            }

            while (session.client.GetStatus() == HttpClient.Status.Requesting)
            {
                session.client.Poll();
                await Instance.ToSignal(Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            GD.Print(session.client.GetStatus());

            if (session.client.GetStatus() == HttpClient.Status.Body)
            {
                var responseBytes = new byte[session.client.GetResponseBodyLength()];
                var index = 0;
                while (session.client.GetStatus() == HttpClient.Status.Body)
                {
                    var chunk = session.client.ReadResponseBodyChunk();
                    if (chunk.Length > 0)
                    {
                        Buffer.BlockCopy(chunk, 0, responseBytes, index, chunk.Length);
                        index += chunk.Length;
                    }
                    session.client.Poll();
                    await Instance.ToSignal(Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
                }

                var response = Encoding.UTF8.GetString(responseBytes, 0, responseBytes.Length);

                var responseDict = (Dictionary<string, Variant>)Json.ParseString(response);

                return responseDict["content"].ToString();
            }
            else
            {
                throw new Exception("Invalid session status: " + session.client.GetStatus());
            }
        }

        private async Task<HttpClient> Connect()
        {
            HttpClient client = new();

            var connection = client.ConnectToHost(host, port);

            if (connection != Error.Ok)
            {
                throw new Exception("There was an error connecting to the AI's endpoint: " + connection);
            }

            while (client.GetStatus() == HttpClient.Status.Connecting || client.GetStatus() == HttpClient.Status.Resolving)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                client.Poll();
            }

            if (client.GetStatus() != HttpClient.Status.Connected)
            {
                throw new Exception("There was an error connecting to the AI's endpoint: " + client.GetStatus());
            }

            return client;
        }

        public static async Task<Session> StartSession(string aiCharacterName, string playerCharacterName)
        {
            return new Session()
            {
                aiCharacterName = aiCharacterName,
                playerCharacterName = playerCharacterName,
                client = await Instance.Connect(),
                fullPrompt = new()
            };
        }

        public static void EndEssion(Session session)
        {
            if (IsInstanceValid(session.client))
            {
                session.client.Close();
                session.client.Free();
            }
        }

        public override void _Ready()
        {
            if (Instance != null) return;

            Instance = this;
        }

        public static void Initialize(int gpuLayers = 20, int cpuThreads = 2, int maximumSessions = 2, string host = "127.0.0.1", short port = 8080, int contextSize = 1024, int maxWaitTime = 600, bool allowMemoryMap = true, bool alwaysKeepInMemory = true)
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

            this.host = host;
            this.port = port;
        }

        private bool StartServer(string modelPath, string workingDirectory, int gpuLayers, int cpuThreads, int maximumSessions, string host, short port, int contextSize, int maxWaitTime, bool allowMemoryMapping, bool alwaysKeepInMemory)
        {
            var debugLabel = GetParent().FindChild("debug_label", true) as Label;

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
                debugText += LLaMAProcess.ExitCode + System.Environment.NewLine;
                debugLabel.SetDeferred("text", debugText);
            }

            void processErrorDataReceived(object sender, DataReceivedEventArgs e)
            {
                debugText += e.Data + System.Environment.NewLine;
                debugLabel.SetDeferred("text", debugText);
            }

            void processOutputDataReceived(object sender, DataReceivedEventArgs e)
            {
                debugText += e.Data + System.Environment.NewLine;
                debugLabel.SetDeferred("text", debugText);
            }

            return true;
        }
    }

    public class Session
    {
        public HttpClient client;
        public StringBuilder fullPrompt;
        public string aiCharacterName;
        public string playerCharacterName;
    }

}

