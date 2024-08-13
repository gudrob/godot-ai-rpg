using Godot;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public partial class SpeechRecognizer : Node
{

    string recordBusName = "Record";
    [Signal]
    public delegate void OnResultEventHandler(string partialResults);
    private int recordBusIdx;
    private AudioEffectRecord _microphoneRecord;
    private GodotObject recognizer;
    public bool Active { get; private set; } = false;

    public override void _Ready()
    {
        recordBusIdx = AudioServer.GetBusIndex(recordBusName);
        _microphoneRecord = AudioServer.GetBusEffect(recordBusIdx, 0) as AudioEffectRecord;
        recognizer = FindChild("speech_recognizer");
        Log("Initialized Speech Recognition");
    }

    private void ProcessMicrophone()
    {
        if (_microphoneRecord != null && _microphoneRecord.IsRecordingActive())
        {
            AudioStreamWav recordedSample = null;
            try
            {
                recordedSample = _microphoneRecord.GetRecording();
            }
            catch (Exception ex)
            {
                GD.Print(ex);
            }
            if (recordedSample == null) return;

            Game.SetProcessingInfo("Recording Speech");


            var sw = Stopwatch.StartNew();
            byte[] data = recordedSample.Stereo ? MixStereoToMono(recordedSample.Data) : recordedSample.Data;
            Log($"Stereo to mono mix: " + sw.Elapsed.TotalMilliseconds);

            sw.Restart();
            var floatData = new float[data.Length / 2];

            for (int i = 0; i < floatData.Length; i++)
            {
                floatData[i] = BitConverter.ToInt16(data, i * 2) / 32767.0f;
            }
            Log($"16 Bit to 32 Bit PCM: " + sw.Elapsed.TotalMilliseconds);

            sw.Restart();
            var newFloatData = Resample(floatData, recordedSample.MixRate, 16000);
            Log($"Resample " + recordedSample.MixRate + " to 16000 hz: " + sw.Elapsed.TotalMilliseconds);

            var total_time = newFloatData.Length / 16000f;
            var audio_ctx = (int)(total_time * 1500 / 30 + 128);


            sw.Restart();
            var token = (Godot.Collections.Array<string>)recognizer.Call("transcribe", newFloatData, "", audio_ctx);
            Log($"Recognizer: " + sw.Elapsed.TotalMilliseconds);

            if (token.Count > 0) CallDeferred("emit_signal", "OnResult", token.First());
        }
    }

    public static float[] Resample(float[] inputSamples, int inputSampleRate, int outputSampleRate)
    {
        int inputLength = inputSamples.Length;
        int outputLength = (int)((double)inputLength / inputSampleRate * outputSampleRate);

        float[] outputSamples = new float[outputLength];

        double ratio = (double)inputSampleRate / outputSampleRate;
        double position = 0;

        for (int i = 0; i < outputLength; i++)
        {
            int leftSampleIndex = (int)position;
            int rightSampleIndex = leftSampleIndex + 1;

            if (rightSampleIndex < inputLength)
            {
                double fraction = position - leftSampleIndex;
                outputSamples[i] = (float)(inputSamples[leftSampleIndex] * (1 - fraction) + inputSamples[rightSampleIndex] * fraction);
            }
            else
            {
                outputSamples[i] = inputSamples[leftSampleIndex];
            }

            position += ratio;
        }

        return outputSamples;
    }

    public void StartSpeechRecognition()
    {
        Active = true;
        if (!_microphoneRecord.IsRecordingActive())
        {
            _microphoneRecord.SetRecordingActive(true);
        }
    }

    public async Task ProcessSpeech()
    {
        bool completed = false;
        Game.SetProcessingInfo("Interpreting speech", true);

        ThreadPool.QueueUserWorkItem((_) =>
        {
            try
            {
                ProcessMicrophone();
            }
            catch (Exception ex)
            {
                GD.Print(ex);
            }
            finally
            {
                completed = true;
            }
        });

        while (!completed)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    public async void StopSpeechRecoginition()
    {
        await ProcessSpeech();
        Active = false;
        if (_microphoneRecord.IsRecordingActive())
        {
            _microphoneRecord.SetRecordingActive(false);
        }
        Game.SetProcessingInfo("Speech processed", false);
    }

    private byte[] MixStereoToMono(byte[] input)
    {
        var sw = Stopwatch.StartNew();
        // If the sample length can be divided by 4, it's a valid stero sound
        if (input.Length % 4 == 0)
        {
            byte[] output = new byte[input.Length / 2];                 // create a new byte array half the size of the stereo length
            int outputIndex = 0;
            for (int n = 0; n < input.Length; n += 4)                     // Loop through each stero sample
            {
                int leftChannel = BitConverter.ToInt16(input, n);        // Get the left channel
                int rightChannel = BitConverter.ToInt16(input, n + 2);     // Get the right channel
                int mixed = (leftChannel + rightChannel) / 2;           // Mix them together
                byte[] outSample = BitConverter.GetBytes((short)mixed); // Convert mix to bytes

                // copy in the first 16 bit sample
                output[outputIndex++] = outSample[0];
                output[outputIndex++] = outSample[1];
            }

            return output;
        }
        else
        {
            byte[] output = new byte[24];

            return output;
        }
    }
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            GetTree().Quit(); // default behavior
        }

    }

    static void Log(string info)
    {
        GD.Print(LogPrefix() + info);
    }

    private static DateTime startTime = Process.GetCurrentProcess().StartTime.ToUniversalTime();
    static string LogPrefix()
    {
        var time = DateTime.UtcNow - startTime;
        return $"[SPEECH RECOGNITION][{time:hh\\:mm\\:ss\\:fff}] ";
    }
}
