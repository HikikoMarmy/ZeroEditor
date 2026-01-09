using System.ComponentModel;
using ZeroEditor.Zero1.DataType;

namespace ZeroEditor.Zero1.Editors.Elf
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
            EnsureArea();
        }

        [Browsable(false)]
        public ENE_DAT Value => _value;

        private void EnsureArea()
        {
            if (_value.area == null || _value.area.Length < 6)
                _value.area = new byte[6];
        }

        private void Set<T>(T newValue, Action apply, string? name = null)
        {
            apply();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // ---- Flat fields ----
        public uint attr1 { get => _value.attr1; set => Set(value, () => _value.attr1 = value, nameof(attr1)); }
        public ushort dst_gthr { get => _value.dst_gthr; set => Set(value, () => _value.dst_gthr = value, nameof(dst_gthr)); }
        public byte way_gthr { get => _value.way_gthr; set => Set(value, () => _value.way_gthr = value, nameof(way_gthr)); }
        public byte atk_ptn { get => _value.atk_ptn; set => Set(value, () => _value.atk_ptn = value, nameof(atk_ptn)); }
        public byte wspd { get => _value.wspd; set => Set(value, () => _value.wspd = value, nameof(wspd)); }
        public byte rspd { get => _value.rspd; set => Set(value, () => _value.rspd = value, nameof(rspd)); }
        public ushort hp { get => _value.hp; set => Set(value, () => _value.hp = value, nameof(hp)); }
        public ushort atk_rng { get => _value.atk_rng; set => Set(value, () => _value.atk_rng = value, nameof(atk_rng)); }
        public ushort hit_rng { get => _value.hit_rng; set => Set(value, () => _value.hit_rng = value, nameof(hit_rng)); }
        public ushort chance_rng { get => _value.chance_rng; set => Set(value, () => _value.chance_rng = value, nameof(chance_rng)); }
        public short hit_adjx { get => _value.hit_adjx; set => Set(value, () => _value.hit_adjx = value, nameof(hit_adjx)); }
        public ushort atk_p { get => _value.atk_p; set => Set(value, () => _value.atk_p = value, nameof(atk_p)); }
        public ushort atk_h { get => _value.atk_h; set => Set(value, () => _value.atk_h = value, nameof(atk_h)); }
        public byte atk { get => _value.atk; set => Set(value, () => _value.atk = value, nameof(atk)); }
        public byte atk_tm { get => _value.atk_tm; set => Set(value, () => _value.atk_tm = value, nameof(atk_tm)); }
        public ushort mdl_no { get => _value.mdl_no; set => Set(value, () => _value.mdl_no = value, nameof(mdl_no)); }
        public ushort anm_no { get => _value.anm_no; set => Set(value, () => _value.anm_no = value, nameof(anm_no)); }
        public byte unk_1E { get => _value.unk_1E; set => Set(value, () => _value.unk_1E = value, nameof(unk_1E)); }
        public byte unk_1F { get => _value.unk_1F; set => Set(value, () => _value.unk_1F = value, nameof(unk_1F)); }
        public uint se_no { get => _value.se_no; set => Set(value, () => _value.se_no = value, nameof(se_no)); }
        public uint adpcm_no { get => _value.adpcm_no; set => Set(value, () => _value.adpcm_no = value, nameof(adpcm_no)); }
        public int dead_adpcm { get => _value.dead_adpcm; set => Set(value, () => _value.dead_adpcm = value, nameof(dead_adpcm)); }
        public ushort point_base { get => _value.point_base; set => Set(value, () => _value.point_base = value, nameof(point_base)); }
        public byte hint_pic { get => _value.hint_pic; set => Set(value, () => _value.hint_pic = value, nameof(hint_pic)); }
        public byte aura_alp { get => _value.aura_alp; set => Set(value, () => _value.aura_alp = value, nameof(aura_alp)); }

        // ---- Area[6] ----
        public byte area0 { get { EnsureArea(); return _value.area[0]; } set => Set(value, () => { EnsureArea(); _value.area[0] = value; }, nameof(area0)); }
        public byte area1 { get { EnsureArea(); return _value.area[1]; } set => Set(value, () => { EnsureArea(); _value.area[1] = value; }, nameof(area1)); }
        public byte area2 { get { EnsureArea(); return _value.area[2]; } set => Set(value, () => { EnsureArea(); _value.area[2] = value; }, nameof(area2)); }
        public byte area3 { get { EnsureArea(); return _value.area[3]; } set => Set(value, () => { EnsureArea(); _value.area[3] = value; }, nameof(area3)); }
        public byte area4 { get { EnsureArea(); return _value.area[4]; } set => Set(value, () => { EnsureArea(); _value.area[4] = value; }, nameof(area4)); }
        public byte area5 { get { EnsureArea(); return _value.area[5]; } set => Set(value, () => { EnsureArea(); _value.area[5] = value; }, nameof(area5)); }

        // ---- Position ----
        public ushort dir { get => _value.dir; set => Set(value, () => _value.dir = value, nameof(dir)); }
        public ushort px { get => _value.px; set => Set(value, () => _value.px = value, nameof(px)); }
        public short py { get => _value.py; set => Set(value, () => _value.py = value, nameof(py)); }
        public ushort pz { get => _value.pz; set => Set(value, () => _value.pz = value, nameof(pz)); }

        public byte unk_3E { get => _value.unk_3E; set => Set(value, () => _value.unk_3E = value, nameof(unk_3E)); }
        public byte unk_3F { get => _value.unk_3F; set => Set(value, () => _value.unk_3F = value, nameof(unk_3F)); }
    }
}
