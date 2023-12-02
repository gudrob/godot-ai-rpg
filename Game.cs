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

        session = await LLaMA2.StartSession("LLaMA", "User");

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

                var input = chatInput.Text;

                chatInput.Text = "";

                var response = await LLaMA2.Prompt(session, input);

                chatHistory.Text += session.playerCharacterName + ": " + input + session.aiCharacterName + ": " + response;
            }
            finally
            {
                locked = false;
            }
        }
    }
}
