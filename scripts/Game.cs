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

    LLaMA llamaInstance;

    public override async void _Ready()
    {
        llamaInstance = FindChild("llama") as LLaMA;
        processingText = FindChild("processing_text") as Label;
        chatHistory = FindChild("chat_history") as Label;
        chatInput = FindChild("chat_input") as LineEdit;
        Instance = this;

        llamaInstance.LoadLLaMALibrary();
        llamaInstance.StartLLaMA(33, 5, 1, 4096);

        while (!LLaMA.LLAMA_Initialized())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }


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

        if (session == null && !LLaMA.LLAMA_IsActive(0))
        {
            session = await llamaInstance.StartSession("You are Bellamy, a kind, energetic, casual and helpful person in a role playing game. You do not acknowledge that you are inside a game. You speak only english. You never use emojis. Your responses must never exceed 40 words. Every subordinate clause should be shorter than 10 words. You answer only with speech. You answer without extraneous actions. Always abstain from using markdown. You are now meeting the player (user) for the first time.");
        }

        bool send = false;

        if (!enterPressed)
        {
            if (Input.IsKeyPressed(Key.Enter) && !LLaMA.LLAMA_IsActive(0))
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

                var input = chatInput.Text + " ";

                var promptTask = llamaInstance.Generate(session, input);

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
            .Replace("<|begin_of_text|>", "")
            .Replace("<|start_header_id|>system<|end_header_id|>\n", "System Prompt:\n")
            .Replace("<|start_header_id|>user<|end_header_id|>\n", "Player:\n")
            .Replace("<|start_header_id|>assistant<|end_header_id|>\n", "Bellamy:\n")
            .Replace("<|start_header_id|>", "")
            .Replace("<|end_header_id|>", "")
            .Replace("<|eot_id|>", "");
        }
    }
}
