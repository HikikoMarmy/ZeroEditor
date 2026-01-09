using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using ZeroEditor.Compress;
using ZeroEditor.Game;

namespace ZeroEditor
{
    public sealed class Zero3Rebuilder : IGameRebuilder
    {
        public const int SectorSize = 2048;
        private static readonly ZeroLess Less = ZeroLess.Instance;

        public sealed class ImgBuildResult
        {
            public string ImgBdPath { get; init; } = "";
            public long ImgBdBytes { get; init; }
            public (uint Lba, uint Size, uint CmpSize, bool Compressed, bool HasData, string RelPath)[] Entries { get; init; } =
                Array.Empty<(uint Lba, uint Size, uint CmpSize, bool Compressed, bool HasData, string RelPath)>();
            public string MapCsvPath { get; init; } = "";
        }

        public sealed class Options
        {
            public bool AllowMissing { get; init; } = true;
            public bool StrictSizes { get; init; } = true;
            public bool RecompressLess { get; init; } = true;
            public bool WriteDebugImgMap { get; init; } = false;
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
            var filesRoot = layout.FilesRoot;

            var manifestPath = ProjectLayout.ResolveManifestPath(projectRoot);
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Manifest not found", manifestPath);

            var baseIsoPath = layout.BaseIsoPath;
            if (!File.Exists(baseIsoPath))
                throw new FileNotFoundException("Base ISO not found (expected _base.iso).", baseIsoPath);

            var ctx = GameContextFactory.FromManifest(manifestPath);
            if (ctx.Key.Game != GameId.FF3)
                throw new NotSupportedException($"Zero3Rebuilder expects FF3 manifest, got {ctx.Key.Game}.");

            Directory.CreateDirectory(layout.RebuildDir);

            var img = await BuildImgBdAndPatchFhdAsync(
                projectRoot: projectRoot,
                outDir: layout.RebuildDir,
                options: new Options { AllowMissing = true, StrictSizes = true, RecompressLess = true },
                log: log,
                ct: ct);

            log?.Report($"FF3 IMG_BD built: {img.ImgBdPath} (0x{img.ImgBdBytes:X} bytes)");
            log?.Report($"FF3 map: {img.MapCsvPath}");

            await IsoRebuilder.BuildIsoFromManifestUsingBaseHeaderAsync(
                projectRoot: projectRoot,
                baseIsoPath: baseIsoPath,
                outIsoPath: outputIsoPath,
                overrideElfPath: null,
                overrideImgBdPath: img.ImgBdPath,
                log: log,
                ct: ct);

            log?.Report("FF3 ISO rebuild complete.");
        }

        public async Task<ImgBuildResult> BuildImgBdAndPatchFhdAsync(
            string projectRoot,
            string outDir,
            Options? options = null,
            IProgress<string>? log = null,
            CancellationToken ct = default)
        {
            options ??= new Options();

            if (string.IsNullOrWhiteSpace(projectRoot) || !Directory.Exists(projectRoot))
                throw new DirectoryNotFoundException(projectRoot);

            var layout = ProjectLayout.FromProjectRoot(projectRoot);
            var filesRoot = layout.FilesRoot;

            var manifestPath = ProjectLayout.ResolveManifestPath(projectRoot);
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Manifest not found", manifestPath);

            var manifest = ManifestJson.Load(manifestPath);
            if (manifest.IsoToc is null || manifest.IsoToc.Count == 0)
                throw new InvalidDataException("Manifest iso_toc missing or empty; cannot locate FHD.");

            static bool IsFhdPath(string p)
            {
                var s = p.Replace('/', '\\');
                var leaf = s.Contains('\\') ? s[(s.LastIndexOf('\\') + 1)..] : s;
                leaf = leaf.ToUpperInvariant();
                return leaf == "ZERO3.FHD;1" || leaf == "ZERO3.FHD" ||
                       leaf == "FILELIST.FHD;1" || leaf == "FILELIST.FHD" ||
                       leaf == "IMG_FHD.BIN;1" || leaf == "IMG_FHD.BIN" ||
                       leaf == "IMG.FHD;1" || leaf == "IMG.FHD";
            }

            var fhdToc = manifest.IsoToc.FirstOrDefault(e => IsFhdPath(e.Path))
                ?? throw new InvalidDataException("FHD entry not found in iso_toc (ZERO3.FHD / FILELIST.FHD / IMG_FHD.BIN / IMG.FHD).");

            string fhdPath = ResolveExtractedIsoPath(filesRoot, fhdToc.Path);
            if (!File.Exists(fhdPath))
                throw new FileNotFoundException($"Extracted FHD not found for iso_toc entry {fhdToc.Path}", fhdPath);

            var fhdBytes = await File.ReadAllBytesAsync(fhdPath, ct);
            var entries = FhdParser.Parse(fhdBytes);
            if (entries.Count == 0)
                throw new InvalidDataException("FHD has zero entries.");

            log?.Report($"FF3: FHD '{fhdToc.Path}' -> {entries.Count} entries (rebuild from files/ BINs).");

            var header = ParseFhdHeader(fhdBytes);
            if (header.EntryCount != entries.Count)
                throw new InvalidDataException($"FHD header count={header.EntryCount}, parsed entries={entries.Count}.");

            Directory.CreateDirectory(outDir);

            string imgBdPath = Path.Combine(outDir, "img_bd.bin");
            string mapCsvPath = Path.Combine(outDir, "img_map.csv");

            var resultEntries = new (uint Lba, uint Size, uint CmpSize, bool Compressed, bool HasData, string RelPath)[entries.Count];

            const int WBUF = 128 * 1024;
            await using var bd = new FileStream(
                imgBdPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                WBUF,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            long currentOffset = 0;

            var newDecSizes = new int[entries.Count];
            var newCmpFields = new int[entries.Count];
            var newRawLbas = new uint[entries.Count];

            for (int i = 0; i < entries.Count; i++)
            {
                int decOff = checked((int)header.DecompressedSizeTableOffset + i * 4);
                int cmpOff = checked((int)header.CompressionInfoTableOffset + i * 4);
                int lbaOff = checked((int)header.LbaTableOffset + i * 4);

                newDecSizes[i] = BinaryPrimitives.ReadInt32LittleEndian(fhdBytes.AsSpan(decOff, 4));
                newCmpFields[i] = BinaryPrimitives.ReadInt32LittleEndian(fhdBytes.AsSpan(cmpOff, 4));
                newRawLbas[i] = BinaryPrimitives.ReadUInt32LittleEndian(fhdBytes.AsSpan(lbaOff, 4));
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var e = entries[i];

                string folder = SanitizeFolder(e.Folder);
                string rel = Path.Combine(folder, e.Name)
                             .Replace('/', Path.DirectorySeparatorChar)
                             .Replace('\\', Path.DirectorySeparatorChar);

                string srcPath = ResolveSrcPath(filesRoot, rel);

                bool hadDataOriginal = !e.HasNoDataFlag;

                byte[]? plain = null;

                if (File.Exists(srcPath))
                {
                    plain = await File.ReadAllBytesAsync(srcPath, ct);
                }
                else if (rel.EndsWith(".pk4", StringComparison.OrdinalIgnoreCase))
                {
                    string? pk4Folder = FindPk4FolderForRel(filesRoot, rel);
                    if (pk4Folder != null && Directory.Exists(pk4Folder))
                    {
                        using var ms = new MemoryStream();
                        Archive.PK4.Pk4Builder.BuildFromFolder(pk4Folder, ms);
                        plain = ms.ToArray();
                    }
                }
                else if (rel.EndsWith(".phf", StringComparison.OrdinalIgnoreCase))
                {
                    string? phfFolder = FindPhfFolderForRel(filesRoot, rel);
                    if (phfFolder != null && Directory.Exists(phfFolder))
                    {
                        using var ms = new MemoryStream();
                        Archive.PHF.PhfBuilder.BuildFromFolder(phfFolder, ms);
                        plain = ms.ToArray();
                    }
                }

                if (plain == null)
                {
                    if (!options.AllowMissing && hadDataOriginal)
                        throw new FileNotFoundException($"Missing BIN/PK4/PHF for FHD entry: {rel}", srcPath);

                    // Keep original FHD table values
                    uint rawLba = newRawLbas[i];
                    bool hasData = (rawLba & 0x8000_0000u) == 0;
                    uint lba = rawLba & 0x7FFF_FFFFu;

                    int decSizeExisting = newDecSizes[i];
                    int cmpFieldExisting = newCmpFields[i];
                    bool compressedExisting = (cmpFieldExisting & 1) != 0;
                    uint cmpSizeExisting = (uint)cmpFieldExisting >> 1;

                    uint sizeField = decSizeExisting > 0 ? (uint)decSizeExisting : 0u;

                    resultEntries[i] = (lba, sizeField, cmpSizeExisting, compressedExisting, hasData, rel);
                    log?.Report($"FF3 IMG: [{i + 1}/{entries.Count}] {rel} -> (missing / keep original FHD entry)");
                    continue;
                }

                if (options.StrictSizes && plain.LongLength > uint.MaxValue)
                    throw new InvalidDataException(srcPath);

                int decSize = plain.Length;
                byte[] payload;
                bool compressed;
                uint cmpSize;

                if (e.IsCompressed && options.RecompressLess && decSize > 0)
                {
                    if (!Less.TryCompress(plain, out var encoded))
                        throw new InvalidDataException($"LESS compression failed for {rel}");

                    payload = encoded;
                    compressed = true;
                    cmpSize = (uint)encoded.Length;
                }
                else
                {
                    payload = plain;

                    uint oldCmpSize = (uint)newCmpFields[i] >> 1;
                    bool oldCompressed = (newCmpFields[i] & 1) != 0;

                    compressed = oldCompressed && oldCmpSize != 0;
                    cmpSize = oldCmpSize != 0 ? oldCmpSize : (uint)payload.Length;
                }

                int padBefore = (int)((SectorSize - (currentOffset % SectorSize)) % SectorSize);
                if (padBefore > 0)
                {
                    await WriteZerosAsync(bd, padBefore, ct);
                    currentOffset += padBefore;
                }

                long entryOffset = currentOffset;
                uint lbaNew = (uint)(entryOffset / SectorSize);

                await bd.WriteAsync(payload.AsMemory(0, payload.Length), ct);
                currentOffset += payload.Length;

                int padAfter = (int)((SectorSize - (currentOffset % SectorSize)) % SectorSize);
                if (padAfter > 0)
                {
                    await WriteZerosAsync(bd, padAfter, ct);
                    currentOffset += padAfter;
                }

                newDecSizes[i] = decSize;
                newCmpFields[i] = (int)((cmpSize << 1) | (compressed ? 1u : 0u));

                // IMPORTANT: writing data => clear "no data" flag and set LBA
                newRawLbas[i] = lbaNew;

                resultEntries[i] = (lbaNew, (uint)decSize, cmpSize, compressed, true, rel);

                log?.Report($"FF3 IMG: [{i + 1}/{entries.Count}] {rel} -> LBA={lbaNew} dec=0x{decSize:X} cmp=0x{cmpSize:X} {(compressed ? "(LESS)" : "(RAW)")}");
            }

            await bd.FlushAsync(ct);

            for (int i = 0; i < entries.Count; i++)
            {
                int decOff = checked((int)header.DecompressedSizeTableOffset + i * 4);
                int cmpOff = checked((int)header.CompressionInfoTableOffset + i * 4);
                int lbaOff = checked((int)header.LbaTableOffset + i * 4);

                BinaryPrimitives.WriteInt32LittleEndian(fhdBytes.AsSpan(decOff, 4), newDecSizes[i]);
                BinaryPrimitives.WriteInt32LittleEndian(fhdBytes.AsSpan(cmpOff, 4), newCmpFields[i]);
                BinaryPrimitives.WriteUInt32LittleEndian(fhdBytes.AsSpan(lbaOff, 4), newRawLbas[i]);
            }

            await File.WriteAllBytesAsync(fhdPath, fhdBytes, ct);
            log?.Report($"FF3: patched FHD in-place: {fhdPath}");

            if (options.WriteDebugImgMap)
            {
                var sb = new StringBuilder();
                sb.AppendLine("index,lba,dec_size,cmp_size,compressed,has_data,rel_path");
                for (int i = 0; i < resultEntries.Length; i++)
                {
                    var e = resultEntries[i];
                    sb.Append(i).Append(',')
                      .Append(e.Lba).Append(',')
                      .Append(e.Size).Append(',')
                      .Append(e.CmpSize).Append(',')
                      .Append(e.Compressed ? "1" : "0").Append(',')
                      .Append(e.HasData ? "1" : "0").Append(',')
                      .AppendLine(e.RelPath.Replace('\\', '/'));
                }
                await File.WriteAllTextAsync(mapCsvPath, sb.ToString(), ct);
            }

            return new ImgBuildResult
            {
                ImgBdPath = imgBdPath,
                ImgBdBytes = currentOffset,
                Entries = resultEntries,
                MapCsvPath = mapCsvPath
            };
        }

        private sealed record FhdHeader(
            uint Signature,
            uint BaseHeaderSize,
            int EntryCount,
            uint DecompressedSizeTableOffset,
            uint ReservedBlockOffset,
            uint CompressionInfoTableOffset,
            uint NamePointerTableOffset,
            uint LbaTableOffset);

        private static FhdHeader ParseFhdHeader(byte[] fhd)
        {
            if (fhd.Length < 0x24)
                throw new InvalidDataException("FHD too small for header.");

            using var br = new BinaryReader(new MemoryStream(fhd, writable: false), Encoding.ASCII, leaveOpen: false);
            uint sig = br.ReadUInt32();
            if (sig != 0x46484400 && sig != 0x00444846)
                throw new InvalidDataException("Not an FHD file.");

            br.BaseStream.Seek(8, SeekOrigin.Begin);
            uint baseHeaderSize = br.ReadUInt32();
            int entryCount = br.ReadInt32();
            uint decompressedSizeTableOffset = br.ReadUInt32();
            uint reservedBlockOffset = br.ReadUInt32();
            uint compressionInfoTableOffset = br.ReadUInt32();
            uint namePointerTableOffset = br.ReadUInt32();
            uint lbaTableOffset = br.ReadUInt32();

            return new FhdHeader(
                Signature: sig,
                BaseHeaderSize: baseHeaderSize,
                EntryCount: entryCount,
                DecompressedSizeTableOffset: decompressedSizeTableOffset,
                ReservedBlockOffset: reservedBlockOffset,
                CompressionInfoTableOffset: compressionInfoTableOffset,
                NamePointerTableOffset: namePointerTableOffset,
                LbaTableOffset: lbaTableOffset);
        }

        private static string SanitizeFolder(string folder)
        {
            return folder.Replace("..", "")
                         .Replace('/', Path.DirectorySeparatorChar)
                         .Replace('\\', Path.DirectorySeparatorChar)
                         .Trim(Path.DirectorySeparatorChar);
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

        private static string ResolveExtractedIsoPath(string filesRoot, string tocPath)
        {
            // tocPath may contain ';1' and may have / or \
            var relOS = tocPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var p = Path.Combine(filesRoot, relOS);
            if (File.Exists(p))
                return p;

            // try stripping version
            var noVer = relOS.Split(';')[0];
            var alt = Path.Combine(filesRoot, noVer);
            if (File.Exists(alt))
                return alt;

            return p;
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

        private static string? FindPk4FolderForRel(string filesRoot, string rel)
        {
            var relOS = rel.Replace('/', Path.DirectorySeparatorChar);
            var dir = Path.GetDirectoryName(relOS) ?? string.Empty;
            var stem = Path.GetFileNameWithoutExtension(relOS);
            var baseDir = Path.Combine(filesRoot, dir);

            var cand = Path.Combine(baseDir, stem + "_pk4");
            if (Directory.Exists(cand))
                return cand;

            return null;
        }

        private static string? FindPhfFolderForRel(string filesRoot, string rel)
        {
            var relOS = rel.Replace('/', Path.DirectorySeparatorChar);
            var dir = Path.GetDirectoryName(relOS) ?? string.Empty;
            var stem = Path.GetFileNameWithoutExtension(relOS);
            var baseDir = Path.Combine(filesRoot, dir);

            var cand = Path.Combine(baseDir, stem + "_phf");
            if (Directory.Exists(cand))
                return cand;

            return null;
        }
    }
}
