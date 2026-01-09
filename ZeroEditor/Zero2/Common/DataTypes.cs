using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ZeroEditor.Zero2.DataType
{

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [DebuggerDisplay("Fog far={fog_far}, near={fog_near}, max={fog_max}, min={fog_min}")]
    public struct FogParams
    {
        public float fog_far, fog_near, fog_max, fog_min;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [DebuggerDisplay("FogColor r={r} g={g} b={b} a={a}")]
    public struct FogRgb
    {
        public int r, g, b, a;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [DebuggerDisplay("COMMON mdl={mdl_no} anm={anm_no} pos=({px},{py},{pz}) attr=0x{attr:X8}")]
    public struct ENE_DAT_COMMON
    {
        public int adpcm_no;     // 0x00
        public float px;           // 0x04
        public float py;           // 0x08
        public float pz;           // 0x0C
        public int se_no;        // 0x10

        public ushort mdl_no;      // 0x14
        public ushort anm_no;      // 0x16
        public ushort alg_no;      // 0x18
        public ushort point_base;  // 0x1A
        public ushort dir;         // 0x1C

        public byte neck_ctl;     // 0x1E
        public byte pad_1F;       // 0x1F

        public uint attr;         // 0x20
        public float near;         // 0x24
        public float far;          // 0x28

        public byte blg_r;        // 0x2C
        public byte blg_g;        // 0x2D
        public byte blg_b;        // 0x2E
        public byte balp;         // 0x2F

        public byte ghost_list_no;      // 0x30
        public byte ghost_list_no_sp;   // 0x31

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] def_type;    // 0x32..0x33

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] def_size;    // 0x34..0x35

        public byte dih_type;     // 0x36
        public byte pad_37;       // 0x37
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [DebuggerDisplay("ENE mdl={cmn.mdl_no} anm={cmn.anm_no} hp={hp} pos=({cmn.px},{cmn.py},{cmn.pz})")]
    public struct ENE_DAT
    {
        public ENE_DAT_COMMON cmn; // 0x00..0x37

        public ushort dst_gthr;    // 0x38
        public byte way_gthr;    // 0x3A
        public byte atk_ptn;     // 0x3B

        public float atk_rng;     // 0x3C
        public float hit_rng;     // 0x40
        public float chance_rng;  // 0x44

        public int dead_adpcm;  // 0x48

        public short hit_adjx;    // 0x4C
        public byte hint_pic;    // 0x4E
        public byte aura_alp;    // 0x4F

        public ushort trgt_chg;    // 0x50
        public ushort hp;          // 0x52
        public ushort atk_p;       // 0x54
        public ushort atk_h;       // 0x56

        public byte atk;         // 0x58
        public byte atk_tm;      // 0x59
        public byte wspd;        // 0x5A
        public byte rspd;        // 0x5B
        public byte rotsp;       // 0x5C
        public byte pad_5D;      // 0x5D

        public ushort hitbk;       // 0x5E

        public uint hp_recv_wait;// 0x60
        public float hp_recv_vol; // 0x64

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public short[] fly_type;   // 0x68..0x6D

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public short[] child_ene;  // 0x6E..0x73
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [DebuggerDisplay("AENE dat={dat_no} soul={soul_no} mdl={cmn.mdl_no} anm={cmn.anm_no}")]
    public struct AENE_DAT
    {
        public ENE_DAT_COMMON cmn; // 0x00..0x37

        public byte dat_no;      // 0x38
        public byte soul_no;     // 0x39
        public ushort adpcm_tm;    // 0x3A

        public short next;        // 0x3C
        public ushort chgattr;     // 0x3E

        public float rng;         // 0x40

        public short time;        // 0x44
        public byte pad_46;      // 0x46
        public byte pad_47;      // 0x47

        public int se_foot;     // 0x48
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [DebuggerDisplay("Item {item_no} room={room} mission={mission_no} pos=({x},{y},{z})")]
    public struct MapItemDat
    {
        public byte item_no, stts, room, mission_no; // 0x00..0x03
        public short x, y, z;                        // 0x04..0x09
        public short get_msg0, get_msg1;            // 0x0A..0x0D
    }
}
