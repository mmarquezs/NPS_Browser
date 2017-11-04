using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Net;
using Microsoft.Win32;

namespace NPS
{
    public partial class Options : Form
    {


        public Options()
        {
            InitializeComponent();
        }

        private void Options_Load(object sender, EventArgs e)
        {
            textDownload.Text = Settings.Instance.downloadDir;
            textPKGPath.Text = Settings.Instance.pkgPath;
            textParams.Text = Settings.Instance.pkgParams;
            textBox1.Text = Settings.Instance.GamesUri;
            textBox2.Text = Settings.Instance.DLCUri;
            checkBox1.Checked = Settings.Instance.deleteAfterUnpack;
            numericUpDown1.Value = Settings.Instance.simultaneousDl;
            textBox3.Text = Settings.Instance.PSMUri;
            textBox4.Text = Settings.Instance.PSXUri;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    textDownload.Text = fbd.SelectedPath;
                    Settings.Instance.downloadDir = textDownload.Text;
                }
            }
        }

        private static bool IsRunningOnMono ()
        {
            return Type.GetType ("Mono.Runtime") != null;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using (var fbd = new OpenFileDialog())
            {
                if (IsRunningOnMono()) {
                        fbd.Filter = "|*";
                    }
                else {
                    fbd.Filter = "|*.exe";
                }

                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.FileName))
                {
                    textPKGPath.Text = fbd.FileName;
                    Settings.Instance.pkgPath = textPKGPath.Text;
                }
            }
        }

        private void Options_FormClosing(object sender, FormClosingEventArgs e)
        {
            Settings.Instance.pkgParams = textParams.Text;
            Settings.Instance.GamesUri = textBox1.Text;
            Settings.Instance.DLCUri = textBox2.Text;
            Settings.Instance.PSMUri = textBox3.Text;
            Settings.Instance.PSXUri = textBox4.Text;
            Settings.Instance.Store();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            using (var fbd = new OpenFileDialog())
            {
                fbd.Filter = "|*.tsv";

                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.FileName))
                {
                    textBox1.Text = fbd.FileName;
                }
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            using (var fbd = new OpenFileDialog())
            {
                fbd.Filter = "|*.tsv";

                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.FileName))
                {
                    textBox2.Text = fbd.FileName;
                }
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Instance.deleteAfterUnpack = checkBox1.Checked;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            Settings.Instance.simultaneousDl = (int)numericUpDown1.Value;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            using (var fbd = new OpenFileDialog())
            {
                fbd.Filter = "|*.tsv";

                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.FileName))
                {
                    textBox3.Text = fbd.FileName;
                }
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            MessageBox.Show(@"Here you can give parameters to pass to your pkg dec tool. Available variables are: 
- {zRifKey}
- {pkgFile}
- {gameTitle}
- {region}
- {titleID}");
        }

        private void button6_Click(object sender, EventArgs e)
        {
            using (var fbd = new OpenFileDialog())
            {
                fbd.Filter = "|*.tsv";

                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.FileName))
                {
                    textBox4.Text = fbd.FileName;
                }
            }
        }
    }
}
