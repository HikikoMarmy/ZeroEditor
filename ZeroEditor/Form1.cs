using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroEditor.Iso;
using ZeroEditor.Game;
using ZeroEditor.Common;
using ZeroEditor.Editors;
using System.Diagnostics;
using ZeroEditor.Zero1.Editors;
using ZeroEditor.Zero1.Editors.Sgd;
using System.Reflection;

namespace ZeroEditor
{
	public partial class Form1 : Form
	{
		private const string ToolVersion = "1.0.0";
		private const string DummyNodeText = "…";
		private WorkIndicator? _currentWorkIndicator;

		public Form1()
		{
			InitializeComponent();

			version.Text = $"Version {ToolVersion}";

			ThemeManager.ApplyDark( this );
			fileTreeView.BeforeExpand += fileTreeView_BeforeExpand;
			fileTreeView.AfterSelect += fileTreeView_AfterSelect;
		}

		private void Log( string msg )
		{
			if( txtLog != null )
				txtLog.AppendText( msg + Environment.NewLine );
		}

		private void LoadExtractedTree( string rootPath )
		{
			fileTreeView.BeginUpdate();
			try
			{
				fileTreeView.Nodes.Clear();

				SetGameContextForExtract( rootPath );

				var root = new TreeNode( Path.GetFileName( rootPath ) ) { Tag = rootPath };
				root.ImageKey = root.SelectedImageKey = "folder";
				AddDummy( root );
				fileTreeView.Nodes.Add( root );

				root.Expand();
			}
			finally
			{
				fileTreeView.EndUpdate();
			}
		}

		private static void AddDummy( TreeNode node )
		{
			node.Nodes.Add( new TreeNode( DummyNodeText ) );
		}

		private static bool IsDummy( TreeNode node )
		{
			return node.Nodes.Count == 1 && node.Nodes[ 0 ].Text == DummyNodeText;
		}

		private void fileTreeView_BeforeExpand( object? sender, TreeViewCancelEventArgs e )
		{
			var node = e.Node;
			if( node?.Tag is not string dirPath )
				return;
			if( !IsDummy( node ) )
				return;

			fileTreeView.BeginUpdate();
			try
			{
				node.Nodes.Clear();

				foreach( var d in Directory.EnumerateDirectories( dirPath ) )
				{
					var dn = new DirectoryInfo( d ).Name;
					var child = new TreeNode( dn ) { Tag = d };
					child.ImageKey = child.SelectedImageKey = "folder";

					if( Directory.EnumerateFileSystemEntries( d ).Any() )
						AddDummy( child );

					node.Nodes.Add( child );
				}

				foreach( var f in Directory.EnumerateFiles( dirPath ) )
				{
					var fn = Path.GetFileName( f );
					var child = new TreeNode( fn ) { Tag = f };
					child.ImageKey = child.SelectedImageKey = "file";
					node.Nodes.Add( child );
				}
			}
			finally
			{
				fileTreeView.EndUpdate();
			}
		}

		private Control? DetermineEditor( string path )
		{
			string fileName = Path.GetFileName( path );
			string ext = Path.GetExtension( path ).ToLowerInvariant();

			if( AppState.CurrentContext != null && IsElfFile( fileName, ext ) )
			{
				Control editor = AppState.CurrentContext.Key.Game switch
				{
					GameId.FF1 => new Zero1ElfEditor { Dock = DockStyle.Fill },
					//GameId.FF2 => new Zero2.Editors.Zero2ElfEditor(),
					//GameId.FF3 => new Zero3.Editors.Zero3ElfEditor(),
					_ => new HexEditor { Dock = DockStyle.Fill }
				};

				return editor;
			}

			if( ext == ".tm2" || ext == ".cl2" )
				return new Tim2Editor { Dock = DockStyle.Fill };

			if( ext == ".str" || ext == ".bd" )
				return new AudioEditor { Dock = DockStyle.Fill };

			if( ext == ".sgd" )
				return new SgdEditor { Dock = DockStyle.Fill };

			if( ext == ".obj" )
			{
				if( fileName.Contains( "msg", StringComparison.OrdinalIgnoreCase ) )
					return new MessageEditor { Dock = DockStyle.Fill };
				//else if( fileName.Contains( "event", StringComparison.OrdinalIgnoreCase ) )
				//	return new Zero2.Editors.Zero2EventEditor { Dock = DockStyle.Fill };
				//else if( fileName.Contains( "mission", StringComparison.OrdinalIgnoreCase ) )
				//	return new Zero2.Editors.Zero2EventEditor { Dock = DockStyle.Fill };
			}

			if( ext == ".b2d" )
				return new Zero3.Editors.B2D.B2DEditor { Dock = DockStyle.Fill };


			return new HexEditor { Dock = DockStyle.Fill };
		}

		private static bool IsElfFile( string fileName, string ext )
		{
			if( fileName.StartsWith( "SLES_", StringComparison.OrdinalIgnoreCase ) ||
				fileName.StartsWith( "SLUS_", StringComparison.OrdinalIgnoreCase ) ||
				fileName.StartsWith( "SLPS_", StringComparison.OrdinalIgnoreCase ) )
				return true;

			return false;
		}

		private void SetGameContextForExtract( string root )
		{
			var manifestPath = Path.Combine( root, "_manifest.json" );
			GameContext ctx;
			if( File.Exists( manifestPath ) )
				ctx = GameContextFactory.FromManifest( manifestPath );
			else
			{
				var serial = DetectSerialFromExtract( root );
				if( string.IsNullOrEmpty( serial ) )
					throw new InvalidDataException( "Could not detect serial from the extracted folder." );
				ctx = GameContextFactory.FromSerial( serial! );
			}
			AppState.CurrentContext = ctx;
			Log( $"Context: {ctx.Key.Game} / {ctx.Key.Region} / {ctx.Key.Serial}" );
		}

		private void fileTreeView_AfterSelect( object? sender, TreeViewEventArgs e )
		{
			var path = e.Node?.Tag as string;
			if( string.IsNullOrEmpty( path ) || !File.Exists( path ) )
				return;

			try
			{
				panelEditor.SuspendLayout();

				var oldControls = panelEditor.Controls.Cast<Control>().ToList();
				panelEditor.Controls.Clear();
				foreach( var c in oldControls )
				{
					if( c is ZeroEditor.Editors.HexEditor hex )
						hex.PrepareForClose();
					c.Dispose();
				}

				var editor = DetermineEditor( path );
				if( editor != null )
				{
					AddEditorToPanel( editor );

					switch( editor )
					{
						case Zero1ElfEditor z1e:
						z1e.LoadElf( path, AppState.CurrentContext! );
						Log( $"Loaded ELF: {path}" );
						break;
						case SgdEditor sgd:
						sgd.LoadFile( path );
						Log( $"Loaded SGD: {path}" );
						break;

						//case Zero2.Editors.Zero2EventEditor z2e:
						//z2e.LoadEventFile( path );
						//Log( $"Loaded EV: {path}" );
						//break;

						case Tim2Editor tim2:
						tim2.LoadTim2( path, clutSet: 0 );
						Log( $"Previewed TIM2: {path}" );
						break;
						case AudioEditor audio:
						audio.Load( path );
						Log( $"Loaded STR: {path}" );
						break;
						case MessageEditor msg:
						msg.LoadFile( path );
						Log( $"Loaded message pack: {path}" );
						break;
						case Zero3.Editors.B2D.B2DEditor b2d:
						b2d.LoadFile( path );
						Log( $"Loaded B2D: {path}" );
						break;
						case HexEditor hex:
						hex.LoadStr( path );
						Log( $"Opened in hex: {path}" );
						break;
					}
				}

				panelEditor.ResumeLayout();
			}
			catch( Exception ex )
			{
				Log( $"Editor load failed: {ex.Message}" );
			}
		}

		private void openToolStripMenuItem_Click( object sender, EventArgs e )
		{
			using var fbd = new FolderBrowserDialog { Description = "Choose folder" };
			if( fbd.ShowDialog( this ) != DialogResult.OK )
				return;
			LoadExtractedTree( fbd.SelectedPath );
		}

		private void exitToolStripMenuItem_Click( object sender, EventArgs e ) => Close();

		private async void btnExtractISO_Click( object sender, EventArgs e )
		{
			using var ofd = new OpenFileDialog { Filter = "Zero ISO|*.iso|All files|*.*" };
			if( ofd.ShowDialog( this ) != DialogResult.OK )
				return;

			using var fbd = new FolderBrowserDialog { Description = "Choose output folder" };
			if( fbd.ShowDialog( this ) != DialogResult.OK )
				return;

			var progress = new Progress<string>( s =>
			{
				txtLog.AppendText( s + Environment.NewLine );
				
			} );

			var extractor = new IsoExtractor();

			ToggleUi( false );
			ShowWorking( "Extracting ISO..." );

			try
			{
				await Task.Run( async () =>
				{
					await extractor.ExtractAllAsync(
						isoPath: ofd.FileName,
						zeroFileDictionaryPath: "ZeroFileDictionary.json",
						outputRoot: fbd.SelectedPath,
						log: progress );
				} );

				LoadExtractedTree( fbd.SelectedPath );
			}
			catch( Exception ex )
			{
				MessageBox.Show( this, ex.Message, "Extract failed" );
			}
			finally
			{
				HideWorking();
				ToggleUi( true );
			}
		}

		private async void btnRebuildISO_Click( object sender, EventArgs e )
		{
			using var fbd = new FolderBrowserDialog { Description = "Choose Source folder (your extracted root)" };
			if( fbd.ShowDialog( this ) != DialogResult.OK )
				return;

			var root = fbd.SelectedPath;

			var progress = new Progress<string>( s =>
			{
				txtLog.AppendText( s + Environment.NewLine );
			} );

			using var sfd = new SaveFileDialog
			{
				Title = "Save rebuilt ISO",
				Filter = "PlayStation 2 ISO|*.iso|All files|*.*",
				FileName = "FatalFrame_rebuilt.iso"
			};

			if( sfd.ShowDialog( this ) != DialogResult.OK )
				return;

			var outputIsoPath = sfd.FileName;

			ToggleUi( false );
			ShowWorking( "Rebuilding ISO..." );

			try
			{
				var manifestPath = Path.Combine( root, "_manifest.json" );
				GameContext ctx;
				if( File.Exists( manifestPath ) )
					ctx = GameContextFactory.FromManifest( manifestPath );
				else
				{
					var serial = DetectSerialFromExtract( root );
					if( string.IsNullOrEmpty( serial ) )
						throw new InvalidDataException( "Could not detect serial from the extracted folder. Ensure SYSTEM.CNF or the ELF is present." );
					ctx = GameContextFactory.FromSerial( serial! );
				}

				await EnsureBaseIsoAvailableAsync( root, progress, this, CancellationToken.None );

				await Task.Run( async () =>
				{
					switch( ctx.Key.Game )
					{
						case GameId.FF1:
						await new Zero1Rebuilder().RebuildIsoAsync( root, outputIsoPath, progress, default );
						break;
						case GameId.FF2:
						await new Zero2Rebuilder().RebuildIsoAsync( root, outputIsoPath, progress, default );
						break;
						case GameId.FF3:
						await new Zero3Rebuilder().RebuildIsoAsync( root, outputIsoPath, progress, default );
						break;
						default:
						throw new NotSupportedException( $"Rebuild not implemented for game '{ctx.Key.Game}'." );
					}
				} );

				txtLog.AppendText( $"Done. Rebuilt ISO: {outputIsoPath}{Environment.NewLine}" );
				MessageBox.Show( this, "ISO rebuilt successfully.", "Rebuild" );
			}
			catch( Exception ex )
			{
				MessageBox.Show( this, ex.Message, "Rebuild failed" );
			}
			finally
			{
				HideWorking();
				ToggleUi( true );
			}
		}


		private static async Task EnsureBaseIsoAvailableAsync(
			string extractedRoot,
			IProgress<string>? log,
			IWin32Window owner,
			CancellationToken ct )
		{
			var baseIsoPath = Path.Combine( extractedRoot, "_base.iso" );
			if( File.Exists( baseIsoPath ) )
			{
				log?.Report( "Base ISO found: _base.iso" );
				return;
			}

			var msgText =
				"ZeroEditor needs the original/base game ISO in order to build the patched image.\n\n" +
				"Please select the original ISO in the next dialog.\n\n" +
				"Note: The selected ISO file will NOT be modified.";

			MessageBox.Show(
				owner,
				msgText,
				"Select Base ISO",
				MessageBoxButtons.OK,
				MessageBoxIcon.Information );

			using var ofd = new OpenFileDialog
			{
				Title = "Select the original/base ISO to use for patching",
				Filter = "PlayStation 2 ISO (*.iso)|*.iso|All files (*.*)|*.*"
			};

			if( ofd.ShowDialog( owner ) != DialogResult.OK )
				throw new FileNotFoundException( "A base ISO is required to rebuild. Operation cancelled by user." );

			var dest = baseIsoPath;
			log?.Report( $"Copying base ISO -> {dest}" );
			Directory.CreateDirectory( extractedRoot );

			await using var src = new FileStream(
				ofd.FileName,
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read,
				1024 * 1024,
				FileOptions.SequentialScan );

			await using var dst = new FileStream(
				dest,
				FileMode.Create,
				FileAccess.Write,
				FileShare.None,
				1024 * 1024,
				FileOptions.SequentialScan );

			await src.CopyToAsync( dst, 1024 * 1024, ct );
			log?.Report( "Base ISO copied." );
		}


		private static string? DetectSerialFromExtract( string root )
		{
			var sysPath = Path.Combine( root, "SYSTEM.CNF" );
			if( File.Exists( sysPath ) )
			{
				var sys = File.ReadAllText( sysPath, Encoding.ASCII );
				foreach( var line in sys.Split( '\n' ) )
				{
					var s = line.Trim();
					if( !s.StartsWith( "BOOT", StringComparison.OrdinalIgnoreCase ) )
						continue;
					int b = s.IndexOf( '\\' );
					int sc = s.IndexOf( ';', b + 1 );
					if( b >= 0 && sc > b )
					{
						var token = s.Substring( b + 1, sc - b - 1 );
						if( token.StartsWith( "SLES_", StringComparison.OrdinalIgnoreCase ) ||
							token.StartsWith( "SLUS_", StringComparison.OrdinalIgnoreCase ) ||
							token.StartsWith( "SLPS_", StringComparison.OrdinalIgnoreCase ) )
							return token;
					}
				}
			}

			var candidates = Directory.GetFiles( root, "SLES_*.*", SearchOption.TopDirectoryOnly )
							 .Concat( Directory.GetFiles( root, "SLUS_*.*", SearchOption.TopDirectoryOnly ) )
							 .Concat( Directory.GetFiles( root, "SLPS_*.*", SearchOption.TopDirectoryOnly ) );
			foreach( var path in candidates )
			{
				var name = Path.GetFileName( path );
				var serial = name.Split( ';' )[ 0 ];
				if( serial.StartsWith( "SLES_", StringComparison.OrdinalIgnoreCase ) ||
					serial.StartsWith( "SLUS_", StringComparison.OrdinalIgnoreCase ) ||
					serial.StartsWith( "SLPS_", StringComparison.OrdinalIgnoreCase ) )
					return serial;
			}

			return null;
		}

		protected override void OnFormClosing( FormClosingEventArgs e )
		{
			fileTreeView.AfterSelect -= fileTreeView_AfterSelect;

			foreach( Control c in panelEditor.Controls )
				if( c is ZeroEditor.Editors.HexEditor hx )
					hx.PrepareForClose();

			try
			{
				fileTreeView.SuspendLayout();
				fileTreeView.Visible = false;

				var il1 = fileTreeView.ImageList;
				var il2 = fileTreeView.StateImageList;
				fileTreeView.ImageList = null;
				fileTreeView.StateImageList = null;

				fileTreeView.BeginUpdate();
				fileTreeView.Nodes.Clear();
				fileTreeView.EndUpdate();

				il1?.Dispose();
				il2?.Dispose();
			}
			catch { }
			finally
			{
				fileTreeView.ResumeLayout( false );
			}

			base.OnFormClosing( e );
		}

		private void ToggleUi( bool enabled )
		{
			btnExtractISO.Enabled = enabled;
			btnRebuildISO.Enabled = enabled;
			openToolStripMenuItem.Enabled = enabled;
		}

		private void ShowWorking( string message )
		{
			try
			{
				panelEditor.SuspendLayout();

				_currentWorkIndicator = new WorkIndicator();
				AddEditorToPanel( _currentWorkIndicator );
				_currentWorkIndicator.StartAnimating( message );
			}
			catch( Exception ex )
			{
				Log( $"Editor load failed (work indicator): {ex.Message}" );
			}
			finally
			{
				panelEditor.ResumeLayout();
				UseWaitCursor = true;
			}
		}

		private void HideWorking()
		{
			UseWaitCursor = false;

			if( _currentWorkIndicator == null )
				return;

			try
			{
				panelEditor.SuspendLayout();
				_currentWorkIndicator.StopAnimating();
				ClearEditorPanel();
				_currentWorkIndicator = null;
			}
			finally
			{
				panelEditor.ResumeLayout();
			}
		}

		private void AddEditorToPanel( Control editor )
		{
			panelEditor.SuspendLayout();

			ClearEditorPanel();

			panelEditor.Padding = Padding.Empty;

			editor.Margin = Padding.Empty;
			editor.Dock = DockStyle.Fill;
			editor.Location = Point.Empty;
			editor.Size = panelEditor.ClientSize;

			panelEditor.Controls.Add( editor );
			editor.BringToFront();

			panelEditor.ResumeLayout( performLayout: true );
			panelEditor.PerformLayout();
		}

		private void ClearEditorPanel()
		{
			panelEditor.SuspendLayout();
			try
			{
				var toDispose = panelEditor.Controls.Cast<Control>().ToList();
				panelEditor.Controls.Clear();

				foreach( var c in toDispose )
				{
					if( c is ZeroEditor.Editors.HexEditor hx )
						hx.PrepareForClose();
					c.Dispose();
				}
			}
			finally
			{
				panelEditor.ResumeLayout();
			}
		}

		private void GitHubMenuItem_Click( object sender, EventArgs e )
		{
			System.Diagnostics.Process.Start( new ProcessStartInfo
			{
				FileName = "https://github.com/HikikoMarmy/ZeroEditor",
				UseShellExecute = true
			} );
		}
	}
}
