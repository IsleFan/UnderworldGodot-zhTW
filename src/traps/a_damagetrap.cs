using System;

namespace Underworld
{
    public class a_damage_trap : trap
    {

        public static void Activate(uwObject trapObj, uwObject triggeringCharacter)
        {
            ApplyDamageTrap(triggeringCharacter, trapObj.owner != 0, trapObj.quality);
        }


        public static void ApplyDamageTrap(uwObject triggeringCharacter, bool Poison, int basedamage)
        {//whack_thing: physical damage (type 4, through the resistances) to whoever set it off. A poison trap
         //poisons the player at that strength unless he resists poison; for anyone else it is plain damage.
            if (triggeringCharacter == null)
            {
                return;
            }
            if (Poison && (triggeringCharacter == playerdat.playerObject))
            {
                if (basedamage > playerdat.play_poison)
                {
                    var test = 1;
                    if (damage.ScaleDamage(127, ref test, 0x10) != 0)
                    {
                        playerdat.play_poison = (byte)basedamage;//a four-bit field, as the original's
                    }
                }
                return;
            }
            if (basedamage > 0)
            {
                damage.DamageObject(objToDamage: triggeringCharacter, basedamage: basedamage, damagetype: 4, objList: UWTileMap.current_tilemap.LevelObjects, WorldObject: true, damagesource: 0);
            }
        }
    }
}
