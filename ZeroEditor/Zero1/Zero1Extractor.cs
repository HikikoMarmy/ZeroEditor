using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroEditor.Game;
using ZeroEditor.Archive;
using ZeroEditor.Zero1.Common;
using ZeroEditor.Iso;

namespace ZeroEditor
{
	public sealed class Zero1Extractor : IGameExtractor
	{
		public Task ExtractBinAsync(
			FileStream isoStream,
			GameRegion region,
			IReadOnlyList<string> names,
			string outputRoot,
			IProgress<string>? log,
			CancellationToken ct )
			=> throw new NotSupportedException( "Use the overload that takes GameContext." );

		public async Task ExtractBinAsync(
			FileStream isoStream,
			GameContext ctx,
			string outputRoot,
			IProgress<string>? log,
			CancellationToken ct )
		{
			if( ctx.FileTable == null || ctx.FileTable.Count == 0 )
				throw new InvalidDataException( "file_table is empty." );

			int count = ctx.FileTable.Count;

			using var iso = IsoFs.Wrap( isoStream );

			if( !TryGetAny( iso, out var imgHd, "IMG_HD.BIN;1", "IMG_HD.BIN", "IMG_HD;1", "IMG_HD" ) )
				throw new InvalidDataException( "IMG_HD not found in ISO." );

			if( !TryGetAny( iso, out var imgBd, "IMG_BD.BIN;1", "IMG_BD.BIN", "IMG_BD;1", "IMG_BD" ) )
				throw new InvalidDataException( "IMG_BD not found in ISO." );

			var tbl = iso.ReadFile( imgHd.PathRaw );
			int expectedTblLen = count * 8;
			if( tbl.Length < expectedTblLen )
				throw new InvalidDataException( $"IMG_HD too small: {tbl.Length} bytes, expected at least {expectedTblLen}." );

			var existsFlags = new bool[ count ];
			var cmpFlags = new bool[ count ];

			for( int i = 0; i < count; i++ )
			{
				ct.ThrowIfCancellationRequested();

				int off = i * 8;
				uint startSector = BinaryPrimitives.ReadUInt32LittleEndian( tbl.AsSpan( off, 4 ) );
				uint size = BinaryPrimitives.ReadUInt32LittleEndian( tbl.AsSpan( off + 4, 4 ) );

				bool exists = size != 0;
				bool cmp = false;

				existsFlags[ i ] = exists;
				cmpFlags[ i ] = cmp;

				var rel = ctx.FileTable[ i ];
				var outPath = Path.Combine( outputRoot, rel.Replace( '/', Path.DirectorySeparatorChar ) );
				Directory.CreateDirectory( Path.GetDirectoryName( outPath )! );

				if( size == 0 )
				{
					using( File.Create( outPath ) )
					{
					}
					log?.Report( $"FF1 BIN: [{i}/{count}] (empty) -> {rel}" );
					continue;
				}

				long abs = imgBd.Offset + (long)startSector * IsoFs.SectorSize;
				isoStream.Position = abs;
				await CopyToFileAsync( isoStream, outPath, size, ct );

				if( outPath.EndsWith( ".pk2", StringComparison.OrdinalIgnoreCase ) )
					await TryExplodePk2Async( outPath, outputRoot, log, ct );

				log?.Report( $"FF1 BIN: [{i}/{count}] 0x{abs:X8} size=0x{size:X8} -> {rel}" );
			}

			TryUpdateManifestFlags( outputRoot, count, existsFlags, cmpFlags );
		}

		private static bool TryGetAny( IsoFs iso, out IsoFs.Entry? e, params string[] names )
		{
			e = null;
			foreach( var n in names )
				if( iso.TryGetEntry( n, out var hit ) && !hit.IsDir )
				{
					e = hit;
					return true;
				}
			return false;
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

		private static async Task TryExplodePk2Async( string pk2Path, string outputRoot, IProgress<string>? log, CancellationToken ct )
		{
			byte[] buffer;
			try
			{
				buffer = await File.ReadAllBytesAsync( pk2Path, ct );
			}
			catch( Exception ex )
			{
				log?.Report( $"PK2: could not read ({ex.Message}), kept as file: {Path.GetRelativePath( outputRoot, pk2Path )}" );
				return;
			}

			try
			{
				string typeSuffix;
				using( var msDetect = new MemoryStream( buffer, writable: false ) )
				{
					var arc = Archive.PK2.Pk2Archive.Open( msDetect );
					typeSuffix = arc.Variant == Archive.PK2.Pk2Variant.Indexed ? "_pk2_index" : "_pk2_linked";
				}

				var folder = Path.Combine(
					Path.GetDirectoryName( pk2Path )!,
					Path.GetFileNameWithoutExtension( pk2Path ) + typeSuffix );

				using( var msExtract = new MemoryStream( buffer, writable: false ) )
				{
					Directory.CreateDirectory( folder );
					Archive.PK2.Pk2Archive.ExtractAll( msExtract, folder );
				}

				log?.Report( $"PK2: exploded -> {Path.GetFileName( folder )}/ ({Directory.GetFiles( folder ).Length} files)" );
				await DeleteWithRetryAsync( pk2Path, 5, 60, ct );
			}
			catch( Exception ex )
			{
				log?.Report( $"PK2: failed to explode ({ex.Message}), kept as file: {Path.GetRelativePath( outputRoot, pk2Path )}" );
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
			await using var dst = new FileStream( outPath, FileMode.Create, FileAccess.Write, FileShare.None, B, true );
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
	}
}
