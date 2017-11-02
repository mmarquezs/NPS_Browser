using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NPS
{
    public class DownloadWorker
    {
        public Item currentDownload;
        //private WebClient webClient;
        private DateTime lastUpdate;
        private long lastBytes;
        public ProgressBar progress = new ProgressBar();
        public ListViewItem lvi;

        //public bool isRunning { get; private set; }
        //public bool isCompleted { get; private set; }
        //public bool isCanceled { get; private set; }

        public WorkerStatus status { get; private set; }
        Timer timer = new Timer();

        Form formCaller;

        public DownloadWorker(Item itm, Form f)
        {
            currentDownload = itm;
            lvi = new ListViewItem(itm.TitleName);
            lvi.SubItems.Add("Waiting");
            lvi.SubItems.Add("");
            lvi.SubItems.Add("");
            lvi.Tag = this;
            //isRunning = false;
            //isCanceled = false;
            //isCompleted = false;

            timer.Interval = 1000;
            timer.Tick += Timer_Tick;
            formCaller = f;
            status = WorkerStatus.Queued;
        }

        public void Start()
        {
            Console.WriteLine("start process " + currentDownload.TitleName);
            timer.Start();
            //isRunning = true;
            status = WorkerStatus.Running;

            Task.Run(() =>
            {
                DownloadFile(currentDownload.pkg, Path.Combine(Settings.Instance.downloadDir, currentDownload.DownloadFileName + ".pkg"));
            });
        }

        public void Cancel()
        {
            timer.Stop();
            if (status == WorkerStatus.Completed) return;

            status = WorkerStatus.Canceled;

            if (smRespStream != null)
                smRespStream.Close();
            if (saveFileStream != null)
                saveFileStream.Close();
            if (unpackProcess != null && !unpackProcess.HasExited)
            {
                unpackProcess.Kill();
            }

            lvi.SubItems[1].Text = "";
            lvi.SubItems[2].Text = "Canceled";
            progress.Value = 0;
            DeletePkg();
        }

        public void Pause()
        {

            if (status == WorkerStatus.Running || status == WorkerStatus.Queued)
            {
                timer.Stop();

                status = WorkerStatus.Paused;

                if (smRespStream != null)
                {
                    smRespStream.Flush();
                    smRespStream.Close();
                }
                if (saveFileStream != null)
                    saveFileStream.Close();
                if (unpackProcess != null && !unpackProcess.HasExited)
                {
                    unpackProcess.Kill();
                }

                lvi.SubItems[1].Text = "Paused";
            }
            //progress.Value = 0;
        }

        public void Resume()
        {
            if (status == WorkerStatus.Paused || status == WorkerStatus.DownloadError)
            {
                lvi.SubItems[1].Text = "Queued";
                status = WorkerStatus.Queued;
            }
        }

        public void DeletePkg()
        {
            if (currentDownload != null)
            {
                for (int i = 0; i < 1; i++)
                {
                    try
                    {
                        if (File.Exists(Path.Combine(Settings.Instance.downloadDir , currentDownload.DownloadFileName + ".pkg")))
                        {
                            System.Threading.Thread.Sleep(400);
                            File.Delete(Path.Combine(Settings.Instance.downloadDir , currentDownload.DownloadFileName + ".pkg"));
                        }
                    }
                    catch { i = 5; }
                }
            }
        }

        Process unpackProcess = null;
        public void Unpack()
        {
            if (this.status == WorkerStatus.Downloaded)
            {

                lvi.SubItems[2].Text = "Unpacking";

                var replacements = new Dictionary<string, string>
                {
                    //["{pkgfile}"] = $"\"{Settings.Instance.downloadDir}\\{currentDownload.DownloadFileName}.pkg\"",
                    ["{pkgfile}"] = Path.Combine(Settings.Instance.downloadDir,currentDownload.DownloadFileName+".pkg"),
                    ["{titleid}"] = currentDownload.TitleId.Substring(0, 9),
                    ["{gametitle}"] = Regex.Replace(currentDownload.IsDLC ? currentDownload.ParentGameTitle : currentDownload.TitleName, "[/:\" *?<>|\\r\\n]+", string.Empty),
                    ["{region}"] = currentDownload.Region,
                    ["{zrifkey}"] = currentDownload.zRif
                };

                ProcessStartInfo a = new ProcessStartInfo();
                a.WorkingDirectory = Path.Combine(Settings.Instance.downloadDir,"\\");
                a.FileName = string.Format("\"{0}\"", Settings.Instance.pkgPath);
                a.WindowStyle = ProcessWindowStyle.Hidden;
                a.CreateNoWindow = true;
                a.Arguments = replacements.Aggregate(Settings.Instance.pkgParams.ToLower(), (str, rep) => str.Replace(rep.Key, rep.Value));

                unpackProcess = new Process();
                unpackProcess.StartInfo = a;

                a.UseShellExecute = false;
                a.RedirectStandardError = true;

                unpackProcess.EnableRaisingEvents = true;
                unpackProcess.Exited += Proc_Exited;
                unpackProcess.ErrorDataReceived += new DataReceivedEventHandler(UnpackProcess_ErrorDataReceived);
                errors = new List<string>();
                unpackProcess.Start();
                unpackProcess.BeginErrorReadLine();
            }
        }

        List<string> errors = new List<string>();

        private void UnpackProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            errors.Add(e.Data);
        }

        private void Proc_Exited(object sender, EventArgs e)
        {
            this.status = WorkerStatus.Completed;

            var proc = (sender as Process);
            if (proc.ExitCode == 0)
            {
                formCaller.Invoke(new Action(() =>
                {
                    lvi.SubItems[1].Text = "";
                    lvi.SubItems[2].Text = "Completed";
                    if (Settings.Instance.deleteAfterUnpack)
                        DeletePkg();
                }));
            }
            else
            {
                formCaller.Invoke(new Action(() =>
                {
                    lvi.SubItems[1].Text = "PKG decrypt err!";
                    lvi.SubItems[2].Text = "";

                    errors.Remove(null);
                    if (errors.Count > 0)
                    {
                        if (errors[0].Contains("pkg_dec - PS Vita PKG decryptor/unpacker")) errors.Remove(errors[0]);
                        if (errors.Count > 0)
                            lvi.SubItems[2].Text = errors[0];
                    }
                }
                ));
            }
        }




        long totalSize = 0;
        long completedSize = 0;
        System.IO.Stream smRespStream;
        System.IO.FileStream saveFileStream;
        void DownloadFile(string sSourceURL, string sDestinationPath)
        {
            try
            {
                long iFileSize = 0;
                int iBufferSize = 1024;
                iBufferSize *= 1000;
                long iExistLen = 0;

                if (System.IO.File.Exists(sDestinationPath))
                {
                    System.IO.FileInfo fINfo =
                       new System.IO.FileInfo(sDestinationPath);
                    iExistLen = fINfo.Length;
                }
                totalSize = iExistLen;
                if (iExistLen > 0)
                    saveFileStream = new System.IO.FileStream(sDestinationPath,
                      System.IO.FileMode.Append, System.IO.FileAccess.Write,
                      System.IO.FileShare.ReadWrite);
                else
                    saveFileStream = new System.IO.FileStream(sDestinationPath,
                      System.IO.FileMode.Create, System.IO.FileAccess.Write,
                      System.IO.FileShare.ReadWrite);

                HttpWebRequest hwRq;
                System.Net.HttpWebResponse hwRes;
                hwRq = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(sSourceURL);
                hwRq.AddRange((int)iExistLen);

                hwRes = (System.Net.HttpWebResponse)hwRq.GetResponse();
                smRespStream = hwRes.GetResponseStream();

                iFileSize = hwRes.ContentLength;
                totalSize += hwRes.ContentLength;

                byte[] downBuffer = new byte[iBufferSize];
                int iByteSize;
                while ((iByteSize = smRespStream.Read(downBuffer, 0, downBuffer.Length)) > 0)
                {
                    if (status == WorkerStatus.Paused) return;

                    saveFileStream.Write(downBuffer, 0, iByteSize);

                    completedSize = saveFileStream.Position;

                    if (lastBytes == 0)
                    {
                        lastUpdate = DateTime.Now;
                        lastBytes = completedSize;

                    }
                    else
                    {
                        var now = DateTime.Now;
                        var timeSpan = now - lastUpdate;
                        var bytesChange = completedSize - lastBytes;
                        if (timeSpan.Seconds != 0)
                        {
                            bytesPerSecond = bytesChange / timeSpan.Seconds;
                            lastBytes = completedSize;
                            lastUpdate = now;


                        }
                    }
                }

                smRespStream.Close();
                saveFileStream.Close();
                formCaller.Invoke(new Action(() => { DownloadCompleted(); }));
            }
            catch (Exception err)
            {
                formCaller.Invoke(new Action(() => { this.Pause(); }));

            }

        }


        long bytesPerSecond = 0;

        //void FetchFileName()
        //{
        //    int count = 1;
        //    string orgTitle = currentDownload.TitleId;

        //    while (File.Exists(Settings.Instance.downloadDir + "\\" + currentDownload.TitleId + ".pkg"))
        //    {
        //        currentDownload.TitleId = orgTitle + "_" + count;
        //        count++;
        //    }
        //}





        private void DownloadCompleted()
        {
            timer.Stop();

            this.status = WorkerStatus.Downloaded;

            lvi.SubItems[1].Text = "";
            Unpack();

            progress.Value = 100;

        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            string speed = "";
            bytesPerSecond = bytesPerSecond / 1024;
            if (bytesPerSecond < 1500)
                speed = bytesPerSecond.ToString() + " KB/s";
            else
            {
                speed = ((float)((float)bytesPerSecond / 1024)).ToString("0.00") + " MB/s";
            }

            lvi.SubItems[1].Text = speed;
            var prgs = (float)completedSize / (float)totalSize;

            try
            {
                if (prgs != float.NaN)
                    progress.Value = Convert.ToInt32(prgs * 100);
            }
            catch { }

            lvi.SubItems[2].Text = completedSize / (1024 * 1024) + "MB/" + totalSize / (1024 * 1024) + "MB";
        }
    }

    public enum WorkerStatus { Queued, Running, Paused, Completed, Downloaded, Canceled, DownloadError }
}


