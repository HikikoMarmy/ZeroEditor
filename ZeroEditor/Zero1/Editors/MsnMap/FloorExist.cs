namespace ZeroEditor.Zero1.Editors.MsnMap
{
    public static class FloorExist
    {
        public static readonly bool[,] Exist = new bool[5, 4]
        {
            { false, true,  true,  false }, // msn00 Intro
            { false, true,  true,  false }, // msn01 Night 1
            { true,  true,  true,  false }, // msn02 Night 2
            { true,  true,  true,  true  }, // msn03 Night 3
            { true,  true,  true,  true  }, // msn04 Night 4
        };

        public static bool IsFloorAvailable(int mission, int floor)
        {
            if ((uint)mission >= 5 || (uint)floor >= 4)
                return false;
            return Exist[mission, floor];
        }
    }
}
