using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.IO;
using FlappyBird.Business.Services;
using FlappyBird.Business.Models;

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

        // StageService để quản lý business logic
        private readonly StageService stageService = new();

        // UI Wrappers để map Business models với UI elements
        private sealed class PipePairUI
        {
            public PipePairUI(Image top, Image bottom, PipePairState state)
            {
                Top = top;
                Bottom = bottom;
                State = state;
            }

            public Image Top { get; }
            public Image Bottom { get; }
            public PipePairState State { get; }
        }

        private sealed class NoTouchUI
        {
            public NoTouchUI(Image image, NoTouchState state)
            {
                Image = image;
                State = state;
            }

            public Image Image { get; }
            public NoTouchState State { get; }
        }

        private sealed class GateUI
        {
            public GateUI(Ellipse gate, GateState state)
            {
                Gate = gate;
                State = state;
            }

            public Ellipse Gate { get; }
            public GateState State { get; }
        }

        private readonly List<PipePairUI> pipePairs = new();
        private readonly List<Image> clouds = new();
        private readonly List<NoTouchUI> noTouchObstacles = new();
        private readonly List<GateUI> gates = new();

        private const double DefaultPipeSpeed = 5;
        private const double MinPipeSpeed = 3;
        private const double MaxPipeSpeed = 10;

        private double pipeSpeed = DefaultPipeSpeed;
        private double selectedPipeSpeed = DefaultPipeSpeed;
        private const int gap = 180;
        private double cloudSpeed = 2;

        private bool isGameOver = false;
        private bool isPlaying = false;
        private int frameCount = 0; // Đếm frame để tối ưu collision detection

        private const double FirstPipeStartLeft = 1100;
        private const double PipeSpacing = 260;
        private const int StartGraceTicks = 60;
        private int graceTicksRemaining = 0;

        private const int CanvasHeight = 500;
        private const int PipeWidth = 80;

        private bool isTransitioning = false;
        private readonly MediaPlayer sfxJump = new();
        private readonly MediaPlayer sfxPoint = new();
        private readonly MediaPlayer sfxFail = new();

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

            // Tắt timer tự động chuyển đổi ngày/đêm, chỉ chuyển khi vào cổng
            // dayNightTimer.Interval = TimeSpan.FromMinutes(0.50);
            // dayNightTimer.Tick += (_, __) => SmoothToggleDayNight();

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

            // Reset StageService
            stageService.Reset();

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

            // Gọi StageService để reset về day mode (business logic)
            stageService.ResetToDay();

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

                var dayGroundAnim = new DoubleAnimation
                {
                    To = 1,
                    Duration = dur,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                var nightGroundAnim = new DoubleAnimation
                {
                    To = 0,
                    Duration = dur,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                dayAnim.Completed += (_, __) =>
                {
                    // StageService đã được reset trong ResetStageToDay()
                    UpdateDynamicAssets(false);
                    UseBirdFramesForTheme(false);
                };

                DayLayer.BeginAnimation(UIElement.OpacityProperty, dayAnim);
                NightLayer.BeginAnimation(UIElement.OpacityProperty, nightAnim);
                DayGround.BeginAnimation(UIElement.OpacityProperty, dayGroundAnim);
                NightGround.BeginAnimation(UIElement.OpacityProperty, nightGroundAnim);
            }
            else
            {
                DayLayer.Opacity = 1;
                NightLayer.Opacity = 0;
                DayGround.Opacity = 1;
                NightGround.Opacity = 0;
                // StageService đã được reset trong ResetStageToDay()
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
            PlaySfx(sfxFail, "Fail.mp3", 0.7);
            
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

            // Gọi StageService để toggle day/night (business logic)
            bool targetNight = stageService.ToggleDayNight();
            // Tăng duration lên 2.5 giây để chuyển đổi mượt mà hơn
            var dur = TimeSpan.FromSeconds(2.5);

            // Dùng PowerEase với Power = 2 để có hiệu ứng mượt mà, tự nhiên hơn
            var dayAnim = new DoubleAnimation
            {
                To = targetNight ? 0 : 1,
                Duration = dur,
                EasingFunction = new PowerEase { Power = 2, EasingMode = EasingMode.EaseInOut }
            };

            var nightAnim = new DoubleAnimation
            {
                To = targetNight ? 1 : 0,
                Duration = dur,
                EasingFunction = new PowerEase { Power = 2, EasingMode = EasingMode.EaseInOut }
            };

            // Animate ground opacity
            var dayGroundAnim = new DoubleAnimation
            {
                To = targetNight ? 0 : 1,
                Duration = dur,
                EasingFunction = new PowerEase { Power = 2, EasingMode = EasingMode.EaseInOut }
            };

            var nightGroundAnim = new DoubleAnimation
            {
                To = targetNight ? 1 : 0,
                Duration = dur,
                EasingFunction = new PowerEase { Power = 2, EasingMode = EasingMode.EaseInOut }
            };

            // Update assets ở giữa quá trình chuyển đổi (50%) và đảm bảo pipes mới cũng đúng màu
            // Dùng DispatcherTimer với interval lớn hơn để tối ưu
            bool assetsUpdated = false;
            var updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(dur.TotalSeconds * 0.5) // Update ở 50%
            };
            
            // Update assets ngay lập tức để pipes mới được tạo sẽ dùng màu đúng
            // (StageService đã được update trong ToggleDayNight())
            
            updateTimer.Tick += (_, __) =>
            {
                if (!assetsUpdated)
                {
                    UpdateDynamicAssets(targetNight);
                    UseBirdFramesForTheme(targetNight);
                    assetsUpdated = true;
                    updateTimer.Stop();
                }
            };
            updateTimer.Start();
            
            // Animate ground
            DayGround.BeginAnimation(UIElement.OpacityProperty, dayGroundAnim);
            NightGround.BeginAnimation(UIElement.OpacityProperty, nightGroundAnim);
            
            dayAnim.Completed += (_, __) =>
            {
                updateTimer.Stop();
                isTransitioning = false;
                // Update lại tất cả assets một lần nữa
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

            // Update tất cả pipes hiện có (bao gồm cả pipes trong group)
            foreach (var pair in pipePairs)
            {
                if (pair.Top != null && pair.Bottom != null)
                {
                    pair.Top.Source = new BitmapImage(new Uri(Pack(pipeFile)));
                    pair.Bottom.Source = new BitmapImage(new Uri(Pack(pipeFile)));
                }
            }
            foreach (var cloud in clouds)
                cloud.Source = new BitmapImage(new Uri(Pack(cloudFile)));
        }

        private void ClearDynamicObjects()
        {
            foreach (var pair in pipePairs)
            {
                GameCanvas.Children.Remove(pair.Top);
                GameCanvas.Children.Remove(pair.Bottom);
            }
            foreach (var cloud in clouds) GameCanvas.Children.Remove(cloud);
            foreach (var state in noTouchObstacles) 
            {
                if (state.Image != null && GameCanvas.Children.Contains(state.Image))
                    GameCanvas.Children.Remove(state.Image);
            }
            foreach (var gate in gates)
            {
                if (gate.Gate != null && GameCanvas.Children.Contains(gate.Gate))
                    GameCanvas.Children.Remove(gate.Gate);
            }
            pipePairs.Clear();
            clouds.Clear();
            noTouchObstacles.Clear();
            gates.Clear();
        }

        private void CreateClouds()
        {
            string cloudFile = stageService.IsNight ? "Cloud-Night.png" : "Cloud-Day.png";
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

        private void SpawnNoTouchGroup(int count, double startX)
        {
            // Sử dụng StageService để spawn NoTouch
            int beforeCount = stageService.NoTouchObstacles.Count;
            stageService.SpawnNoTouchGroup(count, startX);
            
            // Tạo UI elements cho các NoTouch mới
            for (int i = beforeCount; i < stageService.NoTouchObstacles.Count; i++)
            {
                var state = stageService.NoTouchObstacles[i];
                try
                {
                    string imagePath = Pack("NoTouch.png");
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    
                    var obstacle = new Image
                    {
                        Width = 60,
                        Height = 60,
                        Stretch = Stretch.Fill,
                        Source = bitmap,
                        SnapsToDevicePixels = true
                    };
                    
                    RenderOptions.SetBitmapScalingMode(obstacle, BitmapScalingMode.HighQuality);
                    GameCanvas.Children.Add(obstacle);
                    
                    Canvas.SetLeft(obstacle, state.X);
                    Canvas.SetTop(obstacle, state.CurrentY);
                    Panel.SetZIndex(obstacle, 15);
                    
                    noTouchObstacles.Add(new NoTouchUI(obstacle, state));
                }
                catch (Exception ex)
                {
                    // Error creating NoTouch
                }
            }
            
            // Kiểm tra xem có gate mới được tạo không
            if (stageService.Gates.Count > gates.Count)
            {
                var newGateState = stageService.Gates[stageService.Gates.Count - 1];
                CreateGateUI(newGateState);
            }
        }
        
        private void CreateGateUI(GateState gateState)
        {
            try
            {
                Color gateColor = stageService.IsNight ? Colors.Gold : Colors.DarkBlue;
                
                var gate = new Ellipse
                {
                    Width = 80,
                    Height = 80,
                    Fill = new SolidColorBrush(gateColor),
                    Opacity = 0.9,
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 2
                };
                
                RenderOptions.SetBitmapScalingMode(gate, BitmapScalingMode.LowQuality);
                RenderOptions.SetEdgeMode(gate, EdgeMode.Aliased);
                
                GameCanvas.Children.Add(gate);
                Canvas.SetLeft(gate, gateState.X);
                Canvas.SetTop(gate, gateState.Y);
                Panel.SetZIndex(gate, 20);
                
                gates.Add(new GateUI(gate, gateState));
            }
            catch (Exception ex)
            {
                // Error creating Gate
            }
        }

        private void CreateInitialPipes(int count)
        {
            // Tạo pipes trực tiếp như code cũ, nhưng sử dụng StageService để quản lý state
            for (int i = 0; i < count; i++)
            {
                double leftPos = stageService.GetFirstPipeStartLeft() + (i * stageService.GetPipeSpacing());
                CreatePipePair(leftPos);
            }
        }

        private void CreatePipePair(double leftPos)
        {
            var pairState = stageService.CreatePipePair(leftPos, score);
            CreatePipeUI(pairState);
        }

        private void CreatePipeUI(PipePairState pairState)
        {
            string pipeFile = stageService.IsNight ? "Pipe-night.png" : "Pipe-day.png";

            // Đảm bảo các giá trị được set đúng TRƯỚC khi tạo UI
            if (pairState.MinTopHeight == 0) pairState.MinTopHeight = 100;
            if (pairState.MinBottomHeight == 0) pairState.MinBottomHeight = 100;
            if (pairState.CurrentTopHeight == 0)
            {
                if (pairState.BaseTopHeight > 0)
                    pairState.CurrentTopHeight = pairState.BaseTopHeight;
                else
                    pairState.CurrentTopHeight = 200; // Default height
            }

            var top = new Image
            {
                Width = PipeWidth,
                Stretch = Stretch.Fill,
                Source = new BitmapImage(new Uri(Pack(pipeFile))),
                SnapsToDevicePixels = true,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform { ScaleY = -1 },
                Visibility = Visibility.Visible
            };
            var bottom = new Image
            {
                Width = PipeWidth,
                Stretch = Stretch.Fill,
                Source = new BitmapImage(new Uri(Pack(pipeFile))),
                SnapsToDevicePixels = true,
                Visibility = Visibility.Visible
            };

            // Set Z-index
            Panel.SetZIndex(top, 3);
            Panel.SetZIndex(bottom, 3);

            // Thêm vào Canvas TRƯỚC
            GameCanvas.Children.Add(top);
            GameCanvas.Children.Add(bottom);
            
            // Set X position
            Canvas.SetLeft(top, pairState.X);
            Canvas.SetLeft(bottom, pairState.X);
            
            // Apply geometry SAU khi đã thêm vào Canvas (sẽ set Top và Height)
            ApplyPipeGeometry(pairState, top, bottom);
            
            pipePairs.Add(new PipePairUI(top, bottom, pairState));
        }

        private void SyncPipesFromService()
        {
            // Remove old UI pipes
            foreach (var pair in pipePairs)
            {
                if (pair.Top != null && GameCanvas.Children.Contains(pair.Top))
                    GameCanvas.Children.Remove(pair.Top);
                if (pair.Bottom != null && GameCanvas.Children.Contains(pair.Bottom))
                    GameCanvas.Children.Remove(pair.Bottom);
            }
            pipePairs.Clear();

            // Create UI pipes from StageService
            foreach (var pairState in stageService.PipePairs)
            {
                if (pairState != null)
                {
                    CreatePipeUI(pairState);
                }
            }
        }

        // Methods RandomizePipe và RandomizePipeAnimationOnly đã được chuyển vào StageService

        private void ApplyPipeGeometry(PipePairState pairState, Image top, Image bottom)
        {
            // Đảm bảo các giá trị được set đúng
            if (pairState.MinTopHeight == 0) pairState.MinTopHeight = 100;
            if (pairState.MinBottomHeight == 0) pairState.MinBottomHeight = 100;
            if (pairState.CurrentTopHeight == 0) 
            {
                if (pairState.BaseTopHeight > 0)
                    pairState.CurrentTopHeight = pairState.BaseTopHeight;
                else
                    pairState.CurrentTopHeight = 200; // Default
            }
            
            double maxTopHeight = CanvasHeight - gap - pairState.MinBottomHeight;
            double clampedTopHeight = Math.Clamp(pairState.CurrentTopHeight, pairState.MinTopHeight, maxTopHeight);

            // Đảm bảo height > 0
            if (clampedTopHeight <= 0) clampedTopHeight = 100;

            top.Height = clampedTopHeight;
            Canvas.SetTop(top, 0);

            double bottomTop = clampedTopHeight + gap;
            double bottomHeight = CanvasHeight - bottomTop;
            
            // Đảm bảo bottom height > 0
            if (bottomHeight <= 0) bottomHeight = 100;
            
            bottom.Height = bottomHeight;
            Canvas.SetTop(bottom, bottomTop);

            Panel.SetZIndex(top, 3);
            Panel.SetZIndex(bottom, 3);
        }

        private void ApplyPipeAnimation(PipePairUI pairUI)
        {
            var pair = pairUI.State;
            
            // Use StageService to apply animation logic (pair.X đã được update bởi UpdatePipePositions)
            stageService.ApplyPipeAnimation(pair);
            
            // Update UI based on state
            ApplyPipeGeometry(pair, pairUI.Top, pairUI.Bottom);
        }

        private void GameLoop(object? sender, EventArgs e)
        {
            frameCount++; // Tăng frame counter để tối ưu collision detection
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

            // Update pipe positions using StageService
            stageService.UpdatePipePositions(speed);

            // Xử lý pipes (luôn hiển thị, không ẩn)
            for (int i = 0; i < pipePairs.Count; i++)
            {
                var pairUI = pipePairs[i];
                var pair = pairUI.State;
                var top = pairUI.Top;
                var bottom = pairUI.Bottom;

                // Update X position từ StageService (không lấy từ UI)
                // pair.X đã được update bởi stageService.UpdatePipePositions(speed)

                // Bỏ qua pipes trong group (chỉ xử lý leader, các pipes khác sẽ follow leader)
                if (pair.GroupId != -1 && !pair.IsGroupLeader)
                {
                    // Pipes trong group di chuyển cùng leader, giữ khoảng cách GroupPipeSpacing
                    var leader = pipePairs.FirstOrDefault(p => p.State.GroupId == pair.GroupId && p.State.IsGroupLeader);
                    if (leader != null)
                    {
                        double leaderX = leader.State.X;
                        double offset = pair.GroupIndex * stageService.GetGroupPipeSpacing();
                        Canvas.SetLeft(top, leaderX + offset);
                        Canvas.SetLeft(bottom, leaderX + offset);
                        pair.X = leaderX + offset;
                    }
                    continue;
                }

                ApplyPipeAnimation(pairUI);

                // Update UI position from StageService
                // pair.X đã được update bởi stageService.UpdatePipePositions(speed)
                Canvas.SetLeft(top, pair.X);
                Canvas.SetLeft(bottom, pair.X);

                if (pair.X < -stageService.GetPipeWidth())
                {
                    // Nếu là leader của group, xóa tất cả pipes trong group
                    if (pair.GroupId != -1 && pair.IsGroupLeader)
                    {
                        stageService.RemoveGroupPipes(pair.GroupId);
                        var groupPipes = pipePairs.Where(p => p.State.GroupId == pair.GroupId).ToList();
                        foreach (var groupPipe in groupPipes)
                        {
                            if (groupPipe != pairUI)
                            {
                                GameCanvas.Children.Remove(groupPipe.Top);
                                GameCanvas.Children.Remove(groupPipe.Bottom);
                                pipePairs.Remove(groupPipe);
                            }
                        }
                    }
                    
                    // Xóa pipe này khỏi StageService trước khi tính farthestRight
                    stageService.PipePairs.Remove(pair);
                    
                    // Tính farthestRight từ UI pipes còn lại (loại trừ pipe hiện tại)
                    double farthestRight = 0;
                    var remainingPipes = pipePairs.Where(p => p != pairUI && p.State.X >= -stageService.GetPipeWidth()).ToList();
                    if (remainingPipes.Count > 0)
                    {
                        farthestRight = remainingPipes.Max(p => p.State.X);
                    }
                    if (farthestRight <= 0)
                    {
                        farthestRight = stageService.GetFirstPipeStartLeft();
                    }
                    
                    double farthestNoTouchX = stageService.GetFarthestNoTouchX();

                    double newX;
                    if (farthestNoTouchX > 0)
                    {
                        // Có NoTouch, đặt pipe cách NoTouch nửa màn hình (500px)
                        newX = Math.Max(farthestNoTouchX + 500, farthestRight + stageService.GetPipeSpacing());
                    }
                    else
                    {
                        // Không có NoTouch, đặt bình thường - đảm bảo pipe xuất hiện từ bên phải màn hình
                        newX = farthestRight + stageService.GetPipeSpacing();
                        // Đảm bảo newX không nhỏ hơn 1000 (bên phải màn hình)
                        if (newX < 1000)
                        {
                            newX = 1000;
                        }
                    }

                    // Tạo lại pipe với vị trí mới
                    pair.X = newX;
                    Canvas.SetLeft(top, newX);
                    Canvas.SetLeft(bottom, newX);
                    
                    // Thêm lại vào StageService
                    stageService.PipePairs.Add(pair);

                    // Kiểm tra xem có tạo group pipes không
                    bool shouldCreateGroup = stageService.ShouldCreateGroup(score) && pair.GroupId == -1;
                    
                    if (shouldCreateGroup)
                    {
                        int groupSize = stageService.GetGroupSize(score);
                        int currentGroupId = stageService.GetNextGroupId();
                        
                        // TẤT CẢ group pipes đều dùng pattern cầu thang (staircase)
                        // Chọn loại: tĩnh hoặc có animation
                        bool isAnimatedGroup = new Random().NextDouble() < 0.5; // 50% tĩnh, 50% animated
                        
                        // Sử dụng StageService để generate group heights
                        List<double> groupHeights = stageService.GenerateGroupHeights(groupSize, isAnimatedGroup, out bool ascending);
                        
                        // Nếu không tạo được ít nhất 2 pipes, không tạo group (quay lại pipe bình thường)
                        if (groupHeights.Count < 2)
                        {
                            pair.GroupId = -1;
                            pair.IsGroupLeader = false;
                            stageService.RandomizePipe(pair, score);
                            ApplyPipeGeometry(pair, top, bottom);
                            continue; // Bỏ qua phần tạo group, tiếp tục với pipe bình thường
                        }
                        
                        // Cập nhật groupSize thực tế
                        groupSize = groupHeights.Count;
                        
                        // Pipe hiện tại là leader của group
                        pair.GroupId = currentGroupId;
                        pair.IsGroupLeader = true;
                        pair.GroupIndex = 0;
                        pair.BaseTopHeight = groupHeights[0];
                        pair.CurrentTopHeight = groupHeights[0];
                        
                        // Set height trước (không thay đổi)
                        int minTopHeight = 100;
                        int minBottomHeight = 100;
                        pair.MinTopHeight = minTopHeight;
                        pair.MinBottomHeight = minBottomHeight;
                        ApplyPipeGeometry(pair, top, bottom);
                        
                        if (isAnimatedGroup)
                        {
                            // Animated group: Chỉ random animation properties, KHÔNG thay đổi height
                            stageService.RandomizePipeAnimationOnly(pair, score);
                        }
                        else
                        {
                            // Static group: Không có animation
                            pair.EnableVerticalAnimation = false;
                            pair.IsMoving = false;
                            pair.IsOscillating = false;
                            pair.HasTargetMovement = false;
                            pair.IsJumpPattern = false;
                        }
                        
                        // Tạo các pipes còn lại trong group (chỉ tạo đúng số pipes đã tính được)
                        for (int g = 1; g < groupHeights.Count; g++)
                        {
                            double groupPipeX = newX + (g * stageService.GetGroupPipeSpacing());
                            
                            // Tạo PipePairState trong StageService
                            var groupPairState = new PipePairState
                            {
                                X = groupPipeX,
                                GroupId = currentGroupId,
                                IsGroupLeader = false,
                                GroupIndex = g,
                                BaseTopHeight = groupHeights[g],
                                CurrentTopHeight = groupHeights[g],
                                MinTopHeight = minTopHeight,
                                MinBottomHeight = minBottomHeight
                            };
                            
                            stageService.PipePairs.Add(groupPairState);
                            
                            // Tạo UI
                            string pipeFile = stageService.IsNight ? "Pipe-night.png" : "Pipe-day.png";
                            
                            var groupTop = new Image
                            {
                                Width = PipeWidth,
                                Stretch = Stretch.Fill,
                                Source = new BitmapImage(new Uri(Pack(pipeFile))),
                                SnapsToDevicePixels = true,
                                RenderTransformOrigin = new Point(0.5, 0.5),
                                RenderTransform = new ScaleTransform { ScaleY = -1 }
                            };
                            var groupBottom = new Image
                            {
                                Width = PipeWidth,
                                Stretch = Stretch.Fill,
                                Source = new BitmapImage(new Uri(Pack(pipeFile))),
                                SnapsToDevicePixels = true
                            };
                            
                            GameCanvas.Children.Add(groupTop);
                            GameCanvas.Children.Add(groupBottom);
                            Canvas.SetLeft(groupTop, groupPipeX);
                            Canvas.SetLeft(groupBottom, groupPipeX);
                            
                            ApplyPipeGeometry(groupPairState, groupTop, groupBottom);
                            
                            if (isAnimatedGroup)
                            {
                                // Cầu thang animated: Chỉ random animation properties, KHÔNG thay đổi height
                                stageService.RandomizePipeAnimationOnly(groupPairState, score);
                            }
                            else
                            {
                                // Cầu thang tĩnh: Không có animation
                                groupPairState.EnableVerticalAnimation = false;
                                groupPairState.IsMoving = false;
                                groupPairState.IsOscillating = false;
                                groupPairState.HasTargetMovement = false;
                                groupPairState.IsJumpPattern = false;
                            }
                            
                            pipePairs.Add(new PipePairUI(groupTop, groupBottom, groupPairState));
                        }
                    }
                    else
                    {
                        // Pipe bình thường
                        pair.GroupId = -1;
                        pair.IsGroupLeader = false;
                        stageService.RandomizePipe(pair, score);
                        ApplyPipeGeometry(pair, top, bottom);
                    }

                    score++;
                    stageService.OnPipePassed();
                    ScoreText.Text = $"Score: {score}";
                    PlaySfx(sfxPoint, "Point.mp3", 0.6);
                    
                    // Kiểm tra xem có cần spawn NoTouch không (sử dụng StageService)
                    if (stageService.ShouldSpawnNoTouch(stageService.GetTotalPipesPassed(), out int noTouchCount, out int spawnAt))
                    {
                        if (spawnAt > 0)
                        {
                            // Spawn NoTouch ngay sau pipe vừa recycle
                            SpawnNoTouchGroup(noTouchCount, newX + stageService.GetPipeSpacing());
                        }
                    }
                }

                // Kiểm tra collision với pipes - COMMENTED FOR TESTING
                // if (graceTicksRemaining <= 0 &&
                //     (FlappyBird.CollidesWith(top) || FlappyBird.CollidesWith(bottom)))
                // {
                //     EndGame();
                //     return;
                // }
            }

            // Update NoTouch positions using StageService
            stageService.UpdateNoTouchPositions(speed);
            
            // Sync UI với StageService states
            for (int i = noTouchObstacles.Count - 1; i >= 0; i--)
            {
                if (i < 0 || i >= noTouchObstacles.Count) break;
                var noTouchUI = noTouchObstacles[i];
                var state = noTouchUI.State;
                
                if (noTouchUI.Image == null || !GameCanvas.Children.Contains(noTouchUI.Image))
                {
                    noTouchObstacles.RemoveAt(i);
                    continue;
                }

                if (double.IsNaN(state.X) || state.X < -100)
                {
                    if (GameCanvas.Children.Contains(noTouchUI.Image))
                        GameCanvas.Children.Remove(noTouchUI.Image);
                    noTouchObstacles.RemoveAt(i);
                    continue;
                }

                // Update UI position từ state
                Canvas.SetLeft(noTouchUI.Image, state.X);
                Canvas.SetTop(noTouchUI.Image, state.CurrentY);

                // Kiểm tra collision - COMMENTED FOR TESTING
                // if (graceTicksRemaining <= 0 && FlappyBird.CollidesWith(noTouchUI.Image))
                // {
                //     EndGame();
                //     return;
                // }
            }
            
            // Remove offscreen NoTouch from StageService
            stageService.RemoveOffscreenNoTouch();
            
            // Update Gate positions using StageService
            stageService.UpdateGatePositions(speed);
            
            // Sync UI với StageService states
            for (int i = gates.Count - 1; i >= 0; i--)
            {
                if (i < 0 || i >= gates.Count) break;
                var gateUI = gates[i];
                var state = gateUI.State;
                
                if (gateUI.Gate == null || !GameCanvas.Children.Contains(gateUI.Gate))
                {
                    gates.RemoveAt(i);
                    continue;
                }

                if (double.IsNaN(state.X) || state.X < -150)
                {
                    if (GameCanvas.Children.Contains(gateUI.Gate))
                        GameCanvas.Children.Remove(gateUI.Gate);
                    gates.RemoveAt(i);
                    continue;
                }

                // Update UI position từ state
                Canvas.SetLeft(gateUI.Gate, state.X);
                Canvas.SetTop(gateUI.Gate, state.Y);

                // Kiểm tra collision với chim
                if (!state.IsActivated && graceTicksRemaining <= 0)
                {
                    double birdX = Canvas.GetLeft(FlappyBird);
                    double distance = Math.Abs(state.X - birdX);
                    
                    if (distance < 150 && (frameCount % 5 == 0))
                    {
                        if (FlappyBird.CollidesWith(gateUI.Gate))
                        {
                            state.IsActivated = true;
                            SmoothToggleDayNight();
                            gateUI.Gate.Opacity = 1.0;
                        }
                    }
                }
            }
            
            // Remove offscreen Gates from StageService
            stageService.RemoveOffscreenGates();

            double currentBirdTop = Canvas.GetTop(FlappyBird);

            if (currentBirdTop < 0)
            {
                Canvas.SetTop(FlappyBird, 0);
                birdSpeed = 0;
            }

            // Kiểm tra collision với ground - COMMENTED FOR TESTING
            // double groundLevel = CanvasHeight - FlappyBird.Height;
            // if (currentBirdTop > groundLevel)
            // {
            //     Canvas.SetTop(FlappyBird, groundLevel);
            //     birdSpeed = 0;
            //     EndGame();
            // }
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
                PlaySfx(sfxJump, "Jump.mp3", 0.5);
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





























































