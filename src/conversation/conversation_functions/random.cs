namespace Underworld
{
    public partial class ConversationVM:UWClass
    {
        /// <summary>
        /// Returns a random number
        /// </summary>
        public static void Random()
        {
            var max = GetConvoStackValueAtPtr(stack + stackptr - 1);  
            result_register = Rng.r.NextScaled(max) + 1;//rand() * max / 0x8000 + 1 (BabRand)
            return;
        }
    } 
}