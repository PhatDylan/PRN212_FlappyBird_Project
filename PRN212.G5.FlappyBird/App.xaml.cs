using System;
using System.Windows;
using PRN212.G5.FlappyBird.Views;
using FlappyBird.Data.Repositories;
using FlappyBird.Business.Models;

namespace PRN212.G5.FlappyBird
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            const string offlineEmail = "offline@local";
            var accountRepo = new AccountRepo();
            var offlineAccount = accountRepo.GetAccountByEmail(offlineEmail);

            if (offlineAccount == null)
            {
                offlineAccount = new Account
                {
                    Email = offlineEmail,
                    Password = Guid.NewGuid().ToString(),
                    Name = "Player",
                    Avatar = string.Empty,
                    HighScore = 0
                };

                accountRepo.Register(offlineAccount);
                offlineAccount = accountRepo.GetAccountByEmail(offlineEmail) ?? offlineAccount;
            }

            ShutdownMode = ShutdownMode.OnMainWindowClose;

            var mainWindow = new MainWindow(offlineAccount)
            {
                Title = "Flappy Bird"
            };

            MainWindow = mainWindow;
            mainWindow.Show();
        }
    }

}
