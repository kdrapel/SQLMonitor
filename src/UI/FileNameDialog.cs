using System;
using System.Windows.Forms;

namespace Xnlab.SQLMon.UI
{
    public partial class FileNameDialog : BaseDialog {
        private readonly bool _isSave;

        public FileNameDialog() {
            InitializeComponent();
        }

        public FileNameDialog(string title, bool isSave, string name)
            : this() {
            _isSave = isSave;
            Text = title;
            txtName.Text = name;
        }

        public string FilePath => txtFile.Text;

        public string ObjectName => txtName.Text;

        private void OnGoClick(object sender, EventArgs e) {
            if (!string.IsNullOrEmpty(txtFile.Text)) {
                if (!string.IsNullOrEmpty(txtName.Text))
                    DialogResult = DialogResult.OK;
                else
                    epHint.SetError(txtFile, "Please input name.");
            }
            else {
                epHint.SetError(txtFile, "Please input file.");
            }
        }

        private void OnChooseFileClick(object sender, EventArgs e) {
            FileDialog dlg;
            if (_isSave)
                dlg = new SaveFileDialog();
            else
                dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
                txtFile.Text = dlg.FileName;
        }
    }
}