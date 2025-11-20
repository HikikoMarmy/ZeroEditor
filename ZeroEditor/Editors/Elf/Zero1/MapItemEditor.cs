using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ZeroEditor.Zero1.DataType;

namespace ZeroEditor.Editors.Elf.Zero1
{
	public partial class MapItemEditor : UserControl
	{
		private readonly IList<MapItemDat> _items;
		private int _index = -1;
		private bool _updating;

		public MapItemEditor( IList<MapItemDat> items )
		{
			_items = items ?? Array.Empty<MapItemDat>();
			InitializeComponent();
			ThemeManager.ApplyDark( this );

			lst.SelectedIndexChanged += ( _, __ ) => SelectIndex( lst.SelectedIndex );

			numItemNo.ValueChanged += ( _, __ ) => { SetCurrent( it => it.item_no = (byte)numItemNo.Value ); UpdateTitle(); };
			numStts.ValueChanged += ( _, __ ) => { SetCurrent( it => it.stts = (byte)numStts.Value ); };
			numRoom.ValueChanged += ( _, __ ) => { SetCurrent( it => it.room = (byte)numRoom.Value ); UpdateTitle(); };
			numMission.ValueChanged += ( _, __ ) => { SetCurrent( it => it.mission_no = (byte)numMission.Value ); UpdateTitle(); };

			numX.ValueChanged += ( _, __ ) => { SetCurrent( it => it.x = (short)numX.Value ); UpdateTitle(); };
			numY.ValueChanged += ( _, __ ) => { SetCurrent( it => it.y = (short)numY.Value ); UpdateTitle(); };
			numZ.ValueChanged += ( _, __ ) => { SetCurrent( it => it.z = (short)numZ.Value ); UpdateTitle(); };

			numMsg0.ValueChanged += ( _, __ ) => { SetCurrent( it => it.get_msg0 = (short)numMsg0.Value ); };
			numMsg1.ValueChanged += ( _, __ ) => { SetCurrent( it => it.get_msg1 = (short)numMsg1.Value ); };

			Bind();
		}

		private void Bind()
		{
			lst.BeginUpdate();
			lst.Items.Clear();
			for( int i = 0; i < _items.Count; i++ )
				lst.Items.Add( RowTitle( i, _items[ i ] ) );
			lst.EndUpdate();
			if( _items.Count > 0 )
				lst.SelectedIndex = 0;
		}

		private void SelectIndex( int idx )
		{
			_index = idx;
			LoadEntry();
		}

		private void LoadEntry()
		{
			if( _index < 0 || _index >= _items.Count )
				return;
			_updating = true;

			var it = _items[ _index ];

			numItemNo.Value = Clamp( numItemNo, it.item_no );
			numStts.Value = Clamp( numStts, it.stts );
			numRoom.Value = Clamp( numRoom, it.room );
			numMission.Value = Clamp( numMission, it.mission_no );

			numX.Value = Clamp( numX, it.x );
			numY.Value = Clamp( numY, it.y );
			numZ.Value = Clamp( numZ, it.z );

			numMsg0.Value = Clamp( numMsg0, it.get_msg0 );
			numMsg1.Value = Clamp( numMsg1, it.get_msg1 );

			_updating = false;
		}

		private void SetCurrent( Action<MapItemDat> mutator )
		{
			if( _updating || _index < 0 || _index >= _items.Count )
				return;
			var it = _items[ _index ];
			mutator( it );
			_items[ _index ] = it;
			UpdateListRow( _index );
		}

		private static decimal Clamp( NumericUpDown n, byte v )
		{
			if( v < (int)n.Minimum )
				return n.Minimum;
			if( v > (int)n.Maximum )
				return n.Maximum;
			return v;
		}

		private static decimal Clamp( NumericUpDown n, short v )
		{
			if( v < (int)n.Minimum )
				return n.Minimum;
			if( v > (int)n.Maximum )
				return n.Maximum;
			return v;
		}

		private string RowTitle( int i, in MapItemDat it )
			=> $"#{i:000}  item={it.item_no} room={it.room}";

		private void UpdateListRow( int i )
		{
			if( i < 0 || i >= _items.Count )
				return;
			int sel = lst.SelectedIndex;
			lst.BeginUpdate();
			lst.Items[ i ] = RowTitle( i, _items[ i ] );
			lst.EndUpdate();
			if( sel != i )
				lst.SelectedIndex = i;
		}

		private void UpdateTitle() => UpdateListRow( _index );
	}
}
