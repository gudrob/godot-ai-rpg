using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Godot;

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

        public static async Task<string> ProcessRequest(HttpClient session, string prompt, string systemPrompt = "", int predictTokens = 256, float repeatPenalty = 1f, float temperature = 0.5f)
        {
            if (session.GetStatus() != HttpClient.Status.Body || session.GetStatus() != HttpClient.Status.Connected)
            {
                throw new Exception("Session is not ready yet to be used. Please check if its status is either  HttpClient.Status.Body or HttpClient.Status.Connected");
            }

            var connection = session.Request(HttpClient.Method.Post, "/completion", defaultHeaders, "{"
            + $"temperature: {temperature},"
            + $"repeat_penalty: {repeatPenalty},"
            + $"n_predict: {predictTokens},"
            + $"cache_promt: {true},"
            + (systemPrompt == "" ? "" : $"system_prompt: \"{systemPrompt}\",")
            + $"prompt: \"{prompt}\""
            + $"stop: [\"</s>\", \"</s>\", \"</s>\"]"
            + "}");

            if (connection != Error.Ok)
            {
                throw new Exception("Error trying to prompt: " + connection);
            }

            while (session.GetStatus() == HttpClient.Status.Requesting)
            {
                await Instance.ToSignal(Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
                session.Poll();
            }

            if (session.GetStatus() == HttpClient.Status.Body)
            {
                var responseBytes = new byte[session.GetResponseBodyLength()];
                var index = 0;
                while (session.GetStatus() == HttpClient.Status.Body)
                {
                    var chunk = session.ReadResponseBodyChunk();
                    if (chunk.Length > 0)
                    {
                        Buffer.BlockCopy(chunk, 0, responseBytes, index, chunk.Length);
                        index += chunk.Length;
                    }
                    session.Poll();
                }
                return Encoding.UTF8.GetString(responseBytes, 0, responseBytes.Length);
            }
            else
            {
                throw new Exception("Invalid session status: " + session.GetStatus());
            }
        }

        private async Task<HttpClient> Connect(string host, int port)
        {
            HttpClient client = new();

            var connection = client.ConnectToHost(host, port);

            if (connection != Error.Ok)
            {
                throw new Exception("There was an error connecting to the AI's endpoint: " + connection);
            }

            while (client.GetStatus() == HttpClient.Status.Connecting || client.GetStatus() == HttpClient.Status.Resolving)
            {
                await Instance.ToSignal(Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
                client.Poll();
            }

            if (client.GetStatus() != HttpClient.Status.Connected)
            {
                throw new Exception("There was an error connecting to the AI's endpoint: " + client.GetStatus());
            }

            return client;
        }

        public static async Task<HttpClient> StartSession()
        {
            return await Instance.Connect(Instance.host, Instance.port);
        }

        public static void EndEssion(HttpClient client)
        {
            if (IsInstanceValid(client))
            {
                client.Close();
                client.Free();
            }
        }

        public override void _Ready()
        {
            if (Instance != null) return;

            Instance = this;
        }

        public static void Initialize(int gpuLayers = 20, int cpuThreads = 2, int maximumSessions = 2, string host = "127.0.0.1", short port = 8080, int contextSize = 2048, int maxWaitTime = 600, bool allowMemoryMap = true, bool alwaysKeepInMemory = true)
        {
            Instance.InitializeServer(gpuLayers, cpuThreads, maximumSessions, host, port, contextSize, maxWaitTime, allowMemoryMap, alwaysKeepInMemory);
        }

        private void InitializeServer(int gpuLayers, int cpuThreads, int maximumSessions, string host, short port, int contextSize, int maxWaitTime, bool allowMemoryMapping, bool alwaysKeepInMemory)
        {
            var architecture = RuntimeInformation.ProcessArchitecture;

            string modelPath;
            string workingDirectory;
            string fileName;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (architecture == Architecture.Arm64)
                {
                    GD.Print("Detected MacOS on Arm64");

                    modelPath = "./../model/model.gguf";
                    workingDirectory = "./apple-silicon-llama/";
                    fileName = "./apple-silicon-llama/server";
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
                GD.Print("Windows is currently unsupported");
                GetTree().Quit();
                return;
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

            StartServer(modelPath, workingDirectory, fileName, gpuLayers, cpuThreads, maximumSessions, host, port, contextSize, maxWaitTime, allowMemoryMapping, alwaysKeepInMemory);

        }

        private bool StartServer(string modelPath, string workingDirectory, string fileName, int gpuLayers, int cpuThreads, int maximumSessions, string host, short port, int contextSize, int maxWaitTime, bool allowMemoryMapping, bool alwaysKeepInMemory)
        {
            var debugLabel = FindChild("Label") as Label;

            LLaMAProcess = new();
            LLaMAProcess.StartInfo.ErrorDialog = false;
            LLaMAProcess.StartInfo.UseShellExecute = false;
            LLaMAProcess.EnableRaisingEvents = true;
            LLaMAProcess.StartInfo.CreateNoWindow = true;
            LLaMAProcess.StartInfo.RedirectStandardError = true;
            LLaMAProcess.StartInfo.RedirectStandardInput = true;
            LLaMAProcess.StartInfo.RedirectStandardOutput = true;
            LLaMAProcess.StartInfo.WorkingDirectory = workingDirectory;
            LLaMAProcess.StartInfo.FileName = fileName;
            LLaMAProcess.StartInfo.Arguments = $"-m \"{modelPath}\" --n-gpu-layers={gpuLayers} -t {cpuThreads}  --host \"{host}\" -port {port} -c {contextSize} --timeout {maxWaitTime} {(allowMemoryMapping ? "--no-mmap" : "")} {(alwaysKeepInMemory ? "--mlock" : "")} --parallel {maximumSessions} ";
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
        public string characterName;  
    }

}

