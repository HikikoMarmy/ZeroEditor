using System;
using System.IO;
using System.Runtime.InteropServices;

internal static class AtomicIO
{
	public static string CreateTempSibling( string targetPath )
	{
		string dir = Path.GetDirectoryName( targetPath )!;
		string name = Path.GetFileName( targetPath );
		Directory.CreateDirectory( dir );
		return Path.Combine( dir, $".__tmp_{name}_{Guid.NewGuid():N}" );
	}

	public static void ReplaceFile( string tempPath, string targetPath )
	{
		try
		{
			if( File.Exists( targetPath ) )
			{
				File.Replace( tempPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true );
				return;
			}
			else
			{
				SafeMoveOverwrite( tempPath, targetPath );
				return;
			}
		}
		catch( IOException ) { }

		if( MoveFileEx( tempPath, targetPath,
			MoveFileFlags.MOVEFILE_REPLACE_EXISTING | MoveFileFlags.MOVEFILE_WRITE_THROUGH ) )
		{
			return;
		}

		string staged = GetUniqueStagedName( targetPath );
		SafeMoveOverwrite( tempPath, staged );
		throw new IOException(
			"The target file is in use by another process and cannot be replaced right now.\n" +
			$"A new file has been written here:\n  {staged}\n" +
			"Close the program that has the file open, then rename the staged file to replace the original." );
	}

	private static void SafeMoveOverwrite( string src, string dst )
	{
		if( File.Exists( dst ) )
		{
			try
			{ File.Delete( dst ); }
			catch {}
		}

		try
		{
			File.Move( src, dst );
		}
		catch( IOException )
		{
			File.Copy( src, dst, overwrite: true );
			File.Delete( src );
		}
	}

	private static string GetUniqueStagedName( string targetPath )
	{
		string dir = Path.GetDirectoryName( targetPath )!;
		string name = Path.GetFileName( targetPath );
		string staged = Path.Combine( dir, name + ".new" );
		if( !File.Exists( staged ) )
			return staged;

		for( int i = 1; i < 1000; i++ )
		{
			string cand = Path.Combine( dir, $"{name}.new.{i}" );
			if( !File.Exists( cand ) )
				return cand;
		}
		return Path.Combine( dir, $"{name}.new.{Guid.NewGuid():N}" );
	}

	[Flags]
	private enum MoveFileFlags : int
	{
		MOVEFILE_REPLACE_EXISTING = 0x00000001,
		MOVEFILE_WRITE_THROUGH = 0x00000008,
	}

	[DllImport( "kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode )]
	private static extern bool MoveFileEx( string lpExistingFileName, string lpNewFileName, MoveFileFlags dwFlags );
}
