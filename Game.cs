using Godot;
using Godot.Collections;
using AIRPG;

public partial class Game : Node3D
{
    public async override void _Ready()
    {
        var res = (Dictionary<string, Variant>)Json.ParseString("{ \"test\": 1 }");
        GD.Print(res["test"]);
    }
}
