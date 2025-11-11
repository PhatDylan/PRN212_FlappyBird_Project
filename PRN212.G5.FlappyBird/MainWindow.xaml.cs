using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;

namespace PRN212.G5.FlappyBird.Views
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer gameTimer = new();
        private readonly DispatcherTimer birdAnimTimer = new();
        private readonly DispatcherTimer dayNightTimer = new();
        private readonly string playerName = "Player";
        private const string HighScoreFilePath = "highscore.txt";


        private BitmapImage[] dayBirdFlyFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] dayBirdFallFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] nightBirdFlyFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] nightBirdFallFrames = Array.Empty<BitmapImage>();

        private BitmapImage[] currentFlyFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] currentFallFrames = Array.Empty<BitmapImage>();

        private int birdFrameIndex = 0;
        private bool isFallingAnimation = false;

        private double birdSpeed = 0;
        private int score = 0;
        private int highScore = 0;
        private readonly Random rnd = new();

        private readonly List<Image> pipesTop = new();
        private readonly List<Image> pipesBottom = new();
        private readonly List<Image> clouds = new();

        private const double DefaultPipeSpeed = 5;
        private const double MinPipeSpeed = 3;
        private const double MaxPipeSpeed = 10;

        private double pipeSpeed = DefaultPipeSpeed;
        private double selectedPipeSpeed = DefaultPipeSpeed;
        private const int gap = 190;
        private double cloudSpeed = 2;

        private bool isGameOver = false;
        private bool isPlaying = false;
        private bool isNight = false;

        private const double FirstPipeStartLeft = 1100;
        private const double PipeSpacing = 320;
        private const int StartGraceTicks = 60;
        private int graceTicksRemaining = 0;

        private const int CanvasHeight = 500;
        private const int PipeWidth = 80;

        private bool isTransitioning = false;
        private readonly MediaPlayer bgmPlayer = new();
        private readonly MediaPlayer sfxJump = new();
        private readonly MediaPlayer sfxPoint = new();
        private readonly MediaPlayer sfxFail = new();

        public MainWindow(double initialPipeSpeed)
        {
            InitializeComponent();

            selectedPipeSpeed = Math.Clamp(initialPipeSpeed, MinPipeSpeed, MaxPipeSpeed);
            pipeSpeed = selectedPipeSpeed;

            gameTimer.Interval = TimeSpan.FromMilliseconds(20);
            gameTimer.Tick += GameLoop;

            birdAnimTimer.Interval = TimeSpan.FromMilliseconds(120);
            birdAnimTimer.Tick += BirdAnimTick;

            dayNightTimer.Interval = TimeSpan.FromMinutes(0.50);
            dayNightTimer.Tick += (_, __) => SmoothToggleDayNight();

            highScore = LoadHighScore();
            Title = $"Flappy Bird - {playerName}";

            LoadAllBirdFrames();
            UseBirdFramesForTheme(false);

            Loaded += MainWindow_OnLoaded;
        }
        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            StartGame();
        }

        private string Pack(string file) => $"pack://application:,,,/Assets/{file}";
        private string AssetPath(string file) => System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", file);

        private void PlayBgm()
        {
                bgmPlayer.Open(new Uri(AssetPath("BGM.mp3")));
                bgmPlayer.Volume = 0.36;
                bgmPlayer.MediaEnded -= Bgm_Loop;
                bgmPlayer.MediaEnded += Bgm_Loop;
                bgmPlayer.Position = TimeSpan.Zero;
                bgmPlayer.Play();
        }

        private void Bgm_Loop(object? sender, EventArgs e)
        {
                bgmPlayer.Position = TimeSpan.Zero;
                bgmPlayer.Play();
        }

        private void StopBgm()
        {
                bgmPlayer.MediaEnded -= Bgm_Loop;
                bgmPlayer.Stop();
        }

        private void PlaySfx(MediaPlayer player, string file, double volume = 0.6)
        {
                player.Stop();
                player.Open(new Uri(AssetPath(file)));
                player.Volume = volume;
                player.Position = TimeSpan.Zero;
                player.Play();
        }

        private void LoadAllBirdFrames()
        {
            dayBirdFlyFrames = new[]
            {
                LoadBitmapSafe("birdfly-1.png"),
            };
            dayBirdFallFrames = new[]
            {
                LoadBitmapSafe("birdfall-1.png"),
            };

            nightBirdFlyFrames = new[]
            {
                LoadBitmapSafe("birdfly-3.png")
            };
            nightBirdFallFrames = new[]
            {
                LoadBitmapSafe("birdfall-3.png")
            };

            if (HasMissing(nightBirdFlyFrames) || HasMissing(nightBirdFallFrames))
            {
                nightBirdFlyFrames = dayBirdFlyFrames;
                nightBirdFallFrames = dayBirdFallFrames;
            }
        }

        private bool HasMissing(BitmapImage[] arr)
        {
            foreach (var image in arr)
                if (image == null) return true;
            return false;
        }

        private BitmapImage LoadBitmapSafe(string file)
        {
            try
            {
                return new BitmapImage(new Uri(Pack(file)));
            }
            catch
            {
                return null!;
            }
        }

        private void UseBirdFramesForTheme(bool night)
        {
            currentFlyFrames = night ? nightBirdFlyFrames : dayBirdFlyFrames;
            currentFallFrames = night ? nightBirdFallFrames : dayBirdFallFrames;

            birdFrameIndex = 0;
            isFallingAnimation = false;

            if (currentFlyFrames.Length > 0 && currentFlyFrames[0] != null)
                FlappyBird.Source = currentFlyFrames[0];
        }

        private void StartGame()
        {
            ResetStageToDay();

            isGameOver = false;
            isPlaying = true;

            GameOverPanel.Visibility = Visibility.Collapsed;

            ScoreText.Visibility = Visibility.Visible;
            HighScoreText.Visibility = Visibility.Visible;

            Canvas.SetLeft(FlappyBird, 70);
            Canvas.SetTop(FlappyBird, 247);
            Panel.SetZIndex(FlappyBird, 5);
            birdSpeed = 0;
            score = 0;
            pipeSpeed = selectedPipeSpeed;
            ScoreText.Text = "Score: 0";

            HighScoreText.Text = $"High Score: {highScore}";

            ClearDynamicObjects();
            CreateClouds();
            CreateInitialPipes(4);

            graceTicksRemaining = StartGraceTicks;

            isFallingAnimation = false;
            birdFrameIndex = 0;
            UseBirdFramesForTheme(false);

            gameTimer.Start();
            birdAnimTimer.Start();
            dayNightTimer.Start();
            PlayBgm();
        }

        private void ResetStageToDay(bool animate = false)
        {
            isTransitioning = false;

            DayLayer.BeginAnimation(UIElement.OpacityProperty, null);
            NightLayer.BeginAnimation(UIElement.OpacityProperty, null);

            if (animate)
            {
                var dur = TimeSpan.FromSeconds(0.3);

                var dayAnim = new DoubleAnimation
                {
                    To = 1,
                    Duration = dur,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                var nightAnim = new DoubleAnimation
                {
                    To = 0,
                    Duration = dur,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                dayAnim.Completed += (_, __) =>
                {
                    isNight = false;
                    UpdateDynamicAssets(false);
                    UseBirdFramesForTheme(false);
                };

                DayLayer.BeginAnimation(UIElement.OpacityProperty, dayAnim);
                NightLayer.BeginAnimation(UIElement.OpacityProperty, nightAnim);
            }
            else
            {
                DayLayer.Opacity = 1;
                NightLayer.Opacity = 0;
                isNight = false;
                UpdateDynamicAssets(false);
                UseBirdFramesForTheme(false);
            }
        }

        private int LoadHighScore()
        {
            try
            {
                if (File.Exists(HighScoreFilePath) &&
                    int.TryParse(File.ReadAllText(HighScoreFilePath), out var stored) &&
                    stored >= 0)
                {
                    return stored;
                }
            }
            catch
            {
                // ignore and fallback to zero
            }

            return 0;
        }

        private void SaveHighScore(int score)
        {
            try
            {
                File.WriteAllText(HighScoreFilePath, Math.Max(0, score).ToString());
            }
            catch
            {
                // ignore persistence errors
            }
        }

        private void EndGame()
        {
            if (isGameOver) return;
            isGameOver = true;
            isPlaying = false;

            gameTimer.Stop();
            dayNightTimer.Stop();
            StopBgm();

            if (score > highScore)
            {
                highScore = score;
                SaveHighScore(highScore);
                if (HighScoreText != null) HighScoreText.Text = $"High Score: {highScore}";
            }

            GoScoreValue.Text = score.ToString();
            GoBestScoreValue.Text = highScore.ToString();
            GameOverPanel.Visibility = Visibility.Visible;

            isFallingAnimation = true;
            birdFrameIndex = 0;
            PlaySfx(sfxFail, "Fail.mp3", 0.7);
        }

        private void SmoothToggleDayNight()
        {
            if (isTransitioning) return;
            isTransitioning = true;

            bool targetNight = !isNight;
            var dur = TimeSpan.FromSeconds(0.8);

            var dayAnim = new DoubleAnimation
            {
                To = targetNight ? 0 : 1,
                Duration = dur,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            var nightAnim = new DoubleAnimation
            {
                To = targetNight ? 1 : 0,
                Duration = dur,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            dayAnim.Completed += (_, __) =>
            {
                isTransitioning = false;
                isNight = targetNight;

                UpdateDynamicAssets(targetNight);
                UseBirdFramesForTheme(targetNight);
            };

            DayLayer.BeginAnimation(UIElement.OpacityProperty, dayAnim);
            NightLayer.BeginAnimation(UIElement.OpacityProperty, nightAnim);
        }

        private void UpdateDynamicAssets(bool night)
        {
            string pipeFile = night ? "Pipe-night.png" : "Pipe-day.png";
            string cloudFile = night ? "Cloud-Night.png" : "Cloud-Day.png";

            foreach (var pipe in pipesTop)
                pipe.Source = new BitmapImage(new Uri(Pack(pipeFile)));
            foreach (var pipe in pipesBottom)
                pipe.Source = new BitmapImage(new Uri(Pack(pipeFile)));
            foreach (var cloud in clouds)
                cloud.Source = new BitmapImage(new Uri(Pack(cloudFile)));
        }

        private void ClearDynamicObjects()
        {
            foreach (var pipe in pipesTop) GameCanvas.Children.Remove(pipe);
            foreach (var pipe in pipesBottom) GameCanvas.Children.Remove(pipe);
            foreach (var cloud in clouds) GameCanvas.Children.Remove(cloud);
            pipesTop.Clear();
            pipesBottom.Clear();
            clouds.Clear();
        }

        private void CreateClouds()
        {
            string cloudFile = isNight ? "Cloud-Night.png" : "Cloud-Day.png";
            for (int i = 0; i < 4; i++)
            {
                var cloud = new Image
                {
                    Width = rnd.Next(110, 180),
                    Height = rnd.Next(50, 90),
                    Source = new BitmapImage(new Uri(Pack(cloudFile))),
                    Stretch = Stretch.Fill,
                    Opacity = 0.9,
                    SnapsToDevicePixels = true
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
                CreatePipePair(FirstPipeStartLeft + i * PipeSpacing);
        }

        private void CreatePipePair(double leftPos)
        {
            string pipeFile = isNight ? "Pipe-night.png" : "Pipe-day.png";

            var top = new Image
            {
                Width = PipeWidth,
                Stretch = Stretch.Fill,
                Source = new BitmapImage(new Uri(Pack(pipeFile))),
                SnapsToDevicePixels = true
            };
            var bottom = new Image
            {
                Width = PipeWidth,
                Stretch = Stretch.Fill,
                Source = new BitmapImage(new Uri(Pack(pipeFile))),
                SnapsToDevicePixels = true
            };

            GameCanvas.Children.Add(top);
            GameCanvas.Children.Add(bottom);
            Canvas.SetLeft(top, leftPos);
            Canvas.SetLeft(bottom, leftPos);

            RandomizePipe(top, bottom);

            pipesTop.Add(top);
            pipesBottom.Add(bottom);
        }

        private void RandomizePipe(Image top, Image bottom)
        {
            int minTopHeight = 50;
            int maxTopHeight = 590 - gap - 50;
            double topHeight = rnd.Next(minTopHeight, maxTopHeight + 1);

            top.Height = topHeight;
            Canvas.SetTop(top, 0);

            double bottomTop = topHeight + gap;
            bottom.Height = 590 - bottomTop;
            Canvas.SetTop(bottom, bottomTop);

            Panel.SetZIndex(top, 3);
            Panel.SetZIndex(bottom, 3);
        }

        private void GameLoop(object? sender, EventArgs e)
        {
            if (!isPlaying || isGameOver) return;

            double birdTop = Canvas.GetTop(FlappyBird);
            Canvas.SetTop(FlappyBird, birdTop + birdSpeed);
            birdSpeed += 1;

            double speed = pipeSpeed + score * 0.1;
            if (graceTicksRemaining > 0) graceTicksRemaining--;

            foreach (var cloud in clouds)
            {
                Canvas.SetLeft(cloud, Canvas.GetLeft(cloud) - cloudSpeed);
                if (Canvas.GetLeft(cloud) < -150)
                {
                    Canvas.SetLeft(cloud, 1000 + rnd.Next(0, 200));
                    Canvas.SetTop(cloud, rnd.Next(20, 150));
                }
            }

            for (int i = 0; i < pipesTop.Count; i++)
            {
                Canvas.SetLeft(pipesTop[i], Canvas.GetLeft(pipesTop[i]) - speed);
                Canvas.SetLeft(pipesBottom[i], Canvas.GetLeft(pipesBottom[i]) - speed);

                if (Canvas.GetLeft(pipesTop[i]) < -PipeWidth)
                {
                    double farthestRight = double.MinValue;
                    for (int j = 0; j < pipesTop.Count; j++)
                        if (j != i)
                            farthestRight = Math.Max(farthestRight, Canvas.GetLeft(pipesTop[j]));

                    double newX = farthestRight == double.MinValue ? FirstPipeStartLeft : farthestRight + PipeSpacing;
                    Canvas.SetLeft(pipesTop[i], newX);
                    Canvas.SetLeft(pipesBottom[i], newX);

                    RandomizePipe(pipesTop[i], pipesBottom[i]);

                    score++;
                    ScoreText.Text = $"Score: {score}";
                    PlaySfx(sfxPoint, "Point.mp3", 0.6);
                }

                if (graceTicksRemaining <= 0 &&
                    (FlappyBird.CollidesWith(pipesTop[i]) || FlappyBird.CollidesWith(pipesBottom[i])))
                {
                    EndGame();
                    return;
                }
            }

            double currentBirdTop = Canvas.GetTop(FlappyBird);

            if (currentBirdTop < 0)
            {
                Canvas.SetTop(FlappyBird, 0);
                birdSpeed = 0;
            }

            double groundLevel = CanvasHeight - FlappyBird.Height;
            if (currentBirdTop > groundLevel)
            {
                Canvas.SetTop(FlappyBird, groundLevel);
                birdSpeed = 0;
            }
        }

        private void BirdAnimTick(object? sender, EventArgs e)
        {
            var frames = isFallingAnimation ? currentFallFrames : currentFlyFrames;

            if (frames == null || frames.Length == 0) return;

            birdFrameIndex = (birdFrameIndex + 1) % frames.Length;

            if (frames[birdFrameIndex] != null)
                FlappyBird.Source = frames[birdFrameIndex];
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!isPlaying || isGameOver) return;

            if (e.Key == Key.Space)
            {
                birdSpeed = -10;
                PlaySfx(sfxJump, "Jump.mp3", 0.5);
            }
            else if (e.Key == Key.N)
            {
                SmoothToggleDayNight();
            }
        }

        private void BtnReplay_Click(object sender, RoutedEventArgs e)
        {
            StopGameLoops();
            GameOverPanel.Visibility = Visibility.Collapsed;

            ResetStageToDay(true);
            StartGame();
        }

        private void BtnLeft_Click(object sender, RoutedEventArgs e)
        {
            StopGameLoops();

            var loginWindow = new LoginWindow(selectedPipeSpeed);

            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();

            Close();
        }

        private void StopGameLoops()
        {
            gameTimer.Stop();
            birdAnimTimer.Stop();
            dayNightTimer.Stop();
            StopBgm();
        }
    }

    public static class CollisionExtensions
    {
        public static bool CollidesWith(this FrameworkElement a, FrameworkElement b)
        {
            if (a == null || b == null) return false;

            double aLeft = Canvas.GetLeft(a);
            double aTop = Canvas.GetTop(a);
            double bLeft = Canvas.GetLeft(b);
            double bTop = Canvas.GetTop(b);

            double aWidth = double.IsNaN(a.Width) ? a.ActualWidth : a.Width;
            double aHeight = double.IsNaN(a.Height) ? a.ActualHeight : a.Height;
            double bWidth = double.IsNaN(b.Width) ? b.ActualWidth : b.Width;
            double bHeight = double.IsNaN(b.Height) ? b.ActualHeight : b.Height;

            Rect rectA = new(aLeft, aTop, Math.Max(1, aWidth), Math.Max(1, aHeight));
            Rect rectB = new(bLeft, bTop, Math.Max(1, bWidth), Math.Max(1, bHeight));

            return rectA.IntersectsWith(rectB);
        }
    }
}
















