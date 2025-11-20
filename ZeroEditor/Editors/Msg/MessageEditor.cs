using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ZeroEditor.Game;

namespace ZeroEditor.Editors
{
	public partial class MessageEditor : UserControl
	{
		private byte[] _blob;
		private IMsgProfile _profile;
		private IMsgCodec _codec;
		private IMsgDocument _doc;
		private MsgPage _currentPage;
		private bool _suppressTextEvents;

		private static readonly Regex _fdTagRx = new Regex( @"<FD(?<rgb>[0-9A-Fa-f]{6})>", RegexOptions.Compiled );
		private static readonly Color DefaultFontColor = Color.FromArgb( 0x80, 0x80, 0x80 );

		private string _currentPath;
		public bool IsDirty { get; private set; }

		public MessageEditor()
		{
			InitializeComponent();
			ThemeManager.ApplyDark( this );
			richEdit.ForeColor = DefaultFontColor;
		}

		public void LoadBlob( byte[] blob )
		{
			_blob = blob ?? throw new ArgumentNullException( nameof( blob ) );
			_profile = MsgProfiles.ForGame( AppState.CurrentGameId );
			_codec = _profile.CreateCodec();
			_doc = _profile.Parse( _blob );
			PopulateTree();
			richEdit.Clear();
			_currentPage = null;
			IsDirty = false;
		}

		public void LoadFile( string path )
		{
			_currentPath = path ?? throw new ArgumentNullException( nameof( path ) );
			LoadBlob( File.ReadAllBytes( path ) );
			IsDirty = false;
		}

		public byte[] SaveBlob()
		{
			return _profile.Save( _doc );
		}

		private bool SaveToCurrentPath()
		{
			if( string.IsNullOrEmpty( _currentPath ) )
				return false;
			File.WriteAllBytes( _currentPath, SaveBlob() );
			IsDirty = false;
			return true;
		}

		private void PopulateTree()
		{
			treeView.BeginUpdate();
			treeView.Nodes.Clear();

			foreach( var g in _doc.Groups )
			{
				var gNode = new TreeNode( $"Group {g.GroupIndex:D2}" ) { Tag = g };
				foreach( var m in g.Messages )
				{
					var mNode = new TreeNode( $"Message {m.MessageIndex:D3} ({m.Pages.Count}p)" ) { Tag = m };
					for( int pi = 0; pi < m.Pages.Count; pi++ )
					{
						var p = m.Pages[ pi ];
						var pNode = new TreeNode( $"Page {p.PageIndex}" ) { Tag = p };
						mNode.Nodes.Add( pNode );
					}
					gNode.Nodes.Add( mNode );
				}
				treeView.Nodes.Add( gNode );
			}

			treeView.EndUpdate();
		}

		private void treeView_AfterSelect( object sender, TreeViewEventArgs e )
		{
			_suppressTextEvents = true;
			try
			{
				if( e.Node?.Tag is MsgPage page )
				{
					_currentPage = page;
					richEdit.ReadOnly = false;
					richEdit.Text = page.Text ?? string.Empty;
					richEdit.SelectionStart = 0;
					richEdit.SelectionLength = 0;
					ApplySyntaxHighlighting( richEdit );
				}
				else if( e.Node?.Tag is MsgMessage msg )
				{
					_currentPage = null;
					richEdit.ReadOnly = true;
					var sb = new StringBuilder();
					for( int i = 0; i < int.Min( 32, msg.Pages.Count ); i++ )
					{
						if( i > 0 )
							sb.AppendLine().AppendLine( "----- PAGE BREAK -----" ).AppendLine();
						sb.Append( msg.Pages[ i ].Text );
					}
					richEdit.Text = sb.ToString();
					richEdit.SelectionStart = 0;
					richEdit.SelectionLength = 0;
					ApplySyntaxHighlighting( richEdit );
				}
				else
				{
					_currentPage = null;
					richEdit.ReadOnly = true;
					richEdit.Clear();
				}
			}
			finally { _suppressTextEvents = false; }
		}

		private void richEdit_TextChanged( object sender, EventArgs e )
		{
			if( _suppressTextEvents )
				return;
			if( _currentPage == null )
				return;

			_currentPage.Text = richEdit.Text;
			var body = _codec.EncodePage( _currentPage.Text ?? string.Empty );
			var prel = _currentPage.PreludeRaw ?? Array.Empty<byte>();
			var combined = new byte[ prel.Length + body.Length ];
			Buffer.BlockCopy( prel, 0, combined, 0, prel.Length );
			Buffer.BlockCopy( body, 0, combined, prel.Length, body.Length );
			_currentPage.Raw = combined;

			ApplySyntaxHighlighting( richEdit );
			IsDirty = true;
		}

		private void ApplySyntaxHighlighting( RichTextBox rt )
		{
			int caret = rt.SelectionStart;
			int selLen = rt.SelectionLength;

			_suppressTextEvents = true;
			try
			{
				rt.SelectAll();
				rt.SelectionColor = DefaultFontColor;
				rt.SelectionBackColor = rt.BackColor;

				var highlighter = _profile?.CreateHighlighter();
				string text = rt.Text;

				var spans = highlighter?.GetSpans( text );
				if( spans != null )
				{
					foreach( var s in spans )
					{
						if( s.Length <= 0 )
							continue;
						rt.SelectionStart = s.Start;
						rt.SelectionLength = Math.Min( s.Length, Math.Max( 0, text.Length - s.Start ) );
						rt.SelectionColor = s.ForeColor;
						if( s.BackColor.HasValue )
							rt.SelectionBackColor = s.BackColor.Value;
					}
				}

				var rules = highlighter?.GetRules();
				if( rules != null )
				{
					foreach( var rule in rules )
					{
						var matches = rule.Pattern.Matches( text );
						foreach( Match m in matches )
						{
							if( !m.Success || m.Length == 0 )
								continue;

							rt.SelectionStart = m.Index;
							rt.SelectionLength = m.Length;

							if( rule.ForeColor.HasValue )
								rt.SelectionColor = rule.ForeColor.Value;
							if( rule.BackColor.HasValue )
								rt.SelectionBackColor = rule.BackColor.Value;

							if( rule.Bold || rule.Italic )
							{
								var font = rt.SelectionFont ?? rt.Font;
								var style = FontStyle.Regular;
								if( rule.Bold )
									style |= FontStyle.Bold;
								if( rule.Italic )
									style |= FontStyle.Italic;
								rt.SelectionFont = new Font( font, style );
							}
						}
					}
				}

				rt.SelectionStart = caret;
				rt.SelectionLength = selLen;
			}
			finally { _suppressTextEvents = false; }
		}

		protected override void OnHandleDestroyed( EventArgs e )
		{
			try
			{
				if( !this.RecreatingHandle && IsDirty )
				{
					IWin32Window owner = (IWin32Window)FindForm() ?? (IWin32Window)this;
					var res = MessageBox.Show( owner, "You have unsaved changes. Save to the current file?", "Message Editor", MessageBoxButtons.YesNo, MessageBoxIcon.Warning );
					if( res == DialogResult.Yes )
					{
						try
						{
							if( !SaveToCurrentPath() )
							{
								MessageBox.Show( owner, "No current file path to overwrite.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information );
							}
						}
						catch( Exception ex )
						{
							MessageBox.Show( owner, "Save failed:\n" + ex.Message, "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
						}
					}
				}
			}
			finally { base.OnHandleDestroyed( e ); }
		}

		private void btnSaveMsg_Click( object sender, EventArgs e )
		{
			try
			{
				if( !SaveToCurrentPath() )
				{
					IWin32Window owner = (IWin32Window)FindForm() ?? (IWin32Window)this;
					MessageBox.Show( owner, "No current file path to overwrite.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information );
				}
			}
			catch( Exception ex )
			{
				IWin32Window owner = (IWin32Window)FindForm() ?? (IWin32Window)this;
				MessageBox.Show( owner, "Save failed:\n" + ex.Message, "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
			}
		}
	}
}
