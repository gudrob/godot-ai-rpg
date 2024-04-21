using Godot;
using AIRPG;

public partial class Game : Node
{
    Label chatHistory;

    LineEdit chatInput;

    Session session;

    bool locked = false;

    private bool forceSend = false;

    public static Game instance;

    public override void _Ready()
    {
        LLaMA2.Initialize();

        chatHistory = FindChild("chat_history") as Label;
        chatInput = FindChild("chat_input") as LineEdit;
        instance = this;
    }

    public void SetText(string text)
    {
        chatInput.Text = text;
    }

    public void ForceSend()
    {
        forceSend = true;
    }

    public async override void _Process(double delta)
    {
        var send = Input.IsKeyPressed(Key.Enter);

        if (forceSend)
        {
            send = true;
            forceSend = false;
        }

        if (!locked && send)
        {
            try
            {
                chatInput.Editable = false;

                locked = true;

                session ??= LLaMA2.StartSession("Llama", "User", "A transcript of a dialog between a User and a digital assistant named Llama. Llama answers short and precise. Llama does not use symbols or emojis, only letters and numbers.");

                var input = chatInput.Text + " ";

                var promptTask = LLaMA2.Prompt(session, input);

                while (!promptTask.IsCompleted)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }

                chatInput.Editable = true;

                chatInput.Text = "";
            }
            finally
            {
                locked = false;
            }
        }


        if (chatHistory != null && session != null && session.fullPrompt != null)
        {
            chatHistory.Text = session.fullPrompt.ToString();
        }
    }
}
