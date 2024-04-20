using Godot;
using AIRPG;

public partial class Game : Node
{
    Label chatHistory;

    LineEdit chatInput;

    Session session;

    bool locked = false;

    public override void _Ready()
    {
        LLaMA2.Initialize();

        chatHistory = FindChild("chat_history") as Label;
        chatInput = FindChild("chat_input") as LineEdit;
    }

    public async override void _Process(double delta)
    {
        if (!locked && Input.IsKeyPressed(Key.Enter))
        {
            try
            {
                locked = true;

                session ??= LLaMA2.StartSession("Llama", "User", "A transcript of a dialog between a User and a digital assistant named Llama. Llama answers short and precise. Llama does not use symbols or emojis, only letters and numbers.");

                var input = chatInput.Text + " ";

                chatInput.Text = "";

                var promptTask = LLaMA2.Prompt(session, input);

                while (!promptTask.IsCompleted)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
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
