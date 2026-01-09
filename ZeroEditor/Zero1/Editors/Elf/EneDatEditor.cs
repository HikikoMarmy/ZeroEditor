using System.ComponentModel;
using ZeroEditor.Zero1.DataType;

namespace ZeroEditor.Zero1.Editors.Elf
{
    public partial class EneDatEditor : UserControl
    {
        private readonly List<List<ENE_DAT>> _data;
        private int _selNight = -1;

        private BindingList<EneDatRow>? _rows;
        private bool _updating;

        public EneDatEditor(List<List<ENE_DAT>> data)
        {
            InitializeComponent();
            ThemeManager.ApplyDark(this);

            _data = data;
            BuildTree();
            SetupGrid();

            if (treeView.Nodes.Count > 0)
                treeView.SelectedNode = treeView.Nodes[0];
        }

        private void BuildTree()
        {
            treeView.BeginUpdate();
            treeView.Nodes.Clear();

            for (int night = 0; night < _data.Count; night++)
                treeView.Nodes.Add(new TreeNode($"Night {night}") { Tag = night });

            treeView.EndUpdate();
        }

        private void SetupGrid()
        {
            grid.AutoGenerateColumns = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.EditMode = DataGridViewEditMode.EditOnEnter;

            grid.DataError += (_, __) => { /* suppress annoying parse popups */ };

            // When a cell edit commits, push the row back into _data
            grid.CellValueChanged += (_, e) =>
            {
                if (_updating) return;
                if (_selNight < 0) return;
                if (_rows == null) return;
                if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;

                var row = _rows[e.RowIndex];
                _data[_selNight][row.Index] = row.Value;
            };

            // Ensure CellValueChanged fires as soon as user changes cell (not only when leaving row)
            grid.CurrentCellDirtyStateChanged += (_, __) =>
            {
                if (grid.IsCurrentCellDirty)
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
        }

        private void LoadNight(int night)
        {
            _selNight = night;

            _updating = true;

            var list = _data[night];
            var rows = new BindingList<EneDatRow>();
            for (int i = 0; i < list.Count; i++)
                rows.Add(new EneDatRow(i, list[i]));

            _rows = rows;
            grid.DataSource = _rows;

            _updating = false;
        }

        private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag is int night && night >= 0 && night < _data.Count)
                LoadNight(night);
        }
    }
}
