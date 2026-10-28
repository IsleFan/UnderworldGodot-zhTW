using System.Diagnostics;

namespace Underworld
{
    public class a_delete_object_trap : trap
    {
        /// <summary>
        /// Deletes the trap's linked object, with everything in it, from square (quality, owner) (TRAP_DELETE_OBJECT).
        /// UW2 deletes a trap or trigger the trap way, so the triggers pointing at it go too, and an
        /// animation object with its overlay.
        /// </summary>
        public static void Activate(uwObject trapObj, uwObject[] objList)
        {
            var tileX = trapObj.quality; var tileY = trapObj.owner;           
            var indexToDelete = trapObj.link;
            Debug.Print($"Delete object trap: Deleting {indexToDelete}");
            if (indexToDelete == 0 || !UWTileMap.ValidTile(tileX, tileY))
            {
                return;
            }
            var tile = UWTileMap.current_tilemap.Tiles[tileX, tileY];
            var toDelete = objList[indexToDelete];
            if (_RES == GAME_UW2 && toDelete.majorclass == 6)
            {
                ObjectRemover_OLD.RemoveTrapTriggerChain(toDelete, tile.Ptr + 2);
            }
            else
            {
                if (_RES == GAME_UW2 && toDelete.majorclass == 7)
                {
                    AnimationOverlay.RemoveAnimationOverlay(toDelete.index);
                }
                ObjectRemover_OLD.RemoveObjectAndChainFromLists(toDelete, tile.Ptr + 2);
            }
        }        
    }//end class
}//end namespace
