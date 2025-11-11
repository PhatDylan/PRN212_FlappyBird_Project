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

        public LoginWindow(double initialPipeSpeed = DefaultPipeSpeed)
        {
            InitializeComponent();

            selectedPipeSpeed = Math.Clamp(initialPipeSpeed, MinPipeSpeed, MaxPipeSpeed);
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = new MainWindow(selectedPipeSpeed);        
            Application.Current.MainWindow = mainWindow;         mainWindow.Show();

                Close();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(selectedPipeSpeed)
            {
                Owner = this
            };

            if (settingsWindow.ShowDialog() == true)
            {
                selectedPipeSpeed = Math.Clamp(settingsWindow.SelectedPipeSpeed, MinPipeSpeed, MaxPipeSpeed);
            }
        }

        private void SkinsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Skins clicked (TODO).", "Skins", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}


