namespace ZeroEditor.Zero1.Editors.MsnMap
{
    internal static class MapAtlasTransform
    {
        private const float ZFactor = 0.99f;
        private const float WorldToMapX = 1f / 100f;
        private const float WorldToMapZ = ZFactor / 100f;
        private const float MapHeight = 512f;

        public const float Sx = 0.98285f;
        public const float Sy = 0.99626f;
        public const float Tx = -4.5f;
        public const float Ty = 5.5f;

        public static bool TryFit(
            MsnFloor floor,
            byte atlasFloorId,
            out float sx,
            out float sy,
            out float tx,
            out float ty)
        {
            sx = Sx;
            sy = Sy;
            tx = Tx;
            ty = Ty;
            return IsValidScale(sx) && IsValidScale(sy);
        }

        private static bool IsValidScale(float s)
            => s > 0.00001f && !float.IsNaN(s) && !float.IsInfinity(s);

        public static float GetWorldToMapX() => WorldToMapX;
        public static float GetWorldToMapZ() => WorldToMapZ;
        public static float GetMapHeight() => MapHeight;
    }
}
