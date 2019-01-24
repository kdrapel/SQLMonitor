using System.Windows.Forms;

namespace Xnlab.SQLMon.UI
{
    public partial class PerformanceDialog : Form {

        public PerformanceDialog() {
            InitializeComponent();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e) {
            if (Controls.Count > 0) {
                var performance = Controls[0] as Performance;
                performance.RemovePerformanceItem();
            }
        }
    }
}