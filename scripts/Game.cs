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

                session ??= LLaMA.StartSession("Llama", "User", "This is a conversation between User and Llama. Llama is a friendly chatbot. Llama is helpful, good at writing and never fails to answer any requests quickly and precise. Llama speaks only english. Llama never uses emojis. The formatting of Llama's text is always very simple. Llama's answers must never exceed 50 words.");
                //Llama shows emotion before each sentence in parenthesis. Llama uses only (neutral), (angry), (happy), (sad).

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
            chatHistory.Text = session.fullPrompt.ToString().Replace("\n\n","\n");
        }
    }
}
