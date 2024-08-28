using Godot;
using AIRPG;

public partial class Game : Node
{
    Label chatHistory;

    Label processingText;

    LineEdit chatInput;

    Session session;

    bool locked = false;

    private bool forceSend = false;

    private bool enterPressed = false;

    public static Game Instance { private set; get; }

    bool isProcessing = false;

    string processingBaseText = "";

    public double processing = 0;

    public int processingStep = 0;

    LLaMA llamaInstace;

    public override void _Ready()
    {
        llamaInstace = FindChild("llama") as LLaMA;
        processingText = FindChild("processing_text") as Label;
        chatHistory = FindChild("chat_history") as Label;
        chatInput = FindChild("chat_input") as LineEdit;
        Instance = this;

        llamaInstace.LoadLLaMALibrary();
        llamaInstace.StartLLaMA(33, 2, 1, 4096);
    }

    public void SetText(string text)
    {
        chatInput.Text = text;
    }

    public string GetText()
    {
        return chatInput.Text;
    }

    public void ForceSend()
    {
        forceSend = true;
    }

    public static void SetProcessingInfo(string infotext, bool isProcessing = true)
    {
        Instance.isProcessing = isProcessing;
        Instance.processingBaseText = infotext;
    }

    public async override void _Process(double delta)
    {
        if (!LLaMA.LLAMA_Initialized()) return;

        bool send = false;

        if (!enterPressed)
        {
            if (Input.IsKeyPressed(Key.Enter))
            {
                send = true;
                enterPressed = true;
            }
        }
        else if (!Input.IsKeyPressed(Key.Enter))
        {
            enterPressed = false;
        }

        processing += delta;

        if (processing >= 0.33d)
        {
            processing = 0;

            processingStep = (processingStep + 1) % 4;
        }


        if (isProcessing)
        {
            processingText.Text = processingBaseText + new string('.', processingStep) + new string(' ', 3 - processingStep);
        }
        else
        {
            processingText.Text = processingBaseText + new string(' ', 3);
        }

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

                session ??= llamaInstace.StartSession("You are Llama, a kind, energetic, casual and helpful NPC in a role playing game. You do not acknowledge that you are inside a game except when you are asked. You speak only english. You never use emojis. Your respomses must never exceed 40 words. Every subordinate clause must be shorter than 8 words. You are speaking, not writing.");

                var input = chatInput.Text + " ";

                var promptTask = llamaInstace.Generate(session, input);

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
            chatHistory.Text = session.fullPrompt.ToString().Replace("\n\n", "\n")
            .Replace("<|beginning_of_text|>", "")
            .Replace("<|start_header_id|>system<|end_header_id|>\n", "System Prompt:\n")
            .Replace("<|start_header_id|>user<|end_header_id|>\n", "User:\n")
            .Replace("<|start_header_id|>assistant<|end_header_id|>\n", "Llama:\n")
            .Replace("<|start_header_id|>", "")
            .Replace("<|end_header_id|>", "")
            .Replace("<|eot_id|>", "");
        }
    }
}
