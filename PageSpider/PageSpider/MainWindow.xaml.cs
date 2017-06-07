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
        #region 程序启动过程和设计思路

        /* 1、页面加载之后，会新建一个Spider实例，然后调用Spider实例的两个委托ContentsSavedHandler（正文内容被保存到本地后触发）和DownloadFinishHandler（全部链接下载分析完毕后触发）
         * 2、主页面有四个事件：设置深度和最大连接数，设置文件保存路径，启动网络下载，停止网络下载
         * 3、打开深度和最大连接数设置页面点击OK的时候会自动设置Spider的Depth和MaxConnection为输入的值，否则将使用Spider默认的值
         * 4、打开文件保存路径可以选择将下载的文件保存在哪里，否则默认保存在应用程序所在盘符的根路径下
         * 5、点击Download按钮，以文本框的值作为RootUrl开启新线程执行Spider的Download方法并传递参数SavePath（保存路径）
         * 6、WorkingUnitCollection类是判断工作流程的类，里面有开始工作，结束工作，判断是否完成，等待所有工作完成等方法
         * 7、RequestState类是请求获取数据执行Web操作的类，里面有Url，Data, Index, Depth, Stream等一系列属性
         * 8、需要有两个集合存储符合条件未下载的Url和符合条件已下载的Url，最大深度是2级，第0级是RootUrl，从RootUrl中获得的所有html链接是第1级。如果深度大于等于2就不再继续查找
         * 9、MaxConnection是最大连接数目，一次运行N个请求，如果N个请求都是Busy的就等待完成
         * 10、Spider的Download下载方法先判断RootUrl是否为空，如果不是先清空未下载和已下载的集合，然后把RootUrl经过判断和处理添加到未下载的集合中，每次添加的时候需要判断链接是否在已下载列表中
         * 11、添加完之后需要根据未下载列表中的元素请求资源，然后把文件内容下载下来保存在本地文件夹中，并把未下载列表中的资源添加到已下载资源列表中，移除未下载列表中的资源
         * 12、根据正则表达式匹配下载文件中的所有Url，然后Depth+1，判断新的链接是否合法，然后继续循环未下载列表中的资源
         * 13、如果未下载列表中的资源是空的就结束循环
         * 
         * 
         */

        #endregion

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
