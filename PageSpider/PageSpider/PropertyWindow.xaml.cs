using System;
using System.Windows;

namespace PageSpider
{
    /// <summary>
    /// PropertyWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PropertyWindow : Window
    {
        #region Property

        public int maxDepth { get; set; }
        public int maxConnection { get; set; }

        #endregion

        public PropertyWindow()
        {
            InitializeComponent();
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Loaded += new RoutedEventHandler(PropertyWindow_Loaded);
        }

        #region Load Events

        private void PropertyWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TextMaxDepth.Text = maxDepth.ToString();
            TextMaxConnection.Text = maxConnection.ToString();
        }

        #endregion

        #region OK Button Events

        private void OKButton_Click(object sender, EventArgs e)
        {
            maxDepth = int.Parse(TextMaxDepth.Text.Trim());
            maxConnection = int.Parse(TextMaxConnection.Text.Trim());
            DialogResult = true;
            Close();
        }

        #endregion

        #region Cancel Button Events

        private void CancelButton_Click(object sender, EventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

    }
}
