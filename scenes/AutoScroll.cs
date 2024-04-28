using Godot;

public partial class AutoScroll : ScrollContainer
{
    VScrollBar scrollBar;

    double lastMaxValue = 0;

    public override void _Ready()
    {
        scrollBar = GetVScrollBar();
        lastMaxValue = scrollBar.MaxValue;
    }

    public override void _Process(double delta)
    {
        if (lastMaxValue != scrollBar.MaxValue)
        {
            lastMaxValue = scrollBar.MaxValue;
            scrollBar.Value = lastMaxValue;
        }
    }
}
