using Godot;

public partial class SpeechUIManager : Node
{
    [Export] Button speechStartButton;
    [Export] Label speechResult;
    [Export] SpeechRecognizer speechRecognizer;
    [Export] CheckBox speechAutosend;

    private string sttResult;

    public override void _Ready()
    {
        speechStartButton.Pressed += () =>
        {
            if (!speechRecognizer.Active)
            {
                Game.Instance.SetText(speechResult.Text = "");
                OnStartSpeechRecognition();
                speechRecognizer.StartSpeechRecognition();
            }
            else
            {
                OnStopSpeechRecognition();
                speechRecognizer.StopSpeechRecoginition();
            }
        };

        speechRecognizer.OnResult += (result) =>
        {
            Game.Instance.SetText(speechResult.Text = result);
            if (speechAutosend.ButtonPressed && Game.Instance.GetText().Length > 1) Game.Instance.ForceSend();
        };
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
