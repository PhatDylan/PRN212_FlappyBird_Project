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

        private sealed class PipePairState
        {
            public PipePairState(Image top, Image bottom)
            {
                Top = top;
                Bottom = bottom;
            }

            public Image Top { get; }
            public Image Bottom { get; }
            public double BaseTopHeight { get; set; }
            public double CurrentTopHeight { get; set; }
            public double TargetTopHeight { get; set; }
            public double MinTopHeight { get; set; }
            public double MinBottomHeight { get; set; }
            public double AnimationSpeed { get; set; }
            public double TargetMovementSpeed { get; set; } // Tốc độ riêng cho target movement khi kết hợp với oscillation
            public bool EnableVerticalAnimation { get; set; }
            public bool IsMoving { get; set; }
            public bool IsOscillating { get; set; } // true = lên xuống liên tục, false = di chuyển đến điểm rồi dừng
            public bool HasTargetMovement { get; set; } // true = có target movement kết hợp với oscillation
            public bool IsJumpPattern { get; set; } // true = pattern nhảy đột ngột (jump) - di chuyển nhanh rồi dừng
            public double AnimationPhase { get; set; }
            public double AnimationAmplitude { get; set; }
            public double TargetStopX { get; set; } // Vị trí X khi target nên dừng (trước khi chim tới 1-2 ống)
            public double FirstTargetHeight { get; set; } // Target đầu tiên (giảm sâu hoặc tăng cao)
            public double SecondTargetHeight { get; set; } // Target thứ hai (ngược lại, về gần vị trí ban đầu)
            public double JumpTargetHeight { get; set; } // Vị trí nhảy đến (cho jump pattern)
            public int TargetMovementStage { get; set; } // 0 = chưa bắt đầu, 1 = di chuyển đến target 1, 2 = di chuyển đến target 2, 3 = dừng
            public int AnimationDelayFrames { get; set; } // Số frame delay trước khi animation bắt đầu
            public int AnimationFrameCount { get; set; } // Đếm số frame đã trôi qua
        }

        private readonly List<PipePairState> pipePairs = new();
        private readonly List<Image> clouds = new();
        
        // NoTouch Obstacle State
        private sealed class NoTouchState
        {
            public Image Image { get; set; } = null!;
            public double BaseY { get; set; } // Vị trí Y gốc
            public double CurrentY { get; set; } // Vị trí Y hiện tại
            public bool IsOscillating { get; set; } // Có lên xuống không
            public double OscillationAmplitude { get; set; } // Biên độ lên xuống
            public double OscillationPhase { get; set; } // Phase cho sine wave
            public double OscillationSpeed { get; set; } // Tốc độ oscillation
            public double SpawnX { get; set; } // Vị trí X khi spawn để track
        }
        
        private readonly List<NoTouchState> noTouchObstacles = new();

        private const double DefaultPipeSpeed = 5;
        private const double MinPipeSpeed = 3;
        private const double MaxPipeSpeed = 10;

        private double pipeSpeed = DefaultPipeSpeed;
        private double selectedPipeSpeed = DefaultPipeSpeed;
        private const int gap = 180;
        private double cloudSpeed = 2;

        private bool isGameOver = false;
        private bool isPlaying = false;
        private bool isNight = false;

        private const double FirstPipeStartLeft = 1100;
        private const double PipeSpacing = 260;
        private const int StartGraceTicks = 60;
        private int graceTicksRemaining = 0;

        private const int CanvasHeight = 500;
        private const int PipeWidth = 80;

        private bool isTransitioning = false;
        private int totalPipesPassed = 0; // Đếm tổng số pipes đã qua để spawn NoTouch
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
            
            // Reset pipe counter
            totalPipesPassed = 0;

            graceTicksRemaining = StartGraceTicks;

            isFallingAnimation = false;
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

            foreach (var pair in pipePairs)
            {
                pair.Top.Source = new BitmapImage(new Uri(Pack(pipeFile)));
                pair.Bottom.Source = new BitmapImage(new Uri(Pack(pipeFile)));
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
            pipePairs.Clear();
            clouds.Clear();
            noTouchObstacles.Clear();
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

        private void SpawnNoTouchGroup(int count, double startX)
        {
            // Spawn một nhóm NoTouch sau pipes
            for (int i = 0; i < count; i++)
            {
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
                    
                    // Spawn cách nhau 150px để tránh chồng lấn
                    double spawnX = startX + (i * 150);
                    double spawnY = rnd.Next(80, CanvasHeight - 120);
                    
                    Canvas.SetLeft(obstacle, spawnX);
                    Canvas.SetTop(obstacle, spawnY);
                    Panel.SetZIndex(obstacle, 15);
                    
                    // Tạo state với animation
                    var state = new NoTouchState
                    {
                        Image = obstacle,
                        BaseY = spawnY,
                        CurrentY = spawnY,
                        IsOscillating = rnd.NextDouble() < 0.6, // 60% có animation
                        OscillationAmplitude = rnd.Next(25, 50), // Biên độ 25-50px
                        OscillationPhase = rnd.NextDouble() * Math.PI * 2, // Random phase
                        OscillationSpeed = 0.03 + rnd.NextDouble() * 0.02, // Tốc độ 0.03-0.05
                        SpawnX = spawnX // Lưu vị trí spawn để track
                    };
                    
                    noTouchObstacles.Add(state);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NoTouch] ERROR: {ex.Message}");
                }
            }
        }

        private void CreateInitialPipes(int count)
        {
            for (int i = 0; i < count; i++)
            {
                CreatePipePair(FirstPipeStartLeft + i * PipeSpacing);
            }
        }

        private void CreatePipePair(double leftPos)
        {
            string pipeFile = isNight ? "Pipe-night.png" : "Pipe-day.png";

            var top = new Image
            {
                Width = PipeWidth,
                Stretch = Stretch.Fill,
                Source = new BitmapImage(new Uri(Pack(pipeFile))),
                SnapsToDevicePixels = true,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform { ScaleY = -1 }
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

            var pairState = new PipePairState(top, bottom);
            RandomizePipe(pairState);
            pipePairs.Add(pairState);
        }

        private void RandomizePipe(PipePairState pair)
        {
            int minTopHeight = 100;
            int minBottomHeight = 100;
            int maxTopHeight = 590 - gap - minBottomHeight;
            
            double baseTopHeight = rnd.Next(minTopHeight, maxTopHeight + 1);

            // Pattern variations: biased top/bottom gaps
            double patternRoll = rnd.NextDouble();
            if (patternRoll < 0.15)
            {
                baseTopHeight = minTopHeight + rnd.Next(0, Math.Max(1, 40));
            }
            else if (patternRoll < 0.30)
            {
                baseTopHeight = maxTopHeight - rnd.Next(0, Math.Max(1, 40));
            }

            pair.BaseTopHeight = baseTopHeight;
            pair.CurrentTopHeight = baseTopHeight;

            // Chỉ có animation khi score >= 10
            bool enableAnimation = false;
            double animationChance = 0.0;
            
            if (score >= 40)
            {
                animationChance = 0.80; // 80% cột có animation
            }
            else if (score >= 30)
            {
                animationChance = 0.80; // 80% cột có animation
            }
            else if (score >= 20)
            {
                animationChance = 0.65; // 65% cột có animation
            }
            else if (score >= 10)
            {
                animationChance = 0.50; // 50% cột có animation
            }
            
            enableAnimation = rnd.NextDouble() < animationChance;
            
            if (enableAnimation)
            {
                double maxAmplitude = Math.Max(0, Math.Min(baseTopHeight - minTopHeight, maxTopHeight - baseTopHeight));
                
                // Điều chỉnh amplitude dựa trên score
                double baseAmplitude = score >= 40 ? 140 : score >= 30 ? 130 : score >= 20 ? 120 : 100;
                double amplitudeRange = score >= 40 ? 70 : score >= 30 ? 65 : score >= 20 ? 60 : 50;
                double desiredAmplitude = baseAmplitude + rnd.NextDouble() * amplitudeRange;
                double amplitude = maxAmplitude > 0 ? Math.Min(desiredAmplitude, maxAmplitude) : 0;
                
                // Quyết định loại animation theo score
                double oscillationChance = 0.0;
                if (score >= 40)
                {
                    oscillationChance = 0.80; // 80% oscillation
                }
                else if (score >= 30)
                {
                    oscillationChance = 0.80; // 80% oscillation
                }
                else if (score >= 20)
                {
                    oscillationChance = 0.65; // 65% oscillation
                }
                else if (score >= 10)
                {
                    oscillationChance = 0.50; // 50% oscillation, 50% target
                }
                
                bool useOscillation = rnd.NextDouble() < oscillationChance;
                bool useTarget = !useOscillation || (score >= 20 && rnd.NextDouble() < 0.4); // Từ 20 trở đi có thể kết hợp
                
                // Quyết định có dùng jump pattern không (từ score 20 trở đi)
                bool useJumpPattern = false;
                if (score >= 20 && !useOscillation && useTarget)
                {
                    useJumpPattern = rnd.NextDouble() < 0.3; // 30% cơ hội dùng jump pattern
                }
                
                // Delay trước khi animation bắt đầu (từ score 20 trở đi)
                int delayFrames = 0;
                if (score >= 20 && rnd.NextDouble() < 0.25) // 25% cơ hội có delay
                {
                    delayFrames = rnd.Next(10, 40); // Delay 10-40 frames
                }
                pair.AnimationDelayFrames = delayFrames;
                pair.AnimationFrameCount = 0;
                
                if (useOscillation && amplitude > 20)
                {
                    // Lên xuống liên tục (oscillation)
                    pair.IsOscillating = true;
                    pair.AnimationAmplitude = amplitude;
                    pair.AnimationPhase = rnd.NextDouble() * Math.PI * 2;
                    // Tốc độ oscillation không bị ảnh hưởng bởi tốc độ game
                    double oscSpeed = score >= 40 ? 0.05 + rnd.NextDouble() * 0.02 : 
                                     score >= 30 ? 0.04 + rnd.NextDouble() * 0.02 :
                                     score >= 20 ? 0.04 + rnd.NextDouble() * 0.02 : 0.03 + rnd.NextDouble() * 0.02;
                    pair.AnimationSpeed = oscSpeed;
                    pair.EnableVerticalAnimation = true;
                    pair.IsMoving = false;
                    pair.HasTargetMovement = useTarget && score >= 20;
                    
                    // Nếu kết hợp với target
                    if (pair.HasTargetMovement)
                    {
                        // Tăng amplitude để target di chuyển xa hơn, dài hơn
                        double moveAmplitude = amplitude * 1.8; // Tăng lên để di chuyển xa hơn
                        moveAmplitude = Math.Min(moveAmplitude, maxAmplitude);
                        bool moveUp = rnd.NextDouble() < 0.5;
                        double targetOffset = moveUp ? -moveAmplitude : moveAmplitude;
                        pair.TargetTopHeight = Math.Clamp(baseTopHeight + targetOffset, minTopHeight, maxTopHeight);
                        pair.IsMoving = true;
                        // Tăng tốc độ animation để target kịp di chuyển xa hơn (dùng TargetMovementSpeed riêng)
                        // Tốc độ tăng theo score để linh hoạt hơn khi game nhanh
                        double baseSpeed = 0.5;
                        double speedMultiplier = 1.0 + (score * 0.01); // Tăng tốc độ theo score
                        double moveSpeed = (baseSpeed + rnd.NextDouble() * 0.25) * Math.Min(speedMultiplier, 1.4);
                        pair.TargetMovementSpeed = moveSpeed;
                        // Tính vị trí X khi target nên dừng (trước chim 1 cột)
                        double pipeX = Canvas.GetLeft(pair.Top);
                        double birdX = 70; // Vị trí X của chim
                        pair.TargetStopX = birdX + PipeSpacing * 1.0;
                    }
                }
                else if (useTarget)
                {
                    pair.IsOscillating = false;
                    pair.HasTargetMovement = false;
                    
                    if (useJumpPattern)
                    {
                        // Jump pattern: Nhảy đột ngột với tốc độ rất nhanh rồi dừng ngay
                        pair.IsJumpPattern = true;
                        double jumpAmplitude = Math.Min(amplitude * 2.0, maxAmplitude);
                        
                        bool jumpUp = rnd.NextDouble() < 0.5;
                        double jumpOffset = jumpUp ? -jumpAmplitude : jumpAmplitude;
                        double jumpHeight = Math.Clamp(baseTopHeight + jumpOffset, minTopHeight, maxTopHeight);
                        
                        pair.JumpTargetHeight = jumpHeight;
                        pair.TargetTopHeight = jumpHeight;
                        pair.TargetMovementStage = 1;
                        
                        // Tốc độ rất nhanh cho jump pattern
                        double jumpSpeed = 1.5 + rnd.NextDouble() * 0.8; // 1.5-2.3 pixel/frame
                        pair.AnimationSpeed = jumpSpeed;
                        pair.EnableVerticalAnimation = true;
                        pair.IsMoving = true;
                        
                        double pipeX = Canvas.GetLeft(pair.Top);
                        double birdX = 70;
                        pair.TargetStopX = birdX + PipeSpacing * 1.0;
                    }
                    else
                    {
                        // Target movement đơn giản: di chuyển đến một điểm với amplitude lớn rồi dừng
                        pair.IsJumpPattern = false;
                        double moveAmplitude = Math.Min(amplitude * 2.5, maxAmplitude); // Amplitude lớn để dễ thấy
                        
                        bool moveUp = rnd.NextDouble() < 0.5;
                        double targetOffset = moveUp ? -moveAmplitude : moveAmplitude;
                        double targetHeight = Math.Clamp(baseTopHeight + targetOffset, minTopHeight, maxTopHeight);
                        
                        pair.TargetTopHeight = targetHeight;
                        pair.TargetMovementStage = 1; // Đang di chuyển đến target
                        
                        // Tốc độ tăng theo score để linh hoạt hơn khi game nhanh
                        double baseSpeed = 0.7;
                        double speedMultiplier = 1.0 + (score * 0.01);
                        double moveSpeed = (baseSpeed + rnd.NextDouble() * 0.3) * Math.Min(speedMultiplier, 1.5);
                        pair.AnimationSpeed = moveSpeed;
                        pair.EnableVerticalAnimation = true;
                        pair.IsMoving = true;
                        
                        // Tính vị trí X khi target nên dừng (trước chim 1 cột)
                        double pipeX = Canvas.GetLeft(pair.Top);
                        double birdX = 70;
                        pair.TargetStopX = birdX + PipeSpacing * 1.0;
                    }
                }
            }
            else
            {
                pair.TargetTopHeight = baseTopHeight;
                pair.AnimationSpeed = 0;
                pair.EnableVerticalAnimation = false;
                pair.IsMoving = false;
                pair.IsOscillating = false;
                pair.HasTargetMovement = false;
                pair.IsJumpPattern = false;
                pair.FirstTargetHeight = baseTopHeight;
                pair.SecondTargetHeight = baseTopHeight;
                pair.TargetMovementStage = 0;
                pair.AnimationDelayFrames = 0;
                pair.AnimationFrameCount = 0;
            }

            pair.MinTopHeight = minTopHeight;
            pair.MinBottomHeight = minBottomHeight;

            ApplyPipeGeometry(pair, pair.CurrentTopHeight);
        }

        private void ApplyPipeGeometry(PipePairState pair, double desiredTopHeight)
        {
            double clampedTopHeight = Math.Clamp(desiredTopHeight, pair.MinTopHeight, 590 - gap - pair.MinBottomHeight);

            pair.Top.Height = clampedTopHeight;
            Canvas.SetTop(pair.Top, 0);

            double bottomTop = clampedTopHeight + gap;
            double bottomHeight = 590 - bottomTop;
            pair.Bottom.Height = bottomHeight;
            Canvas.SetTop(pair.Bottom, bottomTop);

            Panel.SetZIndex(pair.Top, 3);
            Panel.SetZIndex(pair.Bottom, 3);
        }

        private void ApplyPipeAnimation(PipePairState pair)
        {
            if (!pair.EnableVerticalAnimation)
            {
                ApplyPipeGeometry(pair, pair.CurrentTopHeight);
                return;
            }

            // Kiểm tra delay trước khi animation bắt đầu
            if (pair.AnimationFrameCount < pair.AnimationDelayFrames)
            {
                pair.AnimationFrameCount++;
                ApplyPipeGeometry(pair, pair.CurrentTopHeight);
                return;
            }

            double pipeX = Canvas.GetLeft(pair.Top);
            double targetOffset = 0;

            if (pair.IsOscillating)
            {
                // Lên xuống liên tục (oscillation)
                pair.AnimationPhase += pair.AnimationSpeed;
                if (pair.AnimationPhase > Math.PI * 2)
                {
                    pair.AnimationPhase -= Math.PI * 2;
                }
                
                double oscOffset = Math.Sin(pair.AnimationPhase) * pair.AnimationAmplitude;
                targetOffset = oscOffset;
                
                // Nếu có kết hợp với target movement
                if (pair.HasTargetMovement && pair.IsMoving)
                {
                    // Kiểm tra xem có nên dừng target không (khi cột đến TargetStopX)
                    if (pipeX <= pair.TargetStopX)
                    {
                        // Dừng target movement, chỉ giữ oscillation
                        pair.IsMoving = false;
                        pair.BaseTopHeight = pair.CurrentTopHeight - oscOffset; // Cập nhật base để oscillation tiếp tục từ vị trí hiện tại
                    }
                    else
                    {
                        // Tiếp tục di chuyển target (dùng TargetMovementSpeed riêng)
                        double distance = pair.TargetTopHeight - pair.BaseTopHeight;
                        double moveSpeed = pair.HasTargetMovement ? pair.TargetMovementSpeed : pair.AnimationSpeed;
                        double moveStep = moveSpeed * Math.Sign(distance);
                        
                        if (Math.Abs(distance) <= Math.Abs(moveStep))
                        {
                            // Đã đến target, dừng target movement
                            pair.BaseTopHeight = pair.TargetTopHeight;
                            pair.IsMoving = false;
                        }
                        else
                        {
                            // Tiếp tục di chuyển base height
                            pair.BaseTopHeight += moveStep;
                        }
                    }
                }
                
                pair.CurrentTopHeight = pair.BaseTopHeight + targetOffset;
            }
            else if (pair.IsMoving)
            {
                // Chỉ có target movement (không oscillation)
                // Kiểm tra xem có nên dừng target không (khi cột đến TargetStopX)
                if (pipeX <= pair.TargetStopX)
                {
                    // Dừng lại trước khi chim tới
                    pair.IsMoving = false;
                    pair.TargetMovementStage = 3; // Dừng
                }
                else
                {
                    if (pair.IsJumpPattern)
                    {
                        // Jump pattern: Nhảy đột ngột với tốc độ rất nhanh
                        double distance = pair.JumpTargetHeight - pair.CurrentTopHeight;
                        double moveStep = pair.AnimationSpeed * Math.Sign(distance);
                        
                        if (Math.Abs(distance) <= Math.Abs(moveStep))
                        {
                            // Đã đến target, dừng ngay (jump pattern dừng ngay khi đến)
                            pair.CurrentTopHeight = pair.JumpTargetHeight;
                            pair.IsMoving = false;
                            pair.TargetMovementStage = 3; // Dừng
                        }
                        else
                        {
                            // Tiếp tục nhảy với tốc độ nhanh
                            pair.CurrentTopHeight += moveStep;
                        }
                    }
                    else
                    {
                        // Target movement thông thường: di chuyển đến một điểm rồi dừng
                        double distance = pair.TargetTopHeight - pair.CurrentTopHeight;
                        double moveStep = pair.AnimationSpeed * Math.Sign(distance);
                        
                        if (Math.Abs(distance) <= Math.Abs(moveStep))
                        {
                            // Đã đến target, dừng lại
                            pair.CurrentTopHeight = pair.TargetTopHeight;
                            pair.IsMoving = false;
                            pair.TargetMovementStage = 3; // Dừng
                        }
                        else
                        {
                            // Tiếp tục di chuyển
                            pair.CurrentTopHeight += moveStep;
                        }
                    }
                }
            }
            else
            {
                // Đã dừng, giữ nguyên vị trí
                ApplyPipeGeometry(pair, pair.CurrentTopHeight);
                return;
            }

            ApplyPipeGeometry(pair, pair.CurrentTopHeight);
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

            // Xử lý pipes (luôn hiển thị, không ẩn)
            for (int i = 0; i < pipePairs.Count; i++)
            {
                var pair = pipePairs[i];
                var top = pair.Top;
                var bottom = pair.Bottom;

                ApplyPipeAnimation(pair);

                Canvas.SetLeft(top, Canvas.GetLeft(top) - speed);
                Canvas.SetLeft(bottom, Canvas.GetLeft(bottom) - speed);

                if (Canvas.GetLeft(top) < -PipeWidth)
                {
                    double farthestRight = double.MinValue;
                    for (int j = 0; j < pipePairs.Count; j++)
                        if (j != i)
                            farthestRight = Math.Max(farthestRight, Canvas.GetLeft(pipePairs[j].Top));

                    // Tìm NoTouch xa nhất về bên phải để tạo khoảng trống
                    double farthestNoTouchX = -1;
                    foreach (var noTouchState in noTouchObstacles)
                    {
                        if (noTouchState.Image != null && GameCanvas.Children.Contains(noTouchState.Image))
                        {
                            double noTouchX = Canvas.GetLeft(noTouchState.Image);
                            if (!double.IsNaN(noTouchX) && noTouchX > farthestNoTouchX)
                                farthestNoTouchX = noTouchX;
                        }
                    }

                    double newX;
                    if (farthestNoTouchX > 0)
                    {
                        // Có NoTouch, đặt pipe cách NoTouch nửa màn hình (500px)
                        newX = Math.Max(farthestNoTouchX + 500, farthestRight + PipeSpacing);
                    }
                    else
                    {
                        // Không có NoTouch, đặt bình thường
                        newX = farthestRight == double.MinValue ? FirstPipeStartLeft : farthestRight + PipeSpacing;
                    }

                    Canvas.SetLeft(top, newX);
                    Canvas.SetLeft(bottom, newX);

                    RandomizePipe(pair);

                    score++;
                    totalPipesPassed++;
                    ScoreText.Text = $"Score: {score}";
                    PlaySfx(sfxPoint, "Point.mp3", 0.6);
                    
                    // Kiểm tra xem có cần spawn NoTouch không (cứ mỗi 10 pipes)
                    // Score 10-14: 1 con, 15-19: 2 con, 20-24: 3 con...
                    if (score >= 10 && totalPipesPassed % 10 == 0)
                    {
                        int noTouchCount = Math.Min(7, ((score - 10) / 5) + 1);
                        // Spawn NoTouch ngay sau pipe vừa recycle
                        SpawnNoTouchGroup(noTouchCount, newX + PipeSpacing);
                    }
                }

                // TEST MODE: Comment collision detection để chim xuyên qua ống
                // if (graceTicksRemaining <= 0 &&
                //     (FlappyBird.CollidesWith(top) || FlappyBird.CollidesWith(bottom)))
                // {
                //     EndGame();
                //     return;
                // }
            }

            // Xử lý NoTouch obstacles
            for (int i = noTouchObstacles.Count - 1; i >= 0; i--)
            {
                if (i < 0 || i >= noTouchObstacles.Count) break;
                var state = noTouchObstacles[i];
                if (state == null || state.Image == null || !GameCanvas.Children.Contains(state.Image))
                {
                    if (i < noTouchObstacles.Count)
                        noTouchObstacles.RemoveAt(i);
                    continue;
                }

                double x = Canvas.GetLeft(state.Image);
                if (double.IsNaN(x) || x < -100)
                {
                    if (GameCanvas.Children.Contains(state.Image))
                        GameCanvas.Children.Remove(state.Image);
                    noTouchObstacles.RemoveAt(i);
                    continue;
                }

                // Di chuyển ngang
                Canvas.SetLeft(state.Image, x - speed);

                // Animation lên xuống (nếu có)
                if (state.IsOscillating)
                {
                    state.OscillationPhase += state.OscillationSpeed;
                    state.CurrentY = state.BaseY + Math.Sin(state.OscillationPhase) * state.OscillationAmplitude;
                    Canvas.SetTop(state.Image, state.CurrentY);
                }

                // Kiểm tra collision
                if (graceTicksRemaining <= 0 && FlappyBird.CollidesWith(state.Image))
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
















