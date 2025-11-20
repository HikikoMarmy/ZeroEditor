using System.Runtime.InteropServices;
using System.Diagnostics;

namespace ZeroEditor.Zero1.DataType
{
	public enum EnemyCategory { Jibaku, Fuyu, Auto }

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	[DebuggerDisplay( "Fog far={fog_far}, near={fog_near}, max={fog_max}, min={fog_min}" )]
	public struct FogParams
	{
		public float fog_far, fog_near, fog_max, fog_min;
	}

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	[DebuggerDisplay( "FogColor r={r} g={g} b={b} a={a}" )]
	public struct FogRgb
	{
		public int r, g, b, a;
	}

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	[DebuggerDisplay( "ENE_DAT mdl={mdl_no} anm={anm_no} hp={hp} pos=({px},{py},{pz})" )]
	public struct ENE_DAT
	{
		public uint attr1;        // 0x00
		public ushort dst_gthr;     // 0x04
		public byte way_gthr;      // 0x06
		public byte atk_ptn;       // 0x07
		public byte wspd;          // 0x08
		public byte rspd;          // 0x09
		public ushort hp;           // 0x0A
		public ushort atk_rng;      // 0x0C
		public ushort hit_rng;      // 0x0E
		public ushort chance_rng;   // 0x10
		public short hit_adjx;     // 0x12
		public ushort atk_p;        // 0x14
		public ushort atk_h;        // 0x16
		public byte atk;           // 0x18
		public byte atk_tm;        // 0x19
		public ushort mdl_no;       // 0x1A
		public ushort anm_no;       // 0x1C
		public byte unk_1E;        // 0x1E
		public byte unk_1F;        // 0x1F
		public uint se_no;         // 0x20
		public uint adpcm_no;      // 0x24
		public int dead_adpcm;    // 0x28
		public ushort point_base;   // 0x2C
		public byte hint_pic;      // 0x2E
		public byte aura_alp;      // 0x2F

		[MarshalAs( UnmanagedType.ByValArray, SizeConst = 6 )]
		public byte[] area;         // 0x30..0x35

		public ushort dir;          // 0x36
		public ushort px;           // 0x38
		public short py;           // 0x3A
		public ushort pz;           // 0x3C
		public byte unk_3E;        // 0x3E
		public byte unk_3F;        // 0x3F
	}

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	[DebuggerDisplay( "AENE dat={dat_no} soul={soul_no} mdl={mdl_no} anm={anm_no}" )]
	public struct AeneInfoDat
	{
		public byte dat_no, soul_no;     // 0x00, 0x01
		public ushort dir, x, y, z;      // 0x02..0x09
		public ushort adpcm_tm;          // 0x0A
		public int adpcm_no;          // 0x0C
		public ushort rng;               // 0x10
		public ushort mdl_no;            // 0x12
		public ushort anm_no;            // 0x14
		public ushort point_base;        // 0x16
		public int se_no;             // 0x18
		public int se_foot;           // 0x1C
	}

	[StructLayout( LayoutKind.Sequential, Pack = 1 )]
	[DebuggerDisplay( "Item {item_no} room={room} mission={mission_no} pos=({x},{y},{z})" )]
	public struct MapItemDat
	{
		public byte item_no, stts, room, mission_no; // 0x00..0x03
		public short x, y, z;                        // 0x04..0x09
		public short get_msg0, get_msg1;            // 0x0A..0x0D
	}
}
