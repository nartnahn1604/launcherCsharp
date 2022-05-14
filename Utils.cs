using System;
using System.Security.Cryptography;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;

namespace Launcher
{
    class Utils
    {
        public static string CalculateMD5(string filename)
        {
            try
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(filename))
                    {
                        var hash = md5.ComputeHash(stream);
                        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }
            }
            catch
            {
                return null;
            }
        }
        public static void openLink(string url)
        {
            try
            {
                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = url;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Can not open link: " + url + "\nDetails: " + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                    );
            }
        }

        public static void Extract(string sourceFile, string destinationDirectory)
        {
            sourceFile = Path.GetFullPath(sourceFile);
            destinationDirectory = Path.GetFullPath(destinationDirectory);

            if (!File.Exists(sourceFile))
            {
                return;
            }
            
            destinationDirectory = Path.GetDirectoryName(destinationDirectory);

            if (!Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }
            ZipFile.ExtractToDirectory(sourceFile, destinationDirectory, true);
        }
        public class DownloadProgressTracker
        {
            private long _totalFileSize;
            private readonly int _sampleSize;
            private readonly TimeSpan _valueDelay;

            private DateTime _lastUpdateCalculated;
            private long _previousProgress;

            private double _cachedSpeed;

            private Queue<Tuple<DateTime, long>> _changes = new Queue<Tuple<DateTime, long>>();

            public DownloadProgressTracker(int sampleSize, TimeSpan valueDelay)
            {
                _lastUpdateCalculated = DateTime.Now;
                _sampleSize = sampleSize;
                _valueDelay = valueDelay;
            }

            public void NewFile()
            {
                _previousProgress = 0;
            }

            public void SetProgress(long bytesReceived, long totalBytesToReceive)
            {
                _totalFileSize = totalBytesToReceive;

                long diff = bytesReceived - _previousProgress;
                if (diff <= 0)
                    return;

                _previousProgress = bytesReceived;

                _changes.Enqueue(new Tuple<DateTime, long>(DateTime.Now, diff));
                while (_changes.Count > _sampleSize)
                    _changes.Dequeue();
            }

            public double GetProgress()
            {
                return _previousProgress / (double)_totalFileSize;
            }

            public string GetProgressString()
            {
                return String.Format("{0:P0}", GetProgress());
            }

            public string GetBytesPerSecondString()
            {
                double speed = GetBytesPerSecond();
                var prefix = new[] { "", "K", "M", "G" };

                int index = 0;
                while (speed > 1024 && index < prefix.Length - 1)
                {
                    speed /= 1024;
                    index++;
                }

                int intLen = ((int)speed).ToString().Length;
                int decimals = 3 - intLen;
                if (decimals < 0)
                    decimals = 0;

                string format = String.Format("{{0:F{0}}}", decimals) + "{1}B/s";

                return String.Format(format, speed, prefix[index]);
            }

            public double GetBytesPerSecond()
            {
                if (DateTime.Now >= _lastUpdateCalculated + _valueDelay)
                {
                    _lastUpdateCalculated = DateTime.Now;
                    _cachedSpeed = GetRateInternal();
                }

                return _cachedSpeed;
            }

            private double GetRateInternal()
            {
                if (_changes.Count == 0)
                    return 0;
                try
                {
                    TimeSpan timespan = _changes.Last().Item1 - _changes.First().Item1;
                    long bytes = _changes.Sum(t => t.Item2);

                    double rate = bytes / timespan.TotalSeconds;

                    if (double.IsInfinity(rate) || double.IsNaN(rate))
                        return 0;

                    return rate;
                }
                catch
                {
                    return 0;
                }
            }
        }
        //public static void DownloadFile(string url, string filename, bool isFailed = false)
        //{
        //    try
        //    {
        //        filename = Path.GetFullPath(filename);

        //        string destinationDirectory = Path.GetDirectoryName(filename);
        //        if (!Directory.Exists(destinationDirectory))
        //        {
        //            Directory.CreateDirectory(destinationDirectory);
        //        }

        //        WebClient wc = new WebClient();
        //        wc.DownloadFile(url, filename);
        //    }
        //    catch
        //    {
        //        isFailed = true;
        //    }
        //}
    }
}
