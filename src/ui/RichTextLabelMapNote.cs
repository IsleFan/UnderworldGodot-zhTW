using Godot;
using Underworld;

public partial class RichTextLabelMapNote : RichTextLabel
{
    public automapnote.mapnotetext referenceToMapNote;

        public static RichTextLabelMapNote AddAutoMapNoteToScreen(automapnote.mapnotetext n)
        {
            RichTextLabelMapNote mapnote = new();
            mapnote.referenceToMapNote = n;//to allow ui referencing.
            //string_to_screen(text, x, y) on the 320x200 screen, y counted up from the bottom and the text's top row: FONT4X5P
            //at four times its size puts a glyph's top 8 design units (32 pixels) below the label's top (ascent 11, glyphs 4
            //rows high above a descent of 1, see SysFontBuilder)
            mapnote.Position = new Vector2(n.posX * 4, (199 - n.posY) * 4 - 32);
            mapnote.FitContent = true;
            mapnote.BbcodeEnabled = true;
            mapnote.AutowrapMode = TextServer.AutowrapMode.Off;
            mapnote.Theme = uimanager.MessageScroll.Theme;
            mapnote.AddThemeFontOverride("normal_font", uimanager.instance.Font4X5P);
            mapnote.AddThemeFontSizeOverride("normal_font_size", 64);//the design size 16, four times over like the screen
            mapnote.Text = $"[color=#331C13]{n.notetext}[/color]";
            mapnote.GuiInput += mapnote._on_mapnote_gui;     
            uimanager.instance.NotesPanel.AddChild(mapnote);
            return mapnote;
        }

        public void _on_mapnote_gui(InputEvent @event)
        {
            if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed && eventMouseButton.ButtonIndex == MouseButton.Left)
            {
                if (uimanager.CurrentAutomapAction == uimanager.automapactions.DELETING)
                {
                    var level = automap.currentautomap;
                    int blockno = uimanager.GetAutomapBlock(ref level, automap.currentworld);
                    
                    if (referenceToMapNote!=null)
                    {
                        automapnote.automapsnotes[blockno].notes.Remove(referenceToMapNote);
                        uimanager.CurrentAutomapAction = uimanager.automapactions.NONE;
                        uimanager.instance.mousecursor.SetCursorToCursor(14);
                        this.QueueFree();
                    }                    
                }
            }
        }
}