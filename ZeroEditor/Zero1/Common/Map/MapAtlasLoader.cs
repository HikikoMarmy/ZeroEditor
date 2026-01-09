using ZeroEditor.Tim2;

namespace ZeroEditor.Zero1.Common.Map
{
    public static class MapAtlasLoader
    {
        public const string DefaultAtlasTm2RelPath =
            @"files\bin\tim\pl_smap_e_pk2_index\0005.tm2";

        public static Bitmap LoadAtlasFromExtractedIso(string extractedRootPath, int clutSet = 0, bool halfAlpha = true)
        {
            if (string.IsNullOrWhiteSpace(extractedRootPath))
                throw new ArgumentException("Root path is null/empty.", nameof(extractedRootPath));

            var fullPath = Path.Combine(extractedRootPath, DefaultAtlasTm2RelPath);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Atlas TM2 not found at expected path.", fullPath);

            using var fs = File.OpenRead(fullPath);

            var tim2 = Tim2Image.Load(fs);

            var bmp = Tim2Decode.DecodeToBitmap(tim2, clutSet, halfAlpha);
            return bmp;
        }

        public static void ExportAtlasPng(string extractedRootPath, string outputPngPath, int clutSet = 0, bool halfAlpha = true)
        {
            using var bmp = LoadAtlasFromExtractedIso(extractedRootPath, clutSet, halfAlpha);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPngPath)!);
            bmp.Save(outputPngPath, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
