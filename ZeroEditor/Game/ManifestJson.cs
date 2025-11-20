using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroEditor.Game
{
	public sealed class ManifestJson
	{
		[JsonPropertyName( "version" )]
		public int Version { get; set; } = 1;

		[JsonPropertyName( "game_serial" )]
		public string? GameSerial { get; set; }

		[JsonPropertyName( "elf" )]
		public string? Elf { get; set; }

		[JsonPropertyName( "file_table" )]
		public List<FileEntry>? FileTable { get; set; } = new();

		[JsonPropertyName( "game" )]
		public GameId? Game { get; set; }

		[JsonPropertyName( "region" )]
		public GameRegion? Region { get; set; }

		[JsonPropertyName( "iso_toc" )]
		public List<IsoTocEntry>? IsoToc { get; set; }

		[JsonPropertyName( "iso_layout" )]
		public IsoLayoutSummary? IsoLayout { get; set; }

		[JsonPropertyName( "dir_order" )]
		public Dictionary<string, List<string>>? DirOrder { get; set; }

		public sealed record IsoTocEntry(
			string Path,
			int Lba,
			uint Size,
			byte Flags
		);

		public sealed record IsoLayoutSummary(
			string? ImgBdPath,
			int? ImgBdLba,
			uint? ImgBdSize,
			bool ImgBdIsLast,
			long GapAfterImgBdBytes,
			string? NextFileAfterImgBd,
			int? NextFileAfterImgBdLba,
			string? ImgHdPath,
			int? ImgHdLba,
			uint? ImgHdSize
		);

		[JsonConverter( typeof( FileEntryJsonConverter ) )]
		public sealed record FileEntry
		{
			[JsonPropertyName( "path" )]
			public string Path { get; init; } = "";

			[JsonPropertyName( "exists" )]
			public bool? Exists { get; init; }

			[JsonPropertyName( "cmp" )]
			public bool? Compressed { get; init; }
		}

		public static ManifestJson Create(
			string serial,
			string elfFileName,
			IEnumerable<string> fileTablePaths,
			GameId? game = null,
			GameRegion? region = null )
		{
			static string Norm( string p ) => p.Replace( '\\', '/' );
			var files = fileTablePaths
				.Select( Norm )
				.Where( p => !string.IsNullOrWhiteSpace( p ) )
				.Distinct( StringComparer.OrdinalIgnoreCase )
				.Select( p => new FileEntry { Path = p } )
				.ToList();

			return new ManifestJson
			{
				Version = 1,
				GameSerial = serial,
				Elf = elfFileName,
				FileTable = files,
				Game = game,
				Region = region
			};
		}

		static JsonSerializerOptions CreateSerializerOptionsForWrite()
		{
			var opts = new JsonSerializerOptions
			{
				WriteIndented = true,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			};
			opts.Converters.Add( new JsonStringEnumConverter() );
			return opts;
		}

		static JsonSerializerOptions CreateSerializerOptionsForRead()
		{
			var opts = new JsonSerializerOptions
			{
				AllowTrailingCommas = true,
				ReadCommentHandling = JsonCommentHandling.Skip,
				PropertyNameCaseInsensitive = true,
				DefaultIgnoreCondition = JsonIgnoreCondition.Never
			};
			opts.Converters.Add( new JsonStringEnumConverter() );
			return opts;
		}

		public static async Task SaveAsync( string path, ManifestJson manifest, CancellationToken ct = default )
		{
			NormalizeInPlace( manifest );
			var opts = CreateSerializerOptionsForWrite();
			var json = JsonSerializer.Serialize( manifest, opts );
			await File.WriteAllTextAsync( path, json, Encoding.UTF8, ct ).ConfigureAwait( false );
		}

		public static void Save( string path, ManifestJson manifest )
		{
			NormalizeInPlace( manifest );
			var opts = CreateSerializerOptionsForWrite();
			var json = JsonSerializer.Serialize( manifest, opts );
			File.WriteAllText( path, json, Encoding.UTF8 );
		}

		public async Task SaveAsync( string path, CancellationToken ct = default )
		{
			await SaveAsync( path, this, ct ).ConfigureAwait( false );
		}

		public void Save( string path )
		{
			Save( path, this );
		}

		public static ManifestJson Load( string path )
		{
			using var fs = File.OpenRead( path );
			var opts = CreateSerializerOptionsForRead();

			var mf = JsonSerializer.Deserialize<ManifestJson>( fs, opts )
				?? throw new InvalidDataException( "Manifest JSON is empty or invalid." );

			if( mf.Version <= 0 )
				mf.Version = 1;

			NormalizeInPlace( mf );
			ValidateBasic( mf, path );
			return mf;
		}

		static void NormalizeInPlace( ManifestJson mf )
		{
			static string NormPath( string s ) => s.Replace( '\\', '/' ).Trim();

			mf.GameSerial = mf.GameSerial?.Trim();
			mf.Elf = mf.Elf?.Trim();

			if( mf.FileTable == null )
				mf.FileTable = new List<FileEntry>();

			var seen = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
			var list = new List<FileEntry>( mf.FileTable.Count );
			foreach( var fe in mf.FileTable )
			{
				if( fe == null )
					continue;
				if( string.IsNullOrWhiteSpace( fe.Path ) )
					continue;
				var p = NormPath( fe.Path );
				if( p.Length == 0 )
					continue;
				if( seen.Add( p ) )
				{
					list.Add( new FileEntry
					{
						Path = p,
						Exists = fe.Exists,
						Compressed = fe.Compressed
					} );
				}
			}

			mf.FileTable = list;

			if( mf.DirOrder != null )
			{
				var norm = new Dictionary<string, List<string>>( StringComparer.OrdinalIgnoreCase );
				foreach( var kv in mf.DirOrder )
				{
					var key = kv.Key.Replace( '/', '\\' ).Trim( '\\' ).ToUpperInvariant();
					norm[ key ] = kv.Value?.ToList() ?? new List<string>();
				}
				mf.DirOrder = norm;
			}
		}

		static void ValidateBasic( ManifestJson mf, string? pathForError = null )
		{
			if( string.IsNullOrWhiteSpace( mf.GameSerial ) )
				throw new InvalidDataException( $"Manifest missing game_serial{Where( pathForError )}." );
			if( string.IsNullOrWhiteSpace( mf.Elf ) )
				throw new InvalidDataException( $"Manifest missing elf{Where( pathForError )}." );
			if( mf.FileTable is null || mf.FileTable.Count == 0 )
				throw new InvalidDataException( $"Manifest file_table is empty{Where( pathForError )}." );

			static string Where( string? p ) => p is null ? "" : $" ({p})";
		}

		public sealed class FileEntryJsonConverter : JsonConverter<FileEntry>
		{
			public override FileEntry Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
			{
				if( reader.TokenType == JsonTokenType.String )
				{
					var path = reader.GetString() ?? "";
					return new FileEntry { Path = path };
				}

				if( reader.TokenType == JsonTokenType.StartObject )
				{
					string path = "";
					bool? exists = null;
					bool? cmp = null;

					while( reader.Read() )
					{
						if( reader.TokenType == JsonTokenType.EndObject )
							break;

						if( reader.TokenType != JsonTokenType.PropertyName )
							throw new JsonException();

						var name = reader.GetString();
						reader.Read();

						if( name == null )
						{
							reader.Skip();
							continue;
						}

						switch( name )
						{
							case "path":
							path = reader.TokenType == JsonTokenType.String ? ( reader.GetString() ?? "" ) : "";
							break;
							case "exists":
							if( reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False )
								exists = reader.GetBoolean();
							else
								reader.Skip();
							break;
							case "cmp":
							if( reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False )
								cmp = reader.GetBoolean();
							else
								reader.Skip();
							break;
							default:
							reader.Skip();
							break;
						}
					}

					return new FileEntry
					{
						Path = path ?? "",
						Exists = exists,
						Compressed = cmp
					};
				}

				throw new JsonException();
			}

			public override void Write( Utf8JsonWriter writer, FileEntry value, JsonSerializerOptions options )
			{
				writer.WriteStartObject();
				writer.WriteString( "path", value.Path ?? "" );
				if( value.Exists.HasValue )
					writer.WriteBoolean( "exists", value.Exists.Value );
				if( value.Compressed.HasValue )
					writer.WriteBoolean( "cmp", value.Compressed.Value );
				writer.WriteEndObject();
			}
		}
	}
}
