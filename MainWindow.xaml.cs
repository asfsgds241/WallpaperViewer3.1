using System;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Threading;
using Steamworks;

namespace WallpaperViewer3
{
    public partial class MainWindow : Window
    {
        private SteamWorkshopQuery? workshopQuery;
        private DispatcherTimer steamTimer;

        public MainWindow()
        {
            InitializeComponent();
            InitializeSteamTimer();
            InitializeSteamWorkshop();
        }

        private void InitializeSteamTimer()
        {
            steamTimer = new DispatcherTimer();
            steamTimer.Interval = TimeSpan.FromMilliseconds(33); // ~30fps
            steamTimer.Tick += (s, e) => SteamAPI.RunCallbacks();
            steamTimer.Start();
        }

        private async void InitializeSteamWorkshop()
        {
            try
            {
                Console.WriteLine("开始初始化Steam Workshop查询...");
                workshopQuery = new SteamWorkshopQuery();
                await workshopQuery.Initialize();
                MessageBox.Show("成功获取壁纸信息并保存到LOG.JSON文件中！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取壁纸信息时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"错误：{ex}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            steamTimer.Stop();
            workshopQuery?.Shutdown();
            base.OnClosed(e);
        }
    }
}