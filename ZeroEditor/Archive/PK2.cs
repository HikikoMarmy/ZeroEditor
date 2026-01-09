using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ZeroEditor.Archive.PK2
{
	public sealed class Pk2Entry
	{
		public int Index { get; init; }
		public long EntryPtr { get; init; }
		public int Size { get; init; }
		public uint SizeFlags { get; init; }
		public long DataOffset => EntryPtr + 8;
		public string SuggestedExtension { get; init; } = ".bin";
	}

	public static class Pk2Util
	{
		public static int Align16( int v ) => v + 15 & ~15;
		public static long Align16( long v ) => v + 15L & ~15L;

		public static void WritePad( BinaryWriter bw, long count, byte padByte )
		{
			if( count <= 0 )
				return;

			const int CHUNK = 4096;
			var buf = new byte[ Math.Min( (int)count, CHUNK ) ];
			for( int i = 0; i < buf.Length; i++ )
				buf[ i ] = padByte;

			long left = count;
			while( left > 0 )
			{
				int n = (int)Math.Min( left, buf.Length );
				bw.Write( buf, 0, n );
				left -= n;
			}
		}
	}

	public enum Pk2Variant { Linked = 0, Indexed = 1 }

	public sealed class Pk2Archive
	{
		public int Count { get; private set; }
		public IReadOnlyList<Pk2Entry> Entries => _entries;
		public Pk2Variant Variant { get; private set; } = Pk2Variant.Linked;

		private List<Pk2Entry> _entries = new();

		private static long GetEffectiveLength( Stream s )
		{
			long len = s.Length;
			if( len >= 16 )
			{
				long save = s.Position;
				try
				{
					s.Position = len - 16;
					Span<byte> tail = stackalloc byte[ 16 ];
					int got = s.Read( tail );
					if( got == 16 )
					{
						bool allFF = true;
						for( int i = 0; i < 16; i++ )
						{
							if( tail[ i ] != 0xFF )
							{
								allFF = false;
								break;
							}
						}
						if( allFF )
							return len - 16;
					}
				}
				finally
				{
					s.Position = save;
				}
			}
			return len;
		}

		public static Pk2Archive Open( Stream s )
		{
			if( !s.CanSeek )
				throw new NotSupportedException( "pk2: stream must be seekable." );

			long effLen = GetEffectiveLength( s );
			if( effLen < 16 )
				throw new InvalidDataException( "pk2: file too small for header." );

			using var br = new BinaryReader( s, System.Text.Encoding.ASCII, leaveOpen: true );

			s.Position = 0;
			int packNum = br.ReadInt32();
			br.ReadInt32();
			br.ReadInt32();
			br.ReadInt32();
			if( packNum < 0 || packNum > 100000 )
				throw new InvalidDataException( $"pk2: bad count {packNum}" );

			if( TryReadIndexTableVariant( s, br, packNum, effLen, out var idxEntries ) )
				return new Pk2Archive { Count = packNum, _entries = idxEntries, Variant = Pk2Variant.Indexed };

			var nodeEntries = ReadLinkedNodeVariant( s, br, packNum, effLen );
			return new Pk2Archive { Count = packNum, _entries = nodeEntries, Variant = Pk2Variant.Linked };
		}

		private static bool TryReadIndexTableVariant(
			Stream s,
			BinaryReader br,
			int packNum,
			long len,
			out List<Pk2Entry> entries )
		{
			entries = new List<Pk2Entry>();

			long tableStart = 0x10;
			long tableBytes = 4L * packNum;
			if( tableStart + tableBytes > len )
				return false;

			s.Position = tableStart;
			var offsets = new uint[ packNum ];
			for( int i = 0; i < packNum; i++ )
				offsets[ i ] = br.ReadUInt32();

			for( int i = 0; i < packNum; i++ )
			{
				long off = offsets[ i ];
				if( off < 0 || off >= len )
					return false;
				if( i > 0 && offsets[ i - 1 ] >= off )
					return false;
			}

			long minFirst = tableStart + tableBytes;
			long minFirstAligned = Pk2Util.Align16( minFirst );
			if( !( offsets[ 0 ] >= minFirst && offsets[ 0 ] <= minFirstAligned ) )
				return false;

			for( int i = 0; i < packNum; i++ )
			{
				long start = offsets[ i ];
				long end = i < packNum - 1 ? offsets[ i + 1 ] : len;
				if( end < start )
					return false;

				int physSize = checked((int)( end - start ));
				string ext = ZeroEditor.Archive.FileTyper.GuessFromStream( s, start, 64 );

				entries.Add( new Pk2Entry
				{
					Index = i,
					EntryPtr = start - 8,
					Size = physSize,
					SizeFlags = (uint)physSize,
					SuggestedExtension = ext
				} );
			}

			return true;
		}

		private static List<Pk2Entry> ReadLinkedNodeVariant(
			Stream s,
			BinaryReader br,
			int packNum,
			long len )
		{
			long nodeBase = 0x10;
			var entries = new List<Pk2Entry>( packNum );

			for( int i = 0; i < packNum; i++ )
			{
				if( nodeBase + 16 > len )
					break;

				s.Position = nodeBase;
				uint nextOff = br.ReadUInt32();

				s.Position = nodeBase + 0x08;
				uint sizeFlags = br.ReadUInt32();
				uint reserved = br.ReadUInt32();

				long entryPtr = nodeBase + 8;
				long dataOffset = entryPtr + 8;

				int logical = (int)( sizeFlags & 0x00FF_FFFF );
				bool logicalOk = logical > 0 && dataOffset + (long)logical <= len;

				int size;
				long nextBase;

				if( logicalOk )
				{
					size = logical;
					nextBase = Pk2Util.Align16( dataOffset + (long)logical );
				}
				else
				{
					long linkedNextBase = nodeBase + nextOff + 0x10L;
					bool linkOk = linkedNextBase > nodeBase && linkedNextBase <= len;

					if( linkOk )
					{
						long phys = linkedNextBase - dataOffset;
						if( phys < 0 || phys > int.MaxValue )
							throw new InvalidDataException( $"pk2: bad computed size at entry {i} (phys={phys})." );
						size = (int)phys;
						nextBase = linkedNextBase;
					}
					else
					{
						long tail = Math.Max( 0, len - dataOffset );
						size = (int)Math.Min( tail, int.MaxValue );
						nextBase = len;
					}
				}

				string ext = ZeroEditor.Archive.FileTyper.GuessFromStream( s, dataOffset, 64 );

				entries.Add( new Pk2Entry
				{
					Index = i,
					EntryPtr = entryPtr,
					Size = size,
					SizeFlags = sizeFlags,
					SuggestedExtension = ext
				} );

				nodeBase = nextBase;
				if( nodeBase >= len )
					break;
			}

			return entries;
		}

		private const int MAX_RECURSE_DEPTH = 32;

		private static bool LooksLikePak( Stream s )
		{
			if( !s.CanSeek || s.Length < 16 )
				return false;

			using var br = new BinaryReader( s, System.Text.Encoding.ASCII, leaveOpen: true );
			long save = s.Position;

			try
			{
				s.Position = 0;
				int count = br.ReadInt32();
				if( count <= 0 || count > 128 )
					return false;

				if( br.ReadInt32() != 0 || br.ReadInt32() != 0 || br.ReadInt32() != 0 )
					return false;

				if( s.Position + 16 > s.Length )
					return false;

				uint size0 = br.ReadUInt32();
				br.ReadUInt32();
				br.ReadUInt32();
				br.ReadUInt32();
				long data0 = s.Position;
				long end0 = data0 + size0;
				if( end0 < data0 || end0 > s.Length )
					return false;

				return true;
			}
			catch
			{
				return false;
			}
			finally
			{
				s.Position = save;
			}
		}

		private static int ExtractNestedPak( byte[] blob, string baseDir, string baseName, int depth )
		{
			if( depth > MAX_RECURSE_DEPTH )
				throw new InvalidDataException( "pk2: too-deep recursion into pak (cycle?)" );

			using var ms = new MemoryStream( blob, writable: false );

			if( !LooksLikePak( ms ) )
				return 0;

			ms.Position = 0;
			var pak = ZeroEditor.Archive.PAK.PakArchive.Open( ms );
			string outFolder = Path.Combine( baseDir, baseName + "_pak" );
			Directory.CreateDirectory( outFolder );
			ms.Position = 0;
			int written = pak.ExtractAllTo( ms, outFolder );
			return written;
		}

		public int ExtractAllTo( Stream s, string outDir )
		{
			if( !s.CanSeek )
				throw new NotSupportedException( "pk2: stream must be seekable." );

			using var br = new BinaryReader( s, System.Text.Encoding.ASCII, leaveOpen: true );
			long len = GetEffectiveLength( s );

			Directory.CreateDirectory( outDir );

			long logicalEndMax = 0;
			int filesWritten = 0;

			foreach( var e in Entries )
			{
				if( e.Size < 0 )
					throw new InvalidDataException( $"pk2: negative size at entry {e.Index}." );

				if( e.DataOffset < 0 || e.DataOffset > len )
					throw new EndOfStreamException( $"pk2: entry {e.Index} data offset past EOF (off=0x{e.DataOffset:X}, len=0x{len:X})." );

				long physEnd = e.DataOffset + e.Size;
				if( physEnd > len )
					throw new EndOfStreamException( $"pk2: entry {e.Index} data past EOF (end=0x{physEnd:X}, len=0x{len:X})." );

				long alignedEnd = Pk2Util.Align16( e.DataOffset + (long)e.Size );
				if( alignedEnd > logicalEndMax )
					logicalEndMax = alignedEnd;

				s.Position = e.DataOffset;
				byte[] data = e.Size == 0 ? Array.Empty<byte>() : br.ReadBytes( e.Size );

				string nameNoExt = e.Index.ToString( "D4" );
				string leafName = nameNoExt + e.SuggestedExtension;
				string outPath = Path.Combine( outDir, leafName );

				bool isPak = false;
				if( data.Length >= 16 )
				{
					using var ms = new MemoryStream( data, writable: false );
					isPak = LooksLikePak( ms );
				}

				File.WriteAllBytes( outPath, data );
				filesWritten++;

				try
				{
					if( isPak )
					{
						ExtractNestedPak( data, outDir, nameNoExt, 0 );
						try
						{
							if( File.Exists( outPath ) )
								File.Delete( outPath );
						}
						catch { }
					}
				}
				catch
				{
					// errors! keep raw file for debugging
				}
			}

			// Tail/meta for linked variant
			if( Variant == Pk2Variant.Linked && logicalEndMax > 0 && logicalEndMax < len )
			{
				string metaDir = Path.Combine( outDir, "__pk2_meta" );
				Directory.CreateDirectory( metaDir );
				string tailPath = Path.Combine( metaDir, "tail.bin" );

				s.Position = logicalEndMax;
				int tailLen = checked((int)( len - logicalEndMax ));
				if( tailLen > 0 )
				{
					byte[] tail = br.ReadBytes( tailLen );
					File.WriteAllBytes( tailPath, tail );
				}
			}

			return filesWritten;
		}

		public static void ExtractAll( Stream s, string outDir )
		{
			var arc = Open( s );
			arc.ExtractAllTo( s, outDir );
		}
	}

	public static class Pk2Builder
	{
		public static void BuildFromFolder( string folder, Stream outStream )
		{
			if( IsIndexFolder( folder ) )
				BuildIndexTableFromFolder( folder, outStream );
			else
				BuildLinkedNodeFromFolder( folder, outStream );
		}

		private static bool IsIndexFolder( string folder )
			=> Path.GetFileName( folder ).EndsWith( "_index", StringComparison.OrdinalIgnoreCase );

		private static string Stem4( string file )
		{
			var n = Path.GetFileNameWithoutExtension( file );
			if( n.Length >= 4 && n.Take( 4 ).All( char.IsDigit ) )
				return n.Substring( 0, 4 );
			return n;
		}

		private static string[] SelectPk2PayloadFiles( string folder )
		{
			var allFiles = Directory.GetFiles( folder )
				.Where( p => !Path.GetFileName( p ).StartsWith( "__", StringComparison.Ordinal ) )
				.Where( p => File.Exists( p ) )
				.ToList();

			var pakDirs = Directory.GetDirectories( folder )
				.Where( ZeroEditor.Archive.PAK.PakBuilder.IsPakFolder )
				.ToArray();

			if( pakDirs.Length > 0 )
			{
				string metaDir = Path.Combine( folder, "__pk2_meta" );
				Directory.CreateDirectory( metaDir );

				foreach( var dir in pakDirs )
				{
					string stem = Stem4( Path.GetFileName( dir ) );
					string pakPath = Path.Combine( metaDir, stem + ".pak" );
					using( var fs = new FileStream( pakPath, FileMode.Create, FileAccess.Write, FileShare.None ) )
						ZeroEditor.Archive.PAK.PakBuilder.BuildFromFolder( dir, fs );
					allFiles.Add( pakPath );
				}
			}

			var groups = allFiles.GroupBy( Stem4, StringComparer.OrdinalIgnoreCase );
			var picked = new List<string>( groups.Count() );

			foreach( var g in groups )
			{
				if( g.Key.Equals( "0000", StringComparison.OrdinalIgnoreCase ) )
				{
					var sgd = g.FirstOrDefault( p => Path.GetExtension( p ).Equals( ".sgd", StringComparison.OrdinalIgnoreCase ) );
					if( sgd != null )
					{
						picked.Add( sgd );
						continue;
					}
				}

				var pak = g.FirstOrDefault( p => Path.GetExtension( p ).Equals( ".pak", StringComparison.OrdinalIgnoreCase ) );
				if( pak != null )
				{
					picked.Add( pak );
					continue;
				}

				var withoutPakBin = g.Where( p => !Path.GetExtension( p ).Equals( ".bin", StringComparison.OrdinalIgnoreCase ) )
					.OrderBy( p => p, StringComparer.OrdinalIgnoreCase )
					.FirstOrDefault();
				picked.Add( withoutPakBin ?? g.OrderBy( p => p, StringComparer.OrdinalIgnoreCase ).First() );
			}

			return picked.OrderBy( p => p, StringComparer.OrdinalIgnoreCase ).ToArray();
		}

		private static void BuildLinkedNodeFromFolder( string folder, Stream outStream )
		{
			var files = SelectPk2PayloadFiles( folder );

			using var bw = new BinaryWriter( outStream, System.Text.Encoding.ASCII, leaveOpen: true );

			bw.Write( files.Length );
			bw.Write( 0 );
			bw.Write( 0 );
			bw.Write( 0 );

			long nodeBase = 0x10;

			for( int i = 0; i < files.Length; i++ )
			{
				byte[] payload = File.ReadAllBytes( files[ i ] );

				outStream.Position = nodeBase;

				uint sizeFlags = (uint)( payload.Length & 0x00FF_FFFF );
				bw.Write( sizeFlags );
				bw.Write( 0u );
				bw.Write( 0u );
				bw.Write( 0u );

				bw.Write( payload );

				long padded = Pk2Util.Align16( outStream.Position );
				int pad = (int)( padded - outStream.Position );
				if( pad > 0 )
					bw.Write( new byte[ pad ] );

				long nextBase = outStream.Position;

				long cur = outStream.Position;
				outStream.Position = nodeBase;
				uint nextOffset = (uint)( nextBase - nodeBase - 0x10 );
				bw.Write( nextOffset );
				outStream.Position = cur;

				nodeBase = nextBase;
			}

			string metaDir = Path.Combine( folder, "__pk2_meta" );
			string tailPath = Path.Combine( metaDir, "tail.bin" );
			if( Directory.Exists( metaDir ) && File.Exists( tailPath ) )
			{
				byte[] tail = File.ReadAllBytes( tailPath );
				if( tail.Length > 0 )
					bw.Write( tail );
			}

			bw.Write( new byte[] {
				0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
				0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF
			} );
		}

		private static void BuildIndexTableFromFolder( string folder, Stream outStream )
		{
			var files = SelectPk2PayloadFiles( folder );

			var offsets = new uint[ files.Length ];

			using var bw = new BinaryWriter( outStream, System.Text.Encoding.ASCII, leaveOpen: true );

			bw.Write( files.Length );
			bw.Write( 0 );
			bw.Write( 0 );
			bw.Write( 0 );

			long tableStart = outStream.Position;
			for( int i = 0; i < offsets.Length; i++ )
				bw.Write( 0u );

			long rawTableEnd = outStream.Position;
			long tableAlignedEnd = rawTableEnd + 15 & ~15L;
			if( rawTableEnd < tableAlignedEnd )
				Pk2Util.WritePad( bw, tableAlignedEnd - rawTableEnd, 0x00 );

			long firstPayloadPos = Pk2Util.Align16( tableAlignedEnd );

			long cur = outStream.Position;
			if( cur < firstPayloadPos )
				Pk2Util.WritePad( bw, firstPayloadPos - cur, 0x00 );
			else if( cur > firstPayloadPos )
				throw new InvalidDataException(
					$"PK2 build: stream advanced past first payload offset (cur=0x{cur:X}, want=0x{firstPayloadPos:X})." );

			for( int i = 0; i < files.Length; i++ )
			{
				long want = outStream.Position;
				offsets[ i ] = checked((uint)want);

				using( var fr = File.OpenRead( files[ i ] ) )
					fr.CopyTo( outStream );

				long padded = Pk2Util.Align16( outStream.Position );
				if( padded > outStream.Position )
					Pk2Util.WritePad( bw, padded - outStream.Position, 0x00 );
			}

			long endPos = outStream.Position;
			outStream.Position = tableStart;
			for( int i = 0; i < offsets.Length; i++ )
				bw.Write( offsets[ i ] );
			outStream.Position = endPos;

			string metaDir = Path.Combine( folder, "__pk2_meta" );
			string tailPath = Path.Combine( metaDir, "tail.bin" );
			if( Directory.Exists( metaDir ) && File.Exists( tailPath ) )
			{
				byte[] tail = File.ReadAllBytes( tailPath );
				if( tail.Length > 0 )
					bw.Write( tail );
			}

			bw.Write( new byte[] {
				0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
				0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF
			} );
		}
	}
}
