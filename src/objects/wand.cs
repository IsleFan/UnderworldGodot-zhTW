namespace Underworld
{
    public class wand : objectInstance
    {
        /// <summary>
        /// UseWand: from the inventory a wand fires its traps and casts its spell (checkSpell); a wand whose spell object
        /// is then gone becomes the broken wand (item + 4) with a snap. Broken wands, and wands in the world, do nothing.
        /// </summary>
        public static bool Use(uwObject obj, bool WorldObject)
        {
            use.SpellHasBeenCast = true;//stop later triggering of spells.
            if (WorldObject || (obj.classindex >= 0xC))
            {
                return true;
            }
            trigger.TriggerObjectLink(
                character: 1,
                ObjectUsed: obj,
                triggerType: (int)triggerObjectDat.triggertypes.USE,
                triggerX: obj.tileX,
                triggerY: obj.tileY,
                objList: playerdat.InventoryObjects);
            var spell = MagicEnchantment.GetSpellEnchantment(obj, playerdat.InventoryObjects);
            if ((spell != null) && spell.IsFlagBit2Set && MagicEnchantment.CastObjectSpell(obj, spell))
            {
                if (spell.LinkedSpellObject != null)
                {
                    MagicEnchantment.DecrementSpellQuality(
                        objList: playerdat.InventoryObjects,
                        WorldObject: WorldObject,
                        parentObject: obj,
                        spell: spell);
                }
            }
            if (obj.is_quant == 0)
            {
                var spellObject = (obj.link == 0) ? null : objectsearch.FindMatchInObjectChain(
                    ListHeadIndex: obj.link, majorclass: 4, minorclass: 2, classindex: 0,
                    objList: playerdat.InventoryObjects, SkipLinks: true);
                if (spellObject == null)
                {//no spell left (or never one): it cracks
                    uimanager.AddToMessageScroll(GameStrings.GetDisplayString(1, GameStrings.str_with_a_loud_snap_the_wand_cracks_));
                    obj.item_id += 4;
                    uimanager.UpdateInventoryDisplay();
                }
            }
            return true;
        }
    }//end class
}//end namespace
