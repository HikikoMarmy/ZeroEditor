using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ZeroEditor.Archive.PK4
{
	public sealed class Pk4Entry
	{
		public int Index { get; init; }
		public long SliceStart { get; init; }
		public long SliceEnd { get; init; }
		public long PayloadOffset { get; init; }
		public int PayloadSize { get; init; }
		public string GroupName { get; init; } = "";
		public string EntryName { get; init; } = "";
		public string SuggestedExtension { get; init; } = ".bin";
	}

	public enum Pk4Variant
	{
		Flat = 0,
		Hierarchical = 1
	}

	public sealed class Pk4Archive
	{
		public int Count { get; private set; }
		public IReadOnlyList<Pk4Entry> Entries => _entries;
		public uint HeaderFlags { get; private set; } = 0;
		public Pk4Variant Variant { get; private set; } = Pk4Variant.Flat;

		private List<Pk4Entry> _entries = new();

		private const uint MAGIC_PK4 = 0x00344B50;
		private const uint MAGIC_PHF = 0x00666870;

		public static Pk4Archive Open( Stream s )
		{
			if( !s.CanSeek )
				throw new NotSupportedException( "pk4: stream must be seekable." );

			if( s.Length < 0x20 )
				throw new InvalidDataException( "pk4: file too small." );

			using var br = new BinaryReader( s, Encoding.ASCII, leaveOpen: true );
			s.Position = 0;

			uint magic = br.ReadUInt32();
			if( magic != MAGIC_PK4 )
				throw new InvalidDataException( "pk4: bad magic." );

			uint flags = br.ReadUInt32();
			if( flags != 0u && flags != 1u )
				throw new InvalidDataException( $"pk4: unexpected flags {flags}." );
			var variant = flags == 1u ? Pk4Variant.Hierarchical : Pk4Variant.Flat;

			int count = br.ReadInt32();
			br.ReadUInt32();

			if( count < 0 || count > 200000 )
				throw new InvalidDataException( $"pk4: bad count {count}." );

			var offsets = TryReadOffsetsTable( s, br, count, 0x10 )
				?? TryReadOffsetsTable( s, br, count, 0x20 )
				?? throw new InvalidDataException( "pk4: could not locate a valid offsets table." );

			long len = s.Length;
			var list = new List<Pk4Entry>( count );

			for( int i = 0; i < count; i++ )
			{
				long start = offsets[ i ];
				long end = ( i + 1 < count ) ? offsets[ i + 1 ] : len;
				if( end < start )
					throw new InvalidDataException( $"pk4: slice {i} end<start." );

				int size = checked((int)Math.Max( 0, end - start ));
				list.Add( new Pk4Entry
				{
					Index = i,
					SliceStart = start,
					SliceEnd = end,
					PayloadOffset = start,
					PayloadSize = size,
					GroupName = "",
					EntryName = "",
					SuggestedExtension = ".bin"
				} );
			}

			return new Pk4Archive
			{
				Count = count,
				_entries = list,
				HeaderFlags = flags,
				Variant = variant
			};
		}

		public static int ExtractAllTo( Stream s, string outDir )
			=> ExtractTopLevelPk4( s, outDir, 0 );

		private const int MAX_DEPTH = 32;
		private const uint INNER_NAME_TAG = 0x00000010;

		private static int ExtractTopLevelPk4( Stream s, string outDir, int depth )
		{
			if( depth > MAX_DEPTH )
				throw new InvalidDataException( "pk4: too-deep recursion (possible cycle)." );

			int filesWritten = 0;

			var arc = Open( s );
			using var br = new BinaryReader( s, Encoding.ASCII, leaveOpen: true );
			long len = s.Length;

			bool rootCreated = false;
			void EnsureRoot()
			{
				if( !rootCreated )
				{
					Directory.CreateDirectory( outDir );
					rootCreated = true;
				}
			}

			var topLevelOrder = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
			int nextTopLevelIndex = 0;

			for( int entryIdx = 0; entryIdx < arc.Entries.Count; entryIdx++ )
			{
				var e = arc.Entries[ entryIdx ];
				if( e.SliceStart < 0 || e.SliceEnd > len || e.SliceStart > e.SliceEnd )
					throw new InvalidDataException( $"pk4: bad slice bounds at idx {e.Index}." );

				s.Position = e.SliceStart;
				long cursor = e.SliceStart;
				var pathSegments = new List<string>();

				while( cursor + 8 <= e.SliceEnd )
				{
					uint tag = br.ReadUInt32();
					if( tag != INNER_NAME_TAG )
					{
						s.Position = cursor;
						break;
					}

					int nameLen = br.ReadInt32();
					if( nameLen < 0 || s.Position + nameLen > e.SliceEnd )
						throw new EndOfStreamException( $"pk4: inner name length OOR at idx {e.Index}." );

					string name = Encoding.ASCII.GetString( br.ReadBytes( nameLen ) );
					pathSegments.Add( Sanitize( name ) );

					long afterName = Align16( s.Position );
					if( afterName > e.SliceEnd )
						afterName = e.SliceEnd;
					s.Position = afterName;
					cursor = afterName;

					if( cursor + 4 > e.SliceEnd )
						break;
					uint next = br.ReadUInt32();
					s.Position = cursor;
					if( next != INNER_NAME_TAG )
						break;
				}

				var fsSegments = new List<string>( pathSegments.Count );
				for( int i = 0; i < pathSegments.Count; i++ )
				{
					string seg = pathSegments[ i ];
					if( i == 0 )
					{
						if( !topLevelOrder.TryGetValue( seg, out int ord ) )
						{
							ord = nextTopLevelIndex++;
							topLevelOrder[ seg ] = ord;
						}
						string prefix = ord <= 99 ? ord.ToString( "D2" ) : ord.ToString( "D4" );
						fsSegments.Add( prefix + "_" + seg );
					}
					else
					{
						fsSegments.Add( seg );
					}
				}

				long payloadStart = cursor;
				long payloadLen64 = Math.Max( 0, e.SliceEnd - payloadStart );
				if( payloadLen64 > int.MaxValue )
					throw new InvalidDataException( "pk4: payload too large." );
				int payloadSize = (int)payloadLen64;

				bool nestedPk4 = false;
				bool nestedPhf = false;
				if( payloadSize >= 4 )
				{
					uint maybe = br.ReadUInt32();
					nestedPk4 = ( maybe == MAGIC_PK4 );
					nestedPhf = ( maybe == MAGIC_PHF );
					s.Position = payloadStart;
				}

				string baseDir = outDir;
				if( fsSegments.Count > 0 )
				{
					EnsureRoot();
					foreach( var seg in fsSegments )
					{
						if( !string.IsNullOrEmpty( seg ) )
						{
							baseDir = Path.Combine( baseDir, seg );
							Directory.CreateDirectory( baseDir );
						}
					}
				}

				if( nestedPk4 || nestedPhf )
				{
					EnsureRoot();

					byte[] blob = payloadSize == 0 ? Array.Empty<byte>() : br.ReadBytes( payloadSize );

					string baseName = pathSegments.LastOrDefault();
					if( string.IsNullOrWhiteSpace( baseName ) )
						baseName = e.Index.ToString( "D4" );

					if( nestedPk4 )
					{
						using var ms = new MemoryStream( blob, writable: false );
						filesWritten += ExtractTopLevelPk4( ms, Path.Combine( baseDir, baseName + "_pk4" ), depth + 1 );
					}
					else
					{
						using var ms = new MemoryStream( blob, writable: false );
						filesWritten += ZeroEditor.Archive.PHF.PhfArchive.ExtractAllTo(
							ms,
							Path.Combine( baseDir, baseName + "_phf" ) );
					}
				}
				else
				{
					string ext = GuessExtAt( s, payloadStart, Math.Min( 64, payloadSize ), pathSegments );
					string leafName = e.Index.ToString( "D4" ) + ext;

					EnsureRoot();
					string outPath = Path.Combine( baseDir, leafName );

					if( payloadSize == 0 )
					{
						File.WriteAllBytes( outPath, Array.Empty<byte>() );
					}
					else
					{
						s.Position = payloadStart;
						byte[] data = br.ReadBytes( payloadSize );
						File.WriteAllBytes( outPath, data );
					}

					filesWritten++;
				}
			}

			return filesWritten;
		}

		private static uint[]? TryReadOffsetsTable( Stream s, BinaryReader br, int count, long tableStart )
		{
			long len = s.Length;
			long bytes = 4L * count;
			if( tableStart < 0 || tableStart + bytes > len )
				return null;

			long save = s.Position;
			try
			{
				s.Position = tableStart;
				var offs = new uint[ count ];
				for( int i = 0; i < count; i++ )
					offs[ i ] = br.ReadUInt32();

				for( int i = 0; i < count; i++ )
				{
					long off = offs[ i ];
					if( off < 0 || off >= len )
						return null;
					if( i > 0 && offs[ i - 1 ] >= off )
						return null;
				}
				return offs;
			}
			finally
			{
				s.Position = save;
			}
		}

		private static string GuessExtAt( Stream s, long pos, int maxPeek, List<string> pathSegments )
		{
			string ext = FileTypeGuesser.GuessFromStream( s, pos, (uint)Math.Clamp( maxPeek, 8, 128 ) );
			if( ext != ".bin" )
				return ext;

			if( pathSegments is { Count: > 0 } )
			{
				string last = pathSegments[ pathSegments.Count - 1 ];
				if( LooksLikeExtToken( last ) )
					return "." + last.ToLowerInvariant();
			}
			return ".bin";
		}

		private static bool LooksLikeExtToken( string s )
		{
			if( string.IsNullOrWhiteSpace( s ) )
				return false;
			s = s.Trim().TrimEnd( '.' );
			if( s.Length < 2 || s.Length > 16 )
				return false;
			foreach( char c in s )
			{
				if( !( ( c >= 'A' && c <= 'Z' ) || ( c >= 'a' && c <= 'z' ) || ( c >= '0' && c <= '9' ) || c == '_' ) )
					return false;
			}
			return true;
		}

		private static long Align16( long v ) => ( v + 15L ) & ~15L;

		private static string Sanitize( string s )
		{
			if( string.IsNullOrEmpty( s ) )
				return "";
			foreach( char c in Path.GetInvalidFileNameChars() )
				s = s.Replace( c, '_' );
			return s.Replace( '/', '_' ).Replace( '\\', '_' ).Trim().TrimEnd( '.', ' ' );
		}
	}

	public static class Pk4Builder
	{
		private const uint MAGIC_PK4 = 0x00344B50;
		private const uint INNER_NAME_TAG = 0x00000010;
		private const int ALIGN = 16;

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
			bool hasHierarchy = entries.Any( e => e.PathSegments != null && e.PathSegments.Length > 0 );
			uint flags = hasHierarchy ? 1u : 0u;

			using var bw = new BinaryWriter( outStream, Encoding.ASCII, leaveOpen: true );

			bw.Write( MAGIC_PK4 );
			bw.Write( flags );
			bw.Write( entries.Count );
			bw.Write( 0u );

			long tableStart = outStream.Position;
			for( int i = 0; i < entries.Count; i++ )
				bw.Write( 0u );

			var offsets = new uint[ entries.Count ];

			for( int i = 0; i < entries.Count; i++ )
			{
				var e = entries[ i ];

				AlignStream( outStream, ALIGN );
				long entryStart = outStream.Position;
				if( entryStart > uint.MaxValue )
					throw new InvalidDataException( "PK4 offset > 4GB; not supported." );

				offsets[ i ] = (uint)entryStart;

				WriteInnerNames( bw, e.PathSegments );

				AlignStream( outStream, ALIGN );

				var data = e.PayloadFactory();
				if( data != null && data.Length > 0 )
					bw.Write( data );

				AlignStream( outStream, ALIGN );
			}

			long endPos = outStream.Position;
			outStream.Position = tableStart;
			for( int i = 0; i < offsets.Length; i++ )
				bw.Write( offsets[ i ] );
			outStream.Position = endPos;
		}

		private sealed class BuildEntry
		{
			public string[] PathSegments { get; init; } = Array.Empty<string>();
			public Func<byte[]> PayloadFactory { get; init; } = () => Array.Empty<byte>();
			public int OrderKey { get; init; }
			public string RelPath { get; init; } = "";
		}

		private static List<BuildEntry> CollectEntries( string root )
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

			var entries = new List<BuildEntry>();

			var allFiles = Directory.EnumerateFiles( root, "*", SearchOption.AllDirectories )
				.Where( f =>
				{
					var name = Path.GetFileName( f );
					if( name.StartsWith( "__", StringComparison.Ordinal ) )
						return false;
					if( IsUnderContainer( f ) )
						return false;
					return true;
				} );

			foreach( var f in allFiles )
			{
				var rel = Path.GetRelativePath( root, f );
				var relParts = rel.Split( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar )
								  .Where( p => !string.IsNullOrEmpty( p ) )
								  .ToArray();

				if( relParts.Length == 0 )
					continue;

				string leaf = relParts[ relParts.Length - 1 ];
				string[] segments = relParts.Take( relParts.Length - 1 ).ToArray();

				int orderKey = ExtractNumericPrefix( leaf );

				entries.Add( new BuildEntry
				{
					PathSegments = segments,
					PayloadFactory = () => File.ReadAllBytes( f ),
					OrderKey = orderKey,
					RelPath = rel
				} );
			}

			foreach( var c in containerRoots )
			{
				var rel = Path.GetRelativePath( root, c );
				var relParts = rel.Split( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar )
								  .Where( p => !string.IsNullOrEmpty( p ) )
								  .ToArray();
				if( relParts.Length == 0 )
					continue;

				string last = relParts[ relParts.Length - 1 ];
				string[] parentSegments = relParts.Take( relParts.Length - 1 ).ToArray();
				string stem = last;
				string ext = "";

				if( last.EndsWith( "_pk4", StringComparison.OrdinalIgnoreCase ) )
				{
					stem = last.Substring( 0, last.Length - "_pk4".Length );
					ext = ".pk4";
				}
				else if( last.EndsWith( "_phf", StringComparison.OrdinalIgnoreCase ) )
				{
					stem = last.Substring( 0, last.Length - "_phf".Length );
					ext = ".phf";
				}

				int orderKey = ExtractNumericPrefix( stem );

				entries.Add( new BuildEntry
				{
					PathSegments = parentSegments,
					PayloadFactory = () =>
					{
						using var ms = new MemoryStream();
						if( ext.Equals( ".pk4", StringComparison.OrdinalIgnoreCase ) )
						{
							Pk4Builder.BuildFromFolder( c, ms );
						}
						else
						{
							PHF.PhfBuilder.BuildFromFolder( c, ms );
						}
						return ms.ToArray();
					},
					OrderKey = orderKey,
					RelPath = rel
				} );
			}

			return entries
				.OrderBy( e => e.RelPath, StringComparer.OrdinalIgnoreCase )
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

		private static void AlignStream( Stream s, int align )
		{
			long pos = s.Position;
			long pad = ( align - ( pos % align ) ) % align;
			if( pad <= 0 )
				return;

			Span<byte> z = stackalloc byte[ Math.Min( (int)pad, 4096 ) ];
			while( pad > 0 )
			{
				int chunk = (int)Math.Min( pad, z.Length );
				s.Write( z.Slice( 0, chunk ) );
				pad -= chunk;
			}
		}

		private static void WriteInnerNames( BinaryWriter bw, IReadOnlyList<string> segments )
		{
			if( segments == null || segments.Count == 0 )
				return;

			for( int i = 0; i < segments.Count; i++ )
			{
				string seg = segments[ i ];
				if( string.IsNullOrWhiteSpace( seg ) )
					continue;

				seg = DePrefixNameToken( seg );
				seg = SanitizeNameToken( seg );
				var nameBytes = Encoding.ASCII.GetBytes( seg );

				bw.Write( INNER_NAME_TAG );
				bw.Write( nameBytes.Length );
				bw.Write( nameBytes );

				AlignStream( bw.BaseStream, ALIGN );
			}
		}

		private static string DePrefixNameToken( string s )
		{
			if( string.IsNullOrEmpty( s ) )
				return s;
			int i = 0;
			while( i < s.Length && i < 4 && char.IsDigit( s[ i ] ) )
				i++;
			if( i >= 2 && i <= 4 && i < s.Length && s[ i ] == '_' )
				return s.Substring( i + 1 );
			return s;
		}

		private static string SanitizeNameToken( string s )
		{
			if( string.IsNullOrEmpty( s ) )
				return "";
			foreach( char c in Path.GetInvalidFileNameChars() )
				s = s.Replace( c, '_' );
			return s.Trim().TrimEnd( '.', ' ' );
		}
	}
}
