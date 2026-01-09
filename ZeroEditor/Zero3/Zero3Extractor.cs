using System.Text;
using ZeroEditor.Compress;
using ZeroEditor.Game;

namespace ZeroEditor
{
    public sealed class Zero3Extractor : IGameExtractor
    {
        private static readonly ZeroLess Less = ZeroLess.Instance;
        private const int SectorSize = 2048;
        private const bool WriteDebugImgMap = false;

        public async Task ExtractBinAsync(
            FileStream isoStream,
            GameContext ctx,
            string outputProjectRoot,
            IProgress<string>? log,
            CancellationToken ct)
        {
            var filesRoot = ProjectLayout.ResolveFilesRoot(outputProjectRoot);
            var manifestPath = ProjectLayout.ResolveManifestPath(outputProjectRoot);

            long prevPos = isoStream.Position;
            try
            {
                using var iso = IsoFs.Wrap(isoStream);

                // FHD names (with and without ;1, plus common alternates)
                if (!TryGetAny(iso, out var fhd,
                        "ZERO3.FHD;1", "ZERO3.FHD",
                        "FILELIST.FHD;1", "FILELIST.FHD",
                        "IMG_FHD.BIN;1", "IMG_FHD.BIN",
                        "IMG.FHD;1", "IMG.FHD"))
                    throw new FileNotFoundException(
                        "Zero3 FHD file not found (ZERO3.FHD / FILELIST.FHD / IMG_FHD.BIN / IMG.FHD).");

                if (!TryGetAny(iso, out var imgBd,
                        "IMG_BD.BIN;1", "IMG_BD.BIN",
                        "IMG_BD;1", "IMG_BD",
                        "IMG.BD;1", "IMG.BD"))
                    throw new FileNotFoundException(
                        "IMG_BD file not found (IMG_BD.BIN / IMG_BD / IMG.BD).");

                var fhdBytes = iso.ReadFile(fhd.PathRaw);
                var entries = FhdParser.Parse(fhdBytes);

                Directory.CreateDirectory(filesRoot);

                log?.Report($"FF3: FHD '{fhd.NameRaw}' -> {entries.Count} entries.");
                log?.Report($"FF3: IMG_BD '{imgBd.NameRaw}' @ LBA {imgBd.Lba} (offset 0x{imgBd.Offset:X}, size=0x{imgBd.Length:X}).");

                long imgStart = imgBd.Offset;
                long imgEnd = imgStart + imgBd.Length;

                var existsFlags = new bool[entries.Count];
                var cmpFlags = new bool[entries.Count];

                if (WriteDebugImgMap)
                {
                    string mapPath = Path.Combine(outputProjectRoot, "img_map_original.csv");
                    var mapSb = new StringBuilder();
                    mapSb.AppendLine("index,lba,dec_size,cmp_size,compressed,has_data,rel_path");

                    for (int i = 0; i < entries.Count; i++)
                    {
                        var e = entries[i];
                        existsFlags[i] = !e.HasNoDataFlag;
                        cmpFlags[i] = e.IsCompressed;

                        string folderMeta = SanitizeFolder(e.Folder);
                        string relMeta = Path.Combine(folderMeta, e.Name)
                            .Replace('/', Path.DirectorySeparatorChar)
                            .Replace('\\', Path.DirectorySeparatorChar);

                        uint lba = (uint)(e.LbaOffsetBytes / SectorSize);

                        mapSb.Append(i).Append(',')
                            .Append(lba).Append(',')
                            .Append(e.DecompressedSize).Append(',')
                            .Append(e.CompressedSize).Append(',')
                            .Append(e.IsCompressed ? "1" : "0").Append(',')
                            .Append(e.HasNoDataFlag ? "0" : "1").Append(',')
                            .AppendLine(relMeta.Replace('\\', '/'));
                    }

                    await File.WriteAllTextAsync(mapPath, mapSb.ToString(), ct);
                    log?.Report($"FF3 original map: {Path.GetRelativePath(outputProjectRoot, mapPath)}");
                }
                else
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        existsFlags[i] = !entries[i].HasNoDataFlag;
                        cmpFlags[i] = entries[i].IsCompressed;
                    }
                }

                TryUpdateManifestFlags(manifestPath, entries.Count, existsFlags, cmpFlags);

                for (int i = 0; i < entries.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var e = entries[i];

                    string folder = SanitizeFolder(e.Folder);
                    string rel = Path.Combine(folder, e.Name)
                                 .Replace('/', Path.DirectorySeparatorChar)
                                 .Replace('\\', Path.DirectorySeparatorChar);

                    string outPath = Path.Combine(filesRoot, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

                    long sliceStart = imgStart + e.LbaOffsetBytes;
                    long sliceSize = Math.Max(0, e.CompressedSize);
                    long sliceEnd = sliceStart + sliceSize;

                    if (e.HasNoDataFlag || (sliceSize == 0 && e.DecompressedSize == 0))
                    {
                        await File.WriteAllBytesAsync(outPath, Array.Empty<byte>(), ct);
                        log?.Report($"FF3 BIN: [{i + 1}/{entries.Count}] off=0x{sliceStart:X8} size=0 -> {rel} (empty, LBA flag={e.HasNoDataFlag})");
                        continue;
                    }

                    if (sliceStart < imgStart || sliceStart >= imgEnd)
                    {
                        log?.Report($"FF3 WARN: skip idx={i} ({rel}) off=0x{sliceStart:X} outside IMG_BD [0x{imgStart:X},0x{imgEnd:X}). LBAflag={e.HasNoDataFlag}");
                        continue;
                    }

                    if (sliceEnd > imgEnd)
                    {
                        long room = imgEnd - sliceStart;
                        log?.Report($"FF3 WARN: idx={i} ({rel}) wants 0x{sliceSize:X} @ 0x{sliceStart:X}, only 0x{room:X} remain. Skipping.");
                        continue;
                    }

                    isoStream.Position = sliceStart;

                    try
                    {
                        if (e.IsCompressed)
                        {
                            var cmp = new byte[sliceSize];
                            await ReadExactlyAsync(isoStream, cmp, 0, cmp.Length, ct);

                            if (!Less.TryDecompress(cmp, out var dec))
                                throw new InvalidDataException($"LESS decode failed (cmp=0x{sliceSize:X}).");

                            if (dec.Length != e.DecompressedSize)
                                throw new InvalidDataException($"LESS size mismatch: dec=0x{dec.Length:X}, expected=0x{e.DecompressedSize:X}");

                            await File.WriteAllBytesAsync(outPath, dec, ct);
                            log?.Report($"FF3 BIN: [{i + 1}/{entries.Count}] off=0x{sliceStart:X8} cmp=0x{sliceSize:X8} -> {rel} (dec)");
                        }
                        else
                        {
                            await CopyToFileAsync(isoStream, outPath, (uint)sliceSize, ct);
                            log?.Report($"FF3 BIN: [{i + 1}/{entries.Count}] off=0x{sliceStart:X8} size=0x{sliceSize:X8} -> {rel}");
                        }

                        if (outPath.EndsWith(".pk4", StringComparison.OrdinalIgnoreCase))
                        {
                            await TryExplodePK4Async(outPath, filesRoot, log, ct);
                        }
                        else if (outPath.EndsWith(".phf", StringComparison.OrdinalIgnoreCase))
                        {
                            await TryExplodePHFAsync(outPath, filesRoot, log, ct);
                        }
                        else
                        {
                            try { await TryExplodePHFAsync(outPath, filesRoot, log: null, ct); }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        log?.Report(
                            $"FF3 ERROR: idx={i} ({rel}) @ off=0x{sliceStart:X} size=0x{sliceSize:X} " +
                            $"IMG_BD=[0x{imgStart:X},0x{imgEnd:X}) — {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            finally
            {
                isoStream.Position = prevPos;
            }
        }

        private static void TryUpdateManifestFlags(
            string manifestPath,
            int count,
            bool[] existsFlags,
            bool[] cmpFlags)
        {
            if (!File.Exists(manifestPath))
                return;

            var manifest = ManifestJson.Load(manifestPath);

            if (manifest.FileTable is { Count: > 0 } fileTable)
            {
                int n = Math.Min(fileTable.Count, count);
                for (int i = 0; i < n; i++)
                {
                    var meta = fileTable[i];
                    if (meta == null)
                        continue;

                    fileTable[i] = new ManifestJson.FileEntry
                    {
                        Path = meta.Path,
                        Exists = existsFlags[i],
                        Compressed = cmpFlags[i]
                    };
                }
            }

            ManifestJson.Save(manifestPath, manifest);
        }

        private static bool TryGetAny(IsoFs iso, out IsoFs.Entry? e, params string[] names)
        {
            e = null;
            foreach (var n in names)
            {
                if (iso.TryGetEntry(n, out var hit) && !hit.IsDir)
                {
                    e = hit;
                    return true;
                }
            }
            return false;
        }

        private static string SanitizeFolder(string folder)
        {
            return folder.Replace("..", "")
                         .Replace('/', Path.DirectorySeparatorChar)
                         .Replace('\\', Path.DirectorySeparatorChar)
                         .Trim(Path.DirectorySeparatorChar);
        }

        private static async Task CopyToFileAsync(Stream src, string outPath, uint size, CancellationToken ct)
        {
            const int B = 128 * 1024;
            var buf = new byte[B];
            long left = size;

            await using var dst = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, B, useAsync: true);

            while (left > 0)
            {
                int want = (int)Math.Min(B, left);
                int filled = 0;

                while (filled < want)
                {
                    int r = await src.ReadAsync(buf.AsMemory(filled, want - filled), ct);
                    if (r == 0)
                        throw new EndOfStreamException($"EOF while copying: wanted 0x{want:X}, got 0x{filled:X}. src.Position=0x{src.Position:X}");
                    filled += r;
                }

                await dst.WriteAsync(buf.AsMemory(0, filled), ct);
                left -= filled;
            }
        }

        private static async Task ReadExactlyAsync(Stream s, byte[] b, int o, int c, CancellationToken ct)
        {
            int t = 0;
            while (t < c)
            {
                int r = await s.ReadAsync(b.AsMemory(o + t, c - t), ct);
                if (r == 0)
                    throw new EndOfStreamException($"EOF: wanted 0x{c:X}, got 0x{t:X}");
                t += r;
            }
        }

        private static async Task TryExplodePK4Async(string pk4Path, string filesRoot, IProgress<string>? log, CancellationToken ct)
        {
            byte[] buffer;
            try
            {
                buffer = await File.ReadAllBytesAsync(pk4Path, ct);
            }
            catch (Exception ex)
            {
                log?.Report($"PK4: could not read ({ex.Message}), kept as file: {Path.GetRelativePath(filesRoot, pk4Path)}");
                return;
            }

            string folder = Path.Combine(Path.GetDirectoryName(pk4Path)!, Path.GetFileNameWithoutExtension(pk4Path) + "_pk4");

            try
            {
                using (var msDetect = new MemoryStream(buffer, writable: false))
                {
                    var _ = Archive.PK4.Pk4Archive.Open(msDetect);
                }

                Directory.CreateDirectory(folder);

                using (var msExtract = new MemoryStream(buffer, writable: false))
                    Archive.PK4.Pk4Archive.ExtractAllTo(msExtract, folder);

                log?.Report($"PK4: exploded -> {Path.GetRelativePath(filesRoot, folder)}/ ({Directory.GetFiles(folder).Length} files)");

                await DeleteWithRetryAsync(pk4Path, attempts: 5, delayMs: 60, ct);
            }
            catch (Exception ex)
            {
                log?.Report($"PK4: failed to explode ({ex.Message}), kept as file: {Path.GetRelativePath(filesRoot, pk4Path)}");
            }
        }

        private static async Task TryExplodePHFAsync(string phfPath, string filesRoot, IProgress<string>? log, CancellationToken ct)
        {
            try
            {
                await using var head = new FileStream(phfPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4, useAsync: true);
                var sig = new byte[4];
                int r = await head.ReadAsync(sig.AsMemory(0, sig.Length), ct);
                if (r < 4)
                    return;

                if (BitConverter.ToUInt32(sig, 0) != 0x00666870)
                    return;
            }
            catch
            {
                return;
            }

            byte[] buffer;
            try
            {
                buffer = await File.ReadAllBytesAsync(phfPath, ct);
            }
            catch (Exception ex)
            {
                log?.Report($"PHF: could not read ({ex.Message}), kept as file: {Path.GetRelativePath(filesRoot, phfPath)}");
                return;
            }

            string folder = Path.Combine(
                Path.GetDirectoryName(phfPath)!,
                Path.GetFileNameWithoutExtension(phfPath) + "_phf");

            try
            {
                Directory.CreateDirectory(folder);
                using (var ms = new MemoryStream(buffer, writable: false))
                {
                    int written = Archive.PHF.PhfArchive.ExtractAllTo(ms, folder);
                    if (log is not null)
                        log.Report($"PHF: exploded -> {Path.GetRelativePath(filesRoot, folder)}/ ({written} files)");
                }

                await DeleteWithRetryAsync(phfPath, attempts: 5, delayMs: 60, ct);
            }
            catch (Exception ex)
            {
                log?.Report($"PHF: failed to explode ({ex.Message}), kept as file: {Path.GetRelativePath(filesRoot, phfPath)}");
            }
        }

        private static async Task DeleteWithRetryAsync(string path, int attempts, int delayMs, CancellationToken ct)
        {
            for (int i = 0; i < attempts; i++)
            {
                try { File.Delete(path); return; }
                catch (IOException) when (i < attempts - 1) { await Task.Delay(delayMs, ct); }
                catch (UnauthorizedAccessException) when (i < attempts - 1) { await Task.Delay(delayMs, ct); }
            }
            File.Delete(path);
        }
    }

    internal static class FhdParser
    {
        internal sealed class Entry
        {
            public int Id;
            public string Folder = "";
            public string Name = "";
            public long LbaOffsetBytes;
            public int CompressedSize;
            public int DecompressedSize;
            public bool IsCompressed;
            public bool HasNoDataFlag;
        }

        public static List<Entry> Parse(byte[] fhd)
        {
            using var br = new BinaryReader(new MemoryStream(fhd, writable: false), Encoding.ASCII, leaveOpen: false);

            uint sig = br.ReadUInt32();
            if (sig != 0x46484400 && sig != 0x00444846)
                throw new InvalidDataException("Not an FHD file.");

            br.BaseStream.Seek(8, SeekOrigin.Begin);
            uint baseHeaderSize = br.ReadUInt32();
            int count = br.ReadInt32();
            uint decompressedSizeTableOffset = br.ReadUInt32();
            uint reservedBlockOffset = br.ReadUInt32();
            uint compressionInfoTableOffset = br.ReadUInt32();
            uint namePointerTableOffset = br.ReadUInt32();
            uint lbaTableOffset = br.ReadUInt32();

            const int SectorSize = 2048;
            var list = new List<Entry>(count);

            for (int i = 0; i < count; i++)
            {
                var e = new Entry { Id = i };

                br.BaseStream.Seek(namePointerTableOffset + i * 8, SeekOrigin.Begin);
                uint folderPtr = br.ReadUInt32();
                uint filePtr = br.ReadUInt32();

                br.BaseStream.Seek(folderPtr, SeekOrigin.Begin);
                e.Folder = ReadAsciiZ(br, 64);

                br.BaseStream.Seek(filePtr, SeekOrigin.Begin);
                e.Name = ReadAsciiZ(br, 64);

                br.BaseStream.Seek(lbaTableOffset + i * 4, SeekOrigin.Begin);
                uint lbaField = br.ReadUInt32();
                e.HasNoDataFlag = (lbaField & 0x8000_0000) != 0;
                uint lba = lbaField & 0x7FFF_FFFF;
                e.LbaOffsetBytes = (long)lba * SectorSize;

                br.BaseStream.Seek(decompressedSizeTableOffset + i * 4, SeekOrigin.Begin);
                e.DecompressedSize = br.ReadInt32();

                br.BaseStream.Seek(compressionInfoTableOffset + i * 4, SeekOrigin.Begin);
                int compressionInfoField = br.ReadInt32();
                e.IsCompressed = (compressionInfoField & 1) == 1;
                e.CompressedSize = (int)((uint)compressionInfoField >> 1);

                list.Add(e);
            }

            return list;
        }

        private static string ReadAsciiZ(BinaryReader br, int maxBytes)
        {
            var buf = br.ReadBytes(maxBytes);
            int n = Array.IndexOf<byte>(buf, 0);
            if (n < 0)
                n = buf.Length;
            return Encoding.ASCII.GetString(buf, 0, n);
        }
    }
}
