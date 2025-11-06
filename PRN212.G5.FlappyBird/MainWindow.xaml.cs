using FlappyBird.Business.Models;
using FlappyBird.Data.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PRN212.G5.FlappyBird.Views
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer gameTimer = new();
        private readonly GameRepo gameRepo = new();   // Dùng GameRepo để load/save điểm
        private readonly AccountRepo accountRepo = new();
        private Account currentAccount;

        private double birdSpeed = 0;
        private int score = 0;
        private int highScore = 0;
        private readonly Random rnd = new();

        private readonly List<Rectangle> pipesTop = new();
        private readonly List<Rectangle> pipesBottom = new();
        private readonly List<Rectangle> clouds = new(); // danh sách đám mây

        private double pipeSpeed = 5; // tốc độ di chuyển ống
        private const int gap = 190;  // khoảng cách giữa ống trên/dưới
        private double cloudSpeed = 2; // tốc độ mây (chậm hơn pipe)

        private SoundPlayer? jumpSound;
        private SoundPlayer? hitSound;

        private bool isGameOver = false;
        private bool isPlaying = false;

        // Đẩy lứa ống đầu tiên ra xa + “grace period” sau khi bấm Play
        private const double FirstPipeStartLeft = 1100; // vị trí ống đầu tiên (xa hơn để chim bay 1 đoạn)
        private const double PipeSpacing = 320;         // khoảng cách giữa các cặp ống lúc khởi tạo & respawn
        private const int StartGraceTicks = 60;         // ~1.2s nếu Interval = 20ms
        private int graceTicksRemaining = 0;

        // Kích thước sân chơi và ống
        private const int CanvasHeight = 500;
        private const int PipeWidth = 80;

        public MainWindow(Account account)
        {
            InitializeComponent();
            currentAccount = account;
            this.KeyDown += Window_KeyDown;

            //jumpSound = new SoundPlayer("jump.wav");
            //hitSound  = new SoundPlayer("hit.wav");

            gameTimer.Interval = TimeSpan.FromMilliseconds(20);
            gameTimer.Tick += GameLoop;

            // Update UI with account info
            UserNameText.Text = currentAccount.Name;
            Title = $"Flappy Bird - {currentAccount.Name}";
            LoadUserAvatar();

            // Đảm bảo StartButton có event handler
            if (StartButton != null)
            {
                StartButton.Click -= BtnStart_Click; // Remove để tránh duplicate
                StartButton.Click += BtnStart_Click;
            }

            ShowStartScreen();
        }

        // =================== FLOW UI ===================
        private void ShowStartScreen()
        {
            isPlaying = false;
            isGameOver = false;
            gameTimer.Stop();

            // Xóa toàn bộ vật thể động để về “trạng thái ban đầu”
            ClearDynamicObjects();

            // Reset điểm và hiển thị
            score = 0;
            ScoreText.Text = "Score: 0";

            // Ẩn điểm ở Start screen (nếu muốn hiện HighScore ở Start, để Visible ở đây)
            ScoreText.Visibility = Visibility.Collapsed;
            HighScoreText.Visibility = Visibility.Collapsed;

            // Load và hiển thị high score từ account
            highScore = currentAccount.HighScore;
            HighScoreText.Text = $"High Score: {highScore}";

            // Đưa chim về vị trí gốc và dừng rơi
            Canvas.SetLeft(FlappyBird, 70);
            Canvas.SetTop(FlappyBird, 247);
            birdSpeed = 0;

            // Hiển thị StartPanel, ẩn GameOverPanel
            if (StartPanel != null) StartPanel.Visibility = Visibility.Visible;
            if (StartPanelUI != null) StartPanelUI.Visibility = Visibility.Visible;
            if (GameOverPanel != null) GameOverPanel.Visibility = Visibility.Collapsed;
        }

        private void StartGame()
        {
            try
            {
                isGameOver = false;
                isPlaying = true;

                // Ẩn StartPanel và StartPanelUI
                if (StartPanel != null)
                {
                    StartPanel.Visibility = Visibility.Collapsed;
                    StartPanel.IsHitTestVisible = false;
                }
                if (StartPanelUI != null)
                {
                    StartPanelUI.Visibility = Visibility.Collapsed;
                }
                
                // Đảm bảo GameCanvas có thể nhận input
                if (GameCanvas != null)
                {
                    GameCanvas.Focusable = true;
                    GameCanvas.Focus();
                }
                
                if (GameOverPanel != null)
                {
                    GameOverPanel.Visibility = Visibility.Collapsed;
                }

                // Hiện điểm khi người dùng bấm Play
                if (ScoreText != null) ScoreText.Visibility = Visibility.Visible;
                if (HighScoreText != null) HighScoreText.Visibility = Visibility.Visible;

                // Reset state
                if (FlappyBird != null)
                {
                    Canvas.SetLeft(FlappyBird, 70);
                    Canvas.SetTop(FlappyBird, 247);
                }
                birdSpeed = 0;
                score = 0;
                pipeSpeed = 5;
                if (ScoreText != null) ScoreText.Text = "Score: 0";

                // High score từ account
                highScore = currentAccount.HighScore;
                if (HighScoreText != null) HighScoreText.Text = $"High Score: {highScore}";

                // Clear old objects & tạo lại
                ClearDynamicObjects();
                CreateClouds();
                CreateInitialPipes(count: 4);

                // Bật "grace period": tạm bỏ qua va chạm một lúc sau khi Play
                graceTicksRemaining = StartGraceTicks;

                // Start game timer
                if (gameTimer != null)
                {
                    gameTimer.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi bắt đầu game: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EndGame()
        {
            if (isGameOver) return;
            isGameOver = true;
            isPlaying = false;

            gameTimer.Stop();
            //hitSound?.Play();

            // Cập nhật high score vào account
            if (score > highScore)
            {
                highScore = score;
                accountRepo.UpdateHighScore(currentAccount.Email, highScore);
                // Update local account object
                currentAccount.HighScore = highScore;
            }

            // Hiển thị overlay Game Over
            GoScoreValue.Text = score.ToString();
            GoBestScoreValue.Text = highScore.ToString();
            GameOverPanel.Visibility = Visibility.Visible;
        }

        // =================== BUILD SCENE ===================
        private void ClearDynamicObjects()
        {
            foreach (var p in pipesTop) GameCanvas.Children.Remove(p);
            foreach (var p in pipesBottom) GameCanvas.Children.Remove(p);
            foreach (var c in clouds) GameCanvas.Children.Remove(c);

            pipesTop.Clear();
            pipesBottom.Clear();
            clouds.Clear();
        }

        private void CreateClouds()
        {
            for (int i = 0; i < 4; i++)
            {
                Rectangle cloud = new()
                {
                    Width = rnd.Next(80, 150),
                    Height = rnd.Next(30, 60),
                    Fill = Brushes.White,
                    RadiusX = 20,
                    RadiusY = 20,
                    Opacity = 0.8
                };
                GameCanvas.Children.Add(cloud);
                Canvas.SetLeft(cloud, 200 + i * 250);
                Canvas.SetTop(cloud, rnd.Next(20, 150));
                clouds.Add(cloud);
            }
        }

        private void CreateInitialPipes(int count)
        {
            for (int i = 0; i < count; i++)
            {
                double leftPos = FirstPipeStartLeft + i * PipeSpacing;
                CreatePipePair(leftPos);
            }
        }

        private void CreatePipePair(double leftPos)
        {
            var top = new Rectangle { Width = PipeWidth, Fill = Brushes.Green };
            var bottom = new Rectangle { Width = PipeWidth, Fill = Brushes.Green };

            GameCanvas.Children.Add(top);
            GameCanvas.Children.Add(bottom);

            Canvas.SetLeft(top, leftPos);
            Canvas.SetLeft(bottom, leftPos);

            RandomizePipe(top, bottom);

            pipesTop.Add(top);
            pipesBottom.Add(bottom);
        }

        private void RandomizePipe(Rectangle top, Rectangle bottom)
        {
            // Random 1 lần khi tạo/respawn; KHÔNG dao động theo thời gian
            int minTop = 50;
            int maxTop = CanvasHeight - gap - 50; // để bottom >= 50
            double topHeight = rnd.Next(minTop, maxTop + 1);

            top.Height = topHeight;
            bottom.Height = CanvasHeight - topHeight - gap;

            Canvas.SetTop(top, 0);
            Canvas.SetTop(bottom, top.Height + gap);
        }

        // =================== GAME LOOP ===================
        private void GameLoop(object? sender, EventArgs e)
        {
            if (!isPlaying || isGameOver) return;

            double birdTop = Canvas.GetTop(FlappyBird);
            Canvas.SetTop(FlappyBird, birdTop + birdSpeed);
            birdSpeed += 1; // trọng lực

            double speed = pipeSpeed + score * 0.1;

            if (graceTicksRemaining > 0) graceTicksRemaining--;

            // Clouds
            foreach (var c in clouds)
            {
                Canvas.SetLeft(c, Canvas.GetLeft(c) - cloudSpeed);
                if (Canvas.GetLeft(c) < -150)
                {
                    Canvas.SetLeft(c, 1000 + rnd.Next(0, 200));
                    Canvas.SetTop(c, rnd.Next(20, 150));
                }
            }

            // Pipes
            for (int i = 0; i < pipesTop.Count; i++)
            {
                // Dịch trái đều
                Canvas.SetLeft(pipesTop[i], Canvas.GetLeft(pipesTop[i]) - speed);
                Canvas.SetLeft(pipesBottom[i], Canvas.GetLeft(pipesBottom[i]) - speed);

                // Khi ống đi ra khỏi màn hình, respawn ở farthestRight + spacing để khoảng cách đều
                if (Canvas.GetLeft(pipesTop[i]) < -PipeWidth)
                {
                    double farthestRight = double.MinValue;
                    for (int j = 0; j < pipesTop.Count; j++)
                    {
                        if (j == i) continue;
                        farthestRight = Math.Max(farthestRight, Canvas.GetLeft(pipesTop[j]));
                    }
                    double newX = (farthestRight == double.MinValue)
                        ? FirstPipeStartLeft
                        : farthestRight + PipeSpacing;

                    Canvas.SetLeft(pipesTop[i], newX);
                    Canvas.SetLeft(pipesBottom[i], newX);

                    // Random lại vertical 1 lần khi respawn (KHÔNG dao động)
                    RandomizePipe(pipesTop[i], pipesBottom[i]);

                    // Cộng điểm
                    score++;
                    ScoreText.Text = $"Score: {score}";
                }

                // Kiểm tra va chạm (bỏ qua khi còn grace period)
                if (graceTicksRemaining <= 0 &&
                    (FlappyBird.CollidesWith(pipesTop[i]) || FlappyBird.CollidesWith(pipesBottom[i])))
                {
                    EndGame();
                    return;
                }
            }

            // Chim rơi khỏi canvas hoặc chạm trần (bỏ qua khi còn grace period)
            if (graceTicksRemaining <= 0 &&
                (birdTop < 0 || birdTop + FlappyBird.Height > CanvasHeight))
            {
                EndGame();
            }
        }

        // =================== INPUT ===================
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Chỉ cho nhảy khi đang chơi
            if (!isPlaying || isGameOver) return;

            if (e.Key == Key.Space)
            {
                birdSpeed = -10;
                jumpSound?.Play();
            }
        }

        // =================== BUTTONS ===================
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StartGame();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in BtnStart_Click: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnReplay_Click(object sender, RoutedEventArgs e) => StartGame();
        private void BtnLeft_Click(object sender, RoutedEventArgs e) => ShowStartScreen();

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var profileWindow = new ProfileWindow(currentAccount);
            if (profileWindow.ShowDialog() == true && profileWindow.UpdatedAccount != null)
            {
                // Update account info
                currentAccount = profileWindow.UpdatedAccount;
                UserNameText.Text = currentAccount.Name;
                Title = $"Flappy Bird - {currentAccount.Name}";
                
                // Refresh avatar
                LoadUserAvatar();
                
                // Refresh high score display
                highScore = currentAccount.HighScore;
                HighScoreText.Text = $"High Score: {highScore}";
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Bạn có chắc chắn muốn đăng xuất?", "Xác nhận", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                // Stop game if playing
                if (isPlaying || isGameOver)
                {
                    gameTimer.Stop();
                    isPlaying = false;
                    isGameOver = false;
                    ClearDynamicObjects();
                }
                
                // Hide current game window
                this.Hide();
                
                // Show login window
                var loginWindow = new LoginWindow();
                if (loginWindow.ShowDialog() == true && loginWindow.LoggedInAccount != null)
                {
                    // Update account and restart game
                    currentAccount = loginWindow.LoggedInAccount;
                    UserNameText.Text = currentAccount.Name;
                    Title = $"Flappy Bird - {currentAccount.Name}";
                    LoadUserAvatar();
                    highScore = currentAccount.HighScore;
                    HighScoreText.Text = $"High Score: {highScore}";
                    
                    // Reset game state
                    score = 0;
                    ScoreText.Text = "Score: 0";
                    ShowStartScreen();
                    
                    // Show window again
                    this.Show();
                }
                else
                {
                    // User closed login - close app
                    this.Close();
                    Application.Current.Shutdown();
                }
            }
        }

        private void LoadUserAvatar()
        {
            try
            {
                if (!string.IsNullOrEmpty(currentAccount.Avatar))
                {
                    byte[] imageBytes = Convert.FromBase64String(currentAccount.Avatar);
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new MemoryStream(imageBytes);
                    bitmap.DecodePixelWidth = 64;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    UserAvatarImage.Source = bitmap;
                    DefaultAvatarText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    UserAvatarImage.Source = null;
                    DefaultAvatarText.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                UserAvatarImage.Source = null;
                DefaultAvatarText.Visibility = Visibility.Visible;
            }
        }
    }

    // Collision helper
    public static class CollisionExtensions
    {
        public static bool CollidesWith(this Rectangle a, Rectangle b)
        {
            if (a == null || b == null) return false;

            double aLeft = Canvas.GetLeft(a);
            double aTop = Canvas.GetTop(a);
            double bLeft = Canvas.GetLeft(b);
            double bTop = Canvas.GetTop(b);

            Rect rectA = new(aLeft, aTop, a.Width, a.Height);
            Rect rectB = new(bLeft, bTop, b.Width, b.Height);

            return rectA.IntersectsWith(rectB);
        }
    }
}