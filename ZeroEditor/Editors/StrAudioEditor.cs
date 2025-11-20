using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ZeroEditor.Audio;

namespace ZeroEditor.Editors
{
	public partial class AudioEditor : UserControl
	{
		[DllImport( "winmm.dll", CharSet = CharSet.Auto )]
		private static extern int mciSendString( string command, StringBuilder? returnValue, int returnLength, IntPtr winHandle );

		[DllImport( "winmm.dll", CharSet = CharSet.Auto )]
		private static extern int mciGetErrorString( int errorCode, StringBuilder errorText, int errorTextSize );

		[DllImport( "winmm.dll" )]
		private static extern int waveOutSetVolume( IntPtr hwo, uint dwVolume );

		private static int MciStatusInt( string alias, string field )
		{
			var sb = new StringBuilder( 64 );
			int rc = mciSendString( $"status {alias} {field}", sb, sb.Capacity, IntPtr.Zero );
			if( rc != 0 )
			{
				var err = new StringBuilder( 256 );
				mciGetErrorString( rc, err, err.Capacity );
				throw new InvalidOperationException( $"MCI status error ({field}): {err}" );
			}
			return int.TryParse( sb.ToString(), out var v ) ? v : 0;
		}

		private string? _strPath;
		private string? _tempWavPath;
		private string? _mciAlias;
		private int _durationMs;
		private bool _isPlaying;
		private bool _mciHasVolume;
		private bool _loop;
		private readonly System.Windows.Forms.Timer _uiTimer = new System.Windows.Forms.Timer();
		private bool _userScrubbing;
		private AdpcmFormat? _format;
		private AdpcmBank? _bank;
		private int _currentClipIndex;
		private int _loopStartMs;
		private int _loopEndMs;
		private bool _hasLoopRegion;

		public int Interleave { get; set; } = 0x0800;
		public int Channels { get; set; } = 1;
		public int SampleRate { get; set; } = 48000;

		public AudioEditor()
		{
			InitializeComponent();
			ThemeManager.ApplyDark( this );

			volumeBar.Minimum = 0;
			volumeBar.Maximum = 100;
			volumeBar.TickFrequency = 10;
			volumeBar.Value = 30;
			volumeBar.ValueChanged += VolumeBar_ValueChanged;

			_uiTimer.Interval = 100;
			_uiTimer.Tick += UiTimer_Tick;

			if( trackAudio != null )
			{
				trackAudio.Minimum = 0;
				trackAudio.Maximum = 1;
				trackAudio.TickFrequency = 500;
				trackAudio.SmallChange = 100;
				trackAudio.LargeChange = 1000;
				trackAudio.MouseDown += ( _, __ ) => _userScrubbing = true;
				trackAudio.MouseUp += TrackAudio_MouseUpSeek;
				trackAudio.ValueChanged += TrackAudio_ValueChangedMirror;
				trackAudio.Scroll += TrackAudio_ValueChangedMirror;
				trackAudio.Value = 0;
			}

			if( comboClips != null )
			{
				comboClips.DropDownStyle = ComboBoxStyle.DropDownList;
				comboClips.Visible = false;
			}

			lblTime.Text = "00:00 / 00:00";
			UpdateInfoLabel( null );
			Disposed += ( _, __ ) => Cleanup();
		}

		public void Load( string path )
		{
			StopAndClose();

			_strPath = path;
			_tempWavPath = null;
			_bank = null;
			_currentClipIndex = 0;
			_hasLoopRegion = false;
			_loopStartMs = 0;
			_loopEndMs = 0;

			_format = AdpcmFormatResolver.ForPath( path );

			Interleave = _format.Interleave;
			Channels = _format.Channels;
			SampleRate = _format.SampleRate;

			if( _format.Kind == AdpcmKind.Bd && AdpcmFormatResolver.TryGetHxdBank( path, out var bank ) )
			{
				_bank = bank;
				if( bank.InterleaveBytes > 0 )
					Interleave = bank.InterleaveBytes;
			}

			RefreshClipCombo();
			ApplyCurrentClipFormat();

			UpdateInfoLabel( new FileInfo( path ) );
			SafeSetBars( 0 );
			SetTimeLabel( 0, 0 );
		}


		private void ClipCombo_SelectedIndexChanged( object? sender, EventArgs e )
		{
			if( comboClips == null )
				return;
			if( _bank == null || _bank.Clips.Count == 0 )
				return;
			var idx = comboClips.SelectedIndex;
			if( idx < 0 || idx >= _bank.Clips.Count )
				return;
			_currentClipIndex = idx;
			_tempWavPath = null;
			StopAndClose();
			ApplyCurrentClipFormat();
			UpdateInfoLabel( _strPath != null ? new FileInfo( _strPath ) : null );
			SafeSetBars( 0 );
			SetTimeLabel( 0, 0 );
		}

		private void ApplyCurrentClipFormat()
		{
			if( _bank != null && _bank.Clips.Count > 0 )
			{
				if( _currentClipIndex < 0 || _currentClipIndex >= _bank.Clips.Count )
					_currentClipIndex = 0;
				var clip = _bank.Clips[ _currentClipIndex ];
				SampleRate = clip.SampleRate;
			}
			else if( _format != null )
			{
				SampleRate = _format.SampleRate;
			}
		}

		private void RefreshClipCombo()
		{
			if( comboClips == null )
				return;

			comboClips.Items.Clear();

			if( _bank == null || _bank.Clips.Count <= 1 )
			{
				comboClips.Visible = false;
				return;
			}

			for( int i = 0; i < _bank.Clips.Count; i++ )
			{
				var c = _bank.Clips[ i ];
				string label = $"Clip {c.Index}  {c.SampleRate} Hz  0x{c.OffsetBytes:X} / 0x{c.LengthBytes:X}";
				comboClips.Items.Add( label );
			}

			if( _bank.Clips.Count > 0 )
				comboClips.SelectedIndex = 0;

			comboClips.Visible = true;
		}

		private void buttonPlay_Click( object sender, EventArgs e )
		{
			try
			{
				if( !_isPlaying )
				{
					EnsureDecodedWavOnDisk();
					OpenIfNeeded();
					SetMciVolumeFromSlider();
					Play();
				}
				else
				{
					Stop();
				}
			}
			catch( Exception ex )
			{
				MessageBox.Show( this, ex.Message, "Play error" );
			}
		}

		private void btnExport_Click( object sender, EventArgs e )
		{
			try
			{
				if( string.IsNullOrEmpty( _strPath ) )
					throw new InvalidOperationException( "No file loaded." );

				EnsureDecodedWavOnDisk();

				using var sfd = new SaveFileDialog
				{
					Filter = "WAV (PCM 16-bit)|*.wav|All files|*.*",
					FileName = BuildDefaultExportName()
				};
				if( sfd.ShowDialog( this ) != DialogResult.OK )
					return;

				File.Copy( _tempWavPath!, sfd.FileName, overwrite: true );
				MessageBox.Show( this, "Exported WAV.", "Export" );
			}
			catch( Exception ex )
			{
				MessageBox.Show( this, ex.Message, "Export error" );
			}
		}

		private string BuildDefaultExportName()
		{
			if( string.IsNullOrEmpty( _strPath ) )
				return "audio.wav";
			string baseName = Path.GetFileNameWithoutExtension( _strPath );
			if( _bank != null && _bank.Clips.Count > 1 && _currentClipIndex >= 0 && _currentClipIndex < _bank.Clips.Count )
				return $"{baseName}_clip{_currentClipIndex:D2}.wav";
			return Path.ChangeExtension( Path.GetFileName( _strPath ), ".wav" );
		}

		private void btnImport_Click( object sender, EventArgs e )
		{
			if( string.IsNullOrEmpty( _strPath ) )
			{
				MessageBox.Show( this, "No file loaded.", "Import" );
				return;
			}

			using var ofd = new OpenFileDialog
			{
				Title = "Choose WAV to import (PCM16 mono/stereo)",
				Filter = "WAV|*.wav|All files|*.*"
			};
			if( ofd.ShowDialog( this ) != DialogResult.OK )
				return;

			try
			{
				StopAndClose();

				if( _bank == null || _bank.Clips.Count <= 1 )
				{
					var enc = new AdpcmAudio( Interleave, Channels, SampleRate );
					enc.EncodeWavToAdpcmFile( ofd.FileName, _strPath! );
				}
				else
				{
					if( _currentClipIndex < 0 || _currentClipIndex >= _bank.Clips.Count )
						_currentClipIndex = 0;

					var clip = _bank.Clips[ _currentClipIndex ];
					var enc = new AdpcmAudio( Interleave, Channels, clip.SampleRate );
					byte[] adpcm = enc.EncodeWavToAdpcmBytes( ofd.FileName );

					int minLen = clip.LengthBytes;
					if( clip.LoopEnd > 0 && clip.LoopEnd > minLen )
						minLen = clip.LoopEnd;
					if( adpcm.Length < minLen )
						throw new InvalidOperationException( "Encoded clip is shorter than existing playback region." );

					using var fs = new FileStream( _strPath!, FileMode.Open, FileAccess.ReadWrite, FileShare.None );
					long oldLength = fs.Length;
					if( oldLength <= 0 )
						throw new InvalidOperationException( "ADPCM file is empty." );

					int oldClipLen = clip.LengthBytes;
					int delta = adpcm.Length - oldClipLen;

					if( delta == 0 )
					{
						fs.Position = clip.OffsetBytes;
						fs.Write( adpcm, 0, adpcm.Length );
					}
					else
					{
						byte[] original = new byte[ oldLength ];
						fs.Position = 0;
						fs.Read( original, 0, (int)oldLength );

						int oldClipStart = clip.OffsetBytes;
						int oldClipEnd = clip.OffsetBytes + clip.LengthBytes;
						if( oldClipStart < 0 || oldClipStart > original.Length )
							throw new InvalidOperationException( "Clip offset is out of range." );
						if( oldClipEnd < 0 || oldClipEnd > original.Length )
							throw new InvalidOperationException( "Clip end is out of range." );

						long newFileLen = oldLength + delta;
						if( newFileLen <= 0 )
							throw new InvalidOperationException( "Resulting file length is invalid." );

						var output = new byte[ newFileLen ];

						if( oldClipStart > 0 )
							Buffer.BlockCopy( original, 0, output, 0, oldClipStart );

						Buffer.BlockCopy( adpcm, 0, output, oldClipStart, adpcm.Length );

						int tailLen = (int)( oldLength - oldClipEnd );
						int newClipEnd = oldClipStart + adpcm.Length;
						if( tailLen > 0 )
							Buffer.BlockCopy( original, oldClipEnd, output, newClipEnd, tailLen );

						fs.SetLength( 0 );
						fs.Position = 0;
						fs.Write( output, 0, output.Length );

						if( _bank != null && _bank.Clips.Count > 0 && delta != 0 )
							AdpcmFormatResolver.TryApplySpliceToHxd( _bank, _currentClipIndex, delta );
					}

					if( _bank != null )
					{
						if( AdpcmFormatResolver.TryGetHxdBank( _strPath!, out var newBank ) )
							_bank = newBank;

						RefreshClipCombo();
						if( _currentClipIndex >= 0 && _bank.Clips.Count > 0 && _currentClipIndex < _bank.Clips.Count && comboClips != null )
							comboClips.SelectedIndex = _currentClipIndex;
					}
				}

				_tempWavPath = null;
				UpdateInfoLabel( new FileInfo( _strPath! ) );

				EnsureDecodedWavOnDisk();
				OpenIfNeeded();
				SafeSetBars( 0 );
				SetTimeLabel( 0, _durationMs );

				MessageBox.Show( this, "Reimport complete.", "Import" );
			}
			catch( Exception ex )
			{
				MessageBox.Show( this, ex.Message, "Import error" );
			}
		}

		private void checkBoxRepeat_CheckedChanged( object sender, EventArgs e )
		{
			_loop = checkBoxRepeat.Checked;
		}

		private void VolumeBar_ValueChanged( object? sender, EventArgs e )
		{
			try
			{
				SetMciVolumeFromSlider();
			}
			catch
			{
			}
		}

		private void SetMciVolumeFromSlider()
		{
			if( volumeBar == null )
				return;

			int volPercent = Math.Max( 0, Math.Min( 100, volumeBar.Value ) );

			if( !string.IsNullOrEmpty( _mciAlias ) && _mciHasVolume )
			{
				int mciVol = volPercent * 10;
				int rc = mciSendString( $"setaudio {_mciAlias} volume to {mciVol}", null, 0, IntPtr.Zero );
				if( rc == 0 )
					return;
			}

			ushort chan = (ushort)( volPercent * 65535 / 100 );
			uint packed = (uint)( chan | ( chan << 16 ) );
			waveOutSetVolume( IntPtr.Zero, packed );
		}

		private void EnsureDecodedWavOnDisk()
		{
			if( string.IsNullOrEmpty( _strPath ) )
				throw new InvalidOperationException( "No file loaded." );

			string workDir = Path.Combine( Path.GetTempPath(), "ZeroEditor_STR" );
			Directory.CreateDirectory( workDir );

			string baseName = Path.GetFileNameWithoutExtension( _strPath );
			string suffix;
			if( _bank != null && _bank.Clips.Count > 1 && _currentClipIndex >= 0 && _currentClipIndex < _bank.Clips.Count )
				suffix = $".clip{_currentClipIndex:D2}.decoded.wav";
			else
				suffix = ".decoded.wav";

			string target = Path.Combine( workDir, baseName + suffix );

			if( !string.IsNullOrEmpty( _tempWavPath ) && string.Equals( _tempWavPath, target, StringComparison.OrdinalIgnoreCase ) && File.Exists( _tempWavPath ) )
				return;

			_tempWavPath = target;

			var dec = new AdpcmAudio( Interleave, Channels, SampleRate );
			using var wav = DecodeCurrentToWavStream( dec );
			using var fs = File.Create( _tempWavPath );
			wav.CopyTo( fs );
		}

		private MemoryStream DecodeCurrentToWavStream( AdpcmAudio dec )
		{
			if( _bank != null && _bank.Clips.Count > 0 && _currentClipIndex >= 0 && _currentClipIndex < _bank.Clips.Count )
			{
				var clip = _bank.Clips[ _currentClipIndex ];
				int len = clip.LengthBytes;
				if( clip.LoopEnd > 0 && clip.LoopEnd < len )
					len = clip.LoopEnd;
				return dec.DecodeRangeToWavStream( _strPath!, clip.OffsetBytes, len );
			}
			return dec.DecodeToWavStream( _strPath! );
		}

		private void ComputeLoopRegionMs()
		{
			_hasLoopRegion = false;
			_loopStartMs = 0;
			_loopEndMs = _durationMs;

			if( _bank == null || _bank.Clips.Count == 0 )
				return;
			if( _currentClipIndex < 0 || _currentClipIndex >= _bank.Clips.Count )
				return;

			var clip = _bank.Clips[ _currentClipIndex ];
			if( clip.LoopEnd <= clip.LoopStart || clip.LoopEnd <= 0 )
				return;

			int bytesPerFrame = 16;
			int samplesPerFrame = 28;

			int startFrames = clip.LoopStart / bytesPerFrame;
			int endFrames = clip.LoopEnd / bytesPerFrame;
			if( startFrames < 0 || endFrames <= startFrames )
				return;

			long startSamples = (long)startFrames * samplesPerFrame;
			long endSamples = (long)endFrames * samplesPerFrame;
			if( clip.SampleRate <= 0 )
				return;

			int startMs = (int)( startSamples * 1000L / clip.SampleRate );
			int endMs = (int)( endSamples * 1000L / clip.SampleRate );

			if( startMs < 0 )
				startMs = 0;
			if( endMs <= startMs )
				return;

			if( endMs > _durationMs )
				endMs = _durationMs;
			if( endMs <= startMs )
				return;

			_loopStartMs = startMs;
			_loopEndMs = endMs;
			_hasLoopRegion = true;
		}

		private void OpenIfNeeded()
		{
			if( string.IsNullOrEmpty( _tempWavPath ) )
				throw new InvalidOperationException( "No decoded WAV." );

			CloseMci();

			_mciAlias = "str_" + Guid.NewGuid().ToString( "N" );
			mciSendString( $"open \"{_tempWavPath}\" type waveaudio alias {_mciAlias}", null, 0, IntPtr.Zero );
			mciSendString( $"set {_mciAlias} time format milliseconds", null, 0, IntPtr.Zero );

			_mciHasVolume = false;
			try
			{
				var vsb = new StringBuilder( 16 );
				int vrc = mciSendString( $"status {_mciAlias} volume", vsb, vsb.Capacity, IntPtr.Zero );
				_mciHasVolume = vrc == 0;
			}
			catch
			{
				_mciHasVolume = false;
			}

			var sb = new StringBuilder( 32 );
			mciSendString( $"status {_mciAlias} length", sb, sb.Capacity, IntPtr.Zero );
			int.TryParse( sb.ToString(), out _durationMs );
			if( _durationMs <= 0 )
				_durationMs = 1;

			UpdateInfoLabel( _strPath != null ? new FileInfo( _strPath ) : null, _durationMs );

			if( trackAudio != null )
			{
				trackAudio.Maximum = _durationMs;
				SafeSetTrack( trackAudio, 0 );
			}

			SetTimeLabel( 0, _durationMs );
			mciSendString( $"seek {_mciAlias} to start", null, 0, IntPtr.Zero );

			ComputeLoopRegionMs();
		}

		private void Play()
		{
			if( string.IsNullOrEmpty( _mciAlias ) )
				return;

			mciSendString( $"play {_mciAlias}", null, 0, IntPtr.Zero );

			_isPlaying = true;
			btnPlayStop.Text = "Stop";
			_uiTimer.Start();
		}

		private void Stop()
		{
			if( string.IsNullOrEmpty( _mciAlias ) )
				return;

			mciSendString( $"stop {_mciAlias}", null, 0, IntPtr.Zero );
			mciSendString( $"seek {_mciAlias} to start", null, 0, IntPtr.Zero );

			_isPlaying = false;
			btnPlayStop.Text = "Play";
			_uiTimer.Stop();

			SafeSetBars( 0 );
			SetTimeLabel( 0, _durationMs );
		}

		private void StopAndClose()
		{
			try
			{
				if( !string.IsNullOrEmpty( _mciAlias ) )
				{
					mciSendString( $"stop {_mciAlias}", null, 0, IntPtr.Zero );
					mciSendString( $"close {_mciAlias}", null, 0, IntPtr.Zero );
				}
			}
			catch
			{
			}

			_mciAlias = null;
			_isPlaying = false;
			btnPlayStop.Text = "Play";
			_uiTimer.Stop();

			SafeSetBars( 0 );
			SetTimeLabel( 0, _durationMs );
		}

		private void CloseMci()
		{
			if( string.IsNullOrEmpty( _mciAlias ) )
				return;

			mciSendString( $"stop {_mciAlias}", null, 0, IntPtr.Zero );
			mciSendString( $"close {_mciAlias}", null, 0, IntPtr.Zero );

			_mciAlias = null;
			_isPlaying = false;
			btnPlayStop.Text = "Play";
			_uiTimer.Stop();

			SafeSetBars( 0 );
			SetTimeLabel( 0, _durationMs );
		}

		private void Cleanup()
		{
			try
			{
				StopAndClose();
			}
			catch
			{
			}

			try
			{
				if( !string.IsNullOrEmpty( _tempWavPath ) && File.Exists( _tempWavPath ) )
					File.Delete( _tempWavPath );
			}
			catch
			{
			}
		}

		private void UpdateInfoLabel( FileInfo? fi, int durationMs = 0 )
		{
			if( label1 == null )
				return;

			if( fi == null )
			{
				label1.Text = "";
				return;
			}

			string kindText = _format?.Kind == AdpcmKind.Bd ? "BD" : "STR";
			int ch = Channels;
			int rate = SampleRate;
			int iv = Interleave;

			string name = $"Name: {fi.Name}";
			string size = $"Size: {fi.Length:N0} bytes";
			string preset = $"[{kindText}] Ch={ch}, {rate} Hz";
			string fmt = $"Interleave=0x{iv:X}";
			string dur = durationMs > 0 ? $"  •  {durationMs / 1000.0:0.###} s" : "";

			string clipInfo = "";
			if( _bank != null && _bank.Clips.Count > 0 )
			{
				if( _currentClipIndex < 0 || _currentClipIndex >= _bank.Clips.Count )
					_currentClipIndex = 0;
				var c = _bank.Clips[ _currentClipIndex ];
				clipInfo = $"\nClip {c.Index}  Rate={c.SampleRate} Hz  Offset=0x{c.OffsetBytes:X}  Length=0x{c.LengthBytes:X}";
				if( _hasLoopRegion )
					clipInfo += $"\nLoop: {FormatTimeMs( _loopStartMs )} → {FormatTimeMs( _loopEndMs )}";
			}

			label1.Text = $"{name}\n{size}\n{preset}  {fmt}{dur}{clipInfo}";
		}

		private void UiTimer_Tick( object? sender, EventArgs e )
		{
			if( string.IsNullOrEmpty( _mciAlias ) || _durationMs <= 0 )
				return;
			if( _userScrubbing )
				return;

			int pos;
			try
			{
				pos = MciStatusInt( _mciAlias, "position" );
			}
			catch
			{
				return;
			}

			if( pos < 0 )
				pos = 0;
			if( pos > _durationMs )
				pos = _durationMs;

			SafeSetBars( pos );
			SetTimeLabel( pos, _durationMs );

			if( !_isPlaying )
				return;

			if( _loop )
			{
				if( _hasLoopRegion )
				{
					if( pos >= _loopEndMs )
					{
						mciSendString( $"seek {_mciAlias} to {_loopStartMs}", null, 0, IntPtr.Zero );
						mciSendString( $"play {_mciAlias}", null, 0, IntPtr.Zero );
						SafeSetBars( _loopStartMs );
						SetTimeLabel( _loopStartMs, _durationMs );
					}
				}
				else
				{
					if( pos >= _durationMs )
					{
						mciSendString( $"seek {_mciAlias} to start", null, 0, IntPtr.Zero );
						mciSendString( $"play {_mciAlias}", null, 0, IntPtr.Zero );
						SafeSetBars( 0 );
						SetTimeLabel( 0, _durationMs );
					}
				}
			}
			else
			{
				if( pos >= _durationMs )
				{
					_isPlaying = false;
					btnPlayStop.Text = "Play";
					_uiTimer.Stop();
				}
			}
		}

		private void TrackAudio_ValueChangedMirror( object? sender, EventArgs e )
		{
		}

		private void TrackAudio_MouseUpSeek( object? sender, MouseEventArgs e )
		{
			try
			{
				if( string.IsNullOrEmpty( _mciAlias ) || trackAudio == null )
					return;

				int target = Clamp( trackAudio.Value, 0, _durationMs );

				mciSendString( $"seek {_mciAlias} to {target}", null, 0, IntPtr.Zero );
				if( _isPlaying )
					mciSendString( $"play {_mciAlias}", null, 0, IntPtr.Zero );

				SafeSetBars( target );
				SetTimeLabel( target, _durationMs );
			}
			catch
			{
			}
			finally
			{
				_userScrubbing = false;
			}
		}

		private void SafeSetBars( int ms )
		{
			if( trackAudio != null )
				SafeSetTrack( trackAudio, Clamp( ms, trackAudio.Minimum, trackAudio.Maximum ) );
		}

		private static void SafeSetTrack( TrackBar tb, int value )
		{
			try
			{
				if( value < tb.Minimum )
					value = tb.Minimum;
				if( value > tb.Maximum )
					value = tb.Maximum;
				tb.Value = value;
			}
			catch
			{
			}
		}

		private static int Clamp( int v, int lo, int hi )
		{
			if( v < lo )
				return lo;
			if( v > hi )
				return hi;
			return v;
		}

		private static string FormatTimeMs( int ms )
		{
			if( ms < 0 )
				ms = 0;

			int h = ms / 3600000;
			int m = ( ms % 3600000 ) / 60000;
			int s = ( ms % 60000 ) / 1000;

			if( h > 0 )
				return $"{h:00}:{m:00}:{s:00}";

			return $"{m:00}:{s:00}";
		}

		private void SetTimeLabel( int posMs, int durMs )
		{
			if( lblTime == null )
				return;

			if( durMs <= 0 )
			{
				lblTime.Text = "00:00 / 00:00";
				return;
			}

			lblTime.Text = $"{FormatTimeMs( posMs )} / {FormatTimeMs( durMs )}";
		}
	}
}
