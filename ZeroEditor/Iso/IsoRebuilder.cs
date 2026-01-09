using System.Buffers.Binary;
using System.Text;
using ZeroEditor.Game;

public sealed class IsoRebuilder
{
    public sealed record StagedFile(string IsoPath, Func<Stream> Open, uint? ForcedLength = null);
    public sealed record LayoutResult(Dictionary<string, (int Lba, uint Length)> Map);

    const int Sector = 2048;

    sealed class Node
    {
        public string NameRaw = "";
        public bool IsDir;
        public List<Node> Children = new();
        public Node? Parent;
        public uint DataLen;
        public int Lba;
        public Func<Stream>? Open;
    }

    public static async Task BuildIsoFromManifestAsync(
        string projectRoot,
        string outIsoPath,
        string? overrideImgHdPath = null,
        string? overrideImgBdPath = null,
        IProgress<string>? log = null,
        CancellationToken ct = default)
    {
        var manifestPath = ProjectLayout.ResolveManifestPath(projectRoot);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Manifest not found", manifestPath);

        var filesRoot = ProjectLayout.ResolveFilesRoot(projectRoot);

        var mf = ManifestJson.Load(manifestPath);
        if (mf.IsoToc == null || mf.IsoToc.Count == 0)
            throw new InvalidDataException("Manifest iso_toc is missing or empty.");

        static bool IsImgHd(string p) =>
            p.Equals("IMG_HD.BIN;1", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("IMG.HD;1", StringComparison.OrdinalIgnoreCase);

        static bool IsImgBd(string p) =>
            p.Equals("IMG_BD.BIN;1", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("IMG.BD;1", StringComparison.OrdinalIgnoreCase);

        var staged = new List<StagedFile>(mf.IsoToc.Count);

        foreach (var e in mf.IsoToc)
        {
            ct.ThrowIfCancellationRequested();

            var isoEntryPath = e.Path;
            uint forcedLen;
            Func<Stream> opener;

            if (IsImgHd(isoEntryPath) && !string.IsNullOrWhiteSpace(overrideImgHdPath))
            {
                var fi = new FileInfo(overrideImgHdPath);
                if (!fi.Exists)
                    throw new FileNotFoundException("override img_hd not found", overrideImgHdPath);

                forcedLen = (uint)fi.Length;
                opener = () => new FileStream(overrideImgHdPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                log?.Report($"Stage: override {isoEntryPath} -> {overrideImgHdPath} (0x{fi.Length:X})");
            }
            else if (IsImgBd(isoEntryPath) && !string.IsNullOrWhiteSpace(overrideImgBdPath))
            {
                var fi = new FileInfo(overrideImgBdPath);
                if (!fi.Exists)
                    throw new FileNotFoundException("override img_bd not found", overrideImgBdPath);

                forcedLen = (uint)fi.Length;
                opener = () => new FileStream(overrideImgBdPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                log?.Report($"Stage: override {isoEntryPath} -> {overrideImgBdPath} (0x{fi.Length:X})");
            }
            else
            {
                var rel = isoEntryPath.Replace('\\', Path.DirectorySeparatorChar);
                var full = Path.Combine(filesRoot, rel);
                if (!File.Exists(full))
                    throw new FileNotFoundException($"Extracted file missing for TOC entry: {isoEntryPath}", full);

                var fi = new FileInfo(full);
                forcedLen = (uint)fi.Length;
                opener = () => new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            staged.Add(new StagedFile(
                IsoPath: isoEntryPath,
                Open: opener,
                ForcedLength: forcedLen
            ));
        }

        static string KeyNorm(string p) => p.Replace('/', '\\').Trim('\\').ToUpperInvariant();
        Dictionary<string, List<string>>? dirOrder = null;
        if (mf.DirOrder != null && mf.DirOrder.Count > 0)
        {
            dirOrder = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in mf.DirOrder)
                dirOrder[KeyNorm(kv.Key)] = kv.Value.ToList();
        }

        var rebuilder = new IsoRebuilder();
        var result = await rebuilder.BuildAsync(
            staged, outIsoPath, log, ct,
            dirOrder: mf.DirOrder
        );

        log?.Report($"ISO rebuilt from manifest -> {outIsoPath}");
        log?.Report($"Files staged: {result.Map.Count}");
    }

    public static async Task BuildIsoFromManifestUsingBaseHeaderAsync(
        string projectRoot,
        string baseIsoPath,
        string outIsoPath,
        string? overrideElfPath = null,
        string? overrideImgBdPath = null,
        IProgress<string>? log = null,
        CancellationToken ct = default)
    {
        var manifestPath = ProjectLayout.ResolveManifestPath(projectRoot);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Manifest not found", manifestPath);
        if (!File.Exists(baseIsoPath))
            throw new FileNotFoundException("Base ISO not found", baseIsoPath);

        var filesRoot = ProjectLayout.ResolveFilesRoot(projectRoot);

        var mf = ManifestJson.Load(manifestPath);
        if (mf.IsoToc == null || mf.IsoToc.Count == 0)
            throw new InvalidDataException("Manifest iso_toc is missing or empty.");

        int firstFileLba = mf.IsoToc.Min(e => e.Lba);
        if (firstFileLba <= 0)
            throw new InvalidDataException($"Invalid first file LBA: {firstFileLba}");
        long headerBytes = (long)firstFileLba * Sector;

        string? bootIsoPath;
        using (var baseIsoView = IsoFs.Open(baseIsoPath))
            bootIsoPath = baseIsoView.GetBootPath();

        string? bootNoVer = null;
        if (!string.IsNullOrEmpty(bootIsoPath))
            bootNoVer = bootIsoPath.Split(';')[0];

        static bool IsImgBdPath(string p)
        {
            var s = p.Replace('/', '\\');
            var leaf = s.Contains('\\') ? s.Substring(s.LastIndexOf('\\') + 1) : s;
            leaf = leaf.ToUpperInvariant();
            return leaf == "IMG_BD.BIN;1" || leaf == "IMG_BD;1" || leaf == "IMG.BD;1";
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outIsoPath)!);

        await using var baseIso = new FileStream(
            baseIsoPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1 << 20,
            FileOptions.SequentialScan);

        await using var outIso = new FileStream(
            outIsoPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            1 << 20,
            FileOptions.Asynchronous);

        long left = headerBytes;
        var buffer = new byte[1 << 20];
        while (left > 0)
        {
            ct.ThrowIfCancellationRequested();
            int toRead = (int)Math.Min(left, buffer.Length);
            int r = await baseIso.ReadAsync(buffer.AsMemory(0, toRead), ct);
            if (r <= 0)
                throw new EndOfStreamException();
            await outIso.WriteAsync(buffer.AsMemory(0, r), ct);
            left -= r;
        }

        int currentLba = firstFileLba;
        var newExtents = new Dictionary<string, (int Lba, uint Length)>(StringComparer.OrdinalIgnoreCase);

        List<ManifestJson.IsoTocEntry> normalEntries = new();
        ManifestJson.IsoTocEntry? imgBdEntry = null;
        foreach (var e in mf.IsoToc)
        {
            if (IsImgBdPath(e.Path))
                imgBdEntry = e;
            else
                normalEntries.Add(e);
        }

        async Task WriteEntryAsync(ManifestJson.IsoTocEntry e)
        {
            ct.ThrowIfCancellationRequested();

            string isoEntryPath = e.Path;
            string isoEntryNoVer = isoEntryPath.Split(';')[0];
            string relOS = isoEntryPath.Replace('\\', Path.DirectorySeparatorChar);
            Func<Stream> opener;
            string? srcPath;

            if (overrideImgBdPath != null && IsImgBdPath(isoEntryPath))
            {
                srcPath = overrideImgBdPath;
            }
            else if (overrideElfPath != null && !string.IsNullOrEmpty(bootNoVer) &&
                     isoEntryNoVer.Equals(bootNoVer, StringComparison.OrdinalIgnoreCase))
            {
                srcPath = overrideElfPath;
            }
            else
            {
                srcPath = Path.Combine(filesRoot, relOS);
                if (!File.Exists(srcPath) && isoEntryPath.EndsWith(";1", StringComparison.OrdinalIgnoreCase))
                {
                    var noVerRel = isoEntryNoVer.Replace('\\', Path.DirectorySeparatorChar);
                    var alt = Path.Combine(filesRoot, noVerRel);
                    if (File.Exists(alt))
                        srcPath = alt;
                }
            }

            if (srcPath == null || !File.Exists(srcPath))
                throw new FileNotFoundException($"Source file missing for TOC entry: {isoEntryPath}", srcPath ?? isoEntryPath);

            var fi = new FileInfo(srcPath);
            uint size = (uint)Math.Min(fi.Length, uint.MaxValue);
            int thisLba = currentLba;

            opener = () => new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            await using (var src = opener())
            {
                long remaining = size;
                while (remaining > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    int chunk = (int)Math.Min(remaining, buffer.Length);
                    int r = await src.ReadAsync(buffer.AsMemory(0, chunk), ct);
                    if (r <= 0)
                        throw new EndOfStreamException();
                    await outIso.WriteAsync(buffer.AsMemory(0, r), ct);
                    remaining -= r;
                }
            }

            long pad = (Sector - (outIso.Position % Sector)) % Sector;
            if (pad > 0)
            {
                Array.Clear(buffer, 0, (int)Math.Min(pad, buffer.Length));
                long leftPad = pad;
                while (leftPad > 0)
                {
                    int chunk = (int)Math.Min(leftPad, buffer.Length);
                    await outIso.WriteAsync(buffer.AsMemory(0, chunk), ct);
                    leftPad -= chunk;
                }
            }

            int sectors = CeilDiv((int)size, Sector);
            currentLba += sectors;

            newExtents[isoEntryPath] = (thisLba, size);
            log?.Report($"ISO data: {isoEntryPath} -> LBA {thisLba} size 0x{size:X}");
        }

        foreach (var e in normalEntries)
            await WriteEntryAsync(e);

        if (imgBdEntry != null)
            await WriteEntryAsync(imgBdEntry);

        long finalLen = outIso.Position;
        long padEnd = (Sector - (finalLen % Sector)) % Sector;
        if (padEnd > 0)
        {
            Array.Clear(buffer, 0, (int)Math.Min(padEnd, buffer.Length));
            long leftPad = padEnd;
            while (leftPad > 0)
            {
                int chunk = (int)Math.Min(leftPad, buffer.Length);
                await outIso.WriteAsync(buffer.AsMemory(0, chunk), ct);
                leftPad -= chunk;
            }
        }

        finalLen = outIso.Position;
        int volumeSpaceSectors = (int)(finalLen / Sector);
        await PatchVolumeSpaceSizeAsync(outIso, volumeSpaceSectors, ct);

        outIso.Flush();

        using (var isoView = IsoFs.Wrap(outIso))
        {
            foreach (var e in mf.IsoToc)
            {
                if (!newExtents.TryGetValue(e.Path, out var ext))
                    continue;

                string pathRaw = e.Path;
                string parentPath;
                string nameRaw;
                int idx = pathRaw.LastIndexOf('\\');
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

                int parentLba;
                int parentLen;
                if (string.IsNullOrEmpty(parentPath))
                {
                    parentLba = isoView.RootDirLba;
                    parentLen = isoView.RootDirDataLen;
                }
                else
                {
                    if (!isoView.TryGetEntry(parentPath, out var parentDir) || parentDir.IsDir == false)
                        throw new InvalidDataException($"Cannot resolve parent directory for {pathRaw}: '{parentPath}'.");
                    parentLba = parentDir.Lba;
                    parentLen = (int)parentDir.Length;
                }

                PatchDirectoryEntry(outIso, parentLba, parentLen, nameRaw, ext.Lba, ext.Length);
            }
        }

        await outIso.FlushAsync(ct);
        log?.Report($"ISO rebuilt (base header) -> {outIsoPath}");
    }

    static async Task PatchVolumeSpaceSizeAsync(FileStream iso, int volumeSpaceSectors, CancellationToken ct)
    {
        long pvdOffset = 16L * Sector;
        byte[] buf = new byte[Sector];
        iso.Position = pvdOffset;
        int read = await iso.ReadAsync(buf.AsMemory(0, buf.Length), ct);
        if (read != buf.Length)
            throw new EndOfStreamException();

        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(80, 4), volumeSpaceSectors);
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(84, 4), volumeSpaceSectors);

        iso.Position = pvdOffset;
        await iso.WriteAsync(buf.AsMemory(0, buf.Length), ct);
    }

    public async Task<LayoutResult> BuildAsync(
        IEnumerable<StagedFile> stage,
        string outIsoPath,
        IProgress<string>? log,
        CancellationToken ct,
        Dictionary<string, List<string>>? dirOrder = null)
    {
        var root = new Node { NameRaw = "<root>", IsDir = true };
        foreach (var s in stage)
        {
            var path = NormalizeIsoPath(s.IsoPath);
            var parts = path.Split('\\');
            var cur = root;
            for (int i = 0; i < parts.Length; i++)
            {
                bool last = i == parts.Length - 1;
                var name = parts[i];
                var child = cur.Children.FirstOrDefault(c => c.NameRaw == name);
                if (child == null)
                {
                    child = new Node { NameRaw = name, IsDir = !last };
                    child.Parent = cur;
                    cur.Children.Add(child);
                }
                cur = child;
                if (last)
                {
                    cur.IsDir = false;
                    cur.Open = s.Open;

                    if (s.ForcedLength.HasValue)
                    {
                        cur.DataLen = s.ForcedLength.Value;
                    }
                    else
                    {
                        using var tmp = s.Open();
                        if (!tmp.CanSeek)
                            throw new InvalidDataException($"Staged stream for '{path}' must be seekable or provide ForcedLength.");
                        cur.DataLen = (uint)tmp.Length;
                    }
                }
            }
        }

        ApplyDirOrderRec(root, dirOrder);
        ComputeDirSizesRec(root);
        int nextLba = 18;
        AssignDirectoryLbasRec(root, ref nextLba);

        var (ptLBytes, ptMBytes) = BuildPathTables(root);
        int ptLbaL = nextLba;
        nextLba += CeilDiv(ptLBytes.Length, Sector);
        int ptLbaM = nextLba;
        nextLba += CeilDiv(ptMBytes.Length, Sector);

        var filesInOrder = EnumerateFilesInDesiredOrder(root).ToList();
        foreach (var f in filesInOrder)
        {
            f.Lba = nextLba;
            nextLba += CeilDiv((int)f.DataLen, Sector);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outIsoPath)!);
        using var fs = new FileStream(outIsoPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
        fs.SetLength((long)(nextLba + 32) * Sector);
        fs.Position = 0;
        WriteZeros(fs, 16 * Sector);
        int volumeSpaceSectors2 = nextLba;
        fs.Position = 16L * Sector;
        var pvd = BuildPrimaryVolumeDescriptor(root, volumeSpaceSectors2, ptLbaL, ptLBytes.Length, ptLbaM, ptMBytes.Length);
        fs.Write(pvd);
        fs.Position = 17L * Sector;
        fs.Write(BuildVDTerminator());
        fs.Position = ptLbaL * (long)Sector;
        fs.Write(ptLBytes);
        PadToSector(fs);
        fs.Position = ptLbaM * (long)Sector;
        fs.Write(ptMBytes);
        PadToSector(fs);

        foreach (var d in EnumerateDirsPreOrder(root))
        {
            fs.Position = d.Lba * (long)Sector;
            var dirBytes = BuildDirectoryBlock(d);
            fs.Write(dirBytes);
            PadToSector(fs, (int)d.DataLen);
        }

        foreach (var f in filesInOrder)
        {
            fs.Position = f.Lba * (long)Sector;
            using var s = f.Open()!;
            s.CopyTo(fs);
            PadToSector(fs, (int)f.DataLen);
            log?.Report($"ISO: wrote {f.NameRaw} @ LBA {f.Lba} len 0x{f.DataLen:X}");
        }

        var map = filesInOrder.ToDictionary(n => n.NameRaw, n => (n.Lba, n.DataLen));
        return new LayoutResult(map);
    }

    static string NormalizeIsoPath(string p)
    {
        var u = p.Replace('/', '\\').Trim('\\');
        var parts = u.Split('\\').Select(s => s.ToUpperInvariant()).ToArray();
        var last = parts[^1];
        if (!last.EndsWith(";1"))
            parts[^1] = last + ";1";
        return string.Join('\\', parts);
    }

    static string DirKeyOf(Node n)
    {
        var names = new List<string>();
        var cur = n;
        while (cur.Parent != null)
        {
            names.Add(cur.NameRaw);
            cur = cur.Parent!;
        }
        names.Reverse();
        return string.Join('\\', names).ToUpperInvariant();
    }

    static void ApplyDirOrderRec(Node dir, Dictionary<string, List<string>>? order)
    {
        if (!dir.IsDir)
            return;

        if (order != null && order.TryGetValue(DirKeyOf(dir), out var wanted))
        {
            var map = dir.Children.ToDictionary(c => c.NameRaw, StringComparer.Ordinal);
            var reordered = new List<Node>(dir.Children.Count);

            foreach (var name in wanted)
            {
                if (map.Remove(name, out var node))
                    reordered.Add(node);
            }

            foreach (var leftover in map.Values)
                reordered.Add(leftover);

            dir.Children = reordered;
        }

        foreach (var c in dir.Children.Where(x => x.IsDir))
            ApplyDirOrderRec(c, order);
    }

    static void ComputeDirSizesRec(Node dir)
    {
        foreach (var c in dir.Children.Where(x => x.IsDir))
            ComputeDirSizesRec(c);
        int bytes = DirRecLen(dir, ".");
        bytes += DirRecLen(dir, "..");
        foreach (var c in dir.Children)
            bytes += DirRecLen(c, c.NameRaw);
        dir.DataLen = (uint)RoundUp(bytes, Sector);
    }

    static void AssignDirectoryLbasRec(Node dir, ref int nextLba)
    {
        dir.Lba = nextLba;
        nextLba += CeilDiv((int)dir.DataLen, Sector);
        foreach (var c in dir.Children.Where(x => x.IsDir))
            AssignDirectoryLbasRec(c, ref nextLba);
    }

    static IEnumerable<Node> EnumerateDirsPreOrder(Node dir)
    {
        yield return dir;
        foreach (var c in dir.Children.Where(x => x.IsDir))
            foreach (var d in EnumerateDirsPreOrder(c))
                yield return d;
    }

    IEnumerable<Node> EnumerateFilesInDesiredOrder(Node root)
    {
        var files = EnumerateAll(root).Where(n => !n.IsDir).ToList();
        return files;
    }

    static IEnumerable<Node> EnumerateAll(Node n)
    {
        yield return n;
        if (n.IsDir)
            foreach (var c in n.Children)
                foreach (var k in EnumerateAll(c))
                    yield return k;
    }

    static int CeilDiv(int x, int d) => (x + d - 1) / d;
    static int RoundUp(int x, int d) => CeilDiv(x, d) * d;

    static int DirRecLen(Node n, string nameRaw)
    {
        int nameBytes = Encoding.ASCII.GetByteCount(nameRaw);
        int len = 33 + nameBytes;
        if ((len & 1) != 0)
            len++;
        return len;
    }

    static byte[] BuildPrimaryVolumeDescriptor(
        Node root,
        int volumeSpaceSectors,
        int ptLbaL, int ptLenL,
        int ptLbaM, int ptLenM)
    {
        var buf = new byte[Sector];

        void putAscii(int off, string s) => Encoding.ASCII.GetBytes(s, 0, s.Length, buf, off);
        static void putBothEndianInt32(Span<byte> dst, int off, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(dst.Slice(off, 4), value);
            BinaryPrimitives.WriteInt32BigEndian(dst.Slice(off + 4, 4), value);
        }

        buf[0] = 1;
        putAscii(1, "CD001");
        buf[6] = 1;
        buf[881] = 1;

        putBothEndianInt32(buf, 80, volumeSpaceSectors);

        BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(120, 2), (short)1);
        BinaryPrimitives.WriteInt16BigEndian(buf.AsSpan(122, 2), (short)1);

        BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(124, 2), (short)1);
        BinaryPrimitives.WriteInt16BigEndian(buf.AsSpan(126, 2), (short)1);

        BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(128, 2), (short)Sector);
        BinaryPrimitives.WriteInt16BigEndian(buf.AsSpan(130, 2), (short)Sector);

        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(132, 4), ptLenL);
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(136, 4), ptLenM);

        BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(140, 4), ptLbaL);

        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(148, 4), ptLbaM);

        var rootRec = BuildSingleDirRecordForPvd(root);
        Buffer.BlockCopy(rootRec, 0, buf, 156, rootRec.Length);

        return buf;
    }

    static byte[] BuildSingleDirRecordForPvd(Node n)
    {
        var nameBytes = new byte[] { 0x00 };

        int len = 33 + nameBytes.Length;
        if ((len & 1) != 0)
            len++;
        var b = new byte[len];

        b[0] = (byte)len;

        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(2, 4), n.Lba);
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(6, 4), n.Lba);

        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(10, 4), (int)n.DataLen);
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(14, 4), (int)n.DataLen);

        b[25] = (byte)(n.IsDir ? 0x02 : 0x00);

        BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(28, 2), (short)1);
        BinaryPrimitives.WriteInt16BigEndian(b.AsSpan(30, 2), (short)1);

        b[32] = (byte)nameBytes.Length;
        Buffer.BlockCopy(nameBytes, 0, b, 33, nameBytes.Length);

        return b;
    }

    static byte[] BuildVDTerminator()
    {
        var buf = new byte[Sector];
        buf[0] = 255;
        Encoding.ASCII.GetBytes("CD001", 0, 5, buf, 1);
        buf[6] = 1;
        return buf;
    }

    static (byte[] ptL, byte[] ptM) BuildPathTables(Node root)
    {
        var dirs = new List<Node>();
        foreach (var d in EnumerateDirsPreOrder(root))
            dirs.Add(d);

        using var msL = new MemoryStream();
        using var msM = new MemoryStream();

        for (int i = 0; i < dirs.Count; i++)
        {
            var d = dirs[i];
            var name = i == 0 ? "\0" : d.NameRaw;
            var parentIdx = i == 0 ? 1 : dirs.IndexOf(d.Parent!) + 1;
            WritePathTableRecord(msL, name, d.Lba, parentIdx, little: true);
            WritePathTableRecord(msM, name, d.Lba, parentIdx, little: false);
        }

        return (msL.ToArray(), msM.ToArray());

        static void WritePathTableRecord(Stream s, string name, int lba, int parentIdx, bool little)
        {
            var nameBytes = Encoding.ASCII.GetBytes(name);
            s.WriteByte((byte)nameBytes.Length);
            s.WriteByte(0);
            Span<byte> tmp4 = stackalloc byte[4];
            Span<byte> tmp2 = stackalloc byte[2];
            if (little)
                BinaryPrimitives.WriteInt32LittleEndian(tmp4, lba);
            else
                BinaryPrimitives.WriteInt32BigEndian(tmp4, lba);
            s.Write(tmp4);
            if (little)
                BinaryPrimitives.WriteInt16LittleEndian(tmp2, (short)parentIdx);
            else
                BinaryPrimitives.WriteInt16BigEndian(tmp2, (short)parentIdx);
            s.Write(tmp2);
            s.Write(nameBytes);
            if ((nameBytes.Length & 1) != 0)
                s.WriteByte(0);
        }
    }

    static byte[] BuildDirectoryBlock(Node dir)
    {
        using var ms = new MemoryStream();

        ms.Write(BuildSingleDirRecord(dir, "\0"));
        ms.Write(BuildSingleDirRecord(dir.Parent ?? dir, "\u0001"));
        foreach (var c in dir.Children)
            ms.Write(BuildSingleDirRecord(c, c.NameRaw));

        var bytes = ms.ToArray();
        Array.Resize(ref bytes, RoundUp(bytes.Length, Sector));
        return bytes;
    }

    static byte[] BuildSingleDirRecord(Node n, string nameRaw)
    {
        var nameBytes = Encoding.ASCII.GetBytes(nameRaw);
        int len = 33 + nameBytes.Length + ((33 + nameBytes.Length) & 1);
        var b = new byte[len];
        b[0] = (byte)len;

        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(2, 4), n.Lba);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(10, 4), (int)n.DataLen);

        b[25] = (byte)(n.IsDir ? 0x02 : 0x00);
        b[32] = (byte)nameBytes.Length;
        Buffer.BlockCopy(nameBytes, 0, b, 33, nameBytes.Length);
        return b;
    }

    static void PadToSector(Stream s, int written = -1)
    {
        long cur = s.Position;
        int pad = (int)((Sector - (cur % Sector)) % Sector);
        if (pad == 0)
            return;
        Span<byte> z = stackalloc byte[Math.Min(pad, 4096)];
        while (pad > 0)
        {
            int c = Math.Min(pad, z.Length);
            s.Write(z[..c]);
            pad -= c;
        }
    }

    static void WriteZeros(Stream s, int bytes)
    {
        Span<byte> z = stackalloc byte[Math.Min(bytes, 4096)];
        int left = bytes;
        while (left > 0)
        {
            int c = Math.Min(left, z.Length);
            s.Write(z[..c]);
            left -= c;
        }
    }

    static void PatchDirectoryEntry(
        FileStream iso,
        int dirLba,
        int dirLen,
        string entryNameRaw,
        int newLba,
        uint newLen)
    {
        long dirOffset = (long)dirLba * Sector;
        var buf = new byte[dirLen];
        iso.Position = dirOffset;
        int read = iso.Read(buf, 0, buf.Length);
        if (read != buf.Length)
            throw new EndOfStreamException();

        int p = 0;
        while (p < buf.Length)
        {
            byte reclen = buf[p];
            if (reclen == 0)
            {
                p = ((p / Sector) + 1) * Sector;
                continue;
            }

            int nameLen = buf[p + 32];
            if (nameLen > 0 && p + 33 + nameLen <= buf.Length)
            {
                var name = Encoding.ASCII.GetString(buf, p + 33, nameLen);
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
                        return;
                    }
                }
            }

            p += reclen;
        }

        throw new InvalidDataException($"Directory entry '{entryNameRaw}' not found to patch.");
    }
}
