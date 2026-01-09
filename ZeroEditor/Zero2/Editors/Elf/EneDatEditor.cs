using System.ComponentModel;
using ZeroEditor.Zero2.DataType;

namespace ZeroEditor.Zero2.Editors.Elf
{
    public partial class EneDatEditor : UserControl
    {
        private readonly List<ENE_DAT> _data;
        private BindingList<EneDatRow>? _rows;
        private bool _updating;

        public EneDatEditor(List<ENE_DAT> data)
        {
            InitializeComponent();
            ThemeManager.ApplyDark(this);

            _data = data;
            SetupGrid();
            LoadAll();
        }

        private void SetupGrid()
        {
            grid.AutoGenerateColumns = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.EditMode = DataGridViewEditMode.EditOnEnter;

            grid.DataError += (_, __) => { };

            grid.CellValueChanged += (_, e) =>
            {
                if (_updating) return;
                if (_rows == null) return;
                if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;

                var row = _rows[e.RowIndex];
                _data[row.Index] = row.Value;
            };

            grid.CurrentCellDirtyStateChanged += (_, __) =>
            {
                if (grid.IsCurrentCellDirty)
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
        }

        private void LoadAll()
        {
            _updating = true;

            var rows = new BindingList<EneDatRow>();
            for (int i = 0; i < _data.Count; i++)
                rows.Add(new EneDatRow(i, _data[i]));

            _rows = rows;
            grid.DataSource = _rows;

            _updating = false;
        }
    }
}
