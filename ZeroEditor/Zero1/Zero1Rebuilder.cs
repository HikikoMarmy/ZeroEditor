// Zero1Rebuilder.cs
using System.Buffers;
using System.Buffers.Binary;
using ZeroEditor.Game;

namespace ZeroEditor
{
    public sealed class Zero1Rebuilder : IGameRebuilder
    {
        public const int SectorSize = 2048;
        private const int DataAlign = 32 * 1024;

        public sealed class BuildResult
        {
            public string ImgHdPath { get; init; } = "";
            public string ImgBdPath { get; init; } = "";
            public uint FileCount { get; init; }
            public long ImgBdBytes { get; init; }
            public (uint StartSector, uint Size)[] Table { get; init; } = Array.Empty<(uint, uint)>();
        }

        public sealed class Options
        {
            public bool AllowMissing { get; init; } = true;
            public bool StrictSizes { get; init; } = true;
        }

        public async Task RebuildIsoAsync(
            string projectRoot,
            string outputIsoPath,
            IProgress<string>? log,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(projectRoot) || !Directory.Exists(projectRoot))
                throw new DirectoryNotFoundException(projectRoot);

            var layout = ProjectLayout.FromProjectRoot(projectRoot);

            if (!File.Exists(layout.ManifestPath))
                throw new FileNotFoundException("Manifest not found.", layout.ManifestPath);
            if (!File.Exists(layout.BaseIsoPath))
                throw new FileNotFoundException("Base ISO not found (expected _base.iso).", layout.BaseIsoPath);

            var ctx = GameContextFactory.FromManifest(layout.ManifestPath);
            if (ctx.Key.Game != GameId.FF1)
                throw new NotSupportedException($"Zero1Rebuilder expects FF1; got {ctx.Key.Game}.");

            int rebuiltPk2 = RebuildAllPk2Folders(layout.FilesRoot, log, ct);
            if (rebuiltPk2 > 0)
                log?.Report($"PK2: rebuilt {rebuiltPk2} archives.");

            Directory.CreateDirectory(layout.RebuildDir);

            var mf = ManifestJson.Load(layout.ManifestPath);
            if (mf.FileTable is null || mf.FileTable.Count == 0)
                throw new InvalidDataException("Manifest file_table is empty.");

            var order = mf.FileTable.Select(f => f.Path).ToList();

            log?.Report("Rebuilding IMG (img_hd/img_bd) from manifest...");
            var img = await BuildCoreAsync(
                filesRoot: layout.FilesRoot,
                fileOrder: order,
                outDir: layout.RebuildDir,
                options: new Options { AllowMissing = false, StrictSizes = true },
                log: log,
                ct: ct);

            log?.Report($"IMG built: img_bd=0x{img.ImgBdBytes:X} bytes");
            await BuildIsoRelocatingImgBdAsync(
                projectRoot: projectRoot,
                outputIsoPath: outputIsoPath,
                log: log,
                ct: ct);

            log?.Report("Rebuild complete.");
        }

        public async Task BuildIsoRelocatingImgBdAsync(
            string projectRoot,
            string outputIsoPath,
            IProgress<string>? log = null,
            CancellationToken ct = default)
        {
            var layout = ProjectLayout.FromProjectRoot(projectRoot);

            var baseIsoPath = layout.BaseIsoPath;
            var manifestPath = layout.ManifestPath;
            var rebuildDir = layout.RebuildDir;

            var rebuiltImgHdPath = Path.Combine(rebuildDir, "img_hd.bin");
            var rebuiltImgBdPath = Path.Combine(rebuildDir, "img_bd.bin");

            if (!File.Exists(baseIsoPath))
                throw new FileNotFoundException("Base ISO not found (expected _base.iso).", baseIsoPath);
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Manifest not found.", manifestPath);

            if (!File.Exists(rebuiltImgBdPath) || !File.Exists(rebuiltImgHdPath))
                throw new FileNotFoundException("Rebuilt img_hd.bin/img_bd.bin not found in _rebuild.", rebuildDir);

            var mf = ManifestJson.Load(manifestPath);
            if (mf.IsoToc is null || mf.IsoToc.Count == 0)
                throw new InvalidDataException("Manifest iso_toc missing or empty; cannot map LBAs.");

            static bool IsImgBdPath(string p)
            {
                var s = p.Replace('/', '\\');
                var leaf = s.Contains('\\') ? s.Substring(s.LastIndexOf('\\') + 1) : s;
                leaf = leaf.ToUpperInvariant();
                return leaf == "IMG_BD.BIN;1" || leaf == "IMG_BD;1" || leaf == "IMG.BD;1";
            }

            static bool IsImgHdPath(string p)
            {
                var s = p.Replace('/', '\\');
                var leaf = s.Contains('\\') ? s.Substring(s.LastIndexOf('\\') + 1) : s;
                leaf = leaf.ToUpperInvariant();
                return leaf == "IMG_HD.BIN;1" || leaf == "IMG_HD;1" || leaf == "IMG.HD;1";
            }

            var imgBdEntry = mf.IsoToc.FirstOrDefault(e => IsImgBdPath(e.Path))
                ?? throw new InvalidDataException("IMG_BD* entry not found in iso_toc (expected IMG_BD.BIN;1 or IMG_BD;1 or IMG.BD;1).");

            var imgHdEntry = mf.IsoToc.FirstOrDefault(e => IsImgHdPath(e.Path))
                ?? throw new InvalidDataException("IMG_HD* entry not found in iso_toc (expected IMG_HD.BIN;1 or IMG_HD;1 or IMG.HD;1).");

            Directory.CreateDirectory(Path.GetDirectoryName(outputIsoPath)!);
            File.Copy(baseIsoPath, outputIsoPath, overwrite: true);
            log?.Report($"Cloned base ISO -> {outputIsoPath}");

            await using var iso = new FileStream(outputIsoPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 1 << 20);
            using var isoView = IsoFs.Wrap(iso);

            {
                var srcLen = new FileInfo(rebuiltImgHdPath).Length;
                long dstLen = imgHdEntry.Size;
                if (srcLen > dstLen)
                    throw new InvalidDataException($"IMG_HD larger than original (new=0x{srcLen:X} > old=0x{dstLen:X}). Relocation of IMG_HD would be needed (not implemented).");

                iso.Position = (long)imgHdEntry.Lba * SectorSize;
                await using (var src = new FileStream(rebuiltImgHdPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, true))
                    await src.CopyToAsync(iso, 1 << 20, ct);

                if (srcLen < dstLen)
                {
                    var pad = (int)(dstLen - srcLen);
                    await WriteZerosAsync(iso, pad, ct);
                }

                log?.Report($"Patched IMG_HD in-place @ LBA {imgHdEntry.Lba} size 0x{imgHdEntry.Size:X} (src 0x{srcLen:X}).");
            }

            var newImgBdLen = new FileInfo(rebuiltImgBdPath).Length;

            iso.Position = iso.Length;
            long padTail = (SectorSize - (iso.Position % SectorSize)) % SectorSize;
            if (padTail > 0)
                await WriteZerosAsync(iso, (int)padTail, ct);

            int newImgBdLba = (int)(iso.Position / SectorSize);

            await using (var src = new FileStream(rebuiltImgBdPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, true))
                await src.CopyToAsync(iso, 1 << 20, ct);

            long after = iso.Position;
            long padEnd = (SectorSize - (after % SectorSize)) % SectorSize;
            if (padEnd > 0)
                await WriteZerosAsync(iso, (int)padEnd, ct);

            log?.Report($"Appended IMG_BD to end: LBA={newImgBdLba}  size=0x{newImgBdLen:X}");

            var pathRaw = imgBdEntry.Path;
            string parentPath, nameRaw;
            {
                var idx = pathRaw.LastIndexOf('\\');
                if (idx < 0)
                {
                    parentPath = "";
                    nameRaw = pathRaw;
                }
                else
                {
                    parentPath = pathRaw.Substring(0, idx);
                    nameRaw = pathRaw.Substring(idx + 1);
                }
            }

            int parentLba, parentLen;
            if (string.IsNullOrEmpty(parentPath))
            {
                parentLba = isoView.RootDirLba;
                parentLen = isoView.RootDirDataLen;
            }
            else
            {
                if (!isoView.TryGetEntry(parentPath, out var parentDir) || !parentDir.IsDir)
                    throw new InvalidDataException($"Cannot resolve parent directory for {pathRaw}: '{parentPath}'.");
                parentLba = parentDir.Lba;
                parentLen = (int)parentDir.Length;
            }

            PatchDirectoryEntry(iso, parentLba, parentLen, nameRaw, newImgBdLba, (uint)newImgBdLen, log);

            log?.Report("Relocation complete. IMG_HD updated and IMG_BD moved to end with updated dir entry.");
        }

        private async Task<BuildResult> BuildCoreAsync(
            string filesRoot,
            IReadOnlyList<string> fileOrder,
            string outDir,
            Options options,
            IProgress<string>? log,
            CancellationToken ct)
        {
            if (fileOrder is null || fileOrder.Count == 0)
                throw new ArgumentException("nameTable/fileOrder is empty.", nameof(fileOrder));
            if (!Directory.Exists(filesRoot))
                throw new DirectoryNotFoundException($"Files root not found: {filesRoot}");

            Directory.CreateDirectory(outDir);
            string imgHdPath = Path.Combine(outDir, "img_hd.bin");
            string imgBdPath = Path.Combine(outDir, "img_bd.bin");

            var table = new (uint StartSector, uint Size)[fileOrder.Count];

            const int WBUF = 128 * 1024;
            await using var bd = new FileStream(
                imgBdPath, FileMode.Create, FileAccess.Write, FileShare.None, WBUF,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            long currentOffset = 0;
            uint currentSector = 0;
            byte[] copyBuffer = ArrayPool<byte>.Shared.Rent(WBUF);

            try
            {
                for (int i = 0; i < fileOrder.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    string relPath = fileOrder[i];
                    string srcPath = ResolveSrcPath(filesRoot, relPath);

                    uint size = 0;
                    uint startSector = currentSector;

                    if (File.Exists(srcPath))
                    {
                        var fi = new FileInfo(srcPath);
                        long fsize = fi.Length;
                        if (options.StrictSizes && fsize > uint.MaxValue)
                            throw new InvalidDataException($"File too large (>4GB): {srcPath}");

                        size = (uint)Math.Min(fsize, uint.MaxValue);
                        await CopyFileAsync(srcPath, bd, copyBuffer, ct);

                        int pad = PadNeeded((int)size, DataAlign);
                        if (pad > 0)
                            await WriteZerosAsync(bd, pad, ct);

                        currentOffset += size + pad;
                        currentSector = (uint)(currentOffset / SectorSize);

                        log?.Report($"[{i + 1}/{fileOrder.Count}] {relPath}  size=0x{size:X}  start_sec={startSector}  -> img_bd@0x{startSector * (long)SectorSize:X}");
                    }
                    else
                    {
                        if (!options.AllowMissing)
                            throw new FileNotFoundException($"Missing BIN file (AllowMissing=false): {relPath}", srcPath);

                        log?.Report($"[{i + 1}/{fileOrder.Count}] {relPath}  (missing)  size=0  start_sec={startSector}");
                    }

                    table[i] = (startSector, size);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(copyBuffer);
            }

            await bd.FlushAsync(ct);

            var hdBytes = new byte[checked(table.Length * 8)];
            for (int i = 0; i < table.Length; i++)
            {
                int off = i * 8;
                BinaryPrimitives.WriteUInt32LittleEndian(hdBytes.AsSpan(off, 4), table[i].StartSector);
                BinaryPrimitives.WriteUInt32LittleEndian(hdBytes.AsSpan(off + 4, 4), table[i].Size);
            }
            await File.WriteAllBytesAsync(imgHdPath, hdBytes, ct);

            log?.Report($"Wrote img_hd.bin ({hdBytes.Length} bytes)");
            log?.Report($"Wrote img_bd.bin (0x{currentOffset:X} bytes)");

            return new BuildResult
            {
                ImgHdPath = imgHdPath,
                ImgBdPath = imgBdPath,
                FileCount = (uint)fileOrder.Count,
                ImgBdBytes = currentOffset,
                Table = table
            };
        }

        private static void PatchDirectoryEntry(
            FileStream iso,
            int dirLba,
            int dirLen,
            string entryNameRaw,
            int newLba,
            uint newLen,
            IProgress<string>? log)
        {
            long dirOffset = (long)dirLba * SectorSize;
            var buf = new byte[dirLen];
            iso.Position = dirOffset;
            ReadExactly(iso, buf, 0, buf.Length);

            int p = 0;
            while (p < buf.Length)
            {
                byte reclen = buf[p];
                if (reclen == 0)
                {
                    p = ((p / SectorSize) + 1) * SectorSize;
                    continue;
                }

                int nameLen = buf[p + 32];
                if (nameLen > 0 && p + 33 + nameLen <= buf.Length)
                {
                    var name = System.Text.Encoding.ASCII.GetString(buf, p + 33, nameLen);

                    if (!(name.Length == 1 && (name[0] == '\0' || name[0] == '\u0001')))
                    {
                        if (string.Equals(name, entryNameRaw, StringComparison.OrdinalIgnoreCase))
                        {
                            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p + 2, 4), newLba);
                            BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(p + 6, 4), newLba);
                            BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(p + 10, 4), (int)newLen);
                            BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(p + 14, 4), (int)newLen);

                            iso.Position = dirOffset;
                            iso.Write(buf, 0, buf.Length);
                            log?.Report($"Directory entry patched: {entryNameRaw} -> LBA {newLba}, len 0x{newLen:X}");
                            return;
                        }
                    }
                }

                p += reclen;
            }

            throw new InvalidDataException($"Directory entry '{entryNameRaw}' not found to patch.");
        }

        private static string ResolveSrcPath(string filesRoot, string rel)
        {
            var relOS = rel.Replace('/', Path.DirectorySeparatorChar);
            var p = Path.Combine(filesRoot, relOS);
            if (File.Exists(p))
                return p;

            var prefix = "bin" + Path.DirectorySeparatorChar;
            if (relOS.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var trimmed = relOS.Substring(prefix.Length);
                var alt = Path.Combine(filesRoot, trimmed);
                if (File.Exists(alt))
                    return alt;
            }

            var withBin = Path.Combine(filesRoot, Path.Combine("bin", relOS));
            if (File.Exists(withBin))
                return withBin;

            return p;
        }

        private static int PadNeeded(int size, int align) => (align - (size % align)) & (align - 1);

        private static async Task CopyFileAsync(string srcPath, FileStream dst, byte[] buffer, CancellationToken ct)
        {
            const int BUF = 128 * 1024;
            await using var s = new FileStream(
                srcPath, FileMode.Open, FileAccess.Read, FileShare.Read, BUF,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            int r;
            while ((r = await s.ReadAsync(buffer.AsMemory(0, BUF), ct)) > 0)
                await dst.WriteAsync(buffer.AsMemory(0, r), ct);
        }

        private static async Task WriteZerosAsync(Stream s, int count, CancellationToken ct)
        {
            const int BUF = 16 * 1024;
            byte[] zero = ArrayPool<byte>.Shared.Rent(BUF);
            try
            {
                Array.Clear(zero, 0, BUF);
                int left = count;
                while (left > 0)
                {
                    int chunk = Math.Min(BUF, left);
                    await s.WriteAsync(zero.AsMemory(0, chunk), ct);
                    left -= chunk;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(zero, clearArray: true);
            }
        }

        private static void ReadExactly(Stream s, byte[] b, int o, int c)
        {
            int r, t = 0;
            while (t < c && (r = s.Read(b, o + t, c - t)) > 0)
                t += r;
            if (t != c)
                throw new EndOfStreamException();
        }

        private static bool LooksPk2Folder(string path)
        {
            string n = Path.GetFileName(path).ToLowerInvariant();
            return n.EndsWith("_pk2_index") || n.EndsWith("_pk2_linked");
        }

        private static int RebuildAllPk2Folders(string root, IProgress<string>? log, CancellationToken ct)
        {
            int built = 0;
            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                if (!LooksPk2Folder(dir))
                    continue;

                string baseName = Path.GetFileName(dir);
                string parent = Path.GetDirectoryName(dir)!;

                string stem;
                if (baseName.EndsWith("_pk2_index", StringComparison.OrdinalIgnoreCase))
                    stem = baseName[..^"_pk2_index".Length];
                else if (baseName.EndsWith("_pk2_linked", StringComparison.OrdinalIgnoreCase))
                    stem = baseName[..^"_pk2_linked".Length];
                else
                    continue;

                string pk2Path = Path.Combine(parent, stem + ".pk2");

                using var fs = new FileStream(pk2Path, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024);
                Archive.PK2.Pk2Builder.BuildFromFolder(dir, fs);
                log?.Report($"PK2: {baseName} -> {Path.GetRelativePath(root, pk2Path)}");
                built++;
            }
            return built;
        }
    }
}
