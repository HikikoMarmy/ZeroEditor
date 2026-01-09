using System.Text;
using ZeroEditor.Game;
using ZeroEditor.Zero2.Common;
using ZeroEditor.Zero2.DataType;
using ZeroEditor.Zero2.Editors.Elf;
using static BinUtil;

namespace ZeroEditor.Zero2.Editors
{
    public partial class Zero2ElfEditor : UserControl
    {
        private FileStream? _elfStream;
        private BinaryReader? _br;
        private BinaryWriter? _bw;

        private Segment _jEneSeg;
        private List<ENE_DAT> _jEneDat = new();

        public Zero2ElfEditor()
        {
            InitializeComponent();
            ThemeManager.ApplyDark(this);
            Disposed += (_, __) => DisposeElfIo();
        }

        public void LoadElf(string path, GameContext ctx)
        {
            DisposeElfIo();

            _elfStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            _br = new BinaryReader(_elfStream, Encoding.Default, leaveOpen: true);
            _bw = new BinaryWriter(_elfStream, Encoding.Default, leaveOpen: true);

            if (ctx is not IZero2Layout z2)
                throw new NotSupportedException($"Context {ctx.Key} has no IZero2Layout.");

            _jEneSeg = z2.JEneDat;
            _jEneDat = _jEneSeg.Count > 0
                ? BinUtil.ReadArrayAt<ENE_DAT>(_br!, _jEneSeg)
                : new List<ENE_DAT>();

            if (_jEneDat.Count != 0 && _jEneDat.Count != _jEneSeg.Count)
                System.Diagnostics.Debug.WriteLine($"Warning: JEneDat count={_jEneDat.Count}, expected {_jEneSeg.Count}.");

            listBoxData.Items.Clear();
            listBoxData.Items.Add("Enemy Data");
            listBoxData.SelectedIndex = 0;
        }

        private void DisposeElfIo()
        {
            try { _bw?.Flush(); } catch { }
            _bw?.Dispose(); _bw = null;
            _br?.Dispose(); _br = null;
            _elfStream?.Dispose(); _elfStream = null;
        }

        private void listBoxData_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxData.SelectedIndex == -1)
                return;

            switch (listBoxData.SelectedItem?.ToString())
            {
                case "Enemy Data":
                    ShowEditor(new EneDatEditor(_jEneDat));
                    break;
            }
        }

        private void ShowEditor(Control editor)
        {
            editor.Dock = DockStyle.Fill;
            splitContainer1.Panel2.Controls.Clear();
            splitContainer1.Panel2.Controls.Add(editor);
        }

        private void btnSaveChanges_Click(object sender, EventArgs e)
        {
            try
            {
                SaveElf();
                MessageBox.Show(this, "Saved changes to ELF.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveElf()
        {
            if (_bw == null || _elfStream == null)
                throw new InvalidOperationException("ELF not loaded.");

            if (_jEneSeg.Count > 0)
                AssertCountAndWrite(_bw, _jEneSeg, _jEneDat);

            _bw.Flush();
            _elfStream.Flush(true);
        }

        private static void AssertCountAndWrite<T>(BinaryWriter bw, Segment seg, List<T> data) where T : struct
        {
            if (data.Count != seg.Count)
                throw new InvalidOperationException(
                    $"Count mismatch writing {typeof(T).Name}: data.Count={data.Count}, segment.Count={seg.Count}");

            BinUtil.WriteArrayAt(bw, seg, data);
        }
    }
}
