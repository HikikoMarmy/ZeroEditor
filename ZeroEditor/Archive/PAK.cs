using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ZeroEditor.Archive.PAK
{
	public sealed class PakEntry
	{
		public int Index { get; init; }
		public long HeaderOffset { get; init; }
		public uint Size { get; init; }
		public long DataOffset => HeaderOffset + 16;
		public string SuggestedExtension { get; init; } = ".bin";
	}

	public sealed class PakArchive
	{
		public int Count { get; private set; }
		public IReadOnlyList<PakEntry> Entries => _entries;

		private List<PakEntry> _entries = new();

		public static PakArchive Open( Stream s )
		{
			if( !s.CanSeek )
				throw new NotSupportedException( "pak: stream must be seekable." );
			if( s.Length < 16 )
				throw new InvalidDataException( "pak: file too small for header." );

			using var br = new BinaryReader( s, System.Text.Encoding.ASCII, leaveOpen: true );

			s.Position = 0;
			int count = br.ReadInt32();
			if( count < 0 || count > 128 )
				throw new InvalidDataException( $"pak: suspicious count {count}." );

			byte[] reserved = br.ReadBytes( 12 );

			var entries = new List<PakEntry>( count );
			long pos = 16;

			for( int i = 0; i < count; i++ )
			{
				if( pos + 16 > s.Length )
					throw new EndOfStreamException( $"pak: entry {i} header past EOF." );

				s.Position = pos;
				uint size = br.ReadUInt32();
				br.ReadUInt32();
				br.ReadUInt32();
				br.ReadUInt32();

				long dataOff = pos + 16;
				long end = dataOff + size;
				if( end < dataOff || end > s.Length )
					throw new EndOfStreamException( $"pak: entry {i} data past EOF or negative." );

				string ext = FileTypeGuesser.GuessFromStream( s, dataOff, 64 );

				entries.Add( new PakEntry
				{
					Index = i,
					HeaderOffset = pos,
					Size = size,
					SuggestedExtension = ext
				} );

				pos = end;
			}

			return new PakArchive { Count = count, _entries = entries };
		}

		public int ExtractAllTo( Stream s, string outDir )
		{
			if( !s.CanSeek )
				throw new NotSupportedException( "pak: stream must be seekable." );

			using var br = new BinaryReader( s, System.Text.Encoding.ASCII, leaveOpen: true );

			Directory.CreateDirectory( outDir );

			int filesWritten = 0;
			foreach( var e in Entries )
			{
				if( e.Size == 0 )
				{
					string emptyName = Path.Combine( outDir, $"{e.Index:D4}{e.SuggestedExtension}" );
					using( File.Create( emptyName ) )
					{ }
					filesWritten++;
					continue;
				}

				s.Position = e.DataOffset;
				byte[] data = br.ReadBytes( checked((int)e.Size) );

				string name = $"{e.Index:D4}{e.SuggestedExtension}";
				File.WriteAllBytes( Path.Combine( outDir, name ), data );
				filesWritten++;
			}
			return filesWritten;
		}

		public static void ExtractAll( Stream s, string outDir )
		{
			var arc = Open( s );
			arc.ExtractAllTo( s, outDir );
		}
	}

	public static class PakBuilder
	{
		public static bool IsPakFolder( string folder )
		{
			var name = Path.GetFileName( folder );
			if( name.StartsWith( "_", StringComparison.Ordinal ) )
				return name.Contains( "_pak", StringComparison.OrdinalIgnoreCase );
			return folder.EndsWith( "_pak", StringComparison.OrdinalIgnoreCase );
		}

		public static void BuildFromFolder( string folder, Stream outStream )
		{
			var files = Directory.GetFiles( folder )
				.Where( p => !Path.GetFileName( p ).StartsWith( "__", StringComparison.Ordinal ) )
				.OrderBy( p => p, StringComparer.OrdinalIgnoreCase )
				.ToArray();

			using var bw = new BinaryWriter( outStream, System.Text.Encoding.ASCII, leaveOpen: true );

			bw.Write( files.Length );
			bw.Write( 0 );
			bw.Write( 0 );
			bw.Write( 0 );

			foreach( var f in files )
			{
				using var fr = File.OpenRead( f );
				uint size = checked((uint)fr.Length);
				bw.Write( size );
				bw.Write( 0u );
				bw.Write( 0u );
				bw.Write( 0u );
				fr.CopyTo( outStream );
			}

			bw.Write( new byte[] {
				0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,
				0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF
			} );
		}
	}
}
