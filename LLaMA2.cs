using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Godot;

namespace AIRPG
{
    public partial class LLaMA2 : Node
    {
        private static string[] defaultHeaders = new string[]
            {
                "Content-Type: application/json"
            };

        private static LLaMA2 Instance;

        public static Process aiProcess { private set; get; }

        private string host;

        private int port;

        private string debugText = "";

        public static async Task<string> ProcessRequest()
        {
            

            
        }

        private async Task<HttpClient> Connect(string host, int port)
        {
            HttpClient client = new();

            var connection = client.ConnectToHost(host, port);

            if (connection != Error.Ok)
            {
                throw new Exception("There was an error connecting to the AI's endpoint: "+ connection);
            }

            while (client.GetStatus() == HttpClient.Status.Connecting || client.GetStatus() == HttpClient.Status.Resolving)
            {
                await Instance.ToSignal(Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
                client.Poll();
            }

            if(client.GetStatus() != HttpClient.Status.Connected)
            {
                throw new Exception("There was an error connecting to the AI's endpoint: " + client.GetStatus());
            }

            return client;
        }

        private async Task<string> _ProcessRequest()
        {
            using HttpClient client = await Connect(host, port);


        }

        public override void _Ready()
        {
            if (Instance != null)
            {
                return;
            }

            Instance = this;
        }

        public static void Initialize(int gpuLayers = 20, int cpuThreads = 2, int maxParallelRequests = 2, string host = "127.0.0.1", short port = 8080, int contextSize = 2048, int maxWaitTime = 600, bool allowMemoryMap = true, bool alwaysKeepInMemory = true)
        {
            Instance.InitializeServer(gpuLayers, cpuThreads, maxParallelRequests, host, port, contextSize, maxWaitTime, allowMemoryMap, alwaysKeepInMemory);
        }

        private void InitializeServer(int gpuLayers, int cpuThreads, int maxParallelRequests, string host, short port, int contextSize, int maxWaitTime, bool allowMemoryMapping, bool alwaysKeepInMemory)
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

            StartServer(modelPath, workingDirectory, fileName, gpuLayers, cpuThreads, maxParallelRequests, host, port, contextSize, maxWaitTime, allowMemoryMapping, alwaysKeepInMemory);

        }

        private bool StartServer(string modelPath, string workingDirectory, string fileName, int gpuLayers, int cpuThreads, int maxParallelRequests, string host, short port, int contextSize, int maxWaitTime, bool allowMemoryMapping, bool alwaysKeepInMemory)
        {
            var debugLabel = FindChild("Label") as Label;

            Process process = new();
            process.StartInfo.ErrorDialog = false;
            process.StartInfo.UseShellExecute = false;
            process.EnableRaisingEvents = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.WorkingDirectory = workingDirectory;
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = $"-m \"{modelPath}\" --n-gpu-layers={gpuLayers} -t {cpuThreads}  --host \"{host}\" -port {port} -c {contextSize} --timeout {maxWaitTime} {(allowMemoryMapping ? "--no-mmap" : "")} {(alwaysKeepInMemory ? "--mlock" : "")} --parallel {maxParallelRequests} ";
            process.Exited += processExited;
            process.ErrorDataReceived += processErrorDataReceived;
            process.OutputDataReceived += processOutputDataReceived;

            if (!process.Start())
            {
                GD.Print("Could not start the AI process.");
                return false;
            }

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            void processExited(object sender, EventArgs e)
            {
                debugText += process.ExitCode + System.Environment.NewLine;
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

}

