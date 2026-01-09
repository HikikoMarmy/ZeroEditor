using System.Text;
using ZeroEditor.Game;

namespace ZeroEditor.Iso
{
    public sealed class IsoExtractor
    {
        private readonly IGameExtractor _ff1 = new Zero1Extractor();
        private readonly IGameExtractor _ff2 = new Zero2Extractor();
        private readonly IGameExtractor _ff3 = new Zero3Extractor();

        public async Task ExtractAllAsync(
            string isoPath,
            string zeroFileDictionaryPath,
            string outputProjectRoot,
            IProgress<string>? log,
            CancellationToken ct = default)
        {
            var layout = ProjectLayout.FromProjectRoot(outputProjectRoot);
            var filesRoot = layout.FilesRoot;

            Directory.CreateDirectory(layout.ProjectRoot);
            Directory.CreateDirectory(filesRoot);

            using var iso = IsoFs.Open(isoPath);

            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var sysCnf = iso.ReadText("SYSTEM.CNF");
            string? systemCnfPath = null;
            if (sysCnf is not null)
            {
                var relSys = "SYSTEM.CNF";
                systemCnfPath = Path.Combine(filesRoot, relSys);
                await File.WriteAllTextAsync(systemCnfPath, sysCnf, Encoding.ASCII, ct);
                log?.Report("ISO: SYSTEM.CNF -> SYSTEM.CNF");
                written.Add(relSys.Replace('/', Path.DirectorySeparatorChar));
            }

            var boot = iso.GetBootPath();
            var serial = (boot?.Split(';')[0]) ?? DetectSerialFallback(sysCnf)
                         ?? throw new InvalidDataException("Could not detect game serial.");

            if (boot is not null && iso.TryGetEntry(boot, out var elf))
            {
                var elfRel = serial.Replace('/', Path.DirectorySeparatorChar);
                var elfOutPath = Path.Combine(filesRoot, elfRel);
                await File.WriteAllBytesAsync(elfOutPath, iso.ReadFile(boot), ct);
                log?.Report($"ISO: {elf.NameRaw} -> {serial}");
                written.Add(elfRel);
            }

            foreach (var entry in iso.EnumerateAll()
                .Where(e => !e.IsDir)
                .OrderBy(e => e.Lba)
                .ThenBy(e => e.PathRaw))
            {
                var relNoVer = entry.PathNoVer.Replace('/', Path.DirectorySeparatorChar);
                if (!written.Add(relNoVer))
                    continue;

                var outPath = Path.Combine(filesRoot, relNoVer);

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

                    await using var dst = new FileStream(
                        outPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        128 * 1024,
                        useAsync: true);

                    iso.CopyFileTo(entry.PathRaw, dst, bufferSize: 128 * 1024, ct);

                    log?.Report($"ISO: {isoPath} -> {Path.GetRelativePath(filesRoot, outPath)}");
                }
                catch (Exception ex)
                {
                    log?.Report($"ERROR: extracting {isoPath} failed — {ex.GetType().Name}: {ex.Message}");
                }
            }

            GameContext ctx;
            if (!string.IsNullOrEmpty(systemCnfPath) && File.Exists(systemCnfPath))
                ctx = GameContextFactory.FromSystemCnf(systemCnfPath);
            else
                ctx = GameContextFactory.FromSerial(serial);

            var fileTableForManifest = (ctx.Key.Game == GameId.FF3)
                ? BuildZero3FileTableFromFhd(iso)
                : ctx.FileTable;

            if (fileTableForManifest == null || fileTableForManifest.Count == 0)
                throw new InvalidDataException("Manifest file_table is empty — could not read names.");

            var manifest = ManifestJson.Create(
                ctx.Key.Serial,
                ctx.ElfName,
                fileTableForManifest,
                ctx.Key.Game,
                ctx.Key.Region);

            manifest.IsoToc = BuildIsoToc(iso);
            manifest.IsoLayout = ComputeIsoLayout(iso, isoPath);
            manifest.DirOrder = BuildDirOrder(iso);

            var manifestPath = layout.ManifestPath;
            await ManifestJson.SaveAsync(manifestPath, manifest, ct);
            log?.Report($"Wrote {Path.GetFileName(manifestPath)} (with iso_toc + iso_layout).");

            IGameExtractor extractor = ctx.Key.Game switch
            {
                GameId.FF1 => _ff1,
                GameId.FF2 => _ff2,
                GameId.FF3 => _ff3,
                _ => throw new NotSupportedException($"Game '{ctx.Key.Game}' not yet implemented.")
            };

            await using (var fs = new FileStream(isoPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                await extractor.ExtractBinAsync(fs, ctx, layout.ProjectRoot, log, ct);

            LogLbaMap(iso, isoPath, log);
        }

        private static List<string> BuildZero3FileTableFromFhd(IsoFs iso)
        {
            if (!TryGetAny(iso, out var fhd, "ZERO3.FHD;1"))
                return new List<string>();

            var fhdBytes = iso.ReadFile(fhd.PathRaw);
            var entries = FhdParser.Parse(fhdBytes);

            static string NormFolder(string s)
                => s.Replace("..", "").Replace('\\', '/').Trim('/');

            var list = new List<string>(entries.Count);
            foreach (var e in entries)
            {
                var folder = NormFolder(e.Folder);
                var rel = string.IsNullOrEmpty(folder) ? e.Name : $"{folder}/{e.Name}";
                list.Add(rel);
            }
            return list;
        }

        private static bool TryGetAny(IsoFs iso, out IsoFs.Entry? e, params string[] names)
        {
            e = null;
            foreach (var n in names)
                if (iso.TryGetEntry(n, out var hit) && !hit.IsDir)
                {
                    e = hit;
                    return true;
                }
            return false;
        }

        private static List<ManifestJson.IsoTocEntry> BuildIsoToc(IsoFs iso)
        {
            return iso.EnumerateAll()
                .Where(e => !e.IsDir)
                .OrderBy(e => e.Lba).ThenBy(e => e.NameRaw)
                .Select(e => new ManifestJson.IsoTocEntry(
                    Path: e.PathRaw.Replace('/', '\\'),
                    Lba: e.Lba,
                    Size: e.Length,
                    Flags: e.Flags))
                .ToList();
        }

        private static ManifestJson.IsoLayoutSummary ComputeIsoLayout(IsoFs iso, string isoPath)
        {
            TryGetAny(iso, out var imgBd, "IMG_BD.BIN;1", "IMG_BD.BIN");
            TryGetAny(iso, out var imgHd, "IMG_HD.BIN;1", "IMG_HD.BIN");

            var files = iso.EnumerateAll().Where(e => !e.IsDir).OrderBy(e => e.Lba).ToList();
            long isoLen = new FileInfo(isoPath).Length;

            string? nextName = null;
            int? nextLba = null;
            bool isLast = false;
            long gap = 0;

            if (imgBd != null)
            {
                var next = files.FirstOrDefault(f => f.Lba > imgBd.Lba);
                long bdStart = (long)imgBd.Lba * IsoFs.SectorSize;
                long bdEnd = bdStart + imgBd.Length;

                if (next == null)
                {
                    isLast = true;
                    gap = Math.Max(0, isoLen - bdEnd);
                }
                else
                {
                    nextName = next.NameRaw;
                    nextLba = next.Lba;
                    long nextStart = (long)next.Lba * IsoFs.SectorSize;
                    gap = Math.Max(0, nextStart - bdEnd);
                }
            }

            return new ManifestJson.IsoLayoutSummary(
                ImgBdPath: imgBd?.NameRaw,
                ImgBdLba: imgBd?.Lba,
                ImgBdSize: imgBd?.Length,
                ImgBdIsLast: isLast,
                GapAfterImgBdBytes: gap,
                NextFileAfterImgBd: nextName,
                NextFileAfterImgBdLba: nextLba,
                ImgHdPath: imgHd?.NameRaw,
                ImgHdLba: imgHd?.Lba,
                ImgHdSize: imgHd?.Length);
        }

        private static Dictionary<string, List<string>> BuildDirOrder(IsoFs iso)
        {
            static string KeyNorm(string p) => p.Replace('/', '\\').Trim('\\').ToUpperInvariant();

            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var q = new Queue<string>();
            q.Enqueue("");

            while (q.Count > 0)
            {
                var dirPath = q.Dequeue();

                var children = iso.EnumerateChildren(dirPath).ToList();

                map[KeyNorm(dirPath)] = children.Select(c => c.NameRaw).ToList();

                foreach (var d in children.Where(c => c.IsDir))
                {
                    var childPath = string.IsNullOrEmpty(dirPath) ? d.NameRaw : (dirPath + "\\" + d.NameRaw);
                    q.Enqueue(childPath);
                }
            }

            return map;
        }

        public static void LogLbaMap(IsoFs iso, string isoPath, IProgress<string>? log)
        {
            if (log is null)
                return;

            var files = iso.EnumerateAll().Where(e => !e.IsDir)
                .OrderBy(e => e.Lba).ThenBy(e => e.NameRaw).ToList();

            TryGetAny(iso, out var imgBd, "IMG_BD.BIN;1", "IMG_BD.BIN");

            long isoLen = new FileInfo(isoPath).Length;

            log.Report($"--- ISO LBA MAP (sectors=2048) for {Path.GetFileName(isoPath)} ---");
            log.Report($"{"LBA",10}  {"Offset",12}  {"Length",12}  {"End",12}  Flg  Name");

            foreach (var f in files)
            {
                long off = (long)f.Lba * IsoFs.SectorSize;
                long end = off + f.Length;
                log.Report($"{f.Lba,10}  0x{off:X10}  0x{f.Length:X8}  0x{end:X10}  0x{f.Flags:X2}  {f.NameRaw}");
            }

            log.Report("--- IMG_BD summary ---");
            if (imgBd is null)
            {
                log.Report("IMG_BD not found by common names (IMG_BD.BIN/IMG_BD/IMG.BD).");
                return;
            }

            var nextAfterBd = files.FirstOrDefault(f => f.Lba > imgBd.Lba);
            long bdStart = (long)imgBd.Lba * IsoFs.SectorSize;
            long bdEnd = bdStart + imgBd.Length;

            if (nextAfterBd is null)
            {
                long headroom = Math.Max(0, isoLen - bdEnd);
                log.Report($"IMG_BD = {imgBd.NameRaw} @ LBA {imgBd.Lba} (last file). Headroom to end-of-image: {headroom} bytes (0x{headroom:X}).");
            }
            else
            {
                long nextStart = (long)nextAfterBd.Lba * IsoFs.SectorSize;
                long gap = Math.Max(0, nextStart - bdEnd);
                log?.Report($"IMG_BD = {imgBd.NameRaw} @ LBA {imgBd.Lba}. Next: {nextAfterBd.NameRaw} @ LBA {nextAfterBd.Lba}. Gap after IMG_BD: {gap} bytes (0x{gap:X}).");
            }
        }

        private static string? DetectSerialFallback(string? sysCnf)
        {
            if (string.IsNullOrEmpty(sysCnf))
                return null;

            foreach (var tag in new[] { "SLES_", "SLUS_", "SLPS_" })
            {
                int p = sysCnf.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
                if (p >= 0 && p + 12 <= sysCnf.Length)
                    return sysCnf.Substring(p, 12).Trim('\0', ' ', '\r', '\n');
            }
            return null;
        }
    }
}
