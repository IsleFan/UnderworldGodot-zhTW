using System.Collections.Generic;

namespace Underworld
{
    public class objectCombination : Loader
    {
        public static List<objectCombination> ObjectCombinations = new();

        public int Obj_A;
        public int Obj_B;

        public bool DestroyA;
        public bool DestroyB;

        public int Obj_Out;

        static objectCombination()
        {
            byte[] buffer;
            ReadStreamFile(System.IO.Path.Combine(BasePath, "DATA", "CMB.DAT"), out buffer);
            {
                int add_ptr = 0;
                for (int i = 0; i < 10; i++)
                {
                    int obj_a = (int)getAt(buffer, add_ptr, 16);
                    int obj_b = (int)getAt(buffer, add_ptr + 2, 16);
                    int output = (int)getAt(buffer, add_ptr + 4, 16);
                    if (!(((obj_a & 0x7fff) == (obj_b & 0x7fff)) && ((obj_a & 0x7fff) == (output & 0x7fff))))   // only add if the 3 objects are different.
                    {
                        ObjectCombinations.Add(
                            new objectCombination
                                (
                                    obj_a & 0x7fff,
                                    obj_b & 0x7fff,
                                    (obj_a >> 15) == 1,
                                    (obj_b >> 15) == 1,
                                    output & 0x7fff
                                )
                        );
                    }

                    add_ptr += 6;
                }
            }
        }

        public objectCombination(int obj_a, int obj_b, bool destroy_a, bool destroy_b, int obj_out)
        {
            Obj_A = obj_a;
            Obj_B = obj_b;
            DestroyA = destroy_a;
            DestroyB = destroy_b;
            Obj_Out = obj_out;
        }

        /// <summary>
        /// Checks if the supplied objects (o1 and o2) can combine, 
        /// if so return the combination object that defines how the combination will work
        /// </summary>
        /// <param name="obj_a"></param>
        /// <param name="obj_b"></param>
        /// <param name="output"></param>
        /// <returns>the objectCombination</returns>
        public static objectCombination GetCombination(int obj_a, int obj_b)
        {
            foreach (var cmb in ObjectCombinations)
            {
                if (
                    (cmb.Obj_A == obj_a) && (cmb.Obj_B == obj_b)
                    ||
                    ((cmb.Obj_A == obj_b) && (cmb.Obj_B == obj_a))
                    )
                {
                    return cmb;
                }
            }
            return null; // no combination found.
        }

        /// <summary>
        /// Performs an object combination
        /// </summary>
        /// <param name="ItemInInventory">object in inventory</param>
        /// <param name="ItemInHand">object in hand</param>
        public static bool TryObjectCombination(uwObject ItemInInventory, uwObject ItemInHand)
        {
            //ObjsBeCombinable: neither may be a stack of more than one or hold anything
            if (HoldsOrStacks(ItemInInventory) || HoldsOrStacks(ItemInHand))
            {
                return false;
            }
            var combo = GetCombination(ItemInHand.item_id, ItemInInventory.item_id);
            if (combo == null)
            {
                return false;
            }
            //RemoveAfterCombine: bit 15 of the source word that names the object. The output is a new object: on the cursor
            //when the held object is used up, else in the inventory object's slot when that one is; a source that is not used
            //up stays as it is.
            var handUsed = (ItemInHand.item_id == combo.Obj_A) ? combo.DestroyA : combo.DestroyB;
            var invUsed = (ItemInInventory.item_id == combo.Obj_A) ? combo.DestroyA : combo.DestroyB;
            if (handUsed)
            {
                playerdat.ObjectInHand = -1;
                ObjectFreeLists.ReleaseFreeObject(ItemInHand);
                ObjectCreator.SpawnObjectInHand(combo.Obj_Out);
            }
            if (invUsed)
            {
                if (!handUsed)
                {//the new object takes the inventory object's place
                    playerdat.WeightCarried -= (short)(commonObjDat.mass(ItemInInventory.item_id) * ItemInInventory.ObjectQuantity);
                    MakeFreshObject(ItemInInventory, combo.Obj_Out);
                    playerdat.WeightCarried += (short)commonObjDat.mass(ItemInInventory.item_id);
                    uimanager.RefreshWeightDisplay();
                }
                else
                {
                    playerdat.RemoveFromInventory(ItemInInventory.index, true);
                }
            }
            uimanager.UpdateInventoryDisplay();
            if (playerdat.ObjectInHand > 0)
            {
                uimanager.instance.mousecursor.SetCursorToObject(UWTileMap.current_tilemap.LevelObjects[playerdat.ObjectInHand].item_id);
            }
            else
            {
                uimanager.instance.mousecursor.SetCursorToCursor();
            }
            return true;//combo has worked
        }

        static bool HoldsOrStacks(uwObject obj)
        {
            return ((obj.is_quant == 1) && (obj.link > 1)) || ((obj.is_quant == 0) && (obj.link > 0));
        }

        /// <summary>
        /// Turns obj into a new object of item_id with the defaults a created object has (CreateObj).
        /// </summary>
        static void MakeFreshObject(uwObject obj, int item_id)
        {
            obj.item_id = item_id;
            obj.quality = 0x28;
            obj.owner = 0;
            obj.doordir = 0;
            obj.invis = 0;
            obj.enchantment = 0;
            obj.flags = 0;
            obj.heading = 0;
            var stackable = commonObjDat.stackable(item_id);
            if ((stackable == 0) || (stackable == 2))
            {
                obj.is_quant = 1;
                obj.link = 1;
            }
            else
            {
                obj.is_quant = 0;
                obj.link = 0;
            }
        }

    }//end class
}//end namespace