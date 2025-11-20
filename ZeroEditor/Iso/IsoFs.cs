using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

public sealed class IsoFs : IDisposable
{
	public const int SectorSize = 2048;

	private readonly Stream _s;
	private readonly bool _ownsStream;
	private Entry _root;

	private IsoFs( Stream s, bool owns )
	{
		_s = s;
		_ownsStream = owns;
		_root = ReadRoot();
	}

	public static IsoFs Open( string isoPath )
		=> new IsoFs( new FileStream( isoPath, FileMode.Open, FileAccess.Read, FileShare.Read ), owns: true );

	public static IsoFs Wrap( Stream s ) => new IsoFs( s, owns: false );

	public void Dispose() { if( _ownsStream ) _s.Dispose(); }

	public int RootDirLba => _root.Lba;
	public int RootDirDataLen => (int)_root.Length;

	public byte[] ReadFile( string isoPath )
	{
		if( !TryGetEntry( isoPath, out var e ) || e.IsDir )
			throw new FileNotFoundException( isoPath );
		_s.Position = e.Offset;
		var buf = new byte[ e.Length ];
		ReadExactly( _s, buf, 0, buf.Length );
		return buf;
	}

	public void CopyFileTo( string isoPath, Stream dest, int bufferSize = 128 * 1024, CancellationToken ct = default )
	{
		if( !TryGetEntry( isoPath, out var e ) || e.IsDir )
			throw new FileNotFoundException( isoPath );

		_s.Position = e.Offset;
		long left = e.Length;

		byte[] buf = System.Buffers.ArrayPool<byte>.Shared.Rent( bufferSize );
		try
		{
			while( left > 0 )
			{
				ct.ThrowIfCancellationRequested();
				int chunk = (int)Math.Min( bufferSize, left );
				int readTotal = 0;
				while( readTotal < chunk )
				{
					int r = _s.Read( buf, readTotal, chunk - readTotal );
					if( r <= 0 )
						throw new EndOfStreamException();
					readTotal += r;
				}
				dest.Write( buf, 0, readTotal );
				left -= readTotal;
			}
		}
		finally
		{
			System.Buffers.ArrayPool<byte>.Shared.Return( buf );
		}
	}

	public bool TryGetEntry( string isoPath, out Entry entry )
	{
		var parts = isoPath.TrimStart( '\\', '/' )
						   .Split( new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries );
		var cur = _root;
		for( int i = 0; i < parts.Length; i++ )
		{
			var name = Normalize( parts[ i ] );
			var child = EnumerateDir( cur ).FirstOrDefault( x =>
				x.NameNoVer.Equals( name, StringComparison.OrdinalIgnoreCase ) ||
				x.NameRaw.Equals( name, StringComparison.OrdinalIgnoreCase ) );
			if( child == null )
			{
				entry = default!;
				return false;
			}
			if( i == parts.Length - 1 )
			{
				entry = child;
				return true;
			}
			if( !child.IsDir )
			{
				entry = default!;
				return false;
			}
			cur = child;
		}
		entry = default!;
		return false;
	}

	public string? ReadText( string isoPath, int maxBytes = 256 * 1024 )
	{
		if( !TryGetEntry( isoPath, out var e ) || e.IsDir || e.Length > maxBytes )
			return null;
		_s.Position = e.Offset;
		var buf = new byte[ e.Length ];
		ReadExactly( _s, buf, 0, buf.Length );
		return Encoding.ASCII.GetString( buf );
	}

	public IEnumerable<Entry> EnumerateAll()
	{
		foreach( var e in EnumerateDir( _root ) )
		{
			if( e.IsDir )
			{
				foreach( var f in EnumerateAllUnder( e ) )
					yield return f;
			}
			else
			{
				yield return e;
			}
		}
	}

	public IEnumerable<Entry> EnumerateChildren( string dirPath = "" )
	{
		Entry start;
		if( string.IsNullOrWhiteSpace( dirPath ) || dirPath == "/" || dirPath == "\\" )
		{
			start = _root;
		}
		else
		{
			if( !TryGetEntry( dirPath, out var e ) || !e.IsDir )
				throw new DirectoryNotFoundException( dirPath );
			start = e;
		}
		foreach( var e in EnumerateDir( start ) )
			yield return e;
	}

	public string? GetBootPath()
	{
		var sys = ReadText( "SYSTEM.CNF" );
		if( string.IsNullOrEmpty( sys ) )
			return null;
		foreach( var line in sys.Split( '\n' ) )
		{
			var s = line.Trim();
			if( !s.StartsWith( "BOOT", StringComparison.OrdinalIgnoreCase ) )
				continue;
			int b = s.IndexOf( '\\' );
			int sc = s.IndexOf( ';', b + 1 );
			if( b >= 0 && sc > b )
				return s.Substring( b + 1, sc - b - 1 );
		}
		return null;
	}

	private Entry ReadRoot()
	{
		var pvd = new byte[ SectorSize ];
		_s.Position = 16L * SectorSize;
		ReadExactly( _s, pvd, 0, pvd.Length );
		if( pvd[ 0 ] != 1 || Encoding.ASCII.GetString( pvd, 1, 5 ) != "CD001" )
			throw new InvalidDataException( "Not an ISO9660 image (PVD)." );

		var baseRoot = ParseDirRec( pvd, 156, "<root>" );

		return new Entry(
			raw: baseRoot.NameRaw,
			noVer: baseRoot.NameNoVer,
			pathRaw: "",
			pathNoVer: "",
			lba: baseRoot.Lba,
			len: baseRoot.Length,
			flags: baseRoot.Flags );
	}

	private IEnumerable<Entry> EnumerateAllUnder( Entry dir )
	{
		foreach( var e in EnumerateDir( dir ) )
		{
			if( e.IsDir )
			{
				foreach( var f in EnumerateAllUnder( e ) )
					yield return f;
			}
			else
			{
				yield return e;
			}
		}
	}

	private IEnumerable<Entry> EnumerateDir( Entry dir )
	{
		if( !dir.IsDir )
			yield break;

		_s.Position = dir.Offset;
		var buf = new byte[ dir.Length ];
		ReadExactly( _s, buf, 0, buf.Length );

		int p = 0;
		while( p < buf.Length )
		{
			byte len = buf[ p ];
			if( len == 0 )
			{
				p = ( ( p / SectorSize ) + 1 ) * SectorSize;
				continue;
			}

			var rec = ParseDirRec( buf, p, null );
			bool isDot = rec.NameNoVer.Length == 1 &&
						 ( rec.NameNoVer[ 0 ] == '\0' || rec.NameNoVer[ 0 ] == '\u0001' );

			if( !isDot )
			{
				string parent = dir.PathRaw;
				string full = string.IsNullOrEmpty( parent ) ? rec.NameRaw : parent + "\\" + rec.NameRaw;
				string fullNoVer = full.Split( ';' )[ 0 ];

				yield return new Entry(
					raw: rec.NameRaw,
					noVer: rec.NameNoVer,
					pathRaw: full,
					pathNoVer: fullNoVer,
					lba: rec.Lba,
					len: rec.Length,
					flags: rec.Flags );
			}

			p += len;
		}
	}

	private static Entry ParseDirRec( byte[] src, int off, string? forceName )
	{
		int lba = BinaryPrimitives.ReadInt32LittleEndian( src.AsSpan( off + 2, 4 ) );
		int size = BinaryPrimitives.ReadInt32LittleEndian( src.AsSpan( off + 10, 4 ) );
		byte flg = src[ off + 25 ];
		byte nlen = src[ off + 32 ];
		string name = forceName ?? Encoding.ASCII.GetString( src, off + 33, nlen );
		string nameNoVer = name.Split( ';' )[ 0 ];

		return new Entry(
			raw: name,
			noVer: nameNoVer,
			pathRaw: name,
			pathNoVer: nameNoVer,
			lba: lba,
			len: (uint)size,
			flags: flg );
	}

	private static string Normalize( string c )
	{
		var s = c.Trim().Replace( '/', '\\' ).ToUpperInvariant();
		return s;
	}

	private static void ReadExactly( Stream s, byte[] b, int o, int c )
	{
		int r, t = 0;
		while( t < c && ( r = s.Read( b, o + t, c - t ) ) > 0 )
			t += r;
		if( t != c )
			throw new EndOfStreamException();
	}

	public sealed class Entry
	{
		public string NameRaw { get; }
		public string NameNoVer { get; }
		public string PathRaw { get; }
		public string PathNoVer { get; }
		public int Lba { get; }
		public long Offset => (long)Lba * SectorSize;
		public uint Length { get; }
		public byte Flags { get; }
		public bool IsDir => ( Flags & 0x02 ) != 0;

		internal Entry( string raw, string noVer, string pathRaw, string pathNoVer, int lba, uint len, byte flags )
		{
			NameRaw = raw;
			NameNoVer = noVer;
			PathRaw = pathRaw;
			PathNoVer = pathNoVer;
			Lba = lba;
			Length = len;
			Flags = flags;
		}

		public override string ToString()
			=> $"{( IsDir ? "<DIR>" : "file" )} LBA={Lba} LEN=0x{Length:X} {PathRaw}";
	}
}
