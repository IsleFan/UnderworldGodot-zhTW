namespace Underworld
{
    public partial class ConversationVM : UWClass
    {
        /// <summary>
        /// Gives the listed objects to the NPC, but only if every one of them is among the player's
        /// selected trade items (conv_give_inv): 1 if given, else 0 and nothing is given.
        /// </summary>
        public static void give_to_npc(uwObject talker)
        {
            var CountItems_arg1 = GetConvoStackValueAtPtr(stack + stackptr - 2);//how many items to get
            var ItemIndicesToGive_Array = at(stack + stackptr - 1); // array of item ids to get.  GetConvoStackValueAtPtr(stack + stackptr-1);

            result_register = 0;
            var itemcount = GetPlayerSelectedTradeItems(out int[] itemIds, out int[] itemIndices);
            if (itemcount < CountItems_arg1)
            {//not enough items selected.
                return;
            }
            var used = new bool[itemcount];
            for (int s = 0; s < CountItems_arg1; s++)
            {
                var itemIndexToFind = at(ItemIndicesToGive_Array + s);
                var found = false;
                for (int i = 0; i < itemcount && !found; i++)
                {
                    if (!used[i] && itemIndices[i] == itemIndexToFind)
                    {
                        used[i] = true;
                        found = true;
                    }
                }
                if (!found)
                {
                    return;
                }
            }
            for (int s = 0; s < CountItems_arg1; s++)
            {
                GiveItemIndexToNPC(talker, at(ItemIndicesToGive_Array + s));
            }
            result_register = 1;
        }
    }//end class
}//end namespace
