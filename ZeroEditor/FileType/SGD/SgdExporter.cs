using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Geometry;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Transforms;
using System.Numerics;
using System;
using System.Collections.Generic;

public static class SgdExporter
{
	static bool IsFinite( float f ) => !float.IsNaN( f ) && !float.IsInfinity( f );
	static bool IsFinite( in Vector3 v ) => IsFinite( v.X ) && IsFinite( v.Y ) && IsFinite( v.Z );
	static bool IsFinite( in Vector2 v ) => IsFinite( v.X ) && IsFinite( v.Y );

	static Vector3 SafeNormal( in Vector3 n )
	{
		float len = n.Length();
		if( float.IsNaN( len ) )
			return Vector3.UnitY;
		return n / len;
	}

	public static void SaveGlb( SgdModel model, string outGlbPath )
	{
		if( model == null )
			throw new ArgumentNullException( nameof( model ) );

		if( string.IsNullOrWhiteSpace( outGlbPath ) )
			throw new ArgumentNullException( nameof( outGlbPath ) );

		var scene = new SceneBuilder();

		var matBuilders = new MaterialBuilder[ model.Materials.Count ];
		for( int i = 0; i < model.Materials.Count; i++ )
		{
			var m = model.Materials[ i ];
			var name = string.IsNullOrWhiteSpace( m.Name ) ? $"mat_{i}" : m.Name;
			var mb = new MaterialBuilder( name ).WithDoubleSide( true ).WithMetallicRoughness();
			mb.WithBaseColor( new Vector4( 1, 1, 1, 1 ) );
			matBuilders[ i ] = mb;
		}

		for( int mi = 0; mi < model.Meshes.Count; mi++ )
		{
			var src = model.Meshes[ mi ];

			if( src.Positions.Count == 0 || src.Indices.Count < 3 )
				continue;

			var meshBuilder = new MeshBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>( src.Name );

			var mat = ( src.MaterialIndex >= 0 && src.MaterialIndex < matBuilders.Length )
					? matBuilders[ src.MaterialIndex ]
					: new MaterialBuilder( "default" ).WithMetallicRoughness().WithBaseColor( new Vector4( 1, 1, 1, 1 ) );

			var prim = meshBuilder.UsePrimitive( mat );

			for( int t = 0; t + 2 < src.Indices.Count; t += 3 )
			{
				int ia = src.Indices[ t + 0 ];
				int ib = src.Indices[ t + 1 ];
				int ic = src.Indices[ t + 2 ];

				var pa = src.Positions[ ia ];
				var pb = src.Positions[ ib ];
				var pc = src.Positions[ ic ];
				var na = ( src.Normals.Count == src.Positions.Count ) ? src.Normals[ ia ] : Vector3.UnitY;
				var nb = ( src.Normals.Count == src.Positions.Count ) ? src.Normals[ ib ] : Vector3.UnitY;
				var nc = ( src.Normals.Count == src.Positions.Count ) ? src.Normals[ ic ] : Vector3.UnitY;
				var ta = ( src.UVs.Count == src.Positions.Count ) ? src.UVs[ ia ] : Vector2.Zero;
				var tb = ( src.UVs.Count == src.Positions.Count ) ? src.UVs[ ib ] : Vector2.Zero;
				var tc = ( src.UVs.Count == src.Positions.Count ) ? src.UVs[ ic ] : Vector2.Zero;

				if( !IsFinite( pa ) || !IsFinite( pb ) || !IsFinite( pc ) )
					continue;

				if( !IsFinite( na ) || !IsFinite( nb ) || !IsFinite( nc ) )
				{
					na = nb = nc = Vector3.UnitY;
				}

				if( !IsFinite( ta ) || !IsFinite( tb ) || !IsFinite( tc ) )
				{
					ta = tb = tc = Vector2.Zero;
				}

				var va = new VertexPositionNormal( pa, SafeNormal( na ) );
				var vb = new VertexPositionNormal( pb, SafeNormal( nb ) );
				var vc = new VertexPositionNormal( pc, SafeNormal( nc ) );
				var uva = new VertexTexture1( ta );
				var uvb = new VertexTexture1( tb );
				var uvc = new VertexTexture1( tc );

				prim.AddTriangle( (va, uva), (vb, uvb), (vc, uvc) );
			}

			var node = new NodeBuilder( src.Name );
			scene.AddNode( node );
			scene.AddRigidMesh( meshBuilder, node, AffineTransform.Identity );
		}

		scene.ToGltf2().SaveGLB( outGlbPath );
	}
}
