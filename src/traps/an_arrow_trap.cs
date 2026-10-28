namespace Underworld
{
    public class an_arrow_trap : trap
    {
        public static void Activate(uwObject trapObj, int triggerX, int triggerY, uwObject[] objList)
        {
            
            motion.RangedAmmoItemID = (int)trapObj.owner | ((int)trapObj.quality<<5);
            motion.RangedAmmoType = 0x14;
            motion.MissileHeading = 2;
            motion.MissilePitch = 2;
            motion.projectileXHome = triggerX;
            motion.projectileYHome = triggerY;

            motion.MissileLauncherHeadingBase = 0;

            motion.MissileFlagB = true;//using_bow: not the thrown object's sound
            var arrow = motion.PrepareProjectileObject(trapObj);
            motion.MissileFlagB = false;
            if (arrow!=null)
            {
                ObjectCreator.RenderObject(arrow,UWTileMap.current_tilemap);
                if (_RES == GAME_UW2)
                {//trap_fire's launch sound; UW1 only has missile_fire's
                    UWsoundeffects.PlaySoundEffectAtObject(0xA, arrow, 0x14);
                }
            }
        }
    }//end class
        
}//end namespace
        //owner | (quality<<5)