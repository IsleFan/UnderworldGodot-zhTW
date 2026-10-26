using System;
using System.Collections.Generic;
using Godot;

namespace Underworld
{
    /// <summary>
    /// The two control schemes the launcher offers (uwsettings.controls). "classic" is the
    /// original's: fixed keys, and every click in the 3D view does what the selected interaction
    /// icon says. "modern" adds, without taking any of that away:
    ///   context clicks: with no icon selected, left click talks to a creature, picks up what can be
    ///   carried and uses anything else, right click looks (shift + left click puts what it picks up
    ///   straight into the backpack), and a small icon by the cursor shows what a left click will do;
    ///   mouse look (MouseLook key): the mouse turns and tilts the view, aiming is at the middle of
    ///   the 3D view, left button attacks with the weapon drawn;
    ///   the arrow keys, Space to jump, a key to draw or put away the weapon, the wheel to step
    ///   through the icons, and keys of the player's choosing (uwsettings.keys).
    /// </summary>
    public static class Controls
    {
        public enum Action
        {
            Forward, Back, StrafeLeft, StrafeRight, TurnLeft, TurnRight, Jump, FlyUp, FlyDown,
            LookDown, LookUp, LookCentre, MouseLook, Weapon, Bash, Slash, Stab
        }

        /// <summary>The original's keys; MouseLook and Weapon are modern only.</summary>
        static readonly Dictionary<Action, Key> ClassicKeys = new()
        {
            { Action.Forward, Key.W }, { Action.Back, Key.S }, { Action.StrafeLeft, Key.A }, { Action.StrafeRight, Key.D },
            { Action.TurnLeft, Key.Q }, { Action.TurnRight, Key.E }, { Action.Jump, Key.J }, { Action.FlyUp, Key.R },
            { Action.FlyDown, Key.F }, { Action.LookDown, Key.Key1 }, { Action.LookUp, Key.Key3 }, { Action.LookCentre, Key.Key2 },
            { Action.Bash, Key.P }, { Action.Slash, Key.Semicolon }, { Action.Stab, Key.Period },
        };

        /// <summary>Modern defaults: the classic keys, Space to jump, Tab for mouse look, X for the weapon.</summary>
        public static Key DefaultKey(Action action) => action switch
        {
            Action.Jump => Key.Space,
            Action.MouseLook => Key.Tab,
            Action.Weapon => Key.X,
            _ => ClassicKeys[action],
        };

        /// <summary>Further keys modern controls accept, while nothing else is bound to them.</summary>
        static readonly (Key Key, Action Action)[] ExtraKeys =
        {
            (Key.Up, Action.Forward), (Key.Down, Action.Back), (Key.Left, Action.TurnLeft), (Key.Right, Action.TurnRight), (Key.J, Action.Jump),
        };

        public static bool Modern => uwsettings.instance.controls == "modern";

        /// <summary>The key of <paramref name="action"/>: under modern controls the player's choice, else the original's (none for the modern-only actions).</summary>
        public static Key KeyFor(Action action)
        {
            if (!Modern)
            {
                return ClassicKeys.TryGetValue(action, out var k) ? k : Key.None;
            }
            if (uwsettings.instance.keys != null && uwsettings.instance.keys.TryGetValue(action.ToString(), out var name))
            {
                var chosen = OS.FindKeycodeFromString(name);
                if (chosen != Key.None) { return chosen; }
            }
            return DefaultKey(action);
        }

        /// <summary>Whether the key of <paramref name="action"/> (or, under modern controls, a free extra key for it) is held.</summary>
        public static bool Held(Action action)
        {
            var key = KeyFor(action);
            if (key != Key.None && Input.IsKeyPressed(key)) { return true; }
            if (Modern)
            {
                foreach (var (extra, act) in ExtraKeys)
                {
                    if (act == action && Input.IsKeyPressed(extra) && !IsBound(extra)) { return true; }
                }
            }
            return false;
        }

        /// <summary>Whether <paramref name="keycode"/> is the key of <paramref name="action"/> (or a free extra key for it).</summary>
        public static bool Is(Key keycode, Action action)
        {
            if (keycode == Key.None) { return false; }
            if (KeyFor(action) == keycode) { return true; }
            if (Modern)
            {
                foreach (var (extra, act) in ExtraKeys)
                {
                    if (act == action && extra == keycode && !IsBound(extra)) { return true; }
                }
            }
            return false;
        }

        static bool IsBound(Key key)
        {
            foreach (Action a in Enum.GetValues(typeof(Action)))
            {
                if (KeyFor(a) == key) { return true; }
            }
            return false;
        }

        // ---- mouse look ----

        /// <summary>The player has mouse look on (it stays on through conversations and menus, which only free the mouse for a while).</summary>
        public static bool MouseLook;

        /// <summary>Mouse look on and in effect now: in the 3D view of the game, nothing else waiting for the mouse.</summary>
        public static bool MouseLookActive =>
            MouseLook && Modern && uimanager.InGame && !uimanager.blockinput
            && uimanager.InteractionMode != uimanager.InteractionModes.ModeOptions
            && !MessageDisplay.WaitingForYesOrNo;

        public static void ToggleMouseLook()
        {
            MouseLook = !MouseLook;
            UpdateMouseCapture();
        }

        /// <summary>Captures the mouse while mouse look is in effect, and gives the game's hidden cursor back otherwise.</summary>
        public static void UpdateMouseCapture()
        {
            var want = MouseLookActive ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Hidden;
            if (Input.MouseMode == Input.MouseModeEnum.Captured || want == Input.MouseModeEnum.Captured)
            {
                if (Input.MouseMode != want) { Input.MouseMode = want; }
            }
        }

        /// <summary>Turns and tilts the view by a mouse movement, as far as the keys can tilt it.</summary>
        public static void Look(Vector2 relative)
        {
            int step = Math.Clamp(uwsettings.instance.mousesensitivity, 1, 10) * 6; //view units per pixel; 182 make a degree
            playerdat.PlayerCameraYaw_dseg_8294 += (short)Math.Round(relative.X * step);
            float tilt = relative.Y * step * (uwsettings.instance.invertmouse ? 1 : -1);
            playerdat.PlayerCameraPitch_dseg_67d6_33D6 = (short)Math.Clamp(playerdat.PlayerCameraPitch_dseg_67d6_33D6 + tilt, -4096, 4096);
        }

        /// <summary>The point of the 3D view the mouse is on: its middle under mouse look.</summary>
        public static Vector2 ViewportPoint() =>
            MouseLookActive ? uimanager.instance.uwviewport.Size / 2 : uimanager.instance.uwviewport.GetLocalMousePosition();

        /// <summary>
        /// The screen position the throw and swing code reads (uimanager.ViewPortMouseXPos/YPos): under
        /// mouse look the one that comes out as straight ahead in the 320x200 frame those use.
        /// </summary>
        public static Vector2 ScreenPoint(Vector2 mouse) =>
            MouseLookActive
                ? new Vector2((uimanager.Window3DLeftBorder + uimanager.Window3DHeadingAdjust) * 4, (200 - 54 - uimanager.Window3DPitchAdjust) * 4)
                : mouse;

        // ---- context clicks ----

        /// <summary>What a left click does to <paramref name="obj"/> with no icon selected under modern controls.</summary>
        public static uimanager.InteractionModes ContextMode(uwObject obj)
        {
            if (obj.majorclass == 1)
            {
                return uimanager.InteractionModes.ModeTalk;
            }
            if (commonObjDat.CanBePickedUp(obj.item_id) || pickup.CanBePickedUpOverrides(obj.item_id))
            {
                return uimanager.InteractionModes.ModePickup;
            }
            return uimanager.InteractionModes.ModeUse;
        }

        /// <summary>Whether clicks in the 3D view follow the context rather than an icon: modern controls with no icon selected.</summary>
        public static bool ContextClicks => Modern && uimanager.InteractionMode == uimanager.InteractionModes.ModeNone;

        /// <summary>The icons the wheel steps through, in the order they stand on screen.</summary>
        static readonly uimanager.InteractionModes[] WheelOrder =
        {
            uimanager.InteractionModes.ModeNone, uimanager.InteractionModes.ModeTalk, uimanager.InteractionModes.ModePickup,
            uimanager.InteractionModes.ModeLook, uimanager.InteractionModes.ModeAttack, uimanager.InteractionModes.ModeUse,
        };

        static ulong lastWheelEvent, lastWheelStep;
        static bool strokeStepped;

        /// <summary>
        /// Whether a wheel event steps the icon. A mouse wheel's notch (factor 1, or 0 where the
        /// system gives none) does, a tenth of a second apart at most. A trackpad or Magic Mouse
        /// sends a stream of small ones for one stroke, momentum included, so a stroke (events
        /// less than a third of a second apart) steps once.
        /// </summary>
        public static bool WheelStep(float factor)
        {
            var now = Time.GetTicksMsec();
            bool newStroke = now - lastWheelEvent > 300;
            lastWheelEvent = now;
            if (factor > 0 && factor < 1)
            {
                if (newStroke) { strokeStepped = false; }
                if (strokeStepped) { return false; }
                strokeStepped = true;
            }
            else if (now - lastWheelStep < 100)
            {
                return false;
            }
            lastWheelStep = now;
            return true;
        }

        /// <summary>The icon after (or before) the selected one, with "none" (context clicks) between the last and the first.</summary>
        public static uimanager.InteractionModes NextMode(int direction)
        {
            int i = Array.IndexOf(WheelOrder, uimanager.InteractionMode);
            if (i < 0) { i = 0; }
            return WheelOrder[(i + direction + WheelOrder.Length) % WheelOrder.Length];
        }
    }
}
