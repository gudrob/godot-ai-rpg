using Godot;
using System;
using System.Text.Json;

public partial class SpeechUIManager : Node
{
    [Export] Button speechStartButton;
    [Export] Label speechResult;
    [Export] SpeechRecognizer speechRecognizer;
    [Export] CheckBox speechAutosend;

    private string partialResult;
    private string finalResult;

    public override void _Ready()
    {
        speechStartButton.Pressed += () =>
        {
            if (!speechRecognizer.isCurrentlyListening())
            {
                speechResult.Text = "";
                OnStartSpeechRecognition();
                speechRecognizer.StartSpeechRecognition();
            }
            else
            {
                OnStopSpeechRecognition();
                finalResult = speechRecognizer.StopSpeechRecoginition();
            }
        };

        speechRecognizer.OnPartialResult += (result) =>
        {
            ParseJsonAndSetResult(result, "partial");
        };

        speechRecognizer.OnFinalResult += (result) =>
        {
            var res = ParseJsonAndSetResult(result, "text");
            Game.Instance.SetText(res);
            OnStopSpeechRecognition();
            if (speechAutosend.ButtonPressed) Game.Instance.ForceSend();

        };
    }

    public string ParseJsonAndSetResult(string json, string key)
    {
        var text = speechResult.Text;
        try
        {
            var responseDict = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(json);
            var res = responseDict[key].ToString();
            if (res.Length > 0) text = res;
        }
        catch (Exception ex)
        {
            GD.Print(ex);
        }
        speechResult.Text = text;
        return text;
    }

    public override void _Process(double delta)
    {
    }

    private void OnStopSpeechRecognition()
    {
        speechStartButton.Text = "Start Recognition";
        speechStartButton.Modulate = new Color(1, 1, 1, 1f);
    }


    private void OnStartSpeechRecognition()
    {
        speechResult.Text = "";
        speechStartButton.Text = "Stop Recognition";
        speechStartButton.Modulate = new Color(1f, 0.5f, 0.5f, 1f);
    }
}
