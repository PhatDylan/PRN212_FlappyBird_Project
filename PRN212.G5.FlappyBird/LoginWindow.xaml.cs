using System;
using System.Windows;
using PRN212.G5.FlappyBird.Views;

namespace PRN212.G5.FlappyBird
{
    public partial class LoginWindow : Window
    {
        private const double DefaultPipeSpeed = 5.0;
        private const double MinPipeSpeed = 3.0;
        private const double MaxPipeSpeed = 10.0;

        private double selectedPipeSpeed = DefaultPipeSpeed;
        private double musicVolume = 50; // Volume m?c ??nh 50%

        public LoginWindow(double initialPipeSpeed = DefaultPipeSpeed)
        {
            InitializeComponent();

            selectedPipeSpeed = Math.Clamp(initialPipeSpeed, MinPipeSpeed, MaxPipeSpeed);

            // B?t ??u phát nh?c n?n khi window ???c load
            Loaded += LoginWindow_Loaded;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // ??t âm l??ng và phát nh?c n?n
            BackgroundMusic.Volume = musicVolume / 100.0; // Chuy?n ??i t? 0-100 sang 0-1
            BackgroundMusic.Play();
        }

        private void BackgroundMusic_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Khi nh?c k?t thúc, quay l?i ??u và phát l?i (l?p)
            BackgroundMusic.Position = TimeSpan.Zero;
            BackgroundMusic.Play();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // D?ng nh?c khi chuy?n sang màn hình chính
            BackgroundMusic.Stop();

            var mainWindow = new MainWindow(selectedPipeSpeed);
            Application.Current.MainWindow = mainWindow;
            mainWindow.Show();

            Close();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(selectedPipeSpeed, musicVolume)
            {
                Owner = this
            };

            if (settingsWindow.ShowDialog() == true)
            {
                selectedPipeSpeed = Math.Clamp(settingsWindow.SelectedPipeSpeed, MinPipeSpeed, MaxPipeSpeed);
                musicVolume = settingsWindow.SelectedVolume;

                // C?p nh?t âm l??ng nh?c ngay l?p t?c
                BackgroundMusic.Volume = musicVolume / 100.0;
            }
        }

        private void SkinsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Skins clicked (TODO).", "Skins", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}