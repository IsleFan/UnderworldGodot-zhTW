namespace Underworld
{
    public partial class combat : UWClass
    {
        private static void CastOnWeaponHitSpells()
        {
            if (DefendingCharacter == null)
            {
                return;
            }

            //the first four powers need a mobile target (player_attack: tx is only set for one)
            var mobileTarget = !DefendingCharacter.IsStatic;
            //post apply spell effect if applicable
            switch (OnHitSpell)
            {
                case 1:
                    {
                        //Debug.Print("Lifestealer");
                        if (mobileTarget)
                        {
                            playerdat.HPRegenerationChange(-AttackDamage);
                        }
                        break;
                    }
                case 2:
                    {//Debug.Print("Undeadbane"); 
                        if (mobileTarget)
                        {
                            SpellCasting.SmiteUndead(DefendingCharacter.index, UWTileMap.current_tilemap.LevelObjects, playerdat.playerObject);
                        }
                        break;
                    }
                case 3:
                    {
                        //Debug.Print("Firedoom"); 
                        if (!mobileTarget)
                        {
                            break;
                        }
                        //explosion
                        var tile = UWTileMap.current_tilemap.Tiles[DefendingCharacter.tileX, DefendingCharacter.tileY];
                        var height = tile.floorHeight << 3;
                        if (height < 0x80)
                        {
                            height = height + Rng.r.Next(0x80 - height);
                        }
                        animo.SpawnAnimoInTile(subclassindex: 2, xpos: 3, ypos: 3, zpos: (short)height, tileX: DefendingCharacter.tileX, tileY: DefendingCharacter.tileY);
                        //Do damage in area of tile.
                        damage.DamageObjectsInTile(DefendingCharacter.tileX, DefendingCharacter.tileY, 1, 1);//damage_square(tx, ty, 1, 1): from the player
                        break;
                    }
                case 4:
                    {
                        //stonestrike 
                        if (mobileTarget)
                        {
                            SpellCasting.Paralyse(DefendingCharacter.index, UWTileMap.current_tilemap.LevelObjects, playerdat.playerObject);
                        }
                        break;
                    }
                case 5:
                case 6:
                    {
                        if (DefendingCharacter.OneF0Class == 0x14)
                        {
                            //Debug.Print("Entry");
                            //is door
                            SpellCasting.Unlock(DefendingCharacter.index, UWTileMap.current_tilemap.LevelObjects);
                            door.OpenDoor(DefendingCharacter);//and it swings open (OpenDoor)
                        }
                        break;
                    }
                    //case 7: Debug.Print("unknownspecial 7"); break;
                    //case 8: Debug.Print("unknownspecial 8"); break;
            }
        }
    }//end class
}//end namespace