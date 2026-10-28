using System;
using System.Diagnostics;
using System.Linq;
using Godot;

namespace Underworld
{
    public partial class npc : objectInstance
    {
        //public static int TESTITEM_ID = 74;
        Label3D myLabel;

        /// <summary>
        /// global shader for npcs.
        /// </summary>
        public static Shader textureshader;

        /// <summary>
        /// Mesh this sprite is drawn on
        /// </summary>
        public uwMeshInstance3D sprite;
        // public uwMeshInstance3D sprite_xfer;
        // public uwMeshInstance3D sprite_objectinfo;

        /// <summary>
        /// The material for rendering this unique npc
        /// </summary>
        public ShaderMaterial material;
        // public ShaderMaterial material_xfer;
        // public ShaderMaterial material_objectinfo;

        /// <summary>
        /// Used to pick the sprite at a particular angle.
        /// </summary>
        static byte[] NPCAnimationHeadingTable = { 0x0, 0x0, 0x0, 0x1, 0x1, 0x1, 0x2, 0x2, 0x2, 0x2, 0x2, 0x3, 0x3, 0x3, 0x4, 0x4, 0x4, 0x4, 0x4, 0x5, 0x5, 0x5, 0x6, 0x6, 0x6, 0x6, 0x6, 0x7, 0x7, 0x7, 0x0, 0x0 };

        /// <summary>
        /// Possible AI goals
        /// </summary>
        public enum npc_goals : byte
        {
            npc_goal_stand_still_0 = 0,
            npc_goal_goto_1 = 1,
            npc_goal_wander_2_confusion = 2,  //used in confusion spell
            npc_goal_follow = 3,
            npc_goal_wander_4 = 4, //possibly this should be another standstill goal
            npc_goal_attack_5 = 5,
            npc_goal_fear_6 = 6,  //goal appears to be attack at a distance using ranged weapons, but also fear??
            npc_goal_stand_still_7 = 7, //same hehaviour as 0
            npc_goal_charmed_8 = 8, //8 is the goal the npc gets when charmed, castle npcs have this too.
            npc_goal_attack_9 = 9, //goal appears to also be attack at a distance, possibly using magic attacks
            npc_goal_want_to_talk = 10,
            npc_goal_stand_still_11 = 11, //This goal is only seen in ethereal void creatures. 0xB
            npc_goal_stand_still_12 = 12,
            npc_goal_unk13 = 13,
            npc_goal_unk14 = 14,
            npc_goal_petrified = 15
        };


        public npc(uwObject _uwobject)
        {
            uwobject = _uwobject;
            try
            {
                SetAnimSprite(animationNo: uwobject.npc_animation, frameNo: uwobject.AnimationFrame, relativeHeading: CalculateFacingAngleToNPC(uwobject));
            }
            catch (Exception ex)
            {
                Debug.Print($"{ex.ToString()}");
            }
        }

        /// <summary>
        /// Creates a rendered version of this object in the gameworld
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static npc CreateInstance(Node3D parent, uwObject obj, string name)
        {
            //obj.item_id = TESTITEM_ID;
            //obj.npc_hp = 1;
            var n = new npc(obj);

            var a_sprite = new uwMeshInstance3D(); //new Sprite3D();
            a_sprite.Name = name;
            a_sprite.Mesh = new QuadMesh();
            a_sprite.Mesh.SurfaceSetMaterial(0, n.material);
            a_sprite.Mesh.Set("size", n.FrameSize);
            n.sprite = a_sprite;
            parent.AddChild(a_sprite);
            a_sprite.Position = new Vector3(0, n.FrameSize.Y / 2 + 0.12f, 0);
            //a_sprite.CreateConvexCollision();
            n.SetAnimSprite(animationNo: obj.npc_animation, frameNo: obj.AnimationFrame, relativeHeading: CalculateFacingAngleToNPC(obj));

            if (ObjectCreator.printlabels)
            {
                string animname;
                animname = CritterArt.GetAnimName(obj.npc_animation, CalculateFacingAngleToNPC(obj));
                Label3D obj_lbl = new();
                obj_lbl.Text = $"{name} {obj.item_id & 0x3F} \nAnim={obj.npc_animation} Frame={obj.AnimationFrame} {animname}\n Goal {obj.npc_goal} Gtarg{obj.npc_gtarg}\nHP:{obj.npc_hp}/{critterObjectDat.avghit(obj.item_id)}";
                obj_lbl.Font = uimanager.instance.Font4X5P;
                obj_lbl.FontSize = 16;
                obj_lbl.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                obj_lbl.Position = new Vector3(0f, 0.6f, 0f);
                parent.AddChild(obj_lbl);
                n.myLabel = obj_lbl;
            }
            obj.instance = n;
            return n;
        }

        static npc()
        {
            textureshader = (Shader)ResourceLoader.Load("res://resources/shaders/uwnpc_allred.gdshader");
        }

        public void SetAnimSprite(int animationNo, short frameNo, int relativeHeading)
        {
            if (uwobject.AnimationFrame >= 8)
            {
                uwobject.AnimationFrame = 0;
            }
            string animname = CritterArt.GetAnimName(animationNo, relativeHeading); // "idle_front";
            var crit = CritterArt.GetCritter(
                CritterToLoad: this.uwobject.item_id & 0x3F, 
                GetStoneArt: (_RES == GAME_UW2) && (this.uwobject.npc_goal== (byte)npc.npc_goals.npc_goal_petrified));
            if (crit.Animations.ContainsKey(animname))
            {
                uwobject.AnimationFrame = (byte)ApplyCritterAnimation(obj: uwobject, animationNo: animationNo, frameNo: frameNo, animname: animname, crit: crit);
            }
            else
            {
                if ((animationNo == ANIMATION_COMBAT_SLASH) || (animationNo == ANIMATION_COMBAT_STAB))
                {
                    animationNo = ANIMATION_COMBAT_BASH;//fallback to bash when unable to load slash or stab.
                }
                else
                {
                    uwobject.npc_animation = 0; //default animation to zero;
                    Debug.Print($"{animname} ({animationNo}) was not found for {this.uwobject.a_name}");
                }

                uwobject.AnimationFrame = (byte)ApplyCritterAnimation(obj: uwobject, animationNo: animationNo, frameNo: frameNo, animname: CritterArt.GetAnimName(0, 0), crit: crit);
            }
            if (ObjectCreator.printlabels)
            {
                if (myLabel != null)
                {
                    myLabel.Text = $"{uwobject.a_name} {uwobject.item_id & 0x3F} \nAnim={uwobject.npc_animation} Frame={uwobject.AnimationFrame} {animname}\n Goal {uwobject.npc_goal} Gtarg{uwobject.npc_gtarg}\nHP:{uwobject.npc_hp}/{critterObjectDat.avghit(uwobject.item_id)}";
                }
            }
        }

        private short ApplyCritterAnimation(uwObject obj, int animationNo, short frameNo, string animname, CritterArt crit)
        {
            var anim = crit.Animations[animname];
            if (material == null)
            {//create the initial material
                var newmaterial = new ShaderMaterial();
                newmaterial.Shader = textureshader;

                newmaterial.SetShaderParameter("albedo", new Color(1, 1, 1, 1));
                newmaterial.SetShaderParameter("uv1_scale", new Vector3(1, 1, 1));
                newmaterial.SetShaderParameter("uv2_scale", new Vector3(1, 1, 1));
                newmaterial.SetShaderParameter("UseAlpha", true);
                newmaterial.SetShaderParameter("objectindex_lowerbytes", obj.index & 0xFF);
                newmaterial.SetShaderParameter("objectindex_upperbytes", (obj.index>>8) & 0xFF);                
                material = newmaterial;
            }

            //assign the params to the shader
            if (frameNo >= 8) { frameNo = 0; }
            if (anim.animIndices[frameNo] == -1)
            {
                frameNo = 0;
            }

            if (anim.animIndices[frameNo] != -1)
            {
                //Render nonxfer bits
                var texture = crit.animSprites[anim.animIndices[frameNo]];
                FrameSize = new Vector2(
                    ArtLoader.NPCSpriteScale * texture.GetWidth(),
                    ArtLoader.NPCSpriteScale * texture.GetHeight()
                    );
                material.SetShaderParameter("texture_albedo", (Texture)texture);
                if (sprite != null)
                {
                    sprite.Mesh.Set("size", FrameSize);
                    sprite.Layers = main.LayerGeo | main.LayerObjectInfo | main.LayerXFER;                 
                }
                return frameNo;
            }
            else
            {
                Debug.Print($"invalid animation {animationNo} {frameNo} for {this.uwobject.item_id}");
            }
            return 0;
        }


        public static void RedrawAnimation(uwObject n)
        {
            short CalcedFacing = CalculateFacingAngleToNPC(n);
            var np = (npc)(n.instance);
            if (np != null)
            {
                np.SetAnimSprite(n.npc_animation, n.AnimationFrame, CalcedFacing);
            }
        }

        /// <summary>
        /// Works out which animation angle to use based on player camera and npc heading.
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public static short CalculateFacingAngleToNPC(uwObject n)
        {
            var ax = (playerdat.CameraPointer2C + motion.PlayerCardinalHeadingLookupTable[playerdat.CameraYawHeadingRelated_2B52]) >> 0xB;
            ax = ((0x20 + (n.heading << 2)) - ax) & 0x1F; //index to lookup an animation table.
            var result = NPCAnimationHeadingTable[ax];
            //Debug.Print($"npc {n.a_name} {n.heading} -> {result}");
            return result;
        }

        /// <summary>
        /// CritterLook: 'You see a <attitude> <kind> named <Name>.' The attitude (block 5, 0x60 + attitude) shows only for
        /// whoami below 0xF0 or 0xFF; the kind is the item's name, or in UW2 for a nameless kind with goal 11 string
        /// 0x115 + its animation; the name is block 7, whoami + 0x10. A name starting in lower case replaces the kind.
        /// </summary>
        public static bool LookAt(uwObject critter)
        {
            string desc = GameStrings.GetString(4, critter.item_id);
            var descZh = desc == "" ? null : ZhText.Enabled ? ZhText.ObjectName(critter.item_id) : null;
            if (desc == "")
            {
                desc = null;
                if ((_RES == GAME_UW2) && (critter.npc_goal == 11))
                {
                    desc = GameStrings.GetString(1, critter.npc_animation + 0x115);
                    descZh = ZhText.Enabled ? GameStrings.GetDisplayString(1, critter.npc_animation + 0x115) : null;
                }
            }
            var who = critter.npc_whoami;
            var attitude = -1;
            if ((who < 0xF0 || who == 0xFF) && (GameStrings.GetString(5, 0x60 + critter.npc_attitude) != ""))
            {
                attitude = 0x60 + critter.npc_attitude;
            }
            string name = null;
            if (who > 0)
            {
                name = GameStrings.GetString(7, who + 0x10);
                if (name == "")
                {
                    name = null;
                }
            }
            var named = name != null && char.IsUpper(name[0]);
            var showKind = desc != null && (name == null || named);

            if (ZhText.Enabled)
            {
                uimanager.AddToMessageScroll(ZhText.LookCreature(
                    attitudeString: desc != null ? attitude : -1,
                    kind: showKind ? descZh : null,
                    name: name != null ? Localization.Lookup(7, who + 0x10, name) : null,
                    named: desc != null && named));
                return true;
            }
            var s = GameStrings.GetString(1, GameStrings.str_you_see_);
            if (desc != null && attitude >= 0)
            {
                var mood = GameStrings.GetString(5, attitude);
                s += ("aeiou".IndexOf(mood[0]) >= 0 ? "an " : "a ") + mood + " ";
            }
            if (showKind)
            {
                s += GameStrings.FixNameString(desc, article: attitude < 0, plural: false);
            }
            if (name != null)
            {
                if (desc != null && named)
                {
                    s += " named ";
                }
                s += GameStrings.FixNameString(name, article: desc == null, plural: false);
            }
            uimanager.AddToMessageScroll($"{s}.");
            return true;
        }

        /// <summary>
        /// Gets a list of spells and properties the npc has.
        /// </summary>
        /// <param name="critter"></param>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static bool ListNPCProperties(uwObject critter, out string propertystring)
        {
            var propertycount = 0;
            byte[] prop = new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff };
            propertystring = "";
            if (critterObjectDat.NPCPower_0x2DBits1To7(critter.item_id) != 0)
            {
                int si = 0;

                for (si = 0; si < 3; si++)
                {//read in the spells
                    prop[si] = (byte)critterObjectDat.spell(critter.item_id, si);
                }
                if (critterObjectDat.generaltype(critter.item_id) == 0x17)
                {//liches?
                    if (critterObjectDat.isFlier(critter.item_id))
                    {
                        prop[si++] = 0x39;//can cast fly
                    }
                    if (critterObjectDat.dooropenskill(critter.item_id) >= 0x2D)
                    {
                        prop[si++] = 0x23;//can cast open
                    }
                    var testdam = 1;
                    var scale = damage.ScaleDamage(critter.item_id, ref testdam, 8);

                    if (scale == 0)
                    {
                        prop[si++] = 0x1C; //can cast flameproof
                    }

                    if (critterObjectDat.NPCPower_0x2DBits1To7(critter.item_id) >= 0x19)
                    {
                        if (critterObjectDat.level(critter.item_id) >= 9)
                        {
                            prop[si++] = 0x3b; //can cast ironflesh
                            propertycount++;
                        }
                    }
                }

                //Make sure properties are unique
                if (prop[0] == prop[1])
                {
                    prop[1] = 0xff;
                }
                else
                {
                    if (prop[1] == prop[2])
                    {
                        prop[1] = 0xff;
                    }
                }
                if (prop[0] == prop[2])
                {
                    prop[2] = 0xff;
                }

                for (si = 0; si < 8; si++)
                {
                    if (prop[si] != 0xff)
                    {
                        propertycount++;
                    }
                }
                if (propertycount == 0)
                {
                    return false;
                }
                else
                {
                    var propertiesremaining = propertycount - 1;
                    for (si = 0; si < 8; si++)
                    {
                        //loop the properties and turn them into spellnames
                        if (prop[si] != 0xff)
                        {
                            int major = (prop[si] & 0xC0) >> 6;
                            int minor = prop[si] & 0x3F;
                            if (major == 0)
                            {
                                var spell = RunicMagic.SpellList[prop[si]];
                                if (spell.SpellMajorClass == 4)
                                {//if healing? and a golem??
                                    if (critterObjectDat.generaltype(critter.item_id) == 0xF)
                                    {
                                        major = 1; minor = 6;//bouncing???
                                    }
                                }
                            }
                            if (major <= 0)
                            {
                                minor += 0x100;
                            }
                            else
                            {
                                minor += ((major + 0xC0) << 4);
                            }

                            propertystring += ZhText.Enabled ? GameStrings.GetDisplayString(6, minor) : GameStrings.GetString(6, minor);
                            if (propertiesremaining >= 2)
                            {
                                propertystring += ZhText.Enabled ? "、" : ", ";
                            }
                            else
                            {
                                if ((propertiesremaining == 1) && (propertycount > 1))
                                {
                                    propertystring += ZhText.Enabled ? "和" : GameStrings.GetString(0x274); //AND
                                }
                            }
                            propertiesremaining--;
                        }
                    }
                }
            }
            return (propertycount > 0);
        }


        /// <summary>
        /// Breaks down the angle in the the facing sector. Clockwise from 0)
        /// </summary>
        /// <param name="angle">Angle.</param>
        static short facing(float angle)
        {
            if ((angle >= -22.5) && (angle <= 22.5))
            {
                return 0;//*AnimRange;//Facing forward
            }
            else
            {
                if ((angle > 22.5) && (angle <= 67.5))
                {//Facing forward left
                    return 1;//*AnimRange;
                }
                else
                {
                    if ((angle > 67.5) && (angle <= 112.5))
                    {//facing (right)
                        return 2;//*AnimRange;
                    }
                    else
                    {
                        if ((angle > 112.5) && (angle <= 157.5))
                        {//Facing away left
                            return 3;//*AnimRange;
                        }
                        else
                        {
                            if (((angle > 157.5) && (angle <= 180.0)) || ((angle >= -180) && (angle <= -157.5)))
                            {//Facing away
                                return 4;//*AnimRange;
                            }
                            else
                            {
                                if ((angle >= -157.5) && (angle < -112.5))
                                {//Facing away right
                                    return 5;//*AnimRange;
                                }
                                else
                                {
                                    if ((angle > -112.5) && (angle < -67.5))
                                    {//Facing (left)
                                        return 6;//*AnimRange;
                                    }
                                    else
                                    {
                                        if ((angle > -67.5) && (angle < -22.5))
                                        {//Facing forward right
                                            return 7;//*AnimRange;
                                        }
                                        else
                                        {
                                            return 0;//*AnimRange;//default
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }


        /// <summary>
        /// When sleeping or other circumstances certain npcs will be returned to their home tiles.
        /// Will take a count of how many npcs of the given general type are hostile or friendly.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="generaltypes"></param>
        public static void ReturnNPCToHomeTile(uwObject obj, ref int[] generaltypes)
        {
            if (!UWTileMap.ValidTile(obj.tileX, obj.tileY))
            {
                return;
            }
            if (obj.SpawnedCritter_0XD_Bit8 == 0)
            {
                obj.UnkBit_0x19_7 = 0;
                obj.IsAlly = 0;
                obj.UnkBit_0x19_4 = 0;
                obj.UnkBit_0x19_5 = 0;
                obj.UnkBit_0x19_0_likelyincombat = 0;
                obj.UnkBit_0x19_1 = 0;
                if ((_RES != GAME_UW2) || (obj.npc_goal != (byte)npc.npc_goals.npc_goal_want_to_talk))
                {//UW2 keeps the heading of one waiting to talk
                    obj.heading = (short)(Rng.r.Next(8) & 7);
                }
                var avghit = critterObjectDat.avghit(obj.item_id);
                if (obj.npc_hp < avghit)
                {
                    if (obj.UnkBit_0XD_Bit9_StopHPRegen == 0)
                    {//half way to its average hit points
                        obj.npc_hp = (byte)((obj.npc_hp + avghit) / 2);
                    }
                }
                if (obj.UnkBit_0XA_Bit7 == 0)
                {
                    var generaltype = critterObjectDat.generaltype(obj.item_id);
                    switch (obj.npc_attitude)
                    {
                        case 0:
                            generaltypes[generaltype]--;
                            break;
                        case 3:
                            generaltypes[generaltype]++;
                            break;
                    }
                }

                if ((obj.quality != obj.npc_xhome) || (obj.owner != obj.npc_yhome))
                {//back to the home square kept in quality and owner (npc_xhome/yhome is the square it is on)
                    npc.moveNPCToTile(obj, obj.quality, obj.owner);
                }

            }
            else
            {
                //remove temporary spawned npcs
                ObjectRemover_OLD.DeleteObjectFromTile_DEPRECIATED(tileX: obj.tileX, tileY: obj.tileY, indexToDelete: obj.index);
            }
        }

    }//end class
}//end namespace