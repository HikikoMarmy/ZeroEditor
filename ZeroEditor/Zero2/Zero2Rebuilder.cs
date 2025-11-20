using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroEditor.Game;
using ZeroEditor.Archive;
using ZeroEditor.Zero2.Common;
using ZeroEditor.Compress;

namespace ZeroEditor
{
	public sealed class Zero2Rebuilder : IGameRebuilder
	{
		public const int SectorSize = 2048;

		public sealed class ImgBuildResult
		{
			public string ImgBdPath { get; init; } = "";
			public long ImgBdBytes { get; init; }
			public (uint StartSector, uint Size, uint CmpSize, bool Compressed, bool Exists, string RelPath)[] Entries { get; init; } =
				Array.Empty<(uint StartSector, uint Size, uint CmpSize, bool Compressed, bool Exists, string RelPath)>();
			public string MapCsvPath { get; init; } = "";
		}

		public sealed class Options
		{
			public bool AllowMissing { get; init; } = true;
			public bool StrictSizes { get; init; } = true;
		}

		private sealed record OriginalImgEntry(
			int Index,
			uint StartSector,
			uint Size,
			uint CmpSize,
			bool Exists,
			bool Compressed,
			string RelPath
		);

		private static readonly ZeroLess Less = ZeroLess.Instance;

		public async Task RebuildIsoAsync(
			string extractedFolder,
			string outputIsoPath,
			IProgress<string>? log,
			CancellationToken ct )
		{
			if( string.IsNullOrWhiteSpace( extractedFolder ) || !Directory.Exists( extractedFolder ) )
				throw new DirectoryNotFoundException( extractedFolder );

			var manifestPath = Path.Combine( extractedFolder, "_manifest.json" );
			if( !File.Exists( manifestPath ) )
				throw new FileNotFoundException( "Manifest not found", manifestPath );

			var baseIsoPath = Path.Combine( extractedFolder, "_base.iso" );
			if( !File.Exists( baseIsoPath ) )
				throw new FileNotFoundException( "Base ISO not found (expected _base.iso).", baseIsoPath );

			var ctx = GameContextFactory.FromManifest( manifestPath );
			if( ctx.Key.Game != GameId.FF2 )
				throw new NotSupportedException( $"Zero2Rebuilder expects FF2 manifest, got {ctx.Key.Game}." );

			var rebuildDir = Path.Combine( extractedFolder, "_rebuild" );
			Directory.CreateDirectory( rebuildDir );

			var img = await BuildImgBdAsync(
				extractedRoot: extractedFolder,
				outDir: rebuildDir,
				options: new Options { AllowMissing = true, StrictSizes = true },
				log: log,
				ct: ct );

			log?.Report( $"FF2 IMG_BD built: {img.ImgBdPath} (0x{img.ImgBdBytes:X} bytes)" );
			log?.Report( $"FF2 map: {img.MapCsvPath}" );

			var elfPath = Path.Combine( extractedFolder, ctx.ElfName );
			if( !File.Exists( elfPath ) )
				throw new FileNotFoundException( "ELF not found in extracted folder.", elfPath );

			await PatchElfAsync( elfPath, ctx, img, log, ct );

			await IsoRebuilder.BuildIsoFromManifestUsingBaseHeaderAsync(
				extractedRoot: extractedFolder,
				baseIsoPath: baseIsoPath,
				outIsoPath: outputIsoPath,
				overrideElfPath: elfPath,
				overrideImgBdPath: img.ImgBdPath,
				log: log,
				ct: ct );

			log?.Report( "FF2 ISO rebuild complete." );
		}

		public async Task<ImgBuildResult> BuildImgBdAsync(
			string extractedRoot,
			string outDir,
			Options? options = null,
			IProgress<string>? log = null,
			CancellationToken ct = default )
		{
			options ??= new Options();
			if( string.IsNullOrWhiteSpace( extractedRoot ) || !Directory.Exists( extractedRoot ) )
				throw new DirectoryNotFoundException( extractedRoot );

			var manifestPath = Path.Combine( extractedRoot, "_manifest.json" );
			if( !File.Exists( manifestPath ) )
				throw new FileNotFoundException( "Manifest not found", manifestPath );

			var manifest = ManifestJson.Load( manifestPath );
			var ctx = GameContextFactory.FromManifest( manifestPath );

			if( ctx is not IZero2ImgLayout hasLayout )
				throw new NotSupportedException( $"Context {ctx.Key} has no Zero2 IMG layout (IZero2ImgLayout)." );

			var imgLayout = hasLayout.Img;
			int imgCount = imgLayout.Count;

			var fileTable = ctx.FileTable;
			if( fileTable.Count < imgCount )
				throw new InvalidDataException(
					$"file_table has {fileTable.Count}, expected ≥ IMG entries {imgCount}." );

			var originalEntries = ReadOriginalImgLayoutFromBaseIso(
				extractedRoot,
				ctx,
				manifest,
				log );

			if( originalEntries.Count != imgCount )
				throw new InvalidDataException(
					$"Original IMG layout entries ({originalEntries.Count}) != expected IMG count ({imgCount})." );

			var existing = originalEntries
				.Where( e => e.Exists )
				.OrderBy( e => e.StartSector )
				.Select( e => e.Index );

			var missing = originalEntries
				.Where( e => !e.Exists )
				.Select( e => e.Index );

			var physicalIndexOrder = existing.Concat( missing ).ToList();

			var seen = new bool[ imgCount ];
			foreach( var idx in physicalIndexOrder )
			{
				if( idx < 0 || idx >= imgCount )
					throw new InvalidDataException( $"Original IMG layout refers to bad index {idx}." );
				if( seen[ idx ] )
					throw new InvalidDataException( $"Original IMG layout produced duplicate index {idx} in physical order." );
				seen[ idx ] = true;
			}
			for( int i = 0; i < imgCount; i++ )
			{
				if( !seen[ i ] )
					throw new InvalidDataException( $"Original IMG layout did not produce an order entry for index {i}." );
			}

			log?.Report( "FF2 IMG_BD build: using original CD data table order (StartSector, Exists) as physical build order." );

			Directory.CreateDirectory( outDir );
			string imgBdPath = Path.Combine( outDir, "img_bd.bin" );
			string mapCsvPath = Path.Combine( outDir, "img_map.csv" );
			string orderCsvPath = Path.Combine( outDir, "img_order_by_sector.csv" );

			var entries = new (uint StartSector, uint Size, uint CmpSize, bool Compressed, bool Exists, string RelPath)[ imgCount ];

			const int WBUF = 128 * 1024;
			await using var bd = new FileStream(
				imgBdPath, FileMode.Create, FileAccess.Write, FileShare.None, WBUF,
				FileOptions.Asynchronous | FileOptions.SequentialScan );

			long currentOffset = 0;
			uint currentSector = 0;

			for( int phys = 0; phys < physicalIndexOrder.Count; phys++ )
			{
				ct.ThrowIfCancellationRequested();

				int idx = physicalIndexOrder[ phys ];
				var orig = originalEntries[ idx ];

				string relPath = fileTable[ idx ];
				string srcPath = ResolveSrcPath( extractedRoot, relPath );

				uint startSector = currentSector;
				uint size = 0;
				uint cmpSize = 0;
				bool compressed = false;
				bool exists = orig.Exists;

				byte[]? plainBytes = null;

				if( File.Exists( srcPath ) )
				{
					plainBytes = await File.ReadAllBytesAsync( srcPath, ct );
				}
				else if( relPath.EndsWith( ".pk2", StringComparison.OrdinalIgnoreCase ) )
				{
					string? pk2Folder = FindPk2FolderForRel( extractedRoot, relPath );
					if( pk2Folder != null && Directory.Exists( pk2Folder ) )
					{
						using var ms = new MemoryStream();
						Archive.PK2.Pk2Builder.BuildFromFolder( pk2Folder, ms );
						plainBytes = ms.ToArray();
					}
				}
				else if( relPath.EndsWith( ".pak", StringComparison.OrdinalIgnoreCase ) )
				{
					string? pakFolder = FindPakFolderForRel( extractedRoot, relPath );
					if( pakFolder != null && Directory.Exists( pakFolder ) )
					{
						using var ms = new MemoryStream();
						Archive.PAK.PakBuilder.BuildFromFolder( pakFolder, ms );
						plainBytes = ms.ToArray();
					}
				}

				if( ( plainBytes == null ) && !File.Exists( srcPath ) )
				{
					if( !options.AllowMissing && exists )
						throw new FileNotFoundException( relPath, srcPath );

					log?.Report(
						$"[phys {phys + 1}/{physicalIndexOrder.Count}] idx={idx} {relPath}  (missing, exists={exists})  size=0  start_sec={startSector}" );

					entries[ idx ] = (startSector, 0u, orig.CmpSize, orig.Compressed, exists, relPath);
					continue;
				}

				if( plainBytes != null && plainBytes.Length == 0 )
				{
					log?.Report(
						$"[phys {phys + 1}/{physicalIndexOrder.Count}] idx={idx} {relPath}  (zero-byte file, exists={exists}) start_sec={startSector}" );

					size = 0;
					compressed = orig.Compressed;
					cmpSize = orig.CmpSize;

					entries[ idx ] = (startSector, size, cmpSize, compressed, exists, relPath);
					continue;
				}

				if( plainBytes == null )
				{
					if( !options.AllowMissing && exists )
						throw new FileNotFoundException( relPath, srcPath );

					log?.Report(
						$"[phys {phys + 1}/{physicalIndexOrder.Count}] idx={idx} {relPath}  (no data, treated as missing)  start_sec={startSector}" );
					entries[ idx ] = (startSector, 0u, orig.CmpSize, orig.Compressed, exists, relPath);
					continue;
				}

				if( options.StrictSizes && plainBytes.LongLength > uint.MaxValue )
					throw new InvalidDataException( srcPath );

				size = (uint)plainBytes.Length;

				byte[] bytesToWrite;
				if( orig.Compressed && size > 0 )
				{
					int divSize = 0x8000;

					if( !Less.TryCompress( plainBytes, out var encoded, divSize ) )
						throw new InvalidDataException( $"LESS compression failed for {relPath}" );

					bytesToWrite = encoded;
					compressed = true;
					cmpSize = (uint)encoded.Length;
				}
				else
				{
					bytesToWrite = plainBytes;
					compressed = false;
					cmpSize = orig.CmpSize;
				}

				await bd.WriteAsync( bytesToWrite.AsMemory( 0, bytesToWrite.Length ), ct );

				int pad = PadToSector( bytesToWrite.Length );
				if( pad > 0 )
					await WriteZerosAsync( bd, pad, ct );

				long totalBytes = (long)bytesToWrite.Length + pad;
				currentOffset += totalBytes;
				currentSector = (uint)( currentOffset / SectorSize );

				log?.Report(
					$"[phys {phys + 1}/{physicalIndexOrder.Count}] idx={idx} {relPath}  size=0x{size:X} cmp_size=0x{cmpSize:X} cmp={( compressed ? 1 : 0 )} exists={( exists ? 1 : 0 )} start_sec={startSector} -> img_bd@0x{startSector * (long)SectorSize:X}" );

				entries[ idx ] = (startSector, size, cmpSize, compressed, exists, relPath);
			}

			await bd.FlushAsync( ct );

			var sb = new StringBuilder();
			sb.AppendLine( "index,start_sector,size,cmp_size,exists,compressed,rel_path" );
			for( int i = 0; i < entries.Length; i++ )
			{
				var e = entries[ i ];
				sb.Append( i );
				sb.Append( ',' );
				sb.Append( e.StartSector );
				sb.Append( ',' );
				sb.Append( e.Size );
				sb.Append( ',' );
				sb.Append( e.CmpSize );
				sb.Append( ',' );
				sb.Append( e.Exists ? "1" : "0" );
				sb.Append( ',' );
				sb.Append( e.Compressed ? "1" : "0" );
				sb.Append( ',' );
				sb.AppendLine( e.RelPath.Replace( '\\', '/' ) );
			}
			await File.WriteAllTextAsync( mapCsvPath, sb.ToString(), ct );

			var sbOrder = new StringBuilder();
			sbOrder.AppendLine( "phys_order,index,start_sector,size,cmp_size,exists,compressed,rel_path" );
			for( int phys = 0; phys < physicalIndexOrder.Count; phys++ )
			{
				int idx = physicalIndexOrder[ phys ];
				var e = entries[ idx ];
				sbOrder.Append( phys );
				sbOrder.Append( ',' );
				sbOrder.Append( idx );
				sbOrder.Append( ',' );
				sbOrder.Append( e.StartSector );
				sbOrder.Append( ',' );
				sbOrder.Append( e.Size );
				sbOrder.Append( ',' );
				sbOrder.Append( e.CmpSize );
				sbOrder.Append( ',' );
				sbOrder.Append( e.Exists ? "1" : "0" );
				sbOrder.Append( ',' );
				sbOrder.Append( e.Compressed ? "1" : "0" );
				sbOrder.Append( ',' );
				sbOrder.AppendLine( e.RelPath.Replace( '\\', '/' ) );
			}
			await File.WriteAllTextAsync( orderCsvPath, sbOrder.ToString(), ct );

			return new ImgBuildResult
			{
				ImgBdPath = imgBdPath,
				ImgBdBytes = currentOffset,
				Entries = entries,
				MapCsvPath = mapCsvPath
			};
		}

		private static List<OriginalImgEntry> ReadOriginalImgLayoutFromBaseIso(
			string extractedRoot,
			GameContext ctx,
			ManifestJson manifest,
			IProgress<string>? log )
		{
			if( ctx is not IZero2ImgLayout hasLayout )
				throw new NotSupportedException( $"Context {ctx.Key} has no Zero2 IMG layout (IZero2ImgLayout)." );

			var imgLayout = hasLayout.Img;
			int imgCount = imgLayout.Count;

			if( manifest.IsoToc is null || manifest.IsoToc.Count == 0 )
				throw new InvalidDataException( "Manifest iso_toc missing or empty; cannot map LBAs for ELF." );

			var elfEntry = manifest.IsoToc.FirstOrDefault( e => IsElfPath( e.Path, ctx.ElfName ) )
				?? throw new InvalidDataException( $"ELF entry for {ctx.ElfName} not found in iso_toc." );

			var baseIsoPath = Path.Combine( extractedRoot, "_base.iso" );
			if( !File.Exists( baseIsoPath ) )
				throw new FileNotFoundException( "Base ISO not found (expected _base.iso).", baseIsoPath );

			var entries = new List<OriginalImgEntry>( imgCount );

			using var isoFsStream = new FileStream(
				baseIsoPath,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read,
				1 << 20,
				FileOptions.RandomAccess );

			long elfOffset = (long)elfEntry.Lba * SectorSize;
			long tableOffset = imgLayout.AddrDataTable;
			long tableAbsOffset = elfOffset + tableOffset;

			byte[] tbl = new byte[ checked(imgCount * 12) ];

			isoFsStream.Position = tableAbsOffset;
			int read = isoFsStream.Read( tbl, 0, tbl.Length );
			if( read != tbl.Length )
				throw new InvalidDataException(
					$"Base ELF cd table size mismatch: expected {tbl.Length} bytes, got {read}." );

			for( int i = 0; i < imgCount; i++ )
			{
				int off = i * 12;
				uint startField = BinaryPrimitives.ReadUInt32LittleEndian( tbl.AsSpan( off + 0, 4 ) );
				uint size = BinaryPrimitives.ReadUInt32LittleEndian( tbl.AsSpan( off + 4, 4 ) );
				uint cmpSize = BinaryPrimitives.ReadUInt32LittleEndian( tbl.AsSpan( off + 8, 4 ) );

				bool exists;
				bool cmp;
				uint startSector;

				if( ctx.Key.Region == GameRegion.PROTO )
				{
					exists = ( size != 0 ) || ( cmpSize != 0 );
					cmp = ( cmpSize != size );
					startSector = startField;
				}
				else
				{
					cmp = ( startField & 1 ) != 0;
					exists = ( ( startField >> 1 ) & 1 ) != 0;
					startSector = startField >> 2;
				}

				string rel = i < ctx.FileTable.Count ? ctx.FileTable[ i ] : $"#{i}";

				entries.Add(
					new OriginalImgEntry(
						Index: i,
						StartSector: startSector,
						Size: size,
						CmpSize: cmpSize,
						Exists: exists,
						Compressed: cmp,
						RelPath: rel
					)
				);
			}

			var tableMeta = manifest.FileTable;
			if( tableMeta is not null && tableMeta.Count >= entries.Count )
			{
				for( int i = 0; i < entries.Count; i++ )
				{
					var meta = tableMeta[ i ];
					if( meta == null )
						continue;
					var e = entries[ i ];
					bool exists = meta.Exists ?? e.Exists;
					bool compressed = meta.Compressed ?? e.Compressed;
					entries[ i ] = e with { Exists = exists, Compressed = compressed };
				}
			}

			log?.Report( $"FF2: read original IMG layout from base ELF: {entries.Count} entries." );
			return entries;
		}


		private static async Task PatchElfAsync(
			string elfPath,
			GameContext ctx,
			ImgBuildResult img,
			IProgress<string>? log,
			CancellationToken ct )
		{
			if( ctx is not IZero2ImgLayout hasLayout )
				throw new NotSupportedException( $"Context {ctx.Key} has no Zero2 IMG layout (IZero2ImgLayout)." );

			var layout = hasLayout.Img;
			long tableOffset = layout.AddrDataTable;
			int count = img.Entries.Length;
			var buf = new byte[ checked(count * 12) ];

			await using var fs = new FileStream(
				elfPath,
				FileMode.Open,
				FileAccess.ReadWrite,
				FileShare.Read,
				128 * 1024,
				FileOptions.SequentialScan );

			if( tableOffset < 0 || tableOffset + buf.Length > fs.Length )
				throw new InvalidDataException( $"ELF cd table out of range: off=0x{tableOffset:X}, size=0x{buf.Length:X}, elfLen=0x{fs.Length:X}." );

			fs.Position = tableOffset;
			int read = await fs.ReadAsync( buf.AsMemory( 0, buf.Length ), ct );
			if( read != buf.Length )
				throw new InvalidDataException( $"ELF cd table size mismatch: expected {buf.Length} bytes, got {read}." );

			var orig = (byte[])buf.Clone();

			for( int i = 0; i < count; i++ )
			{
				var e = img.Entries[ i ];
				uint startSector = e.StartSector;
				uint size = e.Size;
				uint cmpSize = e.CmpSize;
				bool cmp = e.Compressed && cmpSize != 0;
				bool exists = e.Exists;

				int off = i * 12;

				if( ctx.Key.Region == GameRegion.PROTO )
				{
					uint startField = exists ? startSector : 0u;
					uint sizeField = exists ? size : 0u;
					uint cmpSizeField = exists ? cmpSize : 0u;

					BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( off + 0, 4 ), startField );
					BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( off + 4, 4 ), sizeField );
					BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( off + 8, 4 ), cmpSizeField );
				}
				else
				{
					uint origStartField = BinaryPrimitives.ReadUInt32LittleEndian( orig.AsSpan( off + 0, 4 ) );
					uint origSizeField = BinaryPrimitives.ReadUInt32LittleEndian( orig.AsSpan( off + 4, 4 ) );
					uint origCmpSizeField = BinaryPrimitives.ReadUInt32LittleEndian( orig.AsSpan( off + 8, 4 ) );

					bool origCmp = ( origStartField & 1 ) != 0;
					bool origExists = ( ( origStartField >> 1 ) & 1 ) != 0;

					if( !exists && origExists )
					{
						string rel = e.RelPath ?? $"#{i}";
						log?.Report(
							$"FF2 ELF: entry {i} '{rel}' was present in original table (size=0x{origSizeField:X}, cmpSize=0x{origCmpSizeField:X}, cmp={( origCmp ? 1 : 0 )}) but is missing in rebuilt IMG_BD (exists=0)." );
					}

					uint field = startSector << 2;
					if( cmp )
						field |= 1u;
					if( exists )
						field |= 2u;

					BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( off + 0, 4 ), field );
					BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( off + 4, 4 ), size );
					BinaryPrimitives.WriteUInt32LittleEndian( buf.AsSpan( off + 8, 4 ), cmpSize );
				}
			}

			fs.Position = tableOffset;
			await fs.WriteAsync( buf.AsMemory( 0, buf.Length ), ct );

			log?.Report( $"FF2 ELF: patched IMG table at 0x{tableOffset:X} for {count} entries." );
		}

		private static int PadToSector( int size ) => ( SectorSize - ( size % SectorSize ) ) & ( SectorSize - 1 );

		private static async Task WriteZerosAsync( FileStream s, int count, CancellationToken ct )
		{
			const int BUF = 16 * 1024;
			byte[] zero = ArrayPool<byte>.Shared.Rent( BUF );
			try
			{
				Array.Clear( zero, 0, BUF );
				int left = count;
				while( left > 0 )
				{
					int chunk = Math.Min( BUF, left );
					await s.WriteAsync( zero.AsMemory( 0, chunk ), ct );
					left -= chunk;
				}
			}
			finally
			{
				ArrayPool<byte>.Shared.Return( zero, clearArray: true );
			}
		}

		private static string ResolveSrcPath( string binRoot, string rel )
		{
			var relOS = rel.Replace( '/', Path.DirectorySeparatorChar );
			var p = Path.Combine( binRoot, relOS );
			if( File.Exists( p ) )
				return p;

			var prefix = "bin" + Path.DirectorySeparatorChar;
			if( relOS.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) )
			{
				var trimmed = relOS.Substring( prefix.Length );
				var alt = Path.Combine( binRoot, trimmed );
				if( File.Exists( alt ) )
					return alt;
			}

			var withBin = Path.Combine( binRoot, Path.Combine( "bin", relOS ) );
			if( File.Exists( withBin ) )
				return withBin;

			return p;
		}

		private static string? FindPk2FolderForRel( string extractedRoot, string rel )
		{
			var relOS = rel.Replace( '/', Path.DirectorySeparatorChar );
			var dir = Path.GetDirectoryName( relOS ) ?? string.Empty;
			var stem = Path.GetFileNameWithoutExtension( relOS );
			var baseDir = Path.Combine( extractedRoot, dir );

			var candIndex = Path.Combine( baseDir, stem + "_pk2_index" );
			if( Directory.Exists( candIndex ) )
				return candIndex;

			var candLinked = Path.Combine( baseDir, stem + "_pk2_linked" );
			if( Directory.Exists( candLinked ) )
				return candLinked;

			return null;
		}

		private static string? FindPakFolderForRel( string extractedRoot, string rel )
		{
			var relOS = rel.Replace( '/', Path.DirectorySeparatorChar );
			var dir = Path.GetDirectoryName( relOS ) ?? string.Empty;
			var stem = Path.GetFileNameWithoutExtension( relOS );
			var baseDir = Path.Combine( extractedRoot, dir );
			var cand = Path.Combine( baseDir, stem + "_pak" );
			if( Directory.Exists( cand ) )
				return cand;
			return null;
		}

		private static bool IsElfPath( string tocPath, string elfName )
		{
			var s = tocPath.Replace( '/', '\\' );
			var leaf = s.Contains( '\\' ) ? s.Substring( s.LastIndexOf( '\\' ) + 1 ) : s;
			leaf = leaf.ToUpperInvariant();

			var elfLeaf = elfName.ToUpperInvariant();
			return leaf == elfLeaf || leaf == ( elfLeaf + ";1" );
		}
	}
}
