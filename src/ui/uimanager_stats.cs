using System;
using Godot;

namespace Underworld
{
	public partial class uimanager : Node2D
	{
		[ExportGroup("Stats")]
		//Stats Display
		[Export] public RichTextLabel Charname;
		[Export] public RichTextLabel CharLevel;
		[Export] public RichTextLabel CharClass;
		[Export] public RichTextLabel STR;
		[Export] public RichTextLabel DEX;
		[Export] public RichTextLabel INT;
		[Export] public RichTextLabel VIT;
		[Export] public RichTextLabel MANA;
		[Export] public RichTextLabel EXP;
		[Export] public RichTextLabel StatsName;
		[Export] public RichTextLabel StatsValue;

		public static int StatsOffset = 0;

		static string AttributesFontColor
		{
			get
			{
				if (UWClass._RES == UWClass.GAME_UW2)
				{
					return PaletteLoader.ToBBCode(0, 0xC4);
				}
				else
				{
					return PaletteLoader.ToBBCode(0, 0xF1);
				}
			}
		}

		static string SkillsFontColor
		{
			get
			{
				if (ZhText.Enabled)
				{
					return "#FFFFFF"; //the grey does not show on the panel, see InitStatsZh
				}
				if (UWClass._RES == UWClass.GAME_UW2)
				{
					return PaletteLoader.ToBBCode(0, 0xC9);
				}
				else
				{
					return PaletteLoader.ToBBCode(0, 0x68);
				}
			}
		}

		private void InitStats()
		{
			if (UWClass._RES == UWClass.GAME_UW2)
			{
				var adjustment = new Godot.Vector2(-2f, -14f);
				instance.Charname.Position += adjustment;
				instance.CharClass.Position += adjustment;
				instance.CharLevel.Position += adjustment;
				instance.STR.Position += adjustment;
				instance.DEX.Position += adjustment;
				instance.INT.Position += adjustment;
				instance.VIT.Position += adjustment;
				instance.MANA.Position += adjustment;
				instance.EXP.Position += adjustment;
				adjustment = new Godot.Vector2(-2f, -18f);
				instance.StatsName.Position += adjustment;
				instance.StatsValue.Position += adjustment;
			}
			if (ZhText.Enabled)
			{
				InitStatsZh();
			}
		}

		/// <summary>The attribute values' font size in Chinese, smaller than the scene's 64 to sit with the Chinese names.</summary>
		const int ZhValueFontSize = 52;

		/// <summary>
		/// The stats panel in Chinese: the attribute values smaller and bold, the skill list raised
		/// to fit its box and drawn white, where the grey did not show. The game fonts are bitmaps,
		/// which Godot will not embolden, so the values are thickened with a shadow of their own colour.
		/// </summary>
		private void InitStatsZh()
		{
			// Each line's baseline is its ascent (11 rows, see SysFontProvider) below the label's
			// top, so a smaller font moves the label down by the difference to keep the baseline.
			var down = new Vector2(0, 11f * (64 - ZhValueFontSize) / SysFontBuilder.DesignSize);
			var ink = Color.FromHtml(AttributesFontColor);
			foreach (var value in new[] { STR, DEX, INT, VIT, MANA, EXP })
			{
				value.AddThemeFontSizeOverride("normal_font_size", ZhValueFontSize);
				value.Position += down;
				value.AddThemeColorOverride("font_shadow_color", ink);
				value.AddThemeConstantOverride("shadow_offset_x", 2);
				value.AddThemeConstantOverride("shadow_offset_y", 0);
			}

			var up = new Vector2(0, -8);
			StatsName.Position += up;
			StatsValue.Position += up;
		}

		/// <summary>
		/// Updates the stats display panel
		/// </summary>
		public static void RefreshStatsDisplay()
		{
			// playerdat setters (max_hp, max_mana, play_level) call this, so it
			// runs from pure game-state paths that have no scene tree, eg the
			// headless save-game unit tests. instance is only assigned in _Ready.
			if (instance == null) { return; }
			instance.Charname.Text =$"[center][color={AttributesFontColor}]{playerdat.CharName.ToUpper()}[/color][/center]";
			instance.CharClass.Text = $"[color={AttributesFontColor}]{playerdat.CharClassName.ToUpper()}[/color]";
			var level = ZhText.Enabled
				? ZhText.Compose($"第{playerdat.play_level}級")
				: $"{playerdat.play_level}{GameStrings.GetOrdinal(playerdat.play_level).ToUpper()}";
			instance.CharLevel.Text = $"[right][color={AttributesFontColor}]{level}[/color][/right]";
			instance.STR.Text = $"[right][color={AttributesFontColor}]{playerdat.STR}[/color][/right]";
			instance.DEX.Text = $"[right][color={AttributesFontColor}]{playerdat.DEX}[/color][/right]";
			instance.INT.Text = $"[right][color={AttributesFontColor}]{playerdat.INT}[/color][/right]";
			instance.VIT.Text = $"[right][color={AttributesFontColor}]{playerdat.play_hp}/{playerdat.max_hp}[/color][/right]";
			instance.MANA.Text = $"[right][color={AttributesFontColor}]{playerdat.play_mana}/{playerdat.max_mana}[/color][/right]";
			instance.EXP.Text = $"[right][color={AttributesFontColor}]{playerdat.Exp / 10}[/color][/right]";
			instance.StatsName.Text = "";
			instance.StatsValue.Text = "";
			for (int s = 0; s < 6; s++)
			{
				if (UWClass._RES != UWClass.GAME_UW2)
				{//UW1 has no skill point row: row s is skill StatsOffset + s
					instance.StatsName.Text += $"[color={SkillsFontColor}]{GameStrings.GetDisplayString(2, 31 + StatsOffset + s).ToUpper()}[/color]\n";
					instance.StatsValue.Text += $"[right][color={SkillsFontColor}]{playerdat.GetSkillValue(StatsOffset + s)}[/color][/right]\n";
				}
				else if ((StatsOffset == 0) && (s == 0))
				{
					//display training points
					instance.StatsName.Text = $"[color={SkillsFontColor}]{(ZhText.Enabled ? ZhText.Compose("技能點") : "Skill Pt")}[/color]\n";
					instance.StatsValue.Text = $"[right][color={SkillsFontColor}]{playerdat.SkillPoints}[/color][/right]\n";
				}
				else
				{//display stat
					instance.StatsName.Text += $"[color={SkillsFontColor}]{GameStrings.GetDisplayString(2, 30 + StatsOffset + s).ToUpper()}[/color]\n";
					instance.StatsValue.Text += $"[right][color={SkillsFontColor}]{playerdat.GetSkillValue(StatsOffset + s - 1)}[/color][/right]\n";
				}
			}
		}


		private void _on_stats_updown_gui_input(InputEvent @event, long extra_arg_0)
		{
			if (@event is InputEventMouseButton eventMouseButton && eventMouseButton.Pressed && eventMouseButton.ButtonIndex == MouseButton.Left)
			{
				switch (extra_arg_0)
				{
					case -1:
						StatsOffset = Math.Max(0, StatsOffset - 1);
						RefreshStatsDisplay();
						break;
					case 1:
						StatsOffset = Math.Min(UWClass._RES == UWClass.GAME_UW2 ? 15 : 14, StatsOffset + 1);//UW1 shows skills 0-19 six at a time
						RefreshStatsDisplay();
						break;
				}
			}
		}

	}//end class
}//end namespace