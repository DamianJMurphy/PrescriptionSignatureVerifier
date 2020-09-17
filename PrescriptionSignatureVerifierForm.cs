using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PrescriptionSignatureVerifier
{
    public partial class PrescriptionSignatureVerifierForm : Form
    {
        private const string LASTUSEDFILENAME = "PrescriptionSignatureVerifier.conf";

        public PrescriptionSignatureVerifierForm()
        {
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            folderBrowserDialog.Description = "Files for validation";
           
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                sourceTextBox.Text = folderBrowserDialog.SelectedPath;
            }
            sourceInfoTextBox.Text = GetFileCounts(sourceTextBox.Text);

        }

        private String GetFileCounts(string d)
        {
            try
            {
                string[] files = Directory.GetFiles(d);
                StringBuilder sb = new StringBuilder();
                sb.Append("Total files: ");
                sb.Append(files.Length);
                // TODO: Some breakdown here
                //            sb.Append(" (");
                //
                //            sb.Append(")");
                return sb.ToString();
            }
            catch (Exception)
            {
                return "";
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            resultsTextBox.Clear();
            if (sourceTextBox.Text.Trim().Length == 0)
            {
                MessageBox.Show(this, "Source not given", "Bad source", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string[] files;
            try
            {
                files = Directory.GetFiles(sourceTextBox.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Bad source", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            foreach (string s in files)
            {
                SignatureVerifier sv = new SignatureVerifier(s);
                sv.Verify();
                resultsTextBox.AppendText(sv.GetResult());
            }
        }

        private void QuitButton_Click(object sender, EventArgs e)
        {
            WriteLastUsed();
            this.Dispose();
        }

        private void CheckLastUsed()
        {
            FileInfo f = new FileInfo(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\" + LASTUSEDFILENAME);
            if (f.Exists)
            {
                try
                {
                    using (StreamReader sr = new StreamReader(f.Open(FileMode.Open)))
                    {
                        string line;
                        line = sr.ReadLine();
                        if (line.StartsWith("source:"))
                        {
                            sourceTextBox.Text = line.Substring("source:".Length);
                        }
                        sourceInfoTextBox.Text = GetFileCounts(sourceTextBox.Text);
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show(this, e.Message, "Cannot read last locations", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void WriteLastUsed()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("source:");
                sb.Append(sourceTextBox.Text);
                FileInfo f = new FileInfo(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\" + LASTUSEDFILENAME);
                using (StreamWriter sw = new StreamWriter(f.Create()))
                {
                    sw.Write(sb.ToString());
                    sw.Flush();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(this, e.Message, "Cannot write last locations", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void PrescriptionSignatureVerifierForm_Load(object sender, EventArgs e)
        {
            CheckLastUsed();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            saveFileDialog.Title = "Save results file";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filename = saveFileDialog.FileName;
                StringBuilder sb = new StringBuilder("Prescription Signature Verifier");
                sb.Append("Saved at: ");
                sb.Append(DateTime.Now);
                sb.Append("\r\nRun by: ");
                sb.Append(System.Security.Principal.WindowsIdentity.GetCurrent().Name);
                sb.Append(" as: ");
                sb.Append(Environment.UserName);
                sb.Append("\r\nResults:\r\n");
                sb.Append(resultsTextBox.Text);
                try
                {
                    using (StreamWriter sw = new StreamWriter(new FileStream(filename, FileMode.Create)))
                    {
                        sw.WriteLine(sb.ToString());
                        sw.Flush();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Failed to write log", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

        }
    }
}
