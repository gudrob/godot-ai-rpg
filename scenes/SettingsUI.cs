using Godot;
using System.Text.Json;

public partial class SettingsUI : Control
{
    [Export]
    Label label_gpu_layers;
    [Export]
    Slider slider_gpu_layers;

    [Export]
    Label label_phoneme_length;
    [Export]
    Slider slider_phoneme_length;

    [Export]
    Label label_temperature;
    [Export]
    Slider slider_temperature;

    [Export]
    Label label_text_delay;
    [Export]
    Slider slider_text_delay;

    [Export]
    Button button_start;


    Settings settings = new();

    public override void _Ready()
    {
        var loadedSettings = LoadSettings();

        if (loadedSettings == null)
        {
            ApplyValues();
        }
        else
        {
            settings.GpuLayers = loadedSettings.GpuLayers;
            settings.PhonemeLength = loadedSettings.PhonemeLength;
            settings.Temperature = loadedSettings.Temperature;
            settings.TextDelay = loadedSettings.TextDelay;
        }

        slider_gpu_layers.ValueChanged += (val) =>
        {
            label_gpu_layers.Text = "GPU layers: " + string.Format("{0:0.##}", val);
        };

        slider_phoneme_length.ValueChanged += (val) =>
        {
            label_phoneme_length.Text = "Phoneme length: " + string.Format("{0:0.##}", val);
        };

        slider_temperature.ValueChanged += (val) =>
        {
            label_temperature.Text = "Temperature: " + string.Format("{0:0.##}", val);
        };

        slider_text_delay.ValueChanged += (val) =>
        {
            label_text_delay.Text = "Text delay: " + string.Format("{0:0.##}", val);
        };

        slider_gpu_layers.Value = settings.GpuLayers;
        slider_phoneme_length.Value = settings.PhonemeLength ;
        slider_temperature.Value = settings.Temperature;
        slider_text_delay.Value = settings.TextDelay;

        button_start.Pressed += () =>
        {
            ApplyValues();
            SaveSettings(settings);
            GetTree().ChangeSceneToFile("res://scenes/default.tscn");
        };
    }

    public void ApplyValues()
    {
        settings.GpuLayers = (int)slider_gpu_layers.Value;
        settings.PhonemeLength = (float)slider_phoneme_length.Value;
        settings.Temperature = (float)slider_temperature.Value;
        settings.TextDelay = (float)slider_text_delay.Value;
    }


    public static Settings LoadSettings(string path = "./settings.json")
    {
        if (!System.IO.File.Exists(path)) return null;

        return JsonSerializer.Deserialize<Settings>(System.IO.File.ReadAllText(path));
    }

    public static void SaveSettings(Settings settings, string path = "./settings.json")
    {
        System.IO.File.WriteAllText(path, JsonSerializer.Serialize(settings));
    }

    [System.Serializable]
    public class Settings
    {
        public int GpuLayers { get; set; }
        public float PhonemeLength { get; set; }
        public float Temperature { get; set; }
        public float TextDelay { get; set; }
        public string LLM { get; set; }
        public string TTS { get; set; }
    }


}
