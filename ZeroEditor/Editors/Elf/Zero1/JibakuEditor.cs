using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ZeroEditor.Zero1.DataType;

namespace ZeroEditor.Zero1.Editors.Elf
{
	public partial class JibakuEditor : UserControl
	{
		private readonly List<List<ENE_DAT>> _jibaku;
		private int _selNight = -1;
		private int _selEnemy = -1;
		private bool _updating;

		public JibakuEditor( List<List<ENE_DAT>> jibakuEnemyData )
		{
			InitializeComponent();
			ThemeManager.ApplyDark( this );

			splitContainer1.BackColor = SystemColors.Control;
			splitContainer1.Panel1.BackColor = SystemColors.Control;
			splitContainer1.Panel2.BackColor = SystemColors.Control;

			// Since the designer disallows it x_x
			numeric_hit_adjx.Minimum = short.MinValue;
			numeric_hit_adjx.Maximum = short.MaxValue;
			numeric_py.Minimum = short.MinValue;
			numeric_py.Maximum = short.MaxValue;

			_jibaku = jibakuEnemyData;
			BuildTree();
			HookNumericHandlers();
			if( treeView.Nodes.Count > 0 )
				treeView.SelectedNode = treeView.Nodes[ 0 ].Nodes.Count > 0 ? treeView.Nodes[ 0 ].Nodes[ 0 ] : treeView.Nodes[ 0 ];
		}

		private void BuildTree()
		{
			treeView.BeginUpdate();
			treeView.Nodes.Clear();
			for( int night = 0; night < _jibaku.Count; night++ )
			{
				var nightNode = new TreeNode( $"Night {night}" ) { Tag = new NodeTag( night, -1 ) };
				var list = _jibaku[ night ];
				for( int i = 0; i < list.Count; i++ )
					nightNode.Nodes.Add( new TreeNode( $"Enemy {i}" ) { Tag = new NodeTag( night, i ) } );
				treeView.Nodes.Add( nightNode );
				nightNode.Expand();
			}
			treeView.EndUpdate();
		}

		private void HookNumericHandlers()
		{
			void hook( NumericUpDown n ) => n.ValueChanged += Numeric_ValueChanged;

			hook( numeric_attr1 );
			hook( numeric_dst_gthr );
			hook( numeric_way_gthr );
			hook( numeric_atk_ptn );
			hook( numeric_wspd );
			hook( numeric_rspd );
			hook( numeric_hp );
			hook( numeric_atk_rng );
			hook( numeric_hit_rng );
			hook( numeric_chance_rng );
			hook( numeric_hit_adjx );
			hook( numeric_atk_p );
			hook( numeric_atk_h );
			hook( numeric_atk );
			hook( numeric_atk_tm );
			hook( numeric_mdl_no );
			hook( numeric_anm_no );
			hook( numeric_se_no );
			hook( numeric_adpcm_no );
			hook( numeric_point_base );
			hook( numeric_hint_pic );
			hook( numeric_aura_alp );
			hook( numeric_area0 );
			hook( numeric_area1 );
			hook( numeric_area2 );
			hook( numeric_area3 );
			hook( numeric_area4 );
			hook( numeric_area5 );
			hook( numeric_dir );
			hook( numeric_px );
			hook( numeric_py );
			hook( numeric_pz );
		}

		private void LoadToUi( ENE_DAT e )
		{
			_updating = true;

			numeric_attr1.Value = e.attr1;
			numeric_dst_gthr.Value = e.dst_gthr;
			numeric_way_gthr.Value = e.way_gthr;
			numeric_atk_ptn.Value = e.atk_ptn;
			numeric_wspd.Value = e.wspd;
			numeric_rspd.Value = e.rspd;
			numeric_hp.Value = e.hp;
			numeric_atk_rng.Value = e.atk_rng;
			numeric_hit_rng.Value = e.hit_rng;
			numeric_chance_rng.Value = e.chance_rng;
			numeric_hit_adjx.Value = e.hit_adjx;
			numeric_atk_p.Value = e.atk_p;
			numeric_atk_h.Value = e.atk_h;
			numeric_atk.Value = e.atk;
			numeric_atk_tm.Value = e.atk_tm;
			numeric_mdl_no.Value = e.mdl_no;
			numeric_anm_no.Value = e.anm_no;
			numeric_se_no.Value = e.se_no;
			numeric_adpcm_no.Value = e.adpcm_no;
			numeric_point_base.Value = e.point_base;
			numeric_hint_pic.Value = e.hint_pic;
			numeric_aura_alp.Value = e.aura_alp;

			if( e.area is { Length: >= 6 } )
			{
				numeric_area0.Value = e.area[ 0 ];
				numeric_area1.Value = e.area[ 1 ];
				numeric_area2.Value = e.area[ 2 ];
				numeric_area3.Value = e.area[ 3 ];
				numeric_area4.Value = e.area[ 4 ];
				numeric_area5.Value = e.area[ 5 ];
			}
			else
			{
				numeric_area0.Value = 0;
				numeric_area1.Value = 0;
				numeric_area2.Value = 0;
				numeric_area3.Value = 0;
				numeric_area4.Value = 0;
				numeric_area5.Value = 0;
			}

			numeric_dir.Value = e.dir;
			numeric_px.Value = e.px;
			numeric_py.Value = e.py;
			numeric_pz.Value = e.pz;

			_updating = false;
		}

		private void SaveFromUiToCurrent()
		{
			if( _selNight < 0 || _selEnemy < 0 )
				return;
			var list = _jibaku[ _selNight ];
			var e = list[ _selEnemy ];

			e.attr1 = (uint)numeric_attr1.Value;
			e.dst_gthr = (ushort)numeric_dst_gthr.Value;
			e.way_gthr = (byte)numeric_way_gthr.Value;
			e.atk_ptn = (byte)numeric_atk_ptn.Value;
			e.wspd = (byte)numeric_wspd.Value;
			e.rspd = (byte)numeric_rspd.Value;
			e.hp = (ushort)numeric_hp.Value;
			e.atk_rng = (ushort)numeric_atk_rng.Value;
			e.hit_rng = (ushort)numeric_hit_rng.Value;
			e.chance_rng = (ushort)numeric_chance_rng.Value;
			e.hit_adjx = (short)numeric_hit_adjx.Value;
			e.atk_p = (ushort)numeric_atk_p.Value;
			e.atk_h = (ushort)numeric_atk_h.Value;
			e.atk = (byte)numeric_atk.Value;
			e.atk_tm = (byte)numeric_atk_tm.Value;
			e.mdl_no = (ushort)numeric_mdl_no.Value;
			e.anm_no = (ushort)numeric_anm_no.Value;
			e.se_no = (uint)numeric_se_no.Value;
			e.adpcm_no = (uint)numeric_adpcm_no.Value;
			e.point_base = (ushort)numeric_point_base.Value;
			e.hint_pic = (byte)numeric_hint_pic.Value;
			e.aura_alp = (byte)numeric_aura_alp.Value;

			if( e.area == null || e.area.Length < 6 )
				e.area = new byte[ 6 ];
			e.area[ 0 ] = (byte)numeric_area0.Value;
			e.area[ 1 ] = (byte)numeric_area1.Value;
			e.area[ 2 ] = (byte)numeric_area2.Value;
			e.area[ 3 ] = (byte)numeric_area3.Value;
			e.area[ 4 ] = (byte)numeric_area4.Value;
			e.area[ 5 ] = (byte)numeric_area5.Value;

			e.dir = (ushort)numeric_dir.Value;
			e.px = (ushort)numeric_px.Value;
			e.py = (short)numeric_py.Value;
			e.pz = (ushort)numeric_pz.Value;

			list[ _selEnemy ] = e;
		}

		private void Numeric_ValueChanged( object? sender, EventArgs e )
		{
			if( _updating )
				return;
			SaveFromUiToCurrent();
		}

		private readonly struct NodeTag
		{
			public readonly int Night;
			public readonly int Enemy;
			public NodeTag( int night, int enemy ) { Night = night; Enemy = enemy; }
		}

		private void treeView_AfterSelect( object sender, TreeViewEventArgs e )
		{
			if( e.Node?.Tag is NodeTag tag )
			{
				if( tag.Enemy < 0 )
				{
					_selNight = tag.Night;
					_selEnemy = -1;
					ClearUi();
					return;
				}
				_selNight = tag.Night;
				_selEnemy = tag.Enemy;
				var list = _jibaku[ _selNight ];
				if( _selEnemy >= 0 && _selEnemy < list.Count )
					LoadToUi( list[ _selEnemy ] );
			}
		}

		private void ClearUi()
		{
			_updating = true;
			foreach( Control c in panel1.Controls )
			{
				if( c is NumericUpDown n )
					n.Value = 0;
			}

			foreach( Control c in groupBox1.Controls )
			{
				if( c is NumericUpDown n )
					n.Value = 0;
			}
			_updating = false;
		}
	}
}
