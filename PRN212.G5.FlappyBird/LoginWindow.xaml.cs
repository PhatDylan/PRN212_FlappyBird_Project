using System;
using System.Windows;
using FlappyBird.Business.Models;
using FlappyBird.Data.Repositories;
using PRN212.G5.FlappyBird.Views;

namespace PRN212.G5.FlappyBird
{
    public partial class LoginWindow : Window
    {
        private const string OfflineEmail = "offline@local";
        private const double DefaultPipeSpeed = 5.0;
        private const double MinPipeSpeed = 3.0;
        private const double MaxPipeSpeed = 10.0;

        private readonly AccountRepo accountRepo = new();
        private Account currentAccount;
        private double selectedPipeSpeed = DefaultPipeSpeed;

        public LoginWindow()
        {
            InitializeComponent();

            currentAccount = EnsureOfflineAccount();
        }



        public LoginWindow(Account account, double initialPipeSpeed) : this()
        {
            currentAccount = account;
            selectedPipeSpeed = Math.Clamp(initialPipeSpeed, MinPipeSpeed, MaxPipeSpeed);
        }

        private Account EnsureOfflineAccount()
        {
            var account = accountRepo.GetAccountByEmail(OfflineEmail);

            if (account != null)
            {
                return account;
            }

            var offlineAccount = new Account
            {
                Email = OfflineEmail,
                Password = Guid.NewGuid().ToString(),
                Name = "Player",
                Avatar = string.Empty,
                HighScore = 0
            };

            accountRepo.Register(offlineAccount);

            return accountRepo.GetAccountByEmail(OfflineEmail) ?? offlineAccount;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mainWindow = new MainWindow(currentAccount, selectedPipeSpeed);

                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở màn hình game: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
