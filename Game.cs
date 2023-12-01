using Godot;
using AIRPG;

public partial class Game : Node3D
{
    public async override void _Ready()
    {
        LLaMA2.Initialize();

        await LLaMA2.StartSession("uwu", "owo");
    }
}
