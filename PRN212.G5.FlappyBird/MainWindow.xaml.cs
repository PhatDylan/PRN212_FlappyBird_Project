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
        private BitmapImage[] dayBirdDeathFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] nightBirdFlyFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] nightBirdFallFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] nightBirdDeathFrames = Array.Empty<BitmapImage>();

        private BitmapImage[] currentFlyFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] currentFallFrames = Array.Empty<BitmapImage>();
        private BitmapImage[] currentDeathFrames = Array.Empty<BitmapImage>();

        private int birdFrameIndex = 0;
        private BirdAnimationState animationState = BirdAnimationState.Flying;

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

        // Animation constants
        private const double FlapThreshold = -3; // Speed threshold to switch to flapping animation
        private const double FallThreshold = 2; // Speed threshold to switch to falling animation
        private double birdRotation = 0;
        private const double MaxUpRotation = -30;
        private const double MaxDownRotation = 90;

        public MainWindow(double initialPipeSpeed)
        {
            InitializeComponent();

            selectedPipeSpeed = Math.Clamp(initialPipeSpeed, MinPipeSpeed, MaxPipeSpeed);
            pipeSpeed = selectedPipeSpeed;

            gameTimer.Interval = TimeSpan.FromMilliseconds(20);
            gameTimer.Tick += GameLoop;

            birdAnimTimer.Interval = TimeSpan.FromMilliseconds(100);
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

        private void LoadAllBirdFrames()
        {
            // Day theme - Fly animation (using single reliable frame)
            var birdFly1 = LoadBitmapSafe("birdfly-1.png");
            var birdFall1 = LoadBitmapSafe("birdfall-1.png");

            // Only use frames that actually exist
            if (birdFly1 != null)
            {
                dayBirdFlyFrames = new[] { birdFly1 };
            }
            else
            {
                // Fallback to a default if birdfly-1.png is missing
                dayBirdFlyFrames = new BitmapImage[0];
            }

            if (birdFall1 != null)
            {
                dayBirdFallFrames = new[] { birdFall1 };
                dayBirdDeathFrames = new[] { birdFall1 };
            }
            else
            {
                dayBirdFallFrames = new BitmapImage[0];
                dayBirdDeathFrames = new BitmapImage[0];
            }

            // Night theme - Try to load night frames, fallback to day if missing
            var birdFly3 = LoadBitmapSafe("birdfly-3.png");
            var birdFall3 = LoadBitmapSafe("birdfall-3.png");

            if (birdFly3 != null)
            {
                nightBirdFlyFrames = new[] { birdFly3 };
            }
            else
            {
                nightBirdFlyFrames = dayBirdFlyFrames;
            }

            if (birdFall3 != null)
            {
                nightBirdFallFrames = new[] { birdFall3 };
                nightBirdDeathFrames = new[] { birdFall3 };
            }
            else
            {
                nightBirdFallFrames = dayBirdFallFrames;
                nightBirdDeathFrames = dayBirdDeathFrames;
            }
        }

        private BitmapImage[] FilterNullFrames(BitmapImage[] frames)
        {
            var validFrames = new List<BitmapImage>();
            foreach (var frame in frames)
            {
                if (frame != null)
                    validFrames.Add(frame);
            }
            return validFrames.Count > 0 ? validFrames.ToArray() : frames;
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
            currentDeathFrames = night ? nightBirdDeathFrames : dayBirdDeathFrames;

            birdFrameIndex = 0;
            animationState = BirdAnimationState.Flying;

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
            birdRotation = 0;
            UpdateBirdRotation();
            score = 0;
            pipeSpeed = selectedPipeSpeed;
            ScoreText.Text = "Score: 0";

            HighScoreText.Text = $"High Score: {highScore}";

            ClearDynamicObjects();
            CreateClouds();
            CreateInitialPipes(4);

            graceTicksRemaining = StartGraceTicks;

            animationState = BirdAnimationState.Flying;
            birdFrameIndex = 0;
            UseBirdFramesForTheme(false);

            gameTimer.Start();
            birdAnimTimer.Start();
            dayNightTimer.Start();
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

            if (score > highScore)
            {
                highScore = score;
                SaveHighScore(highScore);
                if (HighScoreText != null) HighScoreText.Text = $"High Score: {highScore}";
            }

            GoScoreValue.Text = score.ToString();
            GoBestScoreValue.Text = highScore.ToString();
            GameOverPanel.Visibility = Visibility.Visible;

            // Switch to death animation
            animationState = BirdAnimationState.Dead;
            birdFrameIndex = 0;
            
            // Animate bird falling down with rotation
            AnimateBirdDeath();
        }

        private void AnimateBirdDeath()
        {
            // Create a falling animation for the dead bird
            var fallDuration = TimeSpan.FromSeconds(0.8);
            var currentTop = Canvas.GetTop(FlappyBird);
            var groundLevel = CanvasHeight - FlappyBird.Height;

            var fallAnim = new DoubleAnimation
            {
                From = currentTop,
                To = groundLevel,
                Duration = fallDuration,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            // Rotate bird to falling position
            var rotateAnim = new DoubleAnimation
            {
                From = birdRotation,
                To = MaxDownRotation,
                Duration = fallDuration,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            rotateAnim.Completed += (_, __) =>
            {
                birdRotation = MaxDownRotation;
            };

            var rt = FlappyBird.RenderTransform as RotateTransform;
            if (rt != null)
            {
                rt.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
            }

            FlappyBird.BeginAnimation(Canvas.TopProperty, fallAnim);
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

            // Update bird animation state based on speed
            UpdateBirdAnimationState();
            
            // Update bird rotation based on speed
            UpdateBirdRotation();

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
                EndGame();
            }
        }

        private void UpdateBirdAnimationState()
        {
            if (animationState == BirdAnimationState.Dead)
                return;

            // Determine animation state based on bird speed
            if (birdSpeed < FlapThreshold)
            {
                // Bird is going up - show flying animation
                if (animationState != BirdAnimationState.Flying)
                {
                    animationState = BirdAnimationState.Flying;
                    birdFrameIndex = 0;
                }
            }
            else if (birdSpeed > FallThreshold)
            {
                // Bird is falling - show falling animation
                if (animationState != BirdAnimationState.Falling)
                {
                    animationState = BirdAnimationState.Falling;
                    birdFrameIndex = 0;
                }
            }
        }

        private void UpdateBirdRotation()
        {
            if (animationState == BirdAnimationState.Dead)
                return;

            // Calculate rotation based on bird speed
            // Negative speed (going up) = rotate up
            // Positive speed (falling) = rotate down
            double targetRotation = Math.Clamp(birdSpeed * 3, MaxUpRotation, MaxDownRotation);
            
            // Smooth rotation transition
            birdRotation += (targetRotation - birdRotation) * 0.2;

            var rt = FlappyBird.RenderTransform as RotateTransform;
            if (rt != null)
            {
                rt.Angle = birdRotation;
            }
        }

        private void BirdAnimTick(object? sender, EventArgs e)
        {
            BitmapImage[] frames;

            // Select frames based on animation state
            switch (animationState)
            {
                case BirdAnimationState.Flying:
                    frames = currentFlyFrames;
                    break;
                case BirdAnimationState.Falling:
                    frames = currentFallFrames;
                    break;
                case BirdAnimationState.Dead:
                    frames = currentDeathFrames;
                    break;
                default:
                    frames = currentFlyFrames;
                    break;
            }

            if (frames == null || frames.Length == 0) return;

            // Advance to next frame
            birdFrameIndex = (birdFrameIndex + 1) % frames.Length;

            // Update bird sprite
            if (frames[birdFrameIndex] != null)
                FlappyBird.Source = frames[birdFrameIndex];
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!isPlaying || isGameOver) return;

            if (e.Key == Key.Space)
            {
                birdSpeed = -10;
                // Immediately switch to flying animation when player flaps
                animationState = BirdAnimationState.Flying;
                birdFrameIndex = 0;
            }
            else if (e.Key == Key.N)
            {
                SmoothToggleDayNight();
            }
        }

        private void BtnReplay_Click(object sender, RoutedEventArgs e)
        {
            // Stop all timers and animations
            StopGameLoops();
            
            // Clear all bird animations
            FlappyBird.BeginAnimation(Canvas.TopProperty, null);
            var rt = FlappyBird.RenderTransform as RotateTransform;
            if (rt != null)
            {
                rt.BeginAnimation(RotateTransform.AngleProperty, null);
            }
            
            // Hide game over panel
            GameOverPanel.Visibility = Visibility.Collapsed;

            // Reset to day theme without animation for immediate response
            ResetStageToDay(false);
            
            // Start new game
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
        }
    }

    public enum BirdAnimationState
    {
        Flying,     // Wings flapping, bird going up
        Falling,    // Wings down, bird falling
        Dead        // Game over state
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





























































