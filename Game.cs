using Godot;
using AIRPG;

public partial class Game : Node
{
    Label chatHistory;

    LineEdit chatInput;

    Session session;

    bool locked = false;

    public async override void _Ready()
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

                session ??= LLaMA2.StartSession("Llama", "User", "A transcript of a dialog between a User and a digital assistant named Llama. Llama answers short and precise.");

                var input = chatInput.Text + " ";

                chatInput.Text = "";

                await LLaMA2.Prompt(session, input);

                chatHistory.Text = session.fullPrompt.ToString();
            }
            finally
            {
                locked = false;
            }
        }
    }
}
