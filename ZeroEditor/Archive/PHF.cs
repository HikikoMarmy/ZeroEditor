using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ZeroEditor.Archive.PHF
{
	public sealed class PhfEntry
	{
		public int Index { get; init; }
		public string FileName { get; init; } = "";
		public uint RelOffset { get; init; }
		public uint Size { get; init; }
		public uint AbsOffset { get; internal set; }
	}

	public sealed class PhfArchive
	{
		public uint FileSize { get; private set; }
		public int Count { get; private set; }
		public uint PayloadOffset { get; private set; }
		public IReadOnlyList<PhfEntry> Entries => _entries;

		private List<PhfEntry> _entries = new();

		private const uint MAGIC_PHF = 0x00666870;
		private const uint MAGIC_PK4 = 0x00344B50;

		public static PhfArchive Open( Stream s )
		{
			if( !s.CanSeek )
				throw new NotSupportedException( "phf: stream must be seekable." );

			if( s.Length < 16 )
				throw new InvalidDataException( "phf: too small for header." );

			using var br = new BinaryReader( s, Encoding.ASCII, leaveOpen: true );
			s.Position = 0;

			uint sig = br.ReadUInt32();
			if( sig != MAGIC_PHF )
				throw new InvalidDataException( "phf: bad signature (expected lowercase 'phf\\0')." );

			uint fileSize = br.ReadUInt32();
			int count = br.ReadInt32();
			uint payloadBase = br.ReadUInt32();

			if( count < 0 || count > 200000 )
				throw new InvalidDataException( $"phf: unreasonable entry count {count}." );

			long len = s.Length;
			if( fileSize == 0 || fileSize > (ulong)len )
				fileSize = (uint)len;

			var list = new List<PhfEntry>( count );
			for( int i = 0; i < count; i++ )
			{
				byte[] nameBuf = br.ReadBytes( 24 );
				if( nameBuf.Length != 24 )
					throw new EndOfStreamException();

				string name = DecodeZAscii( nameBuf );
				uint rel = br.ReadUInt32();
				uint size = br.ReadUInt32();

				list.Add( new PhfEntry
				{
					Index = i,
					FileName = Sanitize( name ),
					RelOffset = rel,
					Size = size,
					AbsOffset = payloadBase + rel
				} );
			}

			for( int i = 0; i < list.Count; i++ )
			{
				var e = list[ i ];
				ulong start = e.AbsOffset;
				ulong end = start + e.Size;
				if( start > fileSize || end > fileSize || end < start )
				{
					if( start < fileSize )
					{
						list[ i ] = new PhfEntry
						{
							Index = e.Index,
							FileName = e.FileName,
							RelOffset = e.RelOffset,
							Size = (uint)Math.Max( 0, (long)fileSize - (long)start ),
							AbsOffset = e.AbsOffset
						};
					}
					else
					{
						list[ i ] = new PhfEntry
						{
							Index = e.Index,
							FileName = e.FileName,
							RelOffset = e.RelOffset,
							Size = 0,
							AbsOffset = e.AbsOffset
						};
					}
				}
			}

			return new PhfArchive
			{
				FileSize = fileSize,
				Count = count,
				PayloadOffset = payloadBase,
				_entries = list
			};
		}

		public static int ExtractAllTo( Stream s, string outDir )
		{
			var arc = Open( s );
			Directory.CreateDirectory( outDir );

			using var br = new BinaryReader( s, Encoding.ASCII, leaveOpen: true );
			int written = 0;

			var meta = new List<(int Index, bool IsDir, string Leaf)>();

			for( int i = 0; i < arc.Entries.Count; i++ )
			{
				var e = arc.Entries[ i ];
				int size = (int)Math.Min( int.MaxValue, e.Size );
				string leaf = DecideLeafName( s, e, i );

				if( size <= 4 )
				{
					s.Position = e.AbsOffset;
					File.WriteAllBytes( Path.Combine( outDir, leaf ), size > 0 ? br.ReadBytes( size ) : Array.Empty<byte>() );
					written++;
					meta.Add( (i, false, leaf) );
					continue;
				}

				s.Position = e.AbsOffset;
				byte[] head = br.ReadBytes( 4 );
				s.Position = e.AbsOffset;
				byte[] data = br.ReadBytes( size );

				uint sig = BitConverter.ToUInt32( head, 0 );
				bool isPk4 = ( sig == MAGIC_PK4 );
				bool isPhf = ( sig == MAGIC_PHF );

				if( isPk4 || isPhf )
				{
					string baseName = Path.GetFileNameWithoutExtension( leaf );
					if( string.IsNullOrWhiteSpace( baseName ) )
						baseName = i.ToString( "D4" );

					string folder = Path.Combine( outDir, baseName + ( isPk4 ? "_pk4" : "_phf" ) );
					Directory.CreateDirectory( folder );
					meta.Add( (i, true, Path.GetFileName( folder )) );

					using var ms = new MemoryStream( data, writable: false );
					if( isPk4 )
						Archive.PK4.Pk4Archive.ExtractAllTo( ms, folder );
					else
						ExtractAllTo( ms, folder );
				}
				else
				{
					File.WriteAllBytes( Path.Combine( outDir, leaf ), data );
					written++;
					meta.Add( (i, false, leaf) );
				}
			}

			string metaPath = Path.Combine( outDir, "__phf_meta.txt" );
			using( var sw = new StreamWriter( metaPath, false, Encoding.UTF8 ) )
			{
				foreach( var m in meta )
				{
					sw.Write( m.Index );
					sw.Write( ',' );
					sw.Write( m.IsDir ? "dir" : "file" );
					sw.Write( ',' );
					sw.WriteLine( m.Leaf );
				}
			}

			return written;
		}

		private static string DecideLeafName( Stream s, PhfEntry e, int idx )
		{
			var name = e.FileName?.Trim();
			if( !string.IsNullOrEmpty( name ) && LooksLikeSafeFileName( name ) )
				return name;

			string ext = GuessExtAt( s, e.AbsOffset, (int)Math.Min( 128u, e.Size ) );
			return idx.ToString( "D4" ) + ext;
		}

		private static string GuessExtAt( Stream s, long pos, int maxPeek )
		{
			return FileTypeGuesser.GuessFromStream( s, pos, (uint)Math.Clamp( maxPeek, 8, 128 ) );
		}

		private static string DecodeZAscii( ReadOnlySpan<byte> raw )
		{
			int nul = raw.IndexOf( (byte)0 );
			if( nul < 0 )
				nul = raw.Length;
			return Encoding.ASCII.GetString( raw.Slice( 0, nul ) );
		}

		private static bool LooksLikeSafeFileName( string s )
		{
			if( s.Length == 0 || s.Length > 255 )
				return false;
			foreach( char c in Path.GetInvalidFileNameChars() )
				if( s.Contains( c ) )
					return false;
			return true;
		}

		private static string Sanitize( string s )
		{
			if( string.IsNullOrEmpty( s ) )
				return "";
			foreach( char c in Path.GetInvalidFileNameChars() )
				s = s.Replace( c, '_' );
			return s.Replace( '/', '_' ).Replace( '\\', '_' ).Trim().TrimEnd( '.', ' ' );
		}
	}

	public static class PhfBuilder
	{
		private const uint MAGIC_PHF = 0x00666870;
		private const uint ALIGN = 16;

		private static uint Align16( uint v )
		{
			return ( v + ( ALIGN - 1u ) ) & ~( ALIGN - 1u );
		}

		public static void BuildFromFolder( string folder, Stream outStream )
		{
			if( folder is null )
				throw new ArgumentNullException( nameof( folder ) );

			if( !Directory.Exists( folder ) )
				throw new DirectoryNotFoundException( folder );

			if( outStream is null )
				throw new ArgumentNullException( nameof( outStream ) );

			if( !outStream.CanWrite )
				throw new NotSupportedException( "Output stream is not writable." );

			var entries = CollectEntries( folder );

			using var bw = new BinaryWriter( outStream, Encoding.ASCII, leaveOpen: true );

			uint count = (uint)entries.Count;
			uint headerSize = 16u;
			uint entrySize = 24u + 4u + 4u;
			uint tableSize = count * entrySize;
			uint payloadBase = headerSize + tableSize;

			uint currentRel = 0;
			foreach( var e in entries )
			{
				currentRel = Align16( currentRel );
				e.RelOffset = currentRel;
				e.Size = (uint)( e.Data?.Length ?? 0 );
				currentRel += e.Size;
			}
			uint payloadSize = Align16( currentRel );
			uint fileSize = payloadBase + payloadSize;

			bw.Write( MAGIC_PHF );
			bw.Write( fileSize );
			bw.Write( count );
			bw.Write( payloadBase );

			foreach( var e in entries )
			{
				var nameBytes = Encoding.ASCII.GetBytes( e.FileName );
				if( nameBytes.Length > 24 )
				{
					bw.Write( nameBytes, 0, 24 );
				}
				else
				{
					bw.Write( nameBytes );
					if( nameBytes.Length < 24 )
						bw.Write( new byte[ 24 - nameBytes.Length ] );
				}

				bw.Write( e.RelOffset );
				bw.Write( e.Size );
			}

			long payloadStart = outStream.Position;

			if( payloadStart != payloadBase )
				throw new InvalidDataException( "phf: payloadBase mismatch." );

			foreach( var e in entries )
			{
				long desiredPos = payloadStart + e.RelOffset;
				long curPos = outStream.Position;

				if( curPos > desiredPos )
					throw new InvalidDataException( "phf: payload overlap when writing." );

				while( curPos < desiredPos )
				{
					bw.Write( (byte)0 );
					curPos++;
				}

				if( e.Data != null && e.Data.Length > 0 )
				{
					bw.Write( e.Data );
					curPos += e.Data.Length;
				}
			}

			long finalPos = outStream.Position;
			long targetFinal = payloadStart + payloadSize;
			while( finalPos < targetFinal )
			{
				bw.Write( (byte)0 );
				finalPos++;
			}
		}

		private sealed class PhfBuildEntry
		{
			public string FileName { get; set; } = "";
			public byte[]? Data { get; set; }
			public uint RelOffset { get; set; }
			public uint Size { get; set; }
			public int OrderKey { get; set; }
		}

		private static List<PhfBuildEntry> CollectEntries( string root )
		{
			root = Path.GetFullPath( root );
			string metaPath = Path.Combine( root, "__phf_meta.txt" );
			if( File.Exists( metaPath ) )
				return CollectEntriesFromMeta( root, metaPath );
			return CollectEntriesAuto( root );
		}

		private static List<PhfBuildEntry> CollectEntriesFromMeta( string root, string metaPath )
		{
			var files = Directory.EnumerateFiles( root, "*", SearchOption.TopDirectoryOnly )
				.Where( f => !Path.GetFileName( f ).StartsWith( "__", StringComparison.Ordinal ) )
				.ToDictionary( f => Path.GetFileName( f ), f => f, StringComparer.OrdinalIgnoreCase );

			var dirs = Directory.EnumerateDirectories( root, "*", SearchOption.TopDirectoryOnly )
				.Where( d =>
				{
					var name = Path.GetFileName( d );
					if( name.StartsWith( "__", StringComparison.Ordinal ) )
						return false;
					return name.EndsWith( "_pk4", StringComparison.OrdinalIgnoreCase ) ||
						   name.EndsWith( "_phf", StringComparison.OrdinalIgnoreCase );
				} )
				.ToDictionary( d => Path.GetFileName( d ), d => d, StringComparer.OrdinalIgnoreCase );

			var result = new List<PhfBuildEntry>();

			foreach( var line in File.ReadLines( metaPath ) )
			{
				if( string.IsNullOrWhiteSpace( line ) )
					continue;

				var parts = line.Split( new[] { ',' }, 3 );
				if( parts.Length < 3 )
					continue;

				if( !int.TryParse( parts[ 0 ], out int index ) )
					continue;

				var kind = parts[ 1 ].Trim();
				var leaf = parts[ 2 ].Trim();
				if( leaf.Length == 0 )
					continue;

				if( kind.Equals( "file", StringComparison.OrdinalIgnoreCase ) )
				{
					if( !files.TryGetValue( leaf, out var path ) )
						continue;

					string safeName = SanitizeFileName( leaf );
					if( string.IsNullOrEmpty( safeName ) )
						safeName = leaf;

					result.Add( new PhfBuildEntry
					{
						FileName = safeName,
						Data = File.ReadAllBytes( path ),
						RelOffset = 0,
						Size = 0,
						OrderKey = index
					} );
				}
				else if( kind.Equals( "dir", StringComparison.OrdinalIgnoreCase ) )
				{
					if( !dirs.TryGetValue( leaf, out var dir ) )
						continue;

					string stem = leaf;
					string ext = ".bin";

					if( leaf.EndsWith( "_pk4", StringComparison.OrdinalIgnoreCase ) )
					{
						stem = leaf.Substring( 0, leaf.Length - "_pk4".Length );
						ext = ".pk4";
					}
					else if( leaf.EndsWith( "_phf", StringComparison.OrdinalIgnoreCase ) )
					{
						stem = leaf.Substring( 0, leaf.Length - "_phf".Length );
						ext = ".phf";
					}

					string baseName = SanitizeFileName( stem );
					if( string.IsNullOrEmpty( baseName ) )
						baseName = stem;

					string fileName = baseName + ext;

					byte[] data;
					using( var ms = new MemoryStream() )
					{
						if( ext.Equals( ".pk4", StringComparison.OrdinalIgnoreCase ) )
							Archive.PK4.Pk4Builder.BuildFromFolder( dir, ms );
						else
							PhfBuilder.BuildFromFolder( dir, ms );
						data = ms.ToArray();
					}

					result.Add( new PhfBuildEntry
					{
						FileName = fileName,
						Data = data,
						RelOffset = 0,
						Size = 0,
						OrderKey = index
					} );
				}
			}

			return result
				.OrderBy( e => e.OrderKey )
				.ThenBy( e => e.FileName, StringComparer.OrdinalIgnoreCase )
				.ToList();
		}

		private static List<PhfBuildEntry> CollectEntriesAuto( string root )
		{
			root = Path.GetFullPath( root );

			var allDirs = Directory.EnumerateDirectories( root, "*", SearchOption.AllDirectories ).ToArray();

			bool IsContainer( string dirName )
			{
				dirName = Path.GetFileName( dirName );
				if( dirName.StartsWith( "__", StringComparison.Ordinal ) )
					return false;

				return dirName.EndsWith( "_pk4", StringComparison.OrdinalIgnoreCase ) ||
					   dirName.EndsWith( "_phf", StringComparison.OrdinalIgnoreCase );
			}

			var containerCandidates = allDirs.Where( IsContainer ).Select( Path.GetFullPath ).ToArray();

			bool HasContainerAncestor( string dir )
			{
				var cur = Path.GetDirectoryName( dir );
				while( !string.IsNullOrEmpty( cur ) &&
					   string.Compare( cur, root, StringComparison.OrdinalIgnoreCase ) != 0 )
				{
					if( IsContainer( cur ) )
						return true;
					cur = Path.GetDirectoryName( cur );
				}
				return false;
			}

			var containerRoots = new HashSet<string>(
				containerCandidates.Where( d => !HasContainerAncestor( d ) ),
				StringComparer.OrdinalIgnoreCase );

			var containerStemPerDir = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
			foreach( var c in containerRoots )
			{
				var dir = Path.GetDirectoryName( c ) ?? root;
				var leaf = Path.GetFileName( c );
				string stem = leaf;
				if( leaf.EndsWith( "_pk4", StringComparison.OrdinalIgnoreCase ) )
					stem = leaf.Substring( 0, leaf.Length - "_pk4".Length );
				else if( leaf.EndsWith( "_phf", StringComparison.OrdinalIgnoreCase ) )
					stem = leaf.Substring( 0, leaf.Length - "_phf".Length );

				var fullDir = Path.GetFullPath( dir );
				containerStemPerDir.Add( fullDir + "|" + stem );
			}

			bool IsUnderContainer( string path )
			{
				var full = Path.GetFullPath( path );
				foreach( var c in containerRoots )
				{
					if( full.Length <= c.Length )
						continue;

					if( full.StartsWith( c, StringComparison.OrdinalIgnoreCase ) )
					{
						char next = full[ c.Length ];
						if( next == Path.DirectorySeparatorChar || next == Path.AltDirectorySeparatorChar )
							return true;
					}
				}
				return false;
			}

			bool IsRawContainerSibling( string filePath )
			{
				var dir = Path.GetDirectoryName( filePath ) ?? root;
				var leaf = Path.GetFileNameWithoutExtension( filePath );
				var fullDir = Path.GetFullPath( dir );
				return containerStemPerDir.Contains( fullDir + "|" + leaf );
			}

			var result = new List<PhfBuildEntry>();

			var allFiles = Directory.EnumerateFiles( root, "*", SearchOption.AllDirectories )
				.Where( f =>
				{
					var name = Path.GetFileName( f );
					if( name.StartsWith( "__", StringComparison.Ordinal ) )
						return false;
					if( IsUnderContainer( f ) )
						return false;
					if( IsRawContainerSibling( f ) )
						return false;
					return true;
				} );

			foreach( var f in allFiles )
			{
				var leaf = Path.GetFileName( f );
				string safeName = SanitizeFileName( leaf );
				int orderKey = ExtractNumericPrefix( leaf );

				result.Add( new PhfBuildEntry
				{
					FileName = safeName,
					Data = File.ReadAllBytes( f ),
					OrderKey = orderKey
				} );
			}

			foreach( var c in containerRoots )
			{
				var leaf = Path.GetFileName( c );
				string stem = leaf;
				string ext = ".bin";

				if( leaf.EndsWith( "_pk4", StringComparison.OrdinalIgnoreCase ) )
				{
					stem = leaf.Substring( 0, leaf.Length - "_pk4".Length );
					ext = ".pk4";
				}
				else if( leaf.EndsWith( "_phf", StringComparison.OrdinalIgnoreCase ) )
				{
					stem = leaf.Substring( 0, leaf.Length - "_phf".Length );
					ext = ".phf";
				}

				string baseName = SanitizeFileName( stem );
				if( string.IsNullOrEmpty( baseName ) )
					baseName = stem;

				string fileName = baseName + ext;
				int orderKey = ExtractNumericPrefix( stem );

				byte[] data;
				using( var ms = new MemoryStream() )
				{
					if( ext.Equals( ".pk4", StringComparison.OrdinalIgnoreCase ) )
						Archive.PK4.Pk4Builder.BuildFromFolder( c, ms );
					else
						PhfBuilder.BuildFromFolder( c, ms );
					data = ms.ToArray();
				}

				result.Add( new PhfBuildEntry
				{
					FileName = fileName,
					Data = data,
					OrderKey = orderKey
				} );
			}

			return result
				.OrderBy( e => e.OrderKey )
				.ThenBy( e => e.FileName, StringComparer.OrdinalIgnoreCase )
				.ToList();
		}

		private static int ExtractNumericPrefix( string name )
		{
			if( name.Length >= 4 &&
				char.IsDigit( name[ 0 ] ) &&
				char.IsDigit( name[ 1 ] ) &&
				char.IsDigit( name[ 2 ] ) &&
				char.IsDigit( name[ 3 ] ) &&
				int.TryParse( name.Substring( 0, 4 ), out int val ) )
			{
				return val;
			}
			return int.MaxValue;
		}

		private static string SanitizeFileName( string s )
		{
			if( string.IsNullOrEmpty( s ) )
				return "";
			foreach( char c in Path.GetInvalidFileNameChars() )
				s = s.Replace( c, '_' );
			return s.Trim().TrimEnd( '.', ' ' );
		}
	}
}
