using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

public static class BinUtil
{
	public readonly record struct Segment( int Offset, int Count );

	public readonly record struct StridedSegment( int Offset, int Count, int Stride );

	public readonly record struct Blob( int Offset, int Size );

	public static T ReadStruct<T>( BinaryReader br ) where T : struct
	{
		int size = Marshal.SizeOf<T>();
		var bytes = br.ReadBytes( size );
		if( bytes.Length != size )
			throw new EndOfStreamException();

		var handle = GCHandle.Alloc( bytes, GCHandleType.Pinned );
		try
		{ return Marshal.PtrToStructure<T>( handle.AddrOfPinnedObject() )!; }
		finally { handle.Free(); }
	}

	public static void WriteStruct<T>( BinaryWriter bw, in T value ) where T : struct
	{
		int size = Marshal.SizeOf<T>();
		var bytes = new byte[ size ];
		var handle = GCHandle.Alloc( bytes, GCHandleType.Pinned );
		try
		{ Marshal.StructureToPtr( value!, handle.AddrOfPinnedObject(), fDeleteOld: false ); }
		finally { handle.Free(); }
		bw.Write( bytes );
	}

	public static List<T> ReadArray<T>( BinaryReader br, int count ) where T : struct
	{
		var list = new List<T>( count );
		for( int i = 0; i < count; i++ )
			list.Add( ReadStruct<T>( br ) );
		return list;
	}

	public static void WriteArray<T>( BinaryWriter bw, IReadOnlyList<T> list ) where T : struct
	{
		for( int i = 0; i < list.Count; i++ )
			WriteStruct( bw, list[ i ] );
	}

	public static T ReadAt<T>( BinaryReader br, long offset ) where T : struct
	{
		var s = br.BaseStream;
		long prev = s.Position;
		s.Position = offset;
		try
		{ return ReadStruct<T>( br ); }
		finally { s.Position = prev; }
	}

	public static void WriteAt<T>( BinaryWriter bw, long offset, in T value ) where T : struct
	{
		var s = bw.BaseStream;
		long prev = s.Position;
		s.Position = offset;
		try
		{ WriteStruct( bw, value ); }
		finally { s.Position = prev; }
	}

	public static List<T> ReadArrayAt<T>( BinaryReader br, Segment seg ) where T : struct
	{
		var s = br.BaseStream;
		long prev = s.Position;
		s.Position = seg.Offset;
		try
		{ return ReadArray<T>( br, seg.Count ); }
		finally { s.Position = prev; }
	}

	public static void WriteArrayAt<T>( BinaryWriter bw, Segment seg, IReadOnlyList<T> list ) where T : struct
	{
		if( list.Count != seg.Count )
			throw new ArgumentException( $"List.Count ({list.Count}) != Segment.Count ({seg.Count})", nameof( list ) );

		var s = bw.BaseStream;
		long prev = s.Position;
		s.Position = seg.Offset;
		try
		{ WriteArray( bw, list ); }
		finally { s.Position = prev; }
	}

	public static List<T> ReadStridedArrayAt<T>( BinaryReader br, StridedSegment seg ) where T : struct
	{
		int elemSize = Marshal.SizeOf<T>();
		var list = new List<T>( seg.Count );

		var s = br.BaseStream;
		long prev = s.Position;
		try
		{
			long pos = seg.Offset;
			for( int i = 0; i < seg.Count; i++ )
			{
				s.Position = pos;
				list.Add( ReadStruct<T>( br ) );
				pos += seg.Stride == 0 ? elemSize : seg.Stride;
			}
		}
		finally { s.Position = prev; }

		return list;
	}

	public static void WriteStridedArrayAt<T>( BinaryWriter bw, StridedSegment seg, IReadOnlyList<T> list ) where T : struct
	{
		if( list.Count != seg.Count )
			throw new ArgumentException( $"List.Count ({list.Count}) != Segment.Count ({seg.Count})", nameof( list ) );

		int elemSize = Marshal.SizeOf<T>();
		var s = bw.BaseStream;
		long prev = s.Position;
		try
		{
			long pos = seg.Offset;
			for( int i = 0; i < seg.Count; i++ )
			{
				s.Position = pos;
				WriteStruct( bw, list[ i ] );
				pos += seg.Stride == 0 ? elemSize : seg.Stride;
			}
		}
		finally { s.Position = prev; }
	}
}
