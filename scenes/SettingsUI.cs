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
        var settings = LoadSettings();

        if(settings == null)
        {
            this.settings.GpuLayers = (int)slider_gpu_layers.Value;
            this.settings.PhonemeLength = (float)slider_phoneme_length.Value;
            this.settings.Temperature = (float)slider_temperature.Value;
            this.settings.TextDelay = (float)slider_text_delay.Value;
        }
        else
        {
            this.settings = settings;
        }

        SaveSettings(this.settings);

        button_start.Pressed += () => { GetTree().ChangeSceneToFile("res://scenes/default.tscn"); };
    }


    public static Settings LoadSettings(string path = "./settings.json")
    {
        if(!System.IO.File.Exists(path)) return null;

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
    }


}
