using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NPS
{
    public partial class NPSBrowser : Form
    {
        public const string version = "0.73";
        List<Item> currentDatabase = new List<Item>();
        List<Item> gamesDbs = new List<Item>();
        List<Item> dlcsDbs = new List<Item>();
        List<Item> psmDbs = new List<Item>();
        List<Item> psxDbs = new List<Item>();
        HashSet<string> regions = new HashSet<string>();
        int currentOrderColumn = 0;
        bool currentOrderInverted = false;

        List<DownloadWorker> downloads = new List<DownloadWorker>();
        Release[] releases = null;

        public NPSBrowser()
        {
            InitializeComponent();
            this.Text += " " + version;
            this.Icon = Properties.Resources._8_512;
            new Settings();

            if (string.IsNullOrEmpty(Settings.Instance.GamesUri) && string.IsNullOrEmpty(Settings.Instance.DLCUri))
            {
                MessageBox.Show("Application did not provide any links to external files or decrypt mechanism.\r\nYou need to specify tsv (tab splitted text) file with your personal links to pkg files on your own.\r\n\r\nFormat: TitleId Region Name Pkg Key", "Disclaimer!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Options o = new Options();
                o.ShowDialog();
            }
            else if (!File.Exists(Settings.Instance.pkgPath))
            {
                MessageBox.Show("You are missing your pkg_dec.exe", "Whops!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Options o = new Options();
                o.ShowDialog();
            }

            NewVersionCheck();
        }

        private void NoPayStationBrowser_Load(object sender, EventArgs e)
        {
            ServicePointManager.DefaultConnectionLimit = 30;

            LoadDatabase(Settings.Instance.DLCUri, (db) =>
            {
                dlcsDbs = db;
                Invoke(new Action(() =>
                {
                    if (dlcsDbs.Count > 0)
                        rbnDLC.Enabled = true;
                    else rbnDLC.Enabled = false;
                }));

                LoadDatabase(Settings.Instance.GamesUri, (db2) =>
                {
                    gamesDbs = db2;

                    Invoke(new Action(() =>
                    {
                        if (gamesDbs.Count > 0)
                            rbnGames.Enabled = true;
                        else rbnGames.Enabled = false;

                        rbnGames.Checked = true;
                        currentDatabase = gamesDbs;

                        cmbRegion.Items.Clear();

                        cmbRegion.Items.Add("ALL");
                        cmbRegion.Text = "ALL";



                        foreach (string s in regions)
                            cmbRegion.Items.Add(s);

                        // Populate DLC Parent Titles
                        foreach (var item in dlcsDbs)
                        {
                            var result = gamesDbs.FirstOrDefault(i => i.TitleId.StartsWith(item.TitleId.Substring(0, 9)))?.TitleName;
                            item.ParentGameTitle = result ?? string.Empty;
                        }
                    }));
                }, DatabaseType.AddDlc);
            }, DatabaseType.ItsDlc);

            LoadDatabase(Settings.Instance.PSMUri, (db) =>
            {
                psmDbs = db;
                Invoke(new Action(() =>
                {
                    if (psmDbs.Count > 0)
                        rbnPSM.Enabled = true;
                    else rbnPSM.Enabled = false;
                }));
            }, DatabaseType.ItsPsn);


            LoadDatabase(Settings.Instance.PSXUri, (db) =>
            {
                psxDbs = db;
                Invoke(new Action(() =>
                {
                    if (psxDbs.Count > 0)
                        rbnPSX.Enabled = true;
                    else rbnPSX.Enabled = false;
                }));
            }, DatabaseType.ItsPSX);
        }

        private void NewVersionCheck()
        {
            Task.Run(() =>
            {
                try
                {
                    WebClient wc = new WebClient();
                    wc.Credentials = CredentialCache.DefaultCredentials;
                    wc.Headers.Add("user-agent", "MyPersonalApp :)");
                    string content = wc.DownloadString("https://api.github.com/repos/jhonhenry10/NPS_Browser/releases");
                    wc.Dispose();

                    //dynamic test = JsonConvert.DeserializeObject<dynamic>(content);
                    releases = SimpleJson.SimpleJson.DeserializeObject<Release[]>(content);

                    string newVer = releases[0].tag_name;
                    if (version != newVer)
                    {
                        Invoke(new Action(() =>
                        {
                            downloadUpdateToolStripMenuItem.Visible = true;
                            this.Text += string.Format("         (!! new version {0} available !!)", newVer);
                        }));
                    }
                }
                catch { }
            });
        }

        private void LoadDatabase(string path, Action<List<Item>> result, DatabaseType dbType)// bool addDlc = false, bool isDLC = false, bool isPsm = false)
        {
            List<Item> dbs = new List<Item>();
            if (string.IsNullOrEmpty(path))
                result.Invoke(dbs);
            else
            {
                Task.Run(() =>
                {
                    path = new Uri(path).ToString();

                    try
                    {
                        WebClient wc = new WebClient();
                        string content = wc.DownloadString(new Uri(path));
                        wc.Dispose();
                        content = Encoding.UTF8.GetString(Encoding.Default.GetBytes(content));

                        string[] lines = content.Split(new string[] { "\r\n", "\n\r", "\n", "\r" }, StringSplitOptions.None);

                        for (int i = 1; i < lines.Length; i++)
                        {
                            var a = lines[i].Split('\t');
                            if (dbType == DatabaseType.ItsPsn) a[5] = null;

                            var itm = new Item();
                            itm.TitleId = a[0];
                            itm.Region = a[1];
                            itm.TitleName = a[2];
                            itm.pkg = a[3];

                            if (dbType == DatabaseType.ItsPSX)
                            {
                                itm.zRif = "";
                                itm.ContentId = a[4];
                                itm.ItsPsx = true;
                                if (a.Length >= 6)
                                {
                                    DateTime.TryParse(a[5], out itm.lastModifyDate);
                                }
                            }
                            else
                            {
                                itm.zRif = a[4];
                                itm.ContentId = a[5];
                                if (a.Length >= 7)
                                {
                                    DateTime.TryParse(a[6], out itm.lastModifyDate);
                                }
                            }

                            itm.IsDLC = dbType == DatabaseType.ItsDlc;
                            if (!itm.zRif.ToLower().Contains("missing") && itm.pkg.ToLower().Contains("http://"))
                            {
                                if (dbType == DatabaseType.AddDlc)
                                    itm.CalculateDlCs(dlcsDbs.ToArray());
                                dbs.Add(itm);
                                regions.Add(itm.Region.Replace(" ", ""));
                            }
                        }

                        //dbs = dbs.OrderBy(i => i.TitleName).ToList();
                    }
                    catch { }
                    result.Invoke(dbs);
                });
            }
        }

        private void RefreshList(List<Item> items)
        {

            List<ListViewItem> list = new List<ListViewItem>();

            foreach (var item in items)
            {
                var a = new ListViewItem(item.TitleId);
                a.SubItems.Add(item.Region);
                a.SubItems.Add(item.TitleName);
                if (item.DLCs > 0)
                    a.SubItems.Add(item.DLCs.ToString());
                else a.SubItems.Add("");
                if (item.lastModifyDate != DateTime.MinValue)
                    a.SubItems.Add(item.lastModifyDate.ToString());
                else a.SubItems.Add("");
                a.Tag = item;
                list.Add(a);
            }

            lstTitles.BeginUpdate();
            lstTitles.Items.Clear();
            lstTitles.Items.AddRange(list.ToArray());

            lstTitles.ListViewItemSorter = new ListViewItemComparer(2, false);
            lstTitles.Sort();

            lstTitles.EndUpdate();

            string type = "";
            if (rbnGames.Checked) type = "Games";
            else if (rbnDLC.Checked) type = "DLCs";
            else if (rbnPSM.Checked) type = "PSM Games";
            else if (rbnPSX.Checked) type = "PSX Games";

            lblCount.Text = $"{list.Count}/{currentDatabase.Count} {type}";
        }

        // Menu
        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Options o = new Options();
            o.ShowDialog();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void downloadUpdateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string url = releases?[0]?.assets?[0]?.browser_download_url;
            if (!string.IsNullOrEmpty(url))
                System.Diagnostics.Process.Start(url);
        }

        // Search
        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            List<Item> itms = new List<Item>();

            foreach (var item in currentDatabase)
            {
                if (item.CompareName(txtSearch.Text) && (cmbRegion.Text == "ALL" || item.Region.Contains(cmbRegion.Text)))
                    itms.Add(item);
            }

            RefreshList(itms);
        }

        private void cmbRegion_SelectedIndexChanged(object sender, EventArgs e)
        {
            txtSearch_TextChanged(null, null);
        }

        // Browse
        private void rbnGames_CheckedChanged(object sender, EventArgs e)
        {
            if (rbnGames.Checked)
            {
                currentDatabase = gamesDbs;
                txtSearch_TextChanged(null, null);
            }
        }

        private void rbnDLC_CheckedChanged(object sender, EventArgs e)
        {
            if (rbnDLC.Checked)
            {
                currentDatabase = dlcsDbs;
                txtSearch_TextChanged(null, null);
            }
        }

        private void rbnPSM_CheckedChanged(object sender, EventArgs e)
        {
            if (rbnPSM.Checked)
            {
                currentDatabase = psmDbs;
                txtSearch_TextChanged(null, null);
            }
        }

        private void rbnPSX_CheckedChanged(object sender, EventArgs e)
        {
            if (rbnPSX.Checked)
            {
                currentDatabase = psxDbs;
                txtSearch_TextChanged(null, null);
            }
        }

        // Download
        private void btnDownload_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(Settings.Instance.downloadDir) || string.IsNullOrEmpty(Settings.Instance.pkgPath))
            {
                MessageBox.Show("You don't have a proper configuration.", "Whoops!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Options o = new Options();
                o.ShowDialog();
                return;
            }

            if (!File.Exists(Settings.Instance.pkgPath))
            {
                MessageBox.Show("You are missing your pkg dec.", "Whoops!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Options o = new Options();
                o.ShowDialog();
                return;
            }

            if (lstTitles.SelectedItems.Count == 0) return;

            foreach (ListViewItem itm in lstTitles.SelectedItems)
            {
                var a = (itm.Tag as Item);

                bool contains = false;
                foreach (var d in downloads)
                    if (d.currentDownload == a)
                    {
                        contains = true;
                        break; //already downloading
                    }

                if (!contains)
                {
                    DownloadWorker dw = new DownloadWorker(a, this);
                    lstDownloadStatus.Items.Add(dw.lvi);
                    lstDownloadStatus.AddEmbeddedControl(dw.progress, 3, lstDownloadStatus.Items.Count - 1);
                    downloads.Add(dw);
                }
            }
        }

        private void lnkOpenRenaScene_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            //var u = new Uri("https://www.youtube.com/results?search_query=dead or alive");
            System.Diagnostics.Process.Start(lnkOpenRenaScene.Tag.ToString());
        }

        // lstTitles
        private void lstTitles_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void lstTitles_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (currentOrderColumn == e.Column)
                currentOrderInverted = !currentOrderInverted;
            else
            {
                currentOrderColumn = e.Column; currentOrderInverted = false;
            }

            this.lstTitles.ListViewItemSorter = new ListViewItemComparer(currentOrderColumn, currentOrderInverted);
            // Call the sort method to manually sort.
            lstTitles.Sort();
        }

        private void lstTitles_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.A && e.Control)
            {
                //listView1.MultiSelect = true;
                foreach (ListViewItem item in lstTitles.Items)
                {
                    item.Selected = true;
                }
            }
        }

        // lstTitles Menu Strip
        private void showTitleDlcToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstTitles.SelectedItems.Count == 0) return;

            Item t = (lstTitles.SelectedItems[0].Tag as Item);
            if (t.DLCs > 0)
            {
                rbnDLC.Checked = true;
                txtSearch.Text = t.TitleId;
            }
        }

        private void downloadAllDlcsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem itm in lstTitles.SelectedItems)
            {

                var parrent = (itm.Tag as Item);

                foreach (var a in parrent.DlcItm)
                {
                    bool contains = false;
                    foreach (var d in downloads)
                        if (d.currentDownload == a)
                        {
                            contains = true;
                            break; //already downloading
                        }

                    if (!contains)
                    {
                        DownloadWorker dw = new DownloadWorker(a, this);
                        lstDownloadStatus.Items.Add(dw.lvi);
                        lstDownloadStatus.AddEmbeddedControl(dw.progress, 3, lstDownloadStatus.Items.Count - 1);
                        downloads.Add(dw);
                    }
                }
            }
        }

        // lstDownloadStatus
        private void lstDownloadStatus_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.A && e.Control)
            {
                //listView1.MultiSelect = true;
                foreach (ListViewItem item in lstDownloadStatus.Items)
                {
                    item.Selected = true;
                }
            }
        }

        private void pauseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstDownloadStatus.SelectedItems.Count == 0) return;
            foreach (ListViewItem a in lstDownloadStatus.SelectedItems)
            {
                DownloadWorker itm = (a.Tag as DownloadWorker);
                itm.Pause();
            }
        }

        private void resumeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstDownloadStatus.SelectedItems.Count == 0) return;
            foreach (ListViewItem a in lstDownloadStatus.SelectedItems)
            {
                DownloadWorker itm = (a.Tag as DownloadWorker);
                itm.Resume();
            }
        }

        // lstDownloadStatus Menu Strip
        private void cancelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstDownloadStatus.SelectedItems.Count == 0) return;
            foreach (ListViewItem a in lstDownloadStatus.SelectedItems)
            {
                DownloadWorker itm = (a.Tag as DownloadWorker);
                itm.Cancel();
                //itm.DeletePkg();
            }
        }



        private void retryUnpackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lstDownloadStatus.SelectedItems.Count == 0) return;
            foreach (ListViewItem a in lstDownloadStatus.SelectedItems)
            {
                DownloadWorker itm = (a.Tag as DownloadWorker);
                itm.Unpack();
            }
        }

        private void clearCompletedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<DownloadWorker> toDel = new List<DownloadWorker>();
            List<ListViewItem> toDelLVI = new List<ListViewItem>();

            foreach (var i in downloads)
            {
                if (i.status == WorkerStatus.Canceled || i.status == WorkerStatus.Completed)
                    toDel.Add(i);
            }

            foreach (ListViewItem i in lstDownloadStatus.Items)
            {
                if (toDel.Contains(i.Tag as DownloadWorker))
                    toDelLVI.Add(i);
            }

            foreach (var i in toDel)
                downloads.Remove(i);
            toDel.Clear();

            foreach (var i in toDelLVI)
                lstDownloadStatus.Items.Remove(i);
            toDelLVI.Clear();
        }

        // Timers
        private void timer1_Tick(object sender, EventArgs e)
        {
            int workingThreads = 0;
            foreach (var dw in downloads)
            {
                if (dw.status == WorkerStatus.Running)
                    workingThreads++;
            }

            if (workingThreads < Settings.Instance.simultaneousDl)
            {
                foreach (var dw in downloads)
                {
                    if (dw.status == WorkerStatus.Queued)
                    {
                        dw.Start();
                        break;
                    }
                }
            }
        }

        CancellationTokenSource tokenSource = new CancellationTokenSource();
        Item previousSelectedItem = null;

        private void timer2_Tick(object sender, EventArgs e)
        {
            // Update view

            if (lstTitles.SelectedItems.Count == 0) return;
            Item itm = (lstTitles.SelectedItems[0].Tag as Item);

            if (itm != previousSelectedItem)
            {
                previousSelectedItem = itm;

                tokenSource.Cancel();
                tokenSource = new CancellationTokenSource();

                Task.Run(() =>
                {
                    Helpers.Renascene r = new Helpers.Renascene(itm);

                    if (r.imgUrl != null)
                    {
                        Invoke(new Action(() =>
                        {
                            ptbCover.LoadAsync(r.imgUrl);
                            label5.Text = r.ToString();
                            lnkOpenRenaScene.Tag = r.url;
                            lnkOpenRenaScene.Visible = true;
                        }));

                    }
                    else
                    {
                        Invoke(new Action(() =>
                        {
                            ptbCover.Image = null;
                            label5.Text = "";
                            lnkOpenRenaScene.Visible = false;
                        }));

                    }
                }, tokenSource.Token);
            }
        }

        private void PauseAllBtnClick(object sender, EventArgs e)
        {
            foreach (ListViewItem itm in lstDownloadStatus.Items)
            {
                (itm.Tag as DownloadWorker).Pause();
            }
        }

        private void ResumeAllBtnClick(object sender, EventArgs e)
        {
            foreach (ListViewItem itm in lstDownloadStatus.Items)
            {
                (itm.Tag as DownloadWorker).Resume();
            }
        }


    }

    class Release
    {
        public string tag_name = "";
        public Asset[] assets = null;
    }

    class Asset
    {
        public string browser_download_url = "";
    }

    enum DatabaseType { AddDlc, ItsDlc, ItsPsn, ItsPSX }
}
