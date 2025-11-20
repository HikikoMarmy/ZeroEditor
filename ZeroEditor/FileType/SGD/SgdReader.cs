using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

internal static class SgdReader
{
	const uint SGD_VALID_VERSIONID = 0x1050;
	const int PU_HEADER_SIZE = 16;

	enum ProcUnitType : int
	{
		VUVN = 0,
		MESH = 1,
		MATERIAL = 2,
		COORDINATE = 3,
		BOUNDING_BOX = 4,
		GS_IMAGE = 5,
		TRI2 = 10,
		END = 11,
		INVALID = 12,
		MonotoneTRI2 = 13,
		StackTRI2 = 14,
	}

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	struct FileHeader
	{
		public uint uiVersionId;
		public byte ucMapFlag;
		public byte ucModelType;
		public ushort usNumMaterial;
		public uint pCoord, pMaterial, pVectorInfo;
		public uint uiNumBlock;
	}

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	struct PUHeaderPrefix
	{
		public uint pNext;
		public int iCategory;
	}

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	struct VUVNDesc
	{
		public short sNumVertex;
		public short sNumNormal;
		public ushort ucSize;
		public ushort ucVectorType;
	}

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	struct VUMeshDesc
	{
		public int iTagSize;
		public byte ucPad0;
		public byte ucMeshType;
		public byte ucNumMesh;
		public byte ucPad1;
	}

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	struct CoordPair
	{
		public Matrix4x4 matCoord;
		public Matrix4x4 matLocalWorld;
	}

	static readonly string LogPath = Path.Combine( AppContext.BaseDirectory, "sgd_read_log.txt" );
	static void Log( string s ) { try { File.AppendAllText( LogPath, s + Environment.NewLine ); } catch { } }

	static Vector3 ReadVec3F( BinaryReader br )
	{
		return new Vector3( br.ReadSingle(), br.ReadSingle(), br.ReadSingle() );
	}

	static Matrix4x4 ReadMat4( BinaryReader br )
	{
		return new Matrix4x4(
			br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle(),
			br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle(),
			br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle(),
			br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()
		);
	}


	static CoordPair[] ReadCoordinates( BinaryReader br, Stream fs, in FileHeader fh )
	{
		if( fh.pCoord == 0 )
			return Array.Empty<CoordPair>();

		fs.Position = fh.pCoord;

		var count = (int)Math.Max( 0, fh.uiNumBlock - 1 );
		var outArr = new CoordPair[ count ];

		for( int i = 0; i < count; i++ )
		{
			var a = ReadMat4( br );
			var b = ReadMat4( br );
			outArr[ i ] = new CoordPair { matCoord = a, matLocalWorld = b };
			fs.Position += 96;
		}

		return outArr;
	}


	static bool IsFinite( float f ) => !( float.IsNaN( f ) || float.IsInfinity( f ) );
	static bool IsFinite( Vector3 v ) => IsFinite( v.X ) && IsFinite( v.Y ) && IsFinite( v.Z );

	static bool IsUsableMatrix( in Matrix4x4 m )
	{
		if( !IsFinite( m.M11 ) || !IsFinite( m.M22 ) || !IsFinite( m.M33 ) || !IsFinite( m.M44 ) )
			return false;
		return Matrix4x4.Invert( m, out _ );
	}

	static Vector3 TransformPosSafe( Vector3 v, in Matrix4x4 m )
	{
		var r = Vector3.Transform( v, m );
		return IsFinite( r ) ? r : v;
	}

	static Vector3 TransformPos( Vector3 v, CoordPair[] pCoord, int coordId, bool isCharacter )
	{
		if( pCoord == null || pCoord.Length == 0 || coordId < 0 || coordId >= pCoord.Length )
			return v;

		var mc = pCoord[ coordId ].matCoord;
		var mw = pCoord[ coordId ].matLocalWorld;

		var m = isCharacter
			? ( IsUsableMatrix( mc ) ? mc : Matrix4x4.Identity )
			: ( IsUsableMatrix( mw ) ? mw : ( IsUsableMatrix( mc ) ? mc : Matrix4x4.Identity ) );

		return TransformPosSafe( v, m );
	}


	static SgdPrimitive PrimitiveFromVectorType( ushort vtype )
	{
		return vtype == 5 ? SgdPrimitive.TriangleStrip : SgdPrimitive.Triangle;
	}

	static void MakeTriangleIndices( SgdPrimitive prim, int n, List<int> dst )
	{
		dst.Clear();

		switch( prim )
		{
			case SgdPrimitive.Triangle:
			for( int i = 0; i + 2 < n; i += 3 )
			{ dst.Add( i ); dst.Add( i + 1 ); dst.Add( i + 2 ); }
			break;

			case SgdPrimitive.TriangleStrip:
			for( int i = 0; i + 2 < n; i++ )
			{
				if( ( i & 1 ) != 0 )
				{ dst.Add( i ); dst.Add( i + 2 ); dst.Add( i + 1 ); }
				else
				{ dst.Add( i ); dst.Add( i + 1 ); dst.Add( i + 2 ); }
			}
			break;

			case SgdPrimitive.TriangleFan:
			for( int i = 1; i + 1 < n; i++ )
			{ dst.Add( 0 ); dst.Add( i ); dst.Add( i + 1 ); }
			break;
		}
	}


	static long ReadSTChunk( BinaryReader br, Stream fs, long cursor, int fallbackCount, List<Vector2> dst, out int readCount )
	{
		dst.Clear();
		readCount = 0;
		if( cursor == 0 )
			return 0;

		var save = fs.Position;
		try
		{
			if( cursor + 4 > fs.Length )
				return cursor;

			fs.Position = cursor;
			var unpack = br.ReadUInt32();
			var num = (int)( ( unpack >> 16 ) & 0xFF );
			if( num <= 0 )
				num = fallbackCount;

			var start = fs.Position;

			var okFloat = start + (long)num * 8 <= fs.Length;
			if( okFloat )
			{
				for( int i = 0; i < num; i++ )
				{
					var s = br.ReadSingle();
					var t = br.ReadSingle();

					if( !IsFinite( s ) || !IsFinite( t ) || MathF.Abs( s ) > 1e4f || MathF.Abs( t ) > 1e4f )
					{ okFloat = false; break; }

					dst.Add( new Vector2( s, 1f - t ) );
				}

				if( okFloat )
				{ readCount = num; return start + num * 8; }
			}

			fs.Position = start;
			var okFixed = start + (long)num * 4 <= fs.Length;
			if( okFixed )
			{
				for( int i = 0; i < num; i++ )
				{
					var su = br.ReadUInt16();
					var tu = br.ReadUInt16();
					dst.Add( new Vector2( su / 4096f, 1f - ( tu / 4096f ) ) );
				}
				readCount = num;
				return start + num * 4;
			}

			dst.Clear();
			return cursor + 4;
		}
		finally { fs.Position = save; }
	}


	public static SgdModel Load( string path )
	{
		try
		{ File.WriteAllText( LogPath, $"SGD Reading '{path}'\n" ); }
		catch { }

		using var fs = File.OpenRead( path );
		using var br = new BinaryReader( fs );

		var fh = BinUtil.ReadStruct<FileHeader>( br );
		if( fh.uiVersionId != SGD_VALID_VERSIONID )
			throw new InvalidDataException( "Unsupported" );

		var ap = new uint[ fh.uiNumBlock ];
		for( int i = 0; i < ap.Length; i++ )
			ap[ i ] = br.ReadUInt32();

		var coord = ReadCoordinates( br, fs, fh );

		var model = new SgdModel();
		var fileName = Path.GetFileName( path );
		var dir = Path.GetDirectoryName( path ) ?? "";

		// Rooms start with "r"
		var looksLikeRoom = !string.IsNullOrEmpty( fileName ) && fileName.StartsWith( "r", StringComparison.OrdinalIgnoreCase );

		var inRoomFolder =
			dir.IndexOf( $"{Path.DirectorySeparatorChar}room{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase ) >= 0
			||
			dir.EndsWith( $"{Path.DirectorySeparatorChar}room", StringComparison.OrdinalIgnoreCase );

		model.Kind = ( looksLikeRoom || inRoomFolder ) ? SgdKind.Room : SgdKind.Character;
		var isCharacter = model.Kind == SgdKind.Character;


		if( fh.usNumMaterial > 0 && fh.pMaterial != 0 )
		{
			fs.Position = fh.pMaterial;
			for( int i = 0; i < fh.usNumMaterial; i++ )
			{
				var uiPrimType = br.ReadUInt32();
				var nameBytes = br.ReadBytes( 12 );
				var name = System.Text.Encoding.ASCII.GetString( nameBytes ).TrimEnd( '\0', ' ' );

				if( string.IsNullOrWhiteSpace( name ) )
					name = $"mat_{i}";

				model.Materials.Add( new SgdMaterial { Name = name } );

				fs.Position += ( 16 * 4 ) + ( 4 * 4 ) + ( 4 * 3 ) + ( 4 * 8 );
			}
		}


		for( int bi = 0; bi < ap.Length; bi++ )
		{
			var headOff = ap[ bi ];
			if( headOff == 0 )
				continue;

			long node = headOff;
			VUVNDesc vuvnDesc = default;
			long vuvnRel = 0;
			int currentMat = -1, currentCoord = -1;

			while( node != 0 && node < fs.Length )
			{
				fs.Position = node;

				var pref = BinUtil.ReadStruct<PUHeaderPrefix>( br );
				var unionOff = node + 8;
				var payloadOff = node + PU_HEADER_SIZE;
				var next = pref.pNext == 0 ? 0 : node + pref.pNext;

				switch( (ProcUnitType)pref.iCategory )
				{
					case ProcUnitType.VUVN:
					{
						fs.Position = unionOff;
						vuvnDesc = BinUtil.ReadStruct<VUVNDesc>( br );
						vuvnRel = node;
						break;
					}

					case ProcUnitType.MATERIAL:
					{
						fs.Position = unionOff;
						currentMat = br.ReadInt32();
						break;
					}

					case ProcUnitType.COORDINATE:
					{
						fs.Position = unionOff;
						currentCoord = br.ReadInt32();
						br.ReadInt32();
						break;
					}

					case ProcUnitType.MESH:
					{
						if( vuvnRel == 0 )
						{ node = next; break; }

						fs.Position = unionOff;
						var meshDesc = BinUtil.ReadStruct<VUMeshDesc>( br );

						var meshPayloadStart = (int)payloadOff;
						fs.Position = meshPayloadStart + 4;

						var sOffsetToST = br.ReadInt16();
						var sOffsetToPrim = br.ReadInt16();

						long stCur = sOffsetToST != 0 ? meshPayloadStart + sOffsetToST : 0;
						long colorCur = sOffsetToPrim != 0 ? meshPayloadStart + sOffsetToPrim : 0;

						var vuvnData = vuvnRel + PU_HEADER_SIZE;
						var totalVerts = Math.Max( 0, (int)vuvnDesc.sNumVertex );

						var remain = totalVerts;
						var vOff = 0;
						var nOff = 0;

						var prim = PrimitiveFromVectorType( vuvnDesc.ucVectorType );

						for( int sm = 0; sm < meshDesc.ucNumMesh && remain > 0; sm++ )
						{
							var num = remain;
							var isIMT2 = meshDesc.ucMeshType == 0x12 || meshDesc.ucMeshType == 0x32;

							if( isIMT2 && colorCur != 0 )
							{
								var saveHdr = fs.Position;
								fs.Position = colorCur;

								if( fs.Position + 4 <= fs.Length )
								{
									var w0 = br.ReadUInt32();
									var nBits = (int)( w0 & 0x3FF );
									if( nBits > 0 && nBits <= remain )
										num = nBits;
								}
								fs.Position = saveHdr;
							}
							else
							{
								var pMeshInfo = node + ( 4 * PU_HEADER_SIZE ) + sm * 8;
								if( pMeshInfo + 8 <= fs.Length )
								{
									fs.Position = pMeshInfo;
									_ = br.ReadUInt32();
									var uiPointNum = br.ReadUInt32();
									if( uiPointNum > 0 && uiPointNum <= remain )
										num = (int)uiPointNum;
								}
							}

							var pos = new List<Vector3>( num );
							var nrm = new List<Vector3>( num );
							var uv = new List<Vector2>( num );

							var save2 = fs.Position;

							if( meshDesc.ucMeshType == 0x12 )
							{
								var basePos = vuvnData + 40 + (long)vOff * 24;
								var need = (long)num * 24;

								if( basePos + need <= fs.Length )
								{
									fs.Position = basePos;
									for( int i = 0; i < num; i++ )
									{
										var p = ReadVec3F( br );
										var n = Vector3.Normalize( ReadVec3F( br ) );
										pos.Add( TransformPos( p, coord, currentCoord, isCharacter ) );
										nrm.Add( n );
									}
								}
							}
							else if( meshDesc.ucMeshType == 0x32 )
							{
								var basePos = vuvnData + 40 + (long)vOff * 12;
								var needPos = (long)num * 12;

								var vt2fBase = vuvnData + 40 + (long)totalVerts * 12;
								var baseNrm = vt2fBase + (long)nOff * 12;
								var needNrm = (long)num * 12;

								if( basePos + needPos <= fs.Length )
								{
									fs.Position = basePos;
									for( int i = 0; i < num; i++ )
										pos.Add( TransformPos( ReadVec3F( br ), coord, currentCoord, isCharacter ) );
								}

								if( baseNrm + needNrm <= fs.Length )
								{
									fs.Position = baseNrm;
									for( int i = 0; i < num; i++ )
										nrm.Add( Vector3.Normalize( ReadVec3F( br ) ) );
								}
							}
							else
							{
								var basePos = vuvnData + 40 + (long)vOff * 24;
								var need = (long)num * 24;

								if( basePos + need <= fs.Length )
								{
									fs.Position = basePos;
									for( int i = 0; i < num; i++ )
									{
										var p = ReadVec3F( br );
										var n = Vector3.Normalize( ReadVec3F( br ) );
										pos.Add( TransformPos( p, coord, currentCoord, isCharacter ) );
										nrm.Add( n );
									}
								}
							}

							fs.Position = save2;

							var stNum = 0;
							if( stCur != 0 )
							{
								Log( $"ST @{stCur:X}" );
								stCur = ReadSTChunk( br, fs, stCur, num, uv, out stNum );
							}

							if( isIMT2 && colorCur != 0 )
							{
								var nextC = colorCur + 16 + (long)num * 12;
								if( nextC <= fs.Length )
									colorCur = nextC;
							}

							if( pos.Count > num )
								pos.RemoveRange( num, pos.Count - num );
							if( nrm.Count > num )
								nrm.RemoveRange( num, nrm.Count - num );
							if( uv.Count > num )
								uv.RemoveRange( num, uv.Count - num );

							if( uv.Count < num && stNum > 0 )
								while( uv.Count < num )
									uv.Add( uv.Count > 0 ? uv[ ^1 ] : Vector2.Zero );

							if( pos.Count >= 3 )
							{
								var idx = new List<int>( ( pos.Count - 2 ) * 3 );
								MakeTriangleIndices( prim, pos.Count, idx );

								Log( $"SM sm={sm} prim={(int)prim} vt={vuvnDesc.ucVectorType} n={num} vOff={vOff} nOff={nOff}" );

								var mesh = new SgdMesh
								{
									Name = $"blk{bi}_mesh{model.Meshes.Count}",
									MaterialIndex = currentMat,
									Primitive = prim
								};

								mesh.Positions.AddRange( pos );
								mesh.Normals.AddRange( nrm.Count == pos.Count ? nrm : new Vector3[ pos.Count ] );

								if( uv.Count == pos.Count )
								{
									mesh.UVs.AddRange( uv );
								}

								mesh.Indices.AddRange( idx );

								if( mesh.Indices.Count >= 3 )
								{
									model.Meshes.Add( mesh );
								}
							}

							vOff += num;
							nOff += num;
							remain -= num;
						}
						break;
					}
				}

				node = next;
			}
		}

		return model;
	}
}
