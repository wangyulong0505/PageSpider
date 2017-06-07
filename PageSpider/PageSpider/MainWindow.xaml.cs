using System;
using System.Threading;
using System.Windows;

namespace PageSpider
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        #region 

        private Spider spider;
        private delegate void CSHandler(string args1, string args2);
        private delegate void DFHandler(int args1);

        #endregion

        #region constructor

        public MainWindow()
        {
            InitializeComponent();
            spider = new Spider();
            spider.ContentsSaved += new Spider.ContentsSavedHandler(Spider_ContentsSaved);
            spider.DownloadFinish += new Spider.DownloadFinishHandler(Spider_DownloadFinish);
            btnStop.IsEnabled = false;
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        #endregion

        #region methods

        /// <summary>
        /// 下载完成执行的委托方法
        /// </summary>
        /// <param name="count"></param>
        private void Spider_DownloadFinish(int count)
        {
            //DFHandler是一个委托，委托里面封装需要执行的操作，然后调用Dispatcher.Invoke()执行委托
            DFHandler handler = h =>
            {
                spider.Abort();
                btnDownload.IsEnabled = true;
                btnDownload.Content = "Download";
                btnStop.IsEnabled = false;
                MessageBox.Show($"Finished {h.ToString()}");
            };
            Dispatcher.Invoke(handler, count);
        }

        /// <summary>
        /// 保存内容的委托方法
        /// </summary>
        /// <param name="path"></param>
        /// <param name="url"></param>
        private void Spider_ContentsSaved(string path, string url)
        {
            CSHandler handler = (c, s) =>
            {
                ListDownload.Items.Add(new { File = path, Url = url});
            };
            Dispatcher.Invoke(handler, path, url);
            
        }

        #endregion

        #region events

        /// <summary>
        /// 主窗体关闭时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            spider.Abort();
        }

        /// <summary>
        /// 主窗体加载时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TextUrl.Text = "news.sina.com.cn";
        }

        /// <summary>
        /// 点击下载按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDownload_Click(object sender, EventArgs e)
        {
            spider.RootUrl = TextUrl.Text;
            Thread thread = new Thread(new ParameterizedThreadStart(Download));
            thread.Start(TextPath.Text);
            btnDownload.IsEnabled = false;
            btnDownload.Content = "Downloading...";
            btnStop.IsEnabled = true;
        }

        private void Download(object param)
        {
            spider.Download((string)param);
        }

        /// <summary>
        /// 点击停止按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStop_Click(object sender, EventArgs e)
        {
            spider.Abort();
            btnDownload.IsEnabled = true;
            btnDownload.Content = "Download";
            btnStop.IsEnabled = false;
        }

        /// <summary>
        /// 打开属性设置对话框
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PropertyButton_Click(object sender, RoutedEventArgs e)
        {
            PropertyWindow property = new PropertyWindow
            {
                maxDepth = spider.MaxDepth,
                maxConnection = spider.MaxConnection,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            if (property.ShowDialog() == true)
            {
                spider.MaxDepth = property.maxDepth;
                spider.MaxConnection = property.maxConnection;
            }
        }

        /// <summary>
        /// 选择文件夹路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.RootFolder = Environment.SpecialFolder.Desktop;
            dialog.Description = "Contents Root Folder";
            var result = dialog.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                TextPath.Text = dialog.SelectedPath;
            }
        }

        #endregion
    }
}
