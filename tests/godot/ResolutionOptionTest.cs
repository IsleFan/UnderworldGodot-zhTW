using Godot;
using Underworld;

/// <summary>
/// The launcher's Resolution option: it lists the window sizes and full screen, shows the one the
/// settings name, and choosing a size writes the setting and resizes the window to it, shrunk to
/// the screen if the screen is smaller. Full screen is listed but not switched to, to spare the
/// desktop. Restores the setting and the window size it changes.
/// </summary>
public partial class ResolutionOptionTest : Node
{
    private int _failures;
    private void Fail(string m) { _failures++; GD.PrintErr("FAIL " + m); }

    public override void _Ready() => Callable.From(Run).CallDeferred();

    private void Run()
    {
        var settings = uwsettings.instance;
        string saved = settings.resolution;
        var savedSize = DisplayServer.WindowGetSize();
        try
        {
            settings.resolution = "1600x1000";
            var menu = GD.Load<PackedScene>("res://scenes/Launch.tscn").Instantiate<LaunchMenu>();
            AddChild(menu);//runs _Ready, which fills the list and applies the setting
            var list = menu.ResolutionOptionSelector;
            string items = "";
            for (int i = 0; i < list.ItemCount; i++) { items += (i > 0 ? "," : "") + list.GetItemMetadata(i).AsString(); }
            if (items != "1280x800,1600x1000,1920x1200,2560x1600,fullscreen") { Fail($"resolutions are {items}"); }
            if (list.Selected != 1) { Fail($"selected {list.Selected}, expected 1600x1000"); }
            CheckSize(1600, 1000);

            menu._on_resolution_options_item_selected(0);
            if (settings.resolution != "1280x800") { Fail($"choosing 1280x800 set resolution to '{settings.resolution}'"); }
            CheckSize(1280, 800);

            //a size the list does not have is kept as an entry rather than changed
            settings.resolution = "1440x900";
            menu.QueueFree();
            var again = GD.Load<PackedScene>("res://scenes/Launch.tscn").Instantiate<LaunchMenu>();
            AddChild(again);
            if (again.ResolutionOptionSelector.GetItemMetadata(again.ResolutionOptionSelector.Selected).AsString() != "1440x900")
            {
                Fail("a resolution not in the list was not kept");
            }
            CheckSize(1440, 900);
            again.QueueFree();
        }
        catch (System.Exception e)
        {
            Fail($"exception {e}");
        }
        finally
        {
            settings.resolution = saved;
            settings.Save();
            DisplayServer.WindowSetSize(savedSize);
        }
        GD.Print(_failures == 0 ? "GODOT-TEST-VERDICT PASS" : $"GODOT-TEST-VERDICT FAIL {_failures}");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    /// <summary>The window is w x h, or the largest 16:10 that fits the screen's usable area.</summary>
    private void CheckSize(int w, int h)
    {
        var usable = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen()).Size;
        float fit = Mathf.Min(1f, Mathf.Min((float)usable.X / w, (float)usable.Y / h));
        var want = new Vector2I(Mathf.FloorToInt(w * fit), Mathf.FloorToInt(h * fit));
        var got = DisplayServer.WindowGetSize();
        if (Mathf.Abs(got.X - want.X) > 2 || Mathf.Abs(got.Y - want.Y) > 2) { Fail($"window is {got}, expected {want} for {w}x{h}"); }
    }
}
