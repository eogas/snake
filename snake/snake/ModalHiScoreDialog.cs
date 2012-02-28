using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace snake
{
	public partial class ModalHiScoreDialog : Form
	{
		public ModalHiScoreDialog()
		{
			InitializeComponent();
		}

		public string PlayerName
		{
			get { return tbName.Text; }
		}

		private void tbName_TextChanged(object sender, EventArgs e)
		{
			btnOK.Enabled = !tbName.Text.Equals("");
		}

		private void btnOK_Click(object sender, EventArgs e)
		{
			DialogResult = System.Windows.Forms.DialogResult.OK;
		}

		private void btnCancel_Click(object sender, EventArgs e)
		{
			DialogResult = System.Windows.Forms.DialogResult.Cancel;
		}
	}
}
