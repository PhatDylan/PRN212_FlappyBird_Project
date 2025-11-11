using FlappyBird.Business.Models;
using FlappyBird.Data.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace PRN212.G5.FlappyBird.Views
{
    public partial class LeaderboardWindow : Window
    {
        private readonly AccountRepo accountRepo = new();
        private readonly Account currentAccount;

        public LeaderboardWindow(Account currentAccount)
        {
            InitializeComponent();
            this.currentAccount = currentAccount;
            LoadLeaderboard();
        }

        private void LoadLeaderboard()
        {
            var topAccounts = accountRepo.GetTopScores(10);

            if (topAccounts == null || topAccounts.Count == 0)
            {
                LeaderboardItemsControl.Visibility = Visibility.Collapsed;
                EmptyMessageText.Visibility = Visibility.Visible;
                return;
            }

            LeaderboardItemsControl.Visibility = Visibility.Visible;
            EmptyMessageText.Visibility = Visibility.Collapsed;

            var leaderboardItems = topAccounts.Select((account, index) => new LeaderboardItem
            {
                Rank = index + 1,
                Name = account.Name,
                Email = account.Email,
                HighScore = account.HighScore,
                IsCurrentUser = account.Email.Equals(currentAccount.Email, System.StringComparison.OrdinalIgnoreCase)
            }).ToList();

            LeaderboardItemsControl.ItemsSource = leaderboardItems;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class LeaderboardItem
    {
        public int Rank { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int HighScore { get; set; }
        public bool IsCurrentUser { get; set; }
    }
}

