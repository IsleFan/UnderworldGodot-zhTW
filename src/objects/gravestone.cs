using Godot;

namespace Underworld
{
    public class gravestone : model3D
    {

        public static bool Use(uwObject obj)
        {
            DisplayGrave(obj);
            return true;
        }

        /// <summary>
        /// Looking at a gravestone (RectLook): its picture number comes from DATA\GRAVE.DAT, indexed by its text
        /// number (the link of a quantity object, else the owner). Without a picture the inscription follows
        /// "The gravestone reads: "; with one UW1 also shows the picture in the window (cutscene 101h, frame
        /// picture). Nothing at all when GRAVE.DAT has no such entry.
        /// </summary>
        private static void DisplayGrave(uwObject obj)
        {
            var index = obj.is_quant == 1 ? obj.link & 0x1FF : obj.owner;
            if (!Loader.ReadStreamFile(System.IO.Path.Combine(BasePath, "DATA", "GRAVE.DAT"), out byte[] graves)
                || index >= graves.Length)
            {
                return;
            }
            var picture = graves[index];
            if (picture != 0)
            {//the picture replaces what the scroll showed
                uimanager.instance.scroll.Clear();
            }
            if (picture == 0)
            {
                uimanager.NextOutputPrependedString = GameStrings.GetDisplayString(0x1160 + obj.flags);//The gravestone reads:
            }
            if ((_RES != GAME_UW2) || (index != 0) || !HasLinkedTrap(obj.index))
            {//UW2: inscription 0 is not printed when a trap 6/2/3 follows (find_obj from the gravestone on)
                uimanager.AddToMessageScroll(GameStrings.GetDisplayString(8, index));
            }
            else if (picture == 0)
            {
                uimanager.AddToMessageScroll(uimanager.NextOutputPrependedString);
                uimanager.NextOutputPrependedString = "";
            }
            if (picture != 0 && _RES != GAME_UW2)
            {
                uimanager.DisplayCutsImage("cs401.n01", picture - 1, uimanager.CutsSmall);
            }
        }

        /// <summary>
        /// find_obj(6, 2, 3) from the gravestone: depth first through it, what it holds and the objects after it in its list.
        /// </summary>
        static bool HasLinkedTrap(int index)
        {
            var objects = UWTileMap.current_tilemap.LevelObjects;
            while (index != 0)
            {
                var o = objects[index];
                if ((o.majorclass == 6) && (o.minorclass == 2) && (o.classindex == 3))
                {
                    return true;
                }
                if ((o.is_quant == 0) && (o.link != 0) && HasLinkedTrap(o.link))
                {
                    return true;
                }
                index = o.next;
            }
            return false;
        }

        public static gravestone CreateInstance(Node3D parent, uwObject obj, string name)
        {
            var g = new gravestone(obj);
            var modelNode = g.Generate3DModel(parent, name);
            SetModelRotation(parent, g);
            //DisplayModelPoints(g, parent);
            return g;
        }

        public gravestone(uwObject _uwobject)
        {
            uwobject = _uwobject;
        }

        public override int NoOfMeshes()
        {
            return 2;
        }

        public override Vector3[] ModelVertices()
        {
            Vector3[] ModelVerts = new Vector3[8];
            ModelVerts[0] = new Vector3(-0.125f, 0f, -0.015625f);
            ModelVerts[1] = new Vector3(-0.125f, 0.375f, -0.015625f);
            ModelVerts[2] = new Vector3(0.125f, 0.375f, -0.015625f);
            ModelVerts[3] = new Vector3(0.125f, 0f, -0.015625f);
            ModelVerts[4] = new Vector3(-0.125f, 0f, 0.015625f);
            ModelVerts[5] = new Vector3(-0.125f, 0.375f, 0.015625f);
            ModelVerts[6] = new Vector3(0.125f, 0.375f, 0.015625f);
            ModelVerts[7] = new Vector3(0.125f, 0f, 0.015625f);
            return ModelVerts;
        }

        public override int[] ModelTriangles(int meshNo)
        {
            switch (meshNo)
            {
                case 0://headstone
                    return new int[] { 4, 5, 6, 6, 7, 4, 3, 2, 1, 1, 0, 3 };
                case 1://trim
                    return new int[] { 0, 1, 5, 5, 4, 0
                                        , 1, 2, 6, 6, 5, 1, 
                                        7, 6, 2, 2, 3, 7, 
                                        0, 4, 7, 7, 3, 0 };
            }
            return base.ModelTriangles(meshNo);
        }

        public override Vector2[] ModelUVs(Vector3[] verts)
        {
            Vector2[] ModelUvs = new Vector2[8];
            ModelUvs[0] = new Vector2(1f, 1f);
            ModelUvs[1] = new Vector2(1f, 0.3f);
            ModelUvs[2] = new Vector2(0f, 0.3f);
            ModelUvs[3] = new Vector2(0f, 1f);
            ModelUvs[4] = new Vector2(0f, 1f);
            ModelUvs[5] = new Vector2(0f, 0.3f);
            ModelUvs[6] = new Vector2(1f, 0.3f);
            ModelUvs[7] = new Vector2(1f, 1f);
            return ModelUvs;
        }

        public override ShaderMaterial GetMaterial(int textureno, int surface)
        {
            switch (surface)
            {
                case 0://headstone
                    return GetTmObj.GetMaterialForObject(uwobject.flags + 28, uwobject);
                case 1://Trim
                    return GetTmObj.GetMaterialForObject(0, uwobject);
            }
            return base.GetMaterial(textureno,surface);
        }
    }//end class
}//end namespace