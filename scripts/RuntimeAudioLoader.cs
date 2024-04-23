using Godot;
using System;
using System.Threading.Tasks;

public class RuntimeAudioLoader
{

    public static async Task<AudioStreamWav> LoadFile(string filepath)
    {
        byte[] bytes = await System.IO.File.ReadAllBytesAsync(filepath);

        var newStream = new AudioStreamWav();

        int bitsPerSample = 0;

        for (int i = 0; i < 100; i++)
        {
            string header = "" + (char)bytes[i] + (char)bytes[i + 1] + (char)bytes[i + 2] + (char)bytes[i + 3];

            if (header == "fmt ")
            {
                int fsc0 = i + 8;
                newStream.Format = (AudioStreamWav.FormatEnum)BitConverter.ToInt16(bytes, fsc0);

                int channelNum = BitConverter.ToInt16(bytes, fsc0 + 2);
                if (channelNum == 2)
                    newStream.Stereo = true;

                int sampleRate = BitConverter.ToInt32(bytes, fsc0 + 4);
                newStream.MixRate = sampleRate;

                bitsPerSample = BitConverter.ToInt16(bytes, fsc0 + 14);
            }
            else if (header == "data")
            {
                int audioDataSize = BitConverter.ToInt32(bytes, i + 4);

                int dataEntryPoint = i + 8;

                byte[] data = new byte[audioDataSize];
                Array.Copy(bytes, dataEntryPoint, data, 0, audioDataSize);

                if (bitsPerSample == 24 || bitsPerSample == 32)
                    newStream.Data = ConvertTo16Bit(data, bitsPerSample);
                else
                    newStream.Data = data;
                break;
            }
        }

        int sampleNum = newStream.Data.Length / 4;
        newStream.LoopEnd = sampleNum;
        newStream.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
        return newStream;
    }

    private static byte[] ConvertTo16Bit(byte[] data, int from)
    {
        if (from == 24)
        {
            byte[] newData = new byte[data.Length * 2 / 3];
            int j = 0;
            for (int i = 0; i < data.Length; i += 3)
            {
                newData[j] = data[i + 1];
                newData[j + 1] = data[i + 2];
                j += 2;
            }
            return newData;
        }
        else if (from == 32)
        {
            byte[] newData = new byte[data.Length / 2];
            float single;
            int value;
            for (int i = 0; i < data.Length; i += 4)
            {
                single = BitConverter.ToSingle(data, i);
                value = (int)(single * 32768);
                newData[i / 2] = (byte)value;
                newData[i / 2 + 1] = (byte)(value >> 8);
            }
            return newData;
        }
        return data;
    }
}