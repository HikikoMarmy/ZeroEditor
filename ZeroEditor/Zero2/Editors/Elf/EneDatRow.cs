using System.ComponentModel;
using ZeroEditor.Zero2.DataType;

namespace ZeroEditor.Zero2.Editors.Elf
{
    public sealed class EneDatRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Index { get; }
        private ENE_DAT _value;

        public EneDatRow(int index, ENE_DAT value)
        {
            Index = index;
            _value = value;
            EnsureArrays();
        }

        [Browsable(false)]
        public ENE_DAT Value => _value;

        private void EnsureArrays()
        {
            _value.cmn.def_type ??= new byte[2];
            if (_value.cmn.def_type.Length < 2) _value.cmn.def_type = new byte[2];

            _value.cmn.def_size ??= new byte[2];
            if (_value.cmn.def_size.Length < 2) _value.cmn.def_size = new byte[2];

            _value.fly_type ??= new short[3];
            if (_value.fly_type.Length < 3) _value.fly_type = new short[3];

            _value.child_ene ??= new short[3];
            if (_value.child_ene.Length < 3) _value.child_ene = new short[3];
        }

        private void Set<T>(T newValue, Action apply, string? name = null)
        {
            apply();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // ---------- COMMON (cmn) ----------
        public int adpcm_no { get => _value.cmn.adpcm_no; set => Set(value, () => _value.cmn.adpcm_no = value, nameof(adpcm_no)); }
        public float px { get => _value.cmn.px; set => Set(value, () => _value.cmn.px = value, nameof(px)); }
        public float py { get => _value.cmn.py; set => Set(value, () => _value.cmn.py = value, nameof(py)); }
        public float pz { get => _value.cmn.pz; set => Set(value, () => _value.cmn.pz = value, nameof(pz)); }

        public int se_no { get => _value.cmn.se_no; set => Set(value, () => _value.cmn.se_no = value, nameof(se_no)); }

        public ushort mdl_no { get => _value.cmn.mdl_no; set => Set(value, () => _value.cmn.mdl_no = value, nameof(mdl_no)); }
        public ushort anm_no { get => _value.cmn.anm_no; set => Set(value, () => _value.cmn.anm_no = value, nameof(anm_no)); }
        public ushort alg_no { get => _value.cmn.alg_no; set => Set(value, () => _value.cmn.alg_no = value, nameof(alg_no)); }
        public ushort point_base { get => _value.cmn.point_base; set => Set(value, () => _value.cmn.point_base = value, nameof(point_base)); }
        public ushort dir { get => _value.cmn.dir; set => Set(value, () => _value.cmn.dir = value, nameof(dir)); }

        public byte neck_ctl { get => _value.cmn.neck_ctl; set => Set(value, () => _value.cmn.neck_ctl = value, nameof(neck_ctl)); }

        public uint attr { get => _value.cmn.attr; set => Set(value, () => _value.cmn.attr = value, nameof(attr)); }
        public float near { get => _value.cmn.near; set => Set(value, () => _value.cmn.near = value, nameof(near)); }
        public float far { get => _value.cmn.far; set => Set(value, () => _value.cmn.far = value, nameof(far)); }

        public byte blg_r { get => _value.cmn.blg_r; set => Set(value, () => _value.cmn.blg_r = value, nameof(blg_r)); }
        public byte blg_g { get => _value.cmn.blg_g; set => Set(value, () => _value.cmn.blg_g = value, nameof(blg_g)); }
        public byte blg_b { get => _value.cmn.blg_b; set => Set(value, () => _value.cmn.blg_b = value, nameof(blg_b)); }
        public byte balp { get => _value.cmn.balp; set => Set(value, () => _value.cmn.balp = value, nameof(balp)); }

        public byte ghost_list_no { get => _value.cmn.ghost_list_no; set => Set(value, () => _value.cmn.ghost_list_no = value, nameof(ghost_list_no)); }
        public byte ghost_list_no_sp { get => _value.cmn.ghost_list_no_sp; set => Set(value, () => _value.cmn.ghost_list_no_sp = value, nameof(ghost_list_no_sp)); }

        public byte def_type0 { get { EnsureArrays(); return _value.cmn.def_type[0]; } set => Set(value, () => { EnsureArrays(); _value.cmn.def_type[0] = value; }, nameof(def_type0)); }
        public byte def_type1 { get { EnsureArrays(); return _value.cmn.def_type[1]; } set => Set(value, () => { EnsureArrays(); _value.cmn.def_type[1] = value; }, nameof(def_type1)); }

        public byte def_size0 { get { EnsureArrays(); return _value.cmn.def_size[0]; } set => Set(value, () => { EnsureArrays(); _value.cmn.def_size[0] = value; }, nameof(def_size0)); }
        public byte def_size1 { get { EnsureArrays(); return _value.cmn.def_size[1]; } set => Set(value, () => { EnsureArrays(); _value.cmn.def_size[1] = value; }, nameof(def_size1)); }

        public byte dih_type { get => _value.cmn.dih_type; set => Set(value, () => _value.cmn.dih_type = value, nameof(dih_type)); }

        // ---------- ENE_DAT ----------
        public ushort dst_gthr { get => _value.dst_gthr; set => Set(value, () => _value.dst_gthr = value, nameof(dst_gthr)); }
        public byte way_gthr { get => _value.way_gthr; set => Set(value, () => _value.way_gthr = value, nameof(way_gthr)); }
        public byte atk_ptn { get => _value.atk_ptn; set => Set(value, () => _value.atk_ptn = value, nameof(atk_ptn)); }

        public float atk_rng { get => _value.atk_rng; set => Set(value, () => _value.atk_rng = value, nameof(atk_rng)); }
        public float hit_rng { get => _value.hit_rng; set => Set(value, () => _value.hit_rng = value, nameof(hit_rng)); }
        public float chance_rng { get => _value.chance_rng; set => Set(value, () => _value.chance_rng = value, nameof(chance_rng)); }

        public int dead_adpcm { get => _value.dead_adpcm; set => Set(value, () => _value.dead_adpcm = value, nameof(dead_adpcm)); }
        public short hit_adjx { get => _value.hit_adjx; set => Set(value, () => _value.hit_adjx = value, nameof(hit_adjx)); }

        public byte hint_pic { get => _value.hint_pic; set => Set(value, () => _value.hint_pic = value, nameof(hint_pic)); }
        public byte aura_alp { get => _value.aura_alp; set => Set(value, () => _value.aura_alp = value, nameof(aura_alp)); }

        public ushort trgt_chg { get => _value.trgt_chg; set => Set(value, () => _value.trgt_chg = value, nameof(trgt_chg)); }
        public ushort hp { get => _value.hp; set => Set(value, () => _value.hp = value, nameof(hp)); }
        public ushort atk_p { get => _value.atk_p; set => Set(value, () => _value.atk_p = value, nameof(atk_p)); }
        public ushort atk_h { get => _value.atk_h; set => Set(value, () => _value.atk_h = value, nameof(atk_h)); }

        public byte atk { get => _value.atk; set => Set(value, () => _value.atk = value, nameof(atk)); }
        public byte atk_tm { get => _value.atk_tm; set => Set(value, () => _value.atk_tm = value, nameof(atk_tm)); }
        public byte wspd { get => _value.wspd; set => Set(value, () => _value.wspd = value, nameof(wspd)); }
        public byte rspd { get => _value.rspd; set => Set(value, () => _value.rspd = value, nameof(rspd)); }
        public byte rotsp { get => _value.rotsp; set => Set(value, () => _value.rotsp = value, nameof(rotsp)); }

        public ushort hitbk { get => _value.hitbk; set => Set(value, () => _value.hitbk = value, nameof(hitbk)); }
        public uint hp_recv_wait { get => _value.hp_recv_wait; set => Set(value, () => _value.hp_recv_wait = value, nameof(hp_recv_wait)); }
        public float hp_recv_vol { get => _value.hp_recv_vol; set => Set(value, () => _value.hp_recv_vol = value, nameof(hp_recv_vol)); }

        public short fly_type0 { get { EnsureArrays(); return _value.fly_type[0]; } set => Set(value, () => { EnsureArrays(); _value.fly_type[0] = value; }, nameof(fly_type0)); }
        public short fly_type1 { get { EnsureArrays(); return _value.fly_type[1]; } set => Set(value, () => { EnsureArrays(); _value.fly_type[1] = value; }, nameof(fly_type1)); }
        public short fly_type2 { get { EnsureArrays(); return _value.fly_type[2]; } set => Set(value, () => { EnsureArrays(); _value.fly_type[2] = value; }, nameof(fly_type2)); }

        public short child_ene0 { get { EnsureArrays(); return _value.child_ene[0]; } set => Set(value, () => { EnsureArrays(); _value.child_ene[0] = value; }, nameof(child_ene0)); }
        public short child_ene1 { get { EnsureArrays(); return _value.child_ene[1]; } set => Set(value, () => { EnsureArrays(); _value.child_ene[1] = value; }, nameof(child_ene1)); }
        public short child_ene2 { get { EnsureArrays(); return _value.child_ene[2]; } set => Set(value, () => { EnsureArrays(); _value.child_ene[2] = value; }, nameof(child_ene2)); }
    }
}
