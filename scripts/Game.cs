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

    public override void _Ready()
    {
        LLaMA.Initialize();

        processingText = FindChild("processing_text") as Label;
        chatHistory = FindChild("chat_history") as Label;
        chatInput = FindChild("chat_input") as LineEdit;
        Instance = this;
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

                session ??= LLaMA.StartSession("You are Llama, a friendly chatbot. You are helpful, good at writing and never fail to answer any requests quickly and precise. You speak only english. You never use emojis. The formatting of your text is always very simple. Your respomses must never exceed 50 words. Every subordinate clause must be shorter than 10 words. ");

                var input = chatInput.Text + " ";

                var promptTask = LLaMA.Generate(session, input);

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
            .Replace("<|start_header_id|>system\n<|end_header_id|>", "System Prompt:\n")
            .Replace("<|start_header_id|>user\n<|end_header_id|>", "User:\n")
            .Replace("<|start_header_id|>assistant\n<|end_header_id|>", "Llama:\n")
            .Replace("<|start_header_id|>", "")
            .Replace("<|end_header_id|>", "")
            .Replace("<|eot_id|>", "");
        }
    }
}
