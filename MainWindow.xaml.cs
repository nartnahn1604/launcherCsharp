using System;
using System.Diagnostics;
using System.Windows;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Threading;
using static Launcher.Utils;

namespace Launcher
{
    enum LauncherStatus
    {
        ready,
        failed,
        downloadingUpdate,
        Checking,
        _verifying,
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Configurable varibles
        private string NewsUrl = "https://dh3.do-heart.com/posts/News-";
        private string HashSumUri = "https://launcher.do-heart.com/clientnoeffect/hashsum.json";
        private string DownloadFileUri = "https://launcher.do-heart.com/clientnoeffect/";
        private int maxBanners = 6;
        protected string localBannerDir = "Banner";

        // As global variables
        private JObject news = null;
        private List<Button> newsControls = new List<Button>();
        private List<string> banners = new List<string>();
        private JArray updateFiles = new JArray();
        private bool isUpdating = false;

        //Download file from server
        private string versionFile;
        private string gameExe;
        private JObject json = null;
        private JObject clientFiles;
        protected bool isFailed = false;
        DownloadProgressTracker tracker = new DownloadProgressTracker(50, TimeSpan.FromMilliseconds(500)); //Tracker

        //Hash sum
        FileStream fsHashSum = null;

        //Check FixClient on click
        private bool isClick = false;

        //Launcher Status
        private LauncherStatus _status;

        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case LauncherStatus.ready:
                        UpdateStatus.Text = "Press Play Game to start"; UStatus.Text = "(Don't need to update)"; FixClient.IsEnabled = true;
                        break;
                    case LauncherStatus.failed:
                        if (isClick)
                        {
                            UpdateStatus.Text = "Failed to fix client";
                            UpdateStatus_btn.IsEnabled = false;
                            UpdateStatus_btn.Opacity = 1;
                        }
                        else
                        {
                            UpdateStatus.Text = "Update Failed!"; UStatus.Text = "(Please update new version)"; UpdateStatus_btn.IsEnabled = true;
                        }
                        break;
                    case LauncherStatus.downloadingUpdate:
                        if (isClick)
                        {
                            UpdateStatus.Text = "Fixing"; UpdateStatus_btn.IsEnabled = false; UpdateStatus_btn.Opacity = 1;
                            UStatus.Text = "(Client is being fixed)";
                        }
                        else
                        {
                            UpdateStatus.Text = "Updating"; UStatus.Text = "(Please update new version)";
                        }
                        break;
                    case LauncherStatus.Checking:
                        UpdateStatus.Text = "Checking for update";
                        break;
                    case LauncherStatus._verifying:
                        UpdateStatus.Text = "Verifying ..."; UpdateStatus_btn.IsEnabled = false; UpdateStatus_btn.Opacity = 1;
                        break;
                    default:
                        break;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            versionFile = Path.GetFullPath("(version)");
            gameExe = Path.GetFullPath(@"Bin\Game.exe");
        }
        #region ScaleValue Depdency Property
        public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue", typeof(double), typeof(MainWindow), new UIPropertyMetadata(1.0, new PropertyChangedCallback(OnScaleValueChanged), new CoerceValueCallback(OnCoerceScaleValue)));

        private static object OnCoerceScaleValue(DependencyObject o, object value)
        {
            MainWindow mainWindow = o as MainWindow;
            if (mainWindow != null)
                return mainWindow.OnCoerceScaleValue((double)value);
            else return value;
        }

        private static void OnScaleValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            MainWindow mainWindow = o as MainWindow;
            if (mainWindow != null)
                mainWindow.OnScaleValueChanged((double)e.OldValue, (double)e.NewValue);
        }

        protected virtual double OnCoerceScaleValue(double value)
        {
            if (double.IsNaN(value))
                return 1.0f;

            value = Math.Max(0.1, value);
            return value;
        }

        protected virtual void OnScaleValueChanged(double oldValue, double newValue) { }

        public double ScaleValue
        {
            get => (double)GetValue(ScaleValueProperty);
            set => SetValue(ScaleValueProperty, value);
        }
        #endregion

        private void MainGrid_SizeChanged(object sender, EventArgs e) => CalculateScale();

        private void CalculateScale()
        {
            double yScale = ActualHeight / 544f;
            double xScale = ActualWidth / 1080f;
            double value = Math.Min(xScale, yScale);

            ScaleValue = (double)OnCoerceScaleValue(myMainWindow, value);
        }

        private JObject FetchNews()
        {
            try
            {
                WebClient wc = new WebClient();
                string newsUri = "https://new.do-heart.com/posts/filter-posts?limit=15&offset=1&type=News";
                string newsData = wc.DownloadString(new Uri(newsUri));
                return JObject.Parse(newsData);
            }
            catch
            {
                return null;
            }
        }

        private JObject FetchHashSum()
        {
            try
            {
                WebClient wc = new WebClient();
                string downloadfile = wc.DownloadString(new Uri(HashSumUri));
                JObject json = JObject.Parse(downloadfile);
                return json;
            }
            catch
            {
                return null;
            }
        }
        private bool LoadLocalBanners()
        {
            //Check local banner images folder
            if (File.Exists(Path.GetFullPath(localBannerDir + $@"\0.png")))
                Banner.Source = new BitmapImage(new Uri(Path.GetFullPath(localBannerDir + $@"\0.png"))); //Init Banner

            return true;
        }

        //Load news
        private void InitsNews()
        {
            try
            {
                int maxNews = Math.Max(this.newsControls.Count, ((JArray)this.news["data"]).Count);
                maxNews = Math.Min(5, maxNews);

                for (int i = 0; i < maxNews; i++)
                {
                    string title = news["data"][i]["post_title"].ToString();
                    string id = news["data"][i]["id"].ToString();
                    string url = NewsUrl + id;

                    Button newsControl = this.newsControls[i];
                    newsControl.Dispatcher.Invoke(new Action(() =>
                    {
                        newsControl.Content = title;
                        var isUrl = Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute);
                        if (isUrl)
                        {
                            newsControl.IsEnabled = true;
                            newsControl.DataContext = url;
                            newsControl.ToolTip = "Click to open";
                        }
                        else
                             newsControl.ToolTip = "This link has been removed";
                        newsControl.Click += NewsControl_Click;
                    }));
                }
            }
            catch
            {
                for (int i = 0; i < this.newsControls.Count; i++)
                {
                    Button newsControl = this.newsControls[i];
                    newsControl.Dispatcher.Invoke(new Action(() =>
                    {
                        newsControl.IsEnabled = false;
                        newsControl.Opacity = 0.7;
                    }));
                }
            }
        }

        // Fetch banners from news items
        private void FetchBanners()
        {
            WebClient wc = new WebClient();
            int newsCount = ((JArray)news["data"]).Count;
            int i = -1;
            if (!File.Exists(Path.GetFullPath(localBannerDir + $@"\0.png")))
            {
                Directory.CreateDirectory(Path.GetFullPath(localBannerDir));
                //Debug.WriteLine("isExists: " + Path.GetFullPath(localBannerDir));
            }

            while (++i < newsCount && this.banners.Count <= this.maxBanners)
            {
                try
                {
                    string id = news["data"][i]["id"].ToString();
                    string bannerSrc = news["data"][i]["feature_image"].ToString();
                    bool isValidBannerSrc = Uri.IsWellFormedUriString(bannerSrc, UriKind.RelativeOrAbsolute);

                    //Skip the news without feature image
                    if (bannerSrc == null || !isValidBannerSrc)
                        continue;
                    //Debug.WriteLine("bannerSrc: " + bannerSrc);
                    Uri bannerUri = new Uri(bannerSrc);
                    int bannerIndex = this.banners.Count;
                    string bannerLocalPath = Path.GetFullPath(localBannerDir + $@"\{bannerIndex}.png");
                    wc.DownloadFile(bannerUri, bannerLocalPath);
                    this.banners.Add(id);

                    //Show banner for first image
                    if (bannerIndex == 0)
                    {
                        Action action = () => { this.LoadLocalBanners(); };
                        this.Dispatcher.Invoke(action);
                        Banner.DataContext = 0;  //DataContext is the index of banner in this.banners - The shown banner
                    }
                }
                catch
                {
                    //MessageBox.Show($"{ex}");
                    continue;
                }
            }
        }
        private bool IsRequireUpdate()
        {
            try
            {
                if (File.Exists(versionFile))
                {
                    Version localVersion = new Version(File.ReadAllText(versionFile));
                    Ver.Text = localVersion.ToString();

                    Version onlineVersion = new Version(json["version"].ToString()); //Server verion
                    VerSer.Text = onlineVersion.ToString();

                    if (onlineVersion.Equals(localVersion))
                    {
                        return false;
                    }
                }
                else
                {
                    Version localVersion = Version.zero;
                    Ver.Text = localVersion.ToString();
                    File.WriteAllText(Path.GetFullPath("(version)"), localVersion.ToString());
                    Version onlineVersion = new Version(json["version"].ToString()); //Server verion
                    VerSer.Text = onlineVersion.ToString();

                    if (onlineVersion.Equals(localVersion))
                    {
                        return false;
                    }
                }
            }
            catch
            {
            }

            return true;
        }
        private void Update(bool forceRecheck = false)
        {
            Status = LauncherStatus.Checking;
            UpdateStatus_btn.IsEnabled = false;
            UpdateStatus_btn.Opacity = 1;
            Loading.Visibility = Visibility.Visible;
            Progress.Visibility = Visibility.Visible;
            Analyzing.Visibility = Visibility.Visible;
            PlayGame.IsEnabled = false;
            PlayGame.Opacity = 0.7;
            FixClient.IsEnabled = false;
            FixClient.Opacity = 0.7;

            if (File.Exists(gameExe))
            {
                bool isFileinUse = IsFileInUse(gameExe);
                if (isFileinUse)
                {
                    MessageBoxResult mbr = MessageBox.Show("Please close game", "Error", MessageBoxButton.OKCancel, MessageBoxImage.Error);
                    switch (mbr)
                    {
                        case MessageBoxResult.OK:
                            Process[] game = Process.GetProcessesByName("Game");
                            game[0].Kill();
                            break;
                        case MessageBoxResult.Cancel:
                            Environment.Exit(0);
                            break;
                        default:
                            Environment.Exit(0);
                            break;
                    }
                }
            }

            this.AnalyzeRequiredFiles();
            this.isUpdating = true;
            Status = LauncherStatus.downloadingUpdate;
            Thread preVerifyThread = new Thread(() => { this.PreVerifyThread(); });
            preVerifyThread.Start();

            Thread downloadThread = new Thread(() => { this.DownloadThread(); });
            downloadThread.Priority = ThreadPriority.Highest;
            downloadThread.Start();

            Thread extractThread = new Thread(() => { this.ExtractThread(); });
            extractThread.Start();

            Thread progressThread = new Thread(() => {
                //Summary
                int total = this.updateFiles.Count;
                int completed = 0;
                while (this.isUpdating && !this.isFailed && completed != total)
                {
                    completed = 0;
                    foreach (var x in this.updateFiles)
                    {
                        if (x["state"].ToString() == "done")
                            completed++;
                    }

                    try
                    {
                        int completedPercent = (int)(completed * 100 / total);
                        Progress.Dispatcher.Invoke(new Action(() => { Progress.Content = completedPercent.ToString() + "%"; }));
                        Loading_Copy.Dispatcher.Invoke(new Action(() => {Loading_Copy.Value = completedPercent; }));
                    }
                    catch
                    {

                    }

                    Thread.Sleep(300);
                }

                if (completed == total)
                {
                    File.WriteAllText(Path.GetFullPath("(version)"), json["version"].ToString());
                    File.WriteAllText(Path.GetFullPath("Lib.txt"), this.updateFiles.ToString());
                    Thread.Sleep(2000);
                }

                if (!isFailed)
                {
                    Action action = () =>
                    {
                        Status = LauncherStatus.ready;
                        UpdateStatus_btn.IsEnabled = false;
                        UpdateStatus_btn.Opacity = 1;
                        Loading.Value = 100;
                        Loading_Copy.Value = 100;
                        Progress.Visibility = Visibility.Hidden;
                        OneFileProgress.Visibility = Visibility.Hidden;
                        FileName.Visibility = Visibility.Hidden;
                        PlayGame.IsEnabled = true;
                        PlayGame.Opacity = 1;
                        FixClient.IsEnabled = true;
                        FixClient.Opacity = 1;
                        Ver.Text = json["version"].ToString();
                    };
                    this.Dispatcher.Invoke(action);
                    if (fsHashSum != null)
                        fsHashSum.Close();
                }
            });
            progressThread.Priority = ThreadPriority.Lowest;
            progressThread.Start();

        }
        
        private void AnalyzeRequiredFiles(bool forceRecheck = false)
        {

            JArray localFiles = new JArray();
            bool isEmty = false;
            if (File.ReadAllText(Path.GetFullPath("Lib.txt")) == "")
            {
                foreach (var file in clientFiles)
                {
                    if (file.Key == "(version)")
                    {
                        continue;
                    }
                    JObject fileObj = new JObject();
                    fileObj["path"] = file.Key;
                    fileObj["hash"] = "";
                    fileObj["state"] = "";
                    localFiles.Add(fileObj);
                }
                File.WriteAllText(Path.GetFullPath("Lib.txt"), localFiles.ToString());
                isEmty = true;
            }
            else
            {
                try
                {
                    string _localFiles = File.ReadAllText(Path.GetFullPath("Lib.txt"));
                    localFiles = JArray.Parse(_localFiles);
                }
                catch
                {
                    foreach (var file in clientFiles)
                    {
                        if (file.Key == "(version)")
                        {
                            continue;
                        }
                        JObject fileObj = new JObject();
                        fileObj["path"] = file.Key;
                        fileObj["hash"] = "";
                        fileObj["state"] = "";
                        localFiles.Add(fileObj);
                    }
                    File.WriteAllText(Path.GetFullPath("Lib.txt"), localFiles.ToString());
                }
            }
            int i = 0;
            JObject tempObj = null;
            if (!isClick && !isEmty)
            {
                foreach (var file in clientFiles)
                {
                    try
                    {
                        if (file.Key == "(version)")
                        {
                            continue;
                        }
                        JObject fileObj = new JObject();
                        fileObj["path"] = file.Key;
                        fileObj["hash"] = file.Value["hash"];
                        fileObj["state"] = "added";

                        if (file.Key == localFiles[i]["path"].ToString())
                        {
                            if (file.Value["hash"].ToString() == localFiles[i]["hash"].ToString() && localFiles[i]["state"].ToString() == "done")
                            {
                                fileObj["state"] = "done";
                            }
                            i++;
                            this.updateFiles.Add(fileObj);                           
                        }
                        else
                            if(file.Key != "Bin/OgreMain.dll")
                            {
                                this.updateFiles.Add(fileObj);
                            }
                            else
                            {
                                if (file.Value["hash"].ToString() == localFiles[localFiles.Count - 1]["hash"].ToString() && localFiles[localFiles.Count - 1]["state"].ToString() == "done")
                                {
                                    fileObj["state"] = "done";
                                }
                                tempObj = fileObj;
                            }        
                    }
                    catch
                    {

                    }
                }
            }
            else
                foreach (var file in clientFiles)
                {
                    try
                    {
                        if (file.Key == "(version)")
                        {
                            continue;
                        }   
                        JObject fileObj = new JObject();
                        fileObj["path"] = file.Key;
                        fileObj["hash"] = file.Value["hash"];
                        fileObj["state"] = "added";
                        if (file.Key == "Bin/OgreMain.dll")
                        {
                            tempObj = fileObj;
                            continue;
                        }
                        this.updateFiles.Add(fileObj);   
                    }
                    catch
                    {

                    }
                }
            if (tempObj != null)
                this.updateFiles.Add(tempObj);
            File.WriteAllText(Path.GetFullPath("Lib.txt"), updateFiles.ToString());
        }

        private void PreVerifyThread(bool forceRecheck = false)
        {
            //Debug.WriteLine("Start: Pre Verify Thread");
            try
            {
                for (int i = 0; i < this.updateFiles.Count && this.isUpdating; i++)
                {
                    var file = this.updateFiles[i];
                    if (file["state"].ToString() == "done")
                    {
                        continue;
                    }
                    string filePath = file["path"].ToString();
                    string hash;
                    if (File.Exists(Path.GetFullPath(filePath)))
                    {
                        hash = Utils.CalculateMD5(filePath);
                        if (hash == file["hash"].ToString())
                        {
                            file["state"] = "done";
                            continue;
                        }
                        file["hash"] = hash;
                    }
                    file["state"] = "preVerified";
                }
                File.WriteAllText(Path.GetFullPath("Lib.txt"), updateFiles.ToString());
            }
            catch
            {
                Action action = () => { Status = LauncherStatus.failed; };
                this.Dispatcher.Invoke(action);
                isFailed = true;
            }

            //Debug.WriteLine("Complete: Pre Verify Thread");
        }

        private void DownloadThread()
        {
            //Debug.WriteLine("Start: Download Thread");
            try
            {
                int waitPoint = 0;
                for (int i = 0; i < this.updateFiles.Count && this.isUpdating && !this.isFailed; i++)
                {
                    var file = this.updateFiles[i];
                    //Debug.WriteLine("Downloading " + file["path"]);
                    //Waiting for state is preVerified
                    while (this.isUpdating)
                    {
                        if (file["state"].ToString() == "preVerified" || file["state"].ToString() == "done")
                            break;
                        Action action1 = () =>
                        {
                            if (waitPoint == 0)
                                Analyzing.Content = " Analyzing";
                            if (waitPoint < 3)
                            {
                                Analyzing.Content += ".";
                                waitPoint++;
                            }
                            else
                                waitPoint = 0;
                        };
                        this.Dispatcher.Invoke(action1);
                        Thread.Sleep(1000);
                    }
                    if (isFailed)
                        break;
                    //Skip welldone file
                    if (file["state"].ToString() == "done" || file["state"].ToString() == "downloaded")
                    {
                        continue;
                    }
                    Action action = () => { 
                        Analyzing.Visibility = Visibility.Hidden;
                        OneFileProgress.Visibility = Visibility.Visible;
                        FileName.Visibility = Visibility.Visible;
                    };
                    this.Dispatcher.Invoke(action);
                    //Download file
                    string localFilePath = file["path"].ToString();
                    string downloadFilePath = file["path"].ToString() + ".hlzip";
                    string fileUri = this.DownloadFileUri + file["path"].ToString() + ".hlzip";
                    Debug.WriteLine("Name: " + downloadFilePath);
                    try
                    {
                        downloadFilePath = Path.GetFullPath(downloadFilePath);

                        string destinationDirectory = Path.GetDirectoryName(downloadFilePath);
                        if (!Directory.Exists(destinationDirectory))
                        {
                            Directory.CreateDirectory(destinationDirectory);
                        }
                        string[] nameoffile = localFilePath.Split("/");
                        FileName.Dispatcher.Invoke(new Action(() => { FileName.Content = nameoffile[nameoffile.Length - 1]; }));
                        WebClient wc = new WebClient();
                        wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
                        wc.DownloadFileCompleted += Wc_DownloadFileCompleted;
                        var syncObject = new Object();
                        lock (syncObject)
                        {
                            wc.DownloadFileAsync(new Uri(fileUri), downloadFilePath, syncObject);
                            Monitor.Wait(syncObject);
                        }
                    }
                    catch
                    {
                        //MessageBox.Show("Server is overloading!!");
                        this.isFailed = true;
                    }
                    if (!isFailed)
                    {
                        file["state"] = "downloaded";
                    }
                    else
                    {
                        action = () =>
                        {
                            Status = LauncherStatus.failed;
                        };
                        this.Dispatcher.Invoke(action);
                        MessageBox.Show("Update failed!!!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    }

                    //Debug.WriteLine("Downloaded: " + file["path"]);
                    //continue;
                }
            }
            catch (Exception ex)
            {
                Action action = () =>
                {
                    Status = LauncherStatus.failed;
                    FixClient.IsEnabled = true;
                    FixClient.Opacity = 1;
                };
                this.Dispatcher.Invoke(action);
                MessageBox.Show("Can't find internet connection!!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine("Download Thread error: " + ex.Message);
            }
            //Debug.WriteLine("Complete: Download Thread");

        }

        private void Wc_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            lock (e.UserState)
            {
                isFailed = false;
                Monitor.PulseAll(e.UserState);
            }
        }
        private void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            //Action action = () => { OneFileProgress.Content = e.ProgressPercentage.ToString() + "%";  };
            try
            {
                tracker.NewFile();
                tracker.SetProgress(e.BytesReceived, e.TotalBytesToReceive);
                Loading.Dispatcher.Invoke(new Action(() => { Loading.Value = tracker.GetProgress() * 100; }));
                OneFileProgress.Dispatcher.Invoke(new Action(() => { OneFileProgress.Content = " - " + tracker.GetBytesPerSecondString() + " - " + e.ProgressPercentage + "%"; }));
            }
            catch
            {
                //Debug.WriteLine(ex.Message);
            }
        }

        private void ExtractThread()
        {
            //Debug.WriteLine("Start: Extract Thread");
            try
            {
                for (int i = 0; i < this.updateFiles.Count && this.isUpdating; i++)
                {
                    var file = this.updateFiles[i];
                    //Debug.WriteLine("Extracting: " + file["path"]);
                    //Waiting for state is preVerified
                    while (this.isUpdating)
                    {
                        if (file["state"].ToString() == "downloaded" || file["state"].ToString() == "done")
                            break;

                        Thread.Sleep(10);
                    }

                    //Skip welldone file
                    if (file["state"].ToString() == "done")
                    {
                        continue;
                    }

                    //Extract file
                    string localFilePath = file["path"].ToString();
                    string downloadFilePath = file["path"].ToString() + ".hlzip";
                    bool _isExtracting = true;
                    try
                    {
                        Utils.Extract(downloadFilePath, downloadFilePath);
                        File.Delete(downloadFilePath);
                        _isExtracting = false;
                    }
                    catch
                    {
                        //string[] filename = localFilePath.Split("/");
                        //if (IsFileInUse(Path.GetFullPath(localFilePath)))
                        //{
                        //    MessageBoxResult mbr = MessageBox.Show("Please close " + filename[filename.Length - 1], "Error", MessageBoxButton.OKCancel, MessageBoxImage.Error);
                        //    switch (mbr)
                        //    {
                        //        case MessageBoxResult.OK:
                        //            Environment.Exit(0);
                        //            break;
                        //        case MessageBoxResult.Cancel:
                        //            Environment.Exit(0);
                        //            break;
                        //        default:
                        //            Environment.Exit(0);
                        //            break;
                        //    }
                        //}
                        file["state"] = "failed";
                        File.Delete(downloadFilePath);
                        continue;
                    }

                    if (!_isExtracting)
                    {
                        file["state"] = "done";
                    }
                    else
                    {
                        isFailed = true;
                        this.Dispatcher.Invoke(new Action(() => { Status = LauncherStatus.failed; }));
                    }
                    //Debug.WriteLine("Extracted: " + file["path"]);
                }
            }
            catch
            {
                MessageBox.Show("Something went wrong!! Please click FixClient!!");
            }
            //Debug.WriteLine("Complete: Extract Thread");
        }

        private void Banner_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            int currentIndex = (int)Banner.DataContext;
            string newsUrl = NewsUrl + this.banners[currentIndex];
            var isUrl = Uri.IsWellFormedUriString(newsUrl, UriKind.RelativeOrAbsolute);
            if (isUrl)
            {
                Utils.openLink(newsUrl);
            }
        }

        //Exchange Banner Image
        private void Pre_btn_Click(object sender, RoutedEventArgs e)
        {
            int currentIndex = (int)Banner.DataContext;
            currentIndex = (currentIndex > 0 ? currentIndex - 1 : this.banners.Count - 1);

            string localBannerPath = Path.GetFullPath(localBannerDir + $@"\{currentIndex}.png");
            Uri localBannerUri = new Uri(localBannerPath);
            Banner.Source = new BitmapImage(localBannerUri);
            Banner.DataContext = currentIndex; //Keep current index of shown banner
        }
        private void Next_btn_Click(object sender, RoutedEventArgs e)
        {
            int currentIndex = (int)Banner.DataContext;
            currentIndex = (currentIndex < this.banners.Count - 1 ? currentIndex + 1 : 0);

            string localBannerPath = Path.GetFullPath(localBannerDir + $@"\{currentIndex}.png");
            Uri localBannerUri = new Uri(localBannerPath);
            Banner.Source = new BitmapImage(localBannerUri);
            Banner.DataContext = currentIndex; //Keep current index of shown banner
        }

        public bool IsFileInUse(string path)
        {
            try
            {
                if (File.Exists(path))
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write)) { }
                else
                {
                    MessageBoxResult mbr = MessageBox.Show("Cann't find file game!!! Click fixclient to fix the game", "do-heart", MessageBoxButton.OK, MessageBoxImage.Error);
                    switch (mbr)
                    {
                        case MessageBoxResult.OK:
                            this.Update(true);
                            break;
                        default:
                            return true;
                            //break;
                    }
                    return false;
                }
            }
            catch (IOException)
            {
                return true;
            }

            return false;
        }

        public class filename
        {
            public string Hash { get; set; }
        }

        public class FileModel
        {
            public filename Name { get; set; }
        }
        private void PlayGame_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(gameExe) && Status == LauncherStatus.ready)
                {
                    string strCmdText;
                    strCmdText = Path.Combine(Directory.GetCurrentDirectory(), "Bin", "Game.exe");
                    Process.Start(strCmdText, "-fl");

                    Environment.Exit(0);
                }
                else
                {
                    MessageBox.Show("Can't find game!!! You can try clicking FixClient", "do-heart", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch
            {
                MessageBox.Show("Can't find game!!! You can try clicking FixClient", "do-heart", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void AutoFetchBanner()
        {
            int BannerNum = 0;
            do
            {
                Random _random = new Random();
                BannerNum = ((BannerNum + 1) >= this.banners.Count || BannerNum + 1 >= maxBanners) ? 0 : ++BannerNum;
                if (File.Exists(Path.GetFullPath(localBannerDir + @"\" + $"{BannerNum}.png")))
                {
                    Banner.Dispatcher.Invoke(new Action(() => {
                        Banner.Source = new BitmapImage(new Uri(Path.GetFullPath(localBannerDir + @"\" + $"{BannerNum}.png")));
                    }));
                    Thread.Sleep(_random.Next(5000, 10000));
                }
            } while (true);
        }

        //Version struct
        struct Version
        {
            internal static Version zero = new Version("0", "0", "0"); //Version 0

            private string major;   //Part 1 of Version
            private string minor;   //Part 2 of Version
            private string subMinor;//Part 3 of Version

            internal Version(string _major, string _minor, string _subMinor)
            {
                major = _major;
                minor = _minor;
                subMinor = _subMinor;
            }
            internal Version(string _version)
            {
                string[] versionStrings = _version.Split('.');

                //Check is a Version kind
                if (versionStrings.Length != 3)
                {
                    major = "0";
                    minor = "0";
                    subMinor = "0";
                    return;
                }

                //Divide version
                major = versionStrings[0];
                minor = versionStrings[1];
                subMinor = versionStrings[2];
            }

            //Check local version and server version
            internal bool IsDifferentThan(Version _otherVersion)
            {
                if (major != _otherVersion.major)
                {
                    return true;
                }
                else
                {
                    if (minor != _otherVersion.minor)
                    {
                        return true;
                    }
                    else
                    {
                        if (subMinor != _otherVersion.subMinor)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            //Combine part of version
            public override string ToString()
            {
                return $"{major}.{minor}.{subMinor}";
            }
        }

        private void UpdateStatus_btn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                updateFiles.Clear();
                File.Delete(Path.GetFullPath("Lib.txt"));
                fsHashSum = new FileStream(Path.GetFullPath("Lib.txt"), FileMode.Create, FileAccess.ReadWrite);
                fsHashSum.Close();
                this.Update(true);
                isFailed = false;
            }
            catch
            {
                MessageBox.Show("Somethings went wrong!! Please wait for a few minutes and click the FixClient button again");
            }
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = "https://dh3.do-heart.com/home"; //Home link 
                Process.Start(psi);
            }
            catch
            {
                MessageBox.Show("Server is overloading!");
            }
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = "https://dh3.do-heart.com/register"; //Register link
                Process.Start(psi);
            }
            catch
            {
                MessageBox.Show("Server is overloading!");
            }
        }

        private void Recharge_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = "https://dh3.do-heart.com/login"; //Recharge link
                Process.Start(psi);
            }
            catch
            {
                MessageBox.Show("Server is overloading!");
            }
        }

        private void Fanpage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = "https://www.facebook.com/doheartss";
                Process.Start(psi);
            }
            catch
            {
                MessageBox.Show("Server is overloading!");
            }
        }

        private void Group_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = "https://www.facebook.com/groups/224482005141981"; //Group link
                Process.Start(psi);
            }
            catch
            {

            }
        }

        private void NewBiesGuide_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = "https://dh3.do-heart.com/posts/Guide-57"; //Guide link
                Process.Start(psi);
            }
            catch
            {

            }
        }

        private void More_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = "http://do-heart.com/#page2";
                Process.Start(psi);
            }
            catch
            {

            }
        }

        private void FixClient_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                isClick = true;
                updateFiles.Clear();
                File.Delete(Path.GetFullPath("Lib.txt"));
                fsHashSum = new FileStream(Path.GetFullPath("Lib.txt"), FileMode.Create, FileAccess.ReadWrite);
                fsHashSum.Close();
                this.Update(true);
                isClick = false;
                isFailed = false;
            }
            catch
            {
                MessageBox.Show("Somethings went wrong!! Please wait for a few minutes and click the FixClient button again");
            }
        }

        private void NewsControl_Click(object sender, RoutedEventArgs e)
        {
            Button senderButton = (Button)sender;
            try
            {
                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = (string)senderButton.DataContext;
                Process.Start(psi);
            }
            catch
            {

            }
        }

        private void Exit_Button(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        private void Minimize_Button(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void PlayGame_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (PlayGame.IsEnabled)
                PlayGame.Opacity = 0.7;
        }
        private void PlayGame_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (PlayGame.IsEnabled)
                PlayGame.Opacity = 1;
        }
        private void FixClient_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (FixClient.IsEnabled)
                FixClient.Background.Opacity = 0.6;

        }
        private void FixClient_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (FixClient.IsEnabled)
                FixClient.Background.Opacity = 1;
        }
        private void NewBiesGuide_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            NewBiesGuide.Background.Opacity = 0.6;
        }
        private void NewBiesGuide_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            NewBiesGuide.Background.Opacity = 1;
        }
        private void UpdateStatus_btn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (UpdateStatus_btn.IsEnabled)
                UpdateStatus_btn.Opacity = 0.7;
        }
        private void UpdateStatus_btn_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (UpdateStatus_btn.IsEnabled)
                UpdateStatus_btn.Opacity = 1;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            try
            {
                // Todo: Using dynamic controls generation
                this.newsControls.Add(News1);
                this.newsControls.Add(News2);
                this.newsControls.Add(News3);
                this.newsControls.Add(News4);
                this.newsControls.Add(News5);
                Banner.DataContext = 0;

                //Set default properties for controls
                UpdateStatus_btn.IsEnabled = false;
                UpdateStatus_btn.Opacity = 1;
                Loading.Maximum = 100;
                Loading_Copy.Maximum = 100;

                //Fetching data from server in new thread
                Thread wdr = new Thread(() =>
                {
                    bool check = false;
                    do
                    {
                        try
                        {
                            if (this.news == null)
                            {
                                this.news = this.FetchNews();
                                this.InitsNews();
                                this.FetchBanners();
                            }
                            this.AutoFetchBanner();
                            check = true;
                        }
                        catch
                        {
                            if (!File.Exists(Path.GetFullPath(localBannerDir + $@"\0.png")))
                            {
                                Directory.CreateDirectory(Path.GetFullPath(localBannerDir));
                                //Debug.WriteLine("isExists: " + Path.GetFullPath(localBannerDir));
                            }
                            else
                            {
                                Action action = () =>
                                {
                                    Banner.Source = new BitmapImage(new Uri(Path.GetFullPath(localBannerDir + $@"\0.png")));
                                };
                                Banner.Dispatcher.Invoke(action);
                            }
                            MessageBoxResult mbr = MessageBox.Show("Server is overloading!!! Please wait for a few seconds", "do-heart", MessageBoxButton.YesNo, MessageBoxImage.Error);
                            if (mbr == MessageBoxResult.Yes)
                                Thread.Sleep(10000);
                            else
                                Environment.Exit(0);
                        }
                    } while (!check);
                });
                wdr.Start();

                this.json = this.FetchHashSum();
                if (this.json != null)
                {
                    this.clientFiles = this.json["data"].ToObject<JObject>();
                }
                else
                {
                    MessageBoxResult mbr = MessageBox.Show("Server is overloading!!!", "do-heart", MessageBoxButton.OK, MessageBoxImage.Error);
                    switch (mbr)
                    {
                        case MessageBoxResult.OK:
                            Environment.Exit(0);
                            break;
                        default:
                            Environment.Exit(0);
                            break;
                    }
                }

                //Debug.WriteLine("Checking update");
                bool isRequireUpdate = this.IsRequireUpdate();
                Status = LauncherStatus._verifying;
                if (!File.Exists(Path.GetFullPath("Lib.txt")))
                {
                    isRequireUpdate = true;
                    fsHashSum = new FileStream(Path.GetFullPath("Lib.txt"), FileMode.Create, FileAccess.ReadWrite);
                    fsHashSum.Close();
                }
                if (isRequireUpdate)
                {
                    //Debug.WriteLine("UPdate required");
                    this.Update(true);
                }
                else
                {
                    Status = LauncherStatus.ready;
                    Progress.Visibility = Visibility.Hidden;
                    OneFileProgress.Visibility = Visibility.Hidden;
                    FileName.Visibility = Visibility.Hidden;
                    PlayGame.IsEnabled = true;
                }

            }
            catch
            {

            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            double ratio = (double)System.Windows.SystemParameters.PrimaryScreenWidth / 1920;
            this.Height = ratio * 544 * 1.25;
            this.Width = ratio * 1080 * 1.25;
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                    this.DragMove();
            }
            catch
            {

            }
        }
    }
}
