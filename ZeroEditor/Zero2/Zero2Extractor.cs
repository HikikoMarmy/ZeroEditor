using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroEditor.Game;
using ZeroEditor.Zero2.Common;
using ZeroEditor.Compress;

namespace ZeroEditor
{
	public sealed class Zero2Extractor : IGameExtractor
	{
		private static readonly ZeroLess Less = ZeroLess.Instance;
		private static bool WriteDebugImgMap = false;

		public async Task ExtractBinAsync(
			FileStream isoStream,
			GameContext ctx,
			string outputRoot,
			IProgress<string>? log,
			CancellationToken ct )
		{
			if( ctx is not IZero2ImgLayout hasLayout )
				throw new NotSupportedException( $"Context {ctx.Key} has no Zero2 IMG layout (IZero2ImgLayout)." );

			var layout = hasLayout.Img;
			if( ctx.FileTable.Count < layout.Count )
				throw new InvalidDataException( $"file_table has {ctx.FileTable.Count}, expected ≥ {layout.Count}" );

			var elfPath = Path.Combine( outputRoot, ctx.ElfName );
			if( !File.Exists( elfPath ) )
				throw new FileNotFoundException( $"ELF not found for FF2 context {ctx.Key.Serial}", elfPath );

			byte[] tbl = new byte[ checked(layout.Count * 12) ];
			using( var elf = new FileStream( elfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite ) )
			{
				if( layout.AddrDataTable < 0 || layout.AddrDataTable + tbl.Length > elf.Length )
					throw new InvalidDataException( $"FF2 AddrDataTable out of range for ELF {ctx.ElfName}" );

				elf.Position = layout.AddrDataTable;
				ReadExactly( elf, tbl, 0, tbl.Length );
			}

			bool isProto = ctx.Key.Region == GameRegion.PROTO;
			const int SectorSize = 2048;

			long imgBdBase = ResolveImgBdBaseOffset( isoStream );
			var existsFlags = new bool[ layout.Count ];
			var cmpFlags = new bool[ layout.Count ];

			StringBuilder? mapSb = null;
			string? mapPath = null;

			if( WriteDebugImgMap )
			{
				mapPath = Path.Combine( outputRoot, "img_map_original.csv" );
				mapSb = new StringBuilder();
				mapSb.AppendLine( "index,start_sector,size,cmp_size,exists,compressed,rel_path" );
			}

			for( int i = 0; i < layout.Count; i++ )
			{
				DecodeImgEntry(
					isProto,
					tbl,
					i,
					out bool exists,
					out bool cmp,
					out uint startSector,
					out uint size,
					out uint cmpSize );

				existsFlags[ i ] = exists;
				cmpFlags[ i ] = cmp;

				if( WriteDebugImgMap )
				{
					string relMap = i < ctx.FileTable.Count ? ctx.FileTable[ i ] : "";

					mapSb!.Append( i );
					mapSb.Append( ',' );
					mapSb.Append( startSector );
					mapSb.Append( ',' );
					mapSb.Append( size );
					mapSb.Append( ',' );
					mapSb.Append( cmpSize );
					mapSb.Append( ',' );
					mapSb.Append( exists ? "1" : "0" );
					mapSb.Append( ',' );
					mapSb.Append( cmp ? "1" : "0" );
					mapSb.Append( ',' );
					mapSb.AppendLine( relMap.Replace( '\\', '/' ) );
				}
			}

			if( WriteDebugImgMap && mapPath != null && mapSb != null )
			{
				await File.WriteAllTextAsync( mapPath, mapSb.ToString(), ct );
				log?.Report( $"FF2 original map: {Path.GetRelativePath( outputRoot, mapPath )}" );
			}

			TryUpdateManifestFlags( outputRoot, layout.Count, existsFlags, cmpFlags );

			for( int i = 0; i < layout.Count; i++ )
			{
				ct.ThrowIfCancellationRequested();

				DecodeImgEntry(
					isProto,
					tbl,
					i,
					out bool exists,
					out bool cmp,
					out uint startSector,
					out uint size,
					out uint cmpSize );

				if( !exists )
					continue;

				var rel = ctx.FileTable[ i ];
				var outPath = Path.Combine( outputRoot, rel.Replace( '/', Path.DirectorySeparatorChar ) );
				Directory.CreateDirectory( Path.GetDirectoryName( outPath )! );

				long abs = checked(imgBdBase + (long)startSector * SectorSize);
				isoStream.Position = abs;

				if( cmp )
				{
					uint toRead = ( cmpSize != 0 ) ? cmpSize : size;

					if( toRead == 0 || size == 0 )
					{
						using( File.Create( outPath ) )
						{
						}
						log?.Report( $"FF2 BIN: [{i}/{layout.Count}] (cmp dmy -> empty) {rel}" );
						continue;
					}

					var cmpBuf = new byte[ toRead ];
					ReadExactly( isoStream, cmpBuf, 0, cmpBuf.Length );

					if( !Less.TryDecompress( cmpBuf, out var dec ) )
					{
						await File.WriteAllBytesAsync( outPath, cmpBuf, ct );
						log?.Report( $"FF2 BIN: [{i}/{layout.Count}] LESS failed -> wrote RAW ({cmpBuf.Length} bytes): {rel}" );
						continue;
					}

					if( !isProto && size != 0 && dec.Length != size )
						throw new InvalidDataException( $"LESS size mismatch at {i} ({rel}): dec=0x{dec.Length:X}, expected=0x{size:X}" );

					await File.WriteAllBytesAsync( outPath, dec, ct );
					log?.Report( $"FF2 BIN: [{i}/{layout.Count}] 0x{abs:X8} cmp=0x{toRead:X8} -> {rel} (dec)" );
				}
				else
				{
					if( size == 0 )
					{
						using( File.Create( outPath ) )
						{
						}
						log?.Report( $"FF2 BIN: [{i}/{layout.Count}] (empty) -> {rel}" );
						continue;
					}

					await CopyToFileAsync( isoStream, outPath, size, ct );
					log?.Report( $"FF2 BIN: [{i}/{layout.Count}] 0x{abs:X8} size=0x{size:X8} -> {rel}" );
				}

				if( outPath.EndsWith( ".pk2", StringComparison.OrdinalIgnoreCase ) )
				{
					await TryExplodePk2Async( outPath, outputRoot, log, ct );
				}
				else if( outPath.EndsWith( ".pak", StringComparison.OrdinalIgnoreCase ) )
				{
					await TryExplodePakAsync( outPath, outputRoot, log, ct );
				}
			}
		}

		private static void TryUpdateManifestFlags(
			string outputRoot,
			int count,
			bool[] existsFlags,
			bool[] cmpFlags )
		{
			string manifestPath = Path.Combine( outputRoot, "_manifest.json" );
			if( !File.Exists( manifestPath ) )
				return;

			var manifest = ManifestJson.Load( manifestPath );

			if( manifest.FileTable is { Count: > 0 } fileTable )
			{
				int n = Math.Min( fileTable.Count, count );
				for( int i = 0; i < n; i++ )
				{
					var meta = fileTable[ i ];
					if( meta == null )
						continue;
					fileTable[ i ] = new ManifestJson.FileEntry
					{
						Path = meta.Path,
						Exists = existsFlags[ i ],
						Compressed = cmpFlags[ i ]
					};
				}
			}

			ManifestJson.Save( manifestPath, manifest );
		}

		private static void DecodeImgEntry(
			bool isProto,
			byte[] tbl,
			int index,
			out bool exists,
			out bool cmp,
			out uint startSector,
			out uint size,
			out uint cmpSize )
		{
			int off = index * 12;
			uint startField = BinaryPrimitives.ReadUInt32LittleEndian( tbl.AsSpan( off + 0, 4 ) );
			size = BinaryPrimitives.ReadUInt32LittleEndian( tbl.AsSpan( off + 4, 4 ) );
			cmpSize = BinaryPrimitives.ReadUInt32LittleEndian( tbl.AsSpan( off + 8, 4 ) );

			if( isProto )
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
		}

		private static long ResolveImgBdBaseOffset( FileStream isoStream )
		{
			long prev = isoStream.Position;
			try
			{
				using var iso = IsoFs.Wrap( isoStream );
				if( TryGetAny( iso, out var imgBd, "IMG_BD.BIN" ) )
				{
					return (long)imgBd.Lba * IsoFs.SectorSize;
				}
			}
			finally
			{
				isoStream.Position = prev;
			}

			throw new InvalidDataException( "IMG_BD not found in ISO by common names." );
		}

		private static bool TryGetAny( IsoFs iso, out IsoFs.Entry? e, params string[] names )
		{
			e = null;
			foreach( var n in names )
			{
				if( iso.TryGetEntry( n, out var hit ) && !hit.IsDir )
				{
					e = hit;
					return true;
				}
			}
			return false;
		}

		private static async Task TryExplodePk2Async(
			string pk2Path,
			string outputRoot,
			IProgress<string>? log,
			CancellationToken ct )
		{
			var dir = Path.GetDirectoryName( pk2Path )!;
			var stem = Path.GetFileNameWithoutExtension( pk2Path );

			string workDir;

			try
			{
				using var fs = new FileStream( pk2Path, FileMode.Open, FileAccess.Read, FileShare.Read );
				var arc = ZeroEditor.Archive.PK2.Pk2Archive.Open( fs );

				string typeSuffix = arc.Variant == ZeroEditor.Archive.PK2.Pk2Variant.Indexed
					? "_pk2_index"
					: "_pk2_linked";

				workDir = Path.Combine( dir, stem + typeSuffix );
				Directory.CreateDirectory( workDir );

				fs.Position = 0;
				arc.ExtractAllTo( fs, workDir );

				log?.Report(
					$"PK2: exploded -> {Path.GetRelativePath( outputRoot, workDir )}/ " +
					$"(from {Path.GetRelativePath( outputRoot, pk2Path )})" );
			}
			catch( Exception ex )
			{
				log?.Report(
					$"PK2: failed to explode {Path.GetFileName( pk2Path )} ({ex.Message}), kept as file." );
				return;
			}

			try
			{
				await DeleteWithRetryAsync( pk2Path, 5, 60, ct );
				log?.Report(
					$"PK2: deleted original archive after explode: {Path.GetRelativePath( outputRoot, pk2Path )}" );
			}
			catch( Exception ex )
			{
				log?.Report(
					$"PK2: exploded but could not delete original ({ex.Message}), file left in place: {Path.GetRelativePath( outputRoot, pk2Path )}" );
			}
		}

		private static async Task TryExplodePakAsync(
			string pakPath,
			string outputRoot,
			IProgress<string>? log,
			CancellationToken ct )
		{
			var root = Path.Combine(
				Path.GetDirectoryName( pakPath )!,
				Path.GetFileNameWithoutExtension( pakPath ) + "_pak" );

			int written = 0;

			try
			{
				using var fs = new FileStream( pakPath, FileMode.Open, FileAccess.Read, FileShare.Read );
				var arc = ZeroEditor.Archive.PAK.PakArchive.Open( fs );
				fs.Position = 0;
				written = arc.ExtractAllTo( fs, root );
			}
			catch( Exception ex )
			{
				log?.Report(
					$"PAK: failed to explode {Path.GetFileName( pakPath )} ({ex.Message}), kept as file." );
				return;
			}

			if( written > 0 )
				log?.Report( $"PAK: exploded -> {Path.GetRelativePath( outputRoot, root )}/ ({written} files)" );

			try
			{
				await DeleteWithRetryAsync( pakPath, 5, 60, ct );
			}
			catch( Exception ex )
			{
				log?.Report(
					$"PAK: exploded but could not delete original ({ex.Message}), file left in place: {Path.GetRelativePath( outputRoot, pakPath )}" );
			}
		}

		private static async Task DeleteWithRetryAsync( string path, int attempts, int delayMs, CancellationToken ct )
		{
			for( int i = 0; i < attempts; i++ )
			{
				try
				{
					File.Delete( path );
					return;
				}
				catch( IOException ) when( i < attempts - 1 )
				{
					await Task.Delay( delayMs, ct );
				}
				catch( UnauthorizedAccessException ) when( i < attempts - 1 )
				{
					await Task.Delay( delayMs, ct );
				}
			}
			File.Delete( path );
		}

		private static async Task CopyToFileAsync( Stream src, string outPath, uint size, CancellationToken ct )
		{
			const int B = 128 * 1024;
			var buf = new byte[ B ];
			long left = size;

			await using var dst = new FileStream( outPath, FileMode.Create, FileAccess.Write, FileShare.None, B, useAsync: true );

			while( left > 0 )
			{
				int toRead = (int)Math.Min( B, left );
				int got = src.Read( buf, 0, toRead );
				if( got != toRead )
					throw new EndOfStreamException();

				await dst.WriteAsync( buf.AsMemory( 0, got ), ct );
				left -= got;
			}
		}

		private static void ReadExactly( Stream s, byte[] b, int o, int c )
		{
			int t = 0;
			while( t < c )
			{
				int r = s.Read( b, o + t, c - t );
				if( r <= 0 )
					break;
				t += r;
			}
			if( t != c )
				throw new EndOfStreamException();
		}
	}
}
