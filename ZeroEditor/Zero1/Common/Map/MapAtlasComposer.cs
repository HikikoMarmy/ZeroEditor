using System.Drawing.Drawing2D;

namespace ZeroEditor.Zero1.Common.Map
{
    public static class MapAtlasComposer
    {
        public static Bitmap BuildFloorComposite(Image atlas, byte floorId)
        {
            if (atlas == null) throw new ArgumentNullException(nameof(atlas));

            var rooms = MapRoomAtlas.EnumerateFloor(floorId).ToList();
            if (rooms.Count == 0)
                return new Bitmap(1, 1);

            int maxX = 0, maxY = 0;
            foreach (var r in rooms)
            {
                maxX = Math.Max(maxX, r.BasePos.X + r.UV.Width);
                maxY = Math.Max(maxY, r.BasePos.Y + r.UV.Height);
            }

            if (maxX <= 0) maxX = 1;
            if (maxY <= 0) maxY = 1;

            var bmp = new Bitmap(maxX, maxY, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.CompositingMode = CompositingMode.SourceOver;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;

                foreach (var r in rooms)
                {
                    var dst = new Rectangle(r.BasePos.X, r.BasePos.Y, r.UV.Width, r.UV.Height);
                    g.DrawImage(atlas, dst, r.UV, GraphicsUnit.Pixel);
                }
            }

            return bmp;
        }
    }
}
