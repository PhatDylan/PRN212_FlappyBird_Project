using System;
using System.Windows;
using System.Windows.Media;
using System.IO;
using PRN212.G5.FlappyBird.Views;

namespace PRN212.G5.FlappyBird
{
    public partial class LoginWindow : Window
    {
        private const double DefaultPipeSpeed = 5.0;
        private const double MinPipeSpeed = 3.0;
        private const double MaxPipeSpeed = 10.0;

        private double selectedPipeSpeed = DefaultPipeSpeed;
        private double musicVolume = 50;

        private MediaPlayer mediaPlayer; 

        public LoginWindow(double initialPipeSpeed = DefaultPipeSpeed)
        {
            InitializeMediaPlayer();

            PreloadBgm();

            InitializeComponent();

            selectedPipeSpeed = Math.Clamp(initialPipeSpeed, MinPipeSpeed, MaxPipeSpeed);
        }

        private void PreloadBgm()
        {
            mediaPlayer.Volume = 0; 
            mediaPlayer.Play();
            mediaPlayer.Pause();
        }

        private void InitializeMediaPlayer()
        {
                mediaPlayer = new MediaPlayer();
                mediaPlayer.MediaEnded += MediaPlayer_MediaEnded; 
                string assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", "BGM.mp3");
                mediaPlayer.Open(new Uri(assetPath));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (mediaPlayer != null)
            {
                mediaPlayer.Volume = musicVolume / 100.0;
                mediaPlayer.Play();
            }
        }

        private void MediaPlayer_MediaEnded(object sender, EventArgs e)
        {
            if (mediaPlayer != null)
            {
                mediaPlayer.Position = TimeSpan.Zero;
                mediaPlayer.Play();
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Dừng và giải phóng MediaPlayer
            if (mediaPlayer != null)
            {
                mediaPlayer.Stop();
                mediaPlayer.Close();
            }

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

                // Cập nhật volume
                if (mediaPlayer != null)
                {
                    mediaPlayer.Volume = musicVolume / 100.0;
                }
            }
        }

        private void SkinsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Skins clicked (TODO).", "Skins", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected override void OnClosed(EventArgs e)
        {
            // Cleanup khi đóng window
            if (mediaPlayer != null)
            {
                mediaPlayer.Stop();
                mediaPlayer.Close();
                mediaPlayer = null;
            }
            base.OnClosed(e);
        }
    }
}