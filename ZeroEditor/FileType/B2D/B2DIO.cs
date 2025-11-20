using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ZeroEditor.Zero3.Editors.B2D
{
	public static class B2dIO
	{
		const uint Magic = 0x00643262;

		public static B2dFile Read( string path )
		{
			using var fs = File.OpenRead( path );
			using var br = new BinaryReader( fs, Encoding.ASCII, leaveOpen: true );
			if( br.ReadUInt32() != Magic )
				throw new InvalidDataException( "Not a .b2d file (missing magic)" );
			var file = new B2dFile
			{
				StringTableSize = br.ReadUInt32(),
				StringTableOffset = br.ReadUInt32(),
				Misc = br.ReadUInt32()
			};
			fs.Position = file.StringTableOffset;
			long stStart = fs.Position;
			long stEnd = stStart + file.StringTableSize;
			while( fs.Position < stEnd )
				file._strings.Add( ReadZ( br ) );
			file._stringToOffset.Clear();
			long pos = stStart;
			foreach( var s in file._strings )
			{
				file._stringToOffset[ s ] = checked((int)( pos - stStart ));
				pos += s.Length + 1;
			}
			fs.Position = Align16( stEnd );
			while( fs.Position + 4 <= fs.Length )
				file._u32.Add( br.ReadUInt32() );

			var validStrOffs = new HashSet<int>( file._stringToOffset.Values );

			for( int i = 0; i + 3 < file._u32.Count; i++ )
			{
				if( file._u32[ i ] != 2u )
					continue;
				uint a1 = file._u32[ i + 1 ];
				uint a2 = file._u32[ i + 2 ];
				uint a3 = file._u32[ i + 3 ];
				bool a1IsStr = validStrOffs.Contains( unchecked((int)a1) );
				bool a2IsStr = validStrOffs.Contains( unchecked((int)a2) );
				if( a1IsStr && !a2IsStr )
				{
					int id = unchecked((int)a2);
					int strOff = unchecked((int)a1);
					var name = GetStringByOffset( file, strOff ) ?? string.Empty;
					file.ResourceById[ id ] = name;
					file._op2Starts[ i ] = 4;
					file._op2Swap[ i ] = false;
					file._op2Flag[ i ] = a3;
				}
				else if( a2IsStr && !a1IsStr )
				{
					int id = unchecked((int)a1);
					int strOff = unchecked((int)a2);
					var name = GetStringByOffset( file, strOff ) ?? string.Empty;
					file.ResourceById[ id ] = name;
					file._op2Starts[ i ] = 4;
					file._op2Swap[ i ] = true;
					file._op2Flag[ i ] = a3;
				}
				else if( a1IsStr && a2IsStr )
				{
					int s1 = unchecked((int)a1), s2 = unchecked((int)a2);
					string n1 = GetStringByOffset( file, s1 ) ?? "";
					string n2 = GetStringByOffset( file, s2 ) ?? "";
					bool n1Tex = LooksLikeTexture( n1 );
					bool n2Tex = LooksLikeTexture( n2 );
					if( n1Tex && !n2Tex )
					{
						int id = unchecked((int)a2);
						file.ResourceById[ id ] = n1;
						file._op2Starts[ i ] = 4;
						file._op2Swap[ i ] = false;
						file._op2Flag[ i ] = a3;
					}
					else if( n2Tex && !n1Tex )
					{
						int id = unchecked((int)a1);
						file.ResourceById[ id ] = n2;
						file._op2Starts[ i ] = 4;
						file._op2Swap[ i ] = true;
						file._op2Flag[ i ] = a3;
					}
				}
			}

			for( int i = 0; i + 8 < file._u32.Count; i++ )
			{
				if( file._u32[ i ] != 11u )
					continue;
				int sx = unchecked((int)file._u32[ i + 3 ]);
				int sy = unchecked((int)file._u32[ i + 4 ]);
				int sw = unchecked((int)file._u32[ i + 5 ]);
				int sh = unchecked((int)file._u32[ i + 6 ]);
				if( sw <= 0 || sh <= 0 )
					continue;
				int a7 = unchecked((int)file._u32[ i + 7 ]);
				int a8 = unchecked((int)file._u32[ i + 8 ]);
				bool nameAt8 = validStrOffs.Contains( a8 );
				bool nameAt7 = validStrOffs.Contains( a7 );
				if( !nameAt8 && !nameAt7 )
					continue;
				bool useA = nameAt8;
				if( nameAt8 && nameAt7 )
				{
					uint cA = file._u32[ i + 7 ], cB = file._u32[ i + 8 ];
					bool cAok = cA <= 10000, cBok = cB <= 10000;
					if( cAok != cBok )
						useA = cAok;
				}
				int nameOff = useA ? a8 : a7;
				uint constant = useA ? file._u32[ i + 7 ] : file._u32[ i + 8 ];
				var sp = new B2dSprite
				{
					Material = unchecked((int)file._u32[ i + 1 ]),
					OriginalTextureId = unchecked((int)file._u32[ i + 2 ]),
					TextureId = unchecked((int)file._u32[ i + 2 ]),
					SrcRect = new System.Drawing.Rectangle( sx, sy, sw, sh ),
					Constant = constant,
					Name = GetStringByOffset( file, nameOff ) ?? string.Empty,
					U32StartIndex = i
				};
				file.SpritesInOrder.Add( sp );
				file._op11Starts[ i ] = 9;
				file._op11NameAt8[ i ] = useA;
			}

			BuildTextureIdRemapAndClean( file );
			return file;
		}

		public static void Write( B2dFile file, string path )
		{
			using var fs = File.Create( path );
			using var bw = new BinaryWriter( fs, Encoding.ASCII, leaveOpen: true );
			bw.Write( Magic );
			long sizePos = fs.Position;
			bw.Write( 0u );
			long offPos = fs.Position;
			bw.Write( 0u );
			bw.Write( file.Misc );

			var strings = new List<string>( file._strings );
			foreach( var n in file.ResourceById.Values )
				if( !strings.Contains( n ) )
					strings.Add( n );
			foreach( var sp in file.SpritesInOrder )
				if( !strings.Contains( sp.Name ) )
					strings.Add( sp.Name );

			long stOffset = fs.Position;
			var strOffsets = new Dictionary<string, int>( StringComparer.Ordinal );
			using( var ms = new MemoryStream() )
			using( var sbw = new BinaryWriter( ms, Encoding.ASCII, leaveOpen: true ) )
			{
				foreach( var s in strings )
				{
					strOffsets[ s ] = checked((int)ms.Position);
					sbw.Write( Encoding.ASCII.GetBytes( s ) );
					sbw.Write( (byte)0 );
				}
				sbw.Flush();
				var padded = Align16( ms.Length );
				fs.Position = stOffset;
				bw.Write( ms.ToArray() );
				var pad = padded - ms.Length;
				if( pad > 0 )
					bw.Write( new byte[ pad ] );
			}
			long stEnd = fs.Position;
			long cur = fs.Position;
			fs.Position = sizePos;
			bw.Write( checked((uint)( stEnd - stOffset )) );
			fs.Position = offPos;
			bw.Write( checked((uint)stOffset) );
			fs.Position = cur;

			var outU32 = new List<uint>( file._u32 );

			foreach( var kv in file._op2Starts )
			{
				int i = kv.Key;
				bool swap = file._op2Swap.TryGetValue( i, out var s ) && s;
				uint flag = file._op2Flag.TryGetValue( i, out var f ) ? f : 0u;
				int id;
				string name;
				if( swap )
				{
					id = unchecked((int)outU32[ i + 1 ]);
					name = file.ResourceById.TryGetValue( id, out var nm ) ? nm : string.Empty;
					int off = strOffsets.TryGetValue( name, out var so ) ? so : 0;
					outU32[ i + 0 ] = 2u;
					outU32[ i + 1 ] = unchecked((uint)id);
					outU32[ i + 2 ] = unchecked((uint)off);
					outU32[ i + 3 ] = flag;
				}
				else
				{
					id = unchecked((int)outU32[ i + 2 ]);
					name = file.ResourceById.TryGetValue( id, out var nm ) ? nm : string.Empty;
					int off = strOffsets.TryGetValue( name, out var so ) ? so : 0;
					outU32[ i + 0 ] = 2u;
					outU32[ i + 1 ] = unchecked((uint)off);
					outU32[ i + 2 ] = unchecked((uint)id);
					outU32[ i + 3 ] = flag;
				}
			}

			foreach( var sp in file.SpritesInOrder )
			{
				int i = sp.U32StartIndex;
				if( i < 0 || i + 8 >= outU32.Count )
					continue;
				bool nameAt8 = file._op11NameAt8.TryGetValue( i, out var na8 ) && na8;
				outU32[ i + 0 ] = 11u;
				outU32[ i + 1 ] = unchecked((uint)sp.Material);
				outU32[ i + 2 ] = unchecked((uint)sp.OriginalTextureId);
				outU32[ i + 3 ] = unchecked((uint)sp.SrcRect.X);
				outU32[ i + 4 ] = unchecked((uint)sp.SrcRect.Y);
				outU32[ i + 5 ] = unchecked((uint)sp.SrcRect.Width);
				outU32[ i + 6 ] = unchecked((uint)sp.SrcRect.Height);
				int nameOff = strOffsets.TryGetValue( sp.Name ?? string.Empty, out var so ) ? so : 0;
				if( nameAt8 )
				{
					outU32[ i + 7 ] = sp.Constant == 0 ? 100u : sp.Constant;
					outU32[ i + 8 ] = unchecked((uint)nameOff);
				}
				else
				{
					outU32[ i + 7 ] = unchecked((uint)nameOff);
					outU32[ i + 8 ] = sp.Constant == 0 ? 100u : sp.Constant;
				}
			}

			foreach( var v in outU32 )
				bw.Write( v );
		}

		static void BuildTextureIdRemapAndClean( B2dFile file )
		{
			var rawIds = new HashSet<int>();
			foreach( var sp in file.SpritesInOrder )
			{
				if( sp.OriginalTextureId != 0 && file.ResourceById.ContainsKey( sp.OriginalTextureId ) )
					rawIds.Add( sp.OriginalTextureId );
			}
			var sorted = new List<int>( rawIds );
			sorted.Sort();
			file.RawToDense.Clear();
			file.DenseToRaw.Clear();
			for( int i = 0; i < sorted.Count; i++ )
			{
				file.RawToDense[ sorted[ i ] ] = i;
				file.DenseToRaw.Add( sorted[ i ] );
			}
			for( int i = file.SpritesInOrder.Count - 1; i >= 0; i-- )
			{
				var sp = file.SpritesInOrder[ i ];
				if( string.Equals( sp.Name, "_RegistSprite_", StringComparison.OrdinalIgnoreCase ) )
				{
					file.SpritesInOrder.RemoveAt( i );
					continue;
				}
				if( file.RawToDense.TryGetValue( sp.OriginalTextureId, out var dense ) )
					sp.TextureId = dense;
				else
					sp.TextureId = -1;
				if( sp.TextureId < 0 )
					file.SpritesInOrder.RemoveAt( i );
			}
		}

		static bool LooksLikeTexture( string s )
		{
			if( string.IsNullOrEmpty( s ) )
				return false;
			if( s.EndsWith( ".tm2", StringComparison.OrdinalIgnoreCase ) )
				return true;
			if( s.EndsWith( ".cl2", StringComparison.OrdinalIgnoreCase ) )
				return true;
			if( s.StartsWith( "cm_", StringComparison.OrdinalIgnoreCase ) )
				return true;
			if( s.Contains( "tex", StringComparison.OrdinalIgnoreCase ) )
				return true;
			if( s.Contains( "base", StringComparison.OrdinalIgnoreCase ) )
				return true;
			return false;
		}

		static long Align16( long n ) => ( n + 15 ) & ~15L;

		static string ReadZ( BinaryReader br )
		{
			using var ms = new MemoryStream();
			int b;
			while( ( b = br.Read() ) > 0 )
				ms.WriteByte( (byte)b );
			return Encoding.ASCII.GetString( ms.ToArray() );
		}

		static string? GetStringByOffset( B2dFile f, int byteOffset )
		{
			foreach( var kv in f._stringToOffset )
				if( kv.Value == byteOffset )
					return kv.Key;
			return null;
		}
	}
}
