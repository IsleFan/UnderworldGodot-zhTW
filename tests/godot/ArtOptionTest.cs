using System.IO;
using Godot;
using Underworld;

/// <summary>
/// The launcher's Art option: it lists Original and every art set under hdartroot, shows the
/// set hdartpath names, and choosing an entry writes hdartpath (empty for Original).
///
/// Builds its own art root with two empty sets and a folder that is not a set, so it needs no
/// game data. Restores the settings it changes, in memory and on disk.
/// </summary>
public partial class ArtOptionTest : Node
{
    private int _failures;
    private void Fail(string m) { _failures++; GD.PrintErr("FAIL " + m); }

    public override void _Ready() => Callable.From(Run).CallDeferred();

    private void Run()
    {
        var settings = uwsettings.instance;
        string savedPath = settings.hdartpath, savedRoot = settings.hdartroot, savedLanguage = settings.language;
        settings.language = "en"; //the launcher's captions follow the language, and this checks the English ones
        string root = Path.Combine(OS.GetUserDataDir(), "art_option_test");
        try
        {
            foreach (var set in new[] { "b4x/uw1", "a2x/uw2", "notaset" })
            {
                Directory.CreateDirectory(Path.Combine(root, set));
            }
            settings.hdartroot = root;
            settings.hdartpath = Path.Combine(root, "b4x");

            var menu = GD.Load<PackedScene>("res://scenes/Launch.tscn").Instantiate<LaunchMenu>();
            AddChild(menu);//runs _Ready, which fills the list
            var list = menu.ArtOptionSelector;
            string items = "";
            for (int i = 0; i < list.ItemCount; i++) { items += (i > 0 ? "," : "") + list.GetItemText(i); }
            if (items != "Original (low-res pixels),a2x,b4x") { Fail($"items are {items}"); }
            if (list.Selected != 2) { Fail($"selected {list.Selected}, expected b4x"); }

            menu._on_art_options_item_selected(1);
            if (settings.hdartpath != Path.Combine(root, "a2x")) { Fail($"choosing a2x set hdartpath to '{settings.hdartpath}'"); }
            menu._on_art_options_item_selected(0);
            if (settings.hdartpath != "") { Fail($"choosing Original set hdartpath to '{settings.hdartpath}'"); }
            menu._on_art_options_item_selected(2);
            if (settings.hdartpath != Path.Combine(root, "b4x")) { Fail($"choosing b4x set hdartpath to '{settings.hdartpath}'"); }
            menu.QueueFree();

            //with no hdartroot the sets are found beside hdartpath's: choosing Original must not lose them
            settings.hdartroot = "";
            settings.hdartpath = Path.Combine(root, "b4x");
            menu = GD.Load<PackedScene>("res://scenes/Launch.tscn").Instantiate<LaunchMenu>();
            AddChild(menu);
            menu._on_art_options_item_selected(0);
            menu.QueueFree();
            menu = GD.Load<PackedScene>("res://scenes/Launch.tscn").Instantiate<LaunchMenu>();
            AddChild(menu);//the launcher opened again, after Original was chosen
            list = menu.ArtOptionSelector;
            items = "";
            for (int i = 0; i < list.ItemCount; i++) { items += (i > 0 ? "," : "") + list.GetItemText(i); }
            if (items != "Original (low-res pixels),a2x,b4x") { Fail($"after choosing Original with no hdartroot, items are {items}"); }
            if (list.Selected != 0) { Fail($"after choosing Original, selected {list.Selected}"); }
            menu.QueueFree();
        }
        catch (System.Exception e)
        {
            Fail($"exception {e}");
        }
        finally
        {
            settings.hdartpath = savedPath;
            settings.hdartroot = savedRoot;
            settings.language = savedLanguage;
            settings.Save();
            if (Directory.Exists(root)) { Directory.Delete(root, true); }
        }
        GD.Print(_failures == 0 ? "GODOT-TEST-VERDICT PASS" : $"GODOT-TEST-VERDICT FAIL {_failures}");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }
}
