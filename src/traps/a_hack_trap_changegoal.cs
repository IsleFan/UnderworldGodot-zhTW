using System.Diagnostics;

namespace Underworld
{
    /// <summary>
    /// Trap that changes the goal of the linked npc. Only known instance is an NPC triggered instance where LBs goal is changed when avatar is put in jail.
    /// </summary>
    public class a_hack_trap_changegoal : trap
    {
        public static void Activate(uwObject trapObj, int character)
        {
            Debug.Print("UNTESTED HACK TRAP CHANGE GOAL. HAS LORD BRITISH TRIGGERED THIS?");
            //hack 62: when the trap links to whoever set it off, or link is 1, it is the one who set it off that gets the goal
            if ((trapObj.link != character) && (trapObj.link != 1))
            {
                return;
            }
            var triggeringObj = UWTileMap.current_tilemap.LevelObjects[character];
            Debug.Print($"Running change goal to {trapObj.owner} on {triggeringObj.a_name}");
            triggeringObj.npc_goal = (byte)trapObj.owner;
        }
    }
}

