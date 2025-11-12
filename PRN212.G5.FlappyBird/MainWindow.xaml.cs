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
            public int GroupId { get; set; } = -1; // ID của group (nếu là -1 thì không thuộc group nào)
            public bool IsGroupLeader { get; set; } = false; // true nếu là pipe đầu tiên trong group
            public int GroupIndex { get; set; } = 0; // Index trong group (0 = leader, 1, 2, 3...)
        }

        private readonly List<PipePairState> pipePairs = new();
        private readonly List<Image> clouds = new();
        private int nextGroupId = 0; // ID cho group pipes tiếp theo
        private const double GroupPipeSpacing = 90; // Khoảng cách giữa các pipes trong group (nhỏ hơn PipeSpacing = 260)
        
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
        
        // Gate State - Cổng để chuyển đổi ngày/đêm
        private sealed class GateState
        {
            public Ellipse Gate { get; set; } = null!;
            public double SpawnX { get; set; }
            public bool IsActivated { get; set; } = false; // Đã kích hoạt chưa
        }
        
        private readonly List<NoTouchState> noTouchObstacles = new();
        private readonly List<GateState> gates = new();

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
        private int frameCount = 0; // Đếm frame để tối ưu collision detection

        private const double FirstPipeStartLeft = 1100;
        private const double PipeSpacing = 260;
        private const int StartGraceTicks = 60;
        private int graceTicksRemaining = 0;

        private const int CanvasHeight = 500;
        private const int PipeWidth = 80;

        private bool isTransitioning = false;
        private int totalPipesPassed = 0; // Đếm tổng số pipes đã qua để spawn NoTouch
        private int nextNoTouchSpawnAt = -1; // Pipe số mấy sẽ spawn NoTouch tiếp theo (random)
        private int lastSpawnedPhase = -1; // Phase cuối cùng đã spawn NoTouch để tránh spawn nhiều lần
        private int noTouchSpawnCount = 0; // Đếm số lần đã spawn NoTouch (để tạo cổng sau mỗi 2 lần)
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
            
            // Reset pipe counter và random spawn point
            totalPipesPassed = 0;
            nextNoTouchSpawnAt = -1;
            lastSpawnedPhase = -1; // Reset phase tracking
            nextGroupId = 0; // Reset group ID
            noTouchSpawnCount = 0; // Reset NoTouch spawn counter

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

            // Update assets ở giữa quá trình chuyển đổi (50%) và đảm bảo pipes mới cũng đúng màu
            // Dùng DispatcherTimer với interval lớn hơn để tối ưu
            bool assetsUpdated = false;
            var updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(dur.TotalSeconds * 0.5) // Update ở 50%
            };
            
            // Update isNight ngay lập tức để pipes mới được tạo sẽ dùng màu đúng
            isNight = targetNight;
            
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
            
            dayAnim.Completed += (_, __) =>
            {
                updateTimer.Stop();
                isTransitioning = false;
                // Đảm bảo isNight đã được set và update lại tất cả assets một lần nữa
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
            
            // Tăng counter sau khi spawn NoTouch
            noTouchSpawnCount++;
            
            // Chỉ tạo cổng ở lần thứ 2 (sau 1 lần spawn NoTouch)
            // Lần 1: noTouchSpawnCount = 1, không có cổng
            // Lần 2: noTouchSpawnCount = 2, có cổng, reset về 0
            if (noTouchSpawnCount >= 2)
            {
                // Tạo cổng (gate) khi spawn NoTouch - đặt ở giữa màn hình
                CreateGate(startX + (count * 150) + 200); // Đặt cổng sau nhóm NoTouch
                noTouchSpawnCount = 0; // Reset counter
            }
        }
        
        private void CreateGate(double gateX)
        {
            try
            {
                // Tạo cổng hình tròn - ban ngày màu đêm (tối), ban đêm màu ban ngày (sáng)
                // Ban ngày: màu tối (DarkBlue, DarkPurple) để nổi bật trên nền sáng
                // Ban đêm: màu sáng (Yellow, Gold) để nổi bật trên nền tối
                Color gateColor = isNight ? Colors.Gold : Colors.DarkBlue;
                
                var gate = new Ellipse
                {
                    Width = 80, // Kích thước 80x80
                    Height = 80, // Hình tròn
                    Fill = new SolidColorBrush(gateColor),
                    Opacity = 0.9,
                    Stroke = new SolidColorBrush(Colors.White),
                    StrokeThickness = 2
                };
                
                // Tắt animation nhấp nháy để giảm lag - chỉ dùng opacity cố định
                // Không dùng animation để tránh lag khi gần cổng
                
                // Tối ưu rendering
                RenderOptions.SetBitmapScalingMode(gate, BitmapScalingMode.LowQuality);
                RenderOptions.SetEdgeMode(gate, EdgeMode.Aliased);
                
                GameCanvas.Children.Add(gate);
                Canvas.SetLeft(gate, gateX);
                Canvas.SetTop(gate, (CanvasHeight - 80) / 2); // Ở giữa màn hình theo chiều dọc
                Panel.SetZIndex(gate, 20); // Cao hơn NoTouch
                
                var gateState = new GateState
                {
                    Gate = gate,
                    SpawnX = gateX,
                    IsActivated = false
                };
                
                gates.Add(gateState);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Gate] ERROR: {ex.Message}");
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

        // Hàm chỉ random animation properties, KHÔNG thay đổi height
        private void RandomizePipeAnimationOnly(PipePairState pair)
        {
            // Giữ nguyên BaseTopHeight và CurrentTopHeight đã được set
            double baseTopHeight = pair.BaseTopHeight;
            int minTopHeight = (int)pair.MinTopHeight;
            int minBottomHeight = (int)pair.MinBottomHeight;
            int maxTopHeight = 590 - (int)gap - minBottomHeight;
            
            // Chỉ có animation khi score >= 10
            bool enableAnimation = false;
            double animationChance = 0.0;
            
            if (score >= 40)
            {
                animationChance = 0.80;
            }
            else if (score >= 30)
            {
                animationChance = 0.80;
            }
            else if (score >= 20)
            {
                animationChance = 0.65;
            }
            else if (score >= 10)
            {
                animationChance = 0.50;
            }
            
            enableAnimation = rnd.NextDouble() < animationChance;
            
            if (enableAnimation)
            {
                double maxAmplitude = Math.Max(0, Math.Min(baseTopHeight - minTopHeight, maxTopHeight - baseTopHeight));
                
                double baseAmplitude = score >= 40 ? 140 : score >= 30 ? 130 : score >= 20 ? 120 : 100;
                double amplitudeRange = score >= 40 ? 70 : score >= 30 ? 65 : score >= 20 ? 60 : 50;
                double desiredAmplitude = baseAmplitude + rnd.NextDouble() * amplitudeRange;
                double amplitude = maxAmplitude > 0 ? Math.Min(desiredAmplitude, maxAmplitude) : 0;
                
                double oscillationChance = 0.0;
                if (score >= 40)
                {
                    oscillationChance = 0.80;
                }
                else if (score >= 30)
                {
                    oscillationChance = 0.80;
                }
                else if (score >= 20)
                {
                    oscillationChance = 0.65;
                }
                else if (score >= 10)
                {
                    oscillationChance = 0.50;
                }
                
                bool useOscillation = rnd.NextDouble() < oscillationChance;
                bool useTarget = !useOscillation || (score >= 20 && rnd.NextDouble() < 0.4);
                
                bool useJumpPattern = false;
                if (score >= 20 && !useOscillation && useTarget)
                {
                    useJumpPattern = rnd.NextDouble() < 0.3;
                }
                
                int delayFrames = 0;
                if (score >= 20 && rnd.NextDouble() < 0.25)
                {
                    delayFrames = rnd.Next(10, 40);
                }
                pair.AnimationDelayFrames = delayFrames;
                pair.AnimationFrameCount = 0;
                
                if (useOscillation && amplitude > 20)
                {
                    pair.IsOscillating = true;
                    pair.AnimationAmplitude = amplitude;
                    pair.AnimationPhase = rnd.NextDouble() * Math.PI * 2;
                    double oscSpeed = score >= 40 ? 0.05 + rnd.NextDouble() * 0.02 : 
                                     score >= 30 ? 0.04 + rnd.NextDouble() * 0.02 :
                                     score >= 20 ? 0.04 + rnd.NextDouble() * 0.02 : 0.03 + rnd.NextDouble() * 0.02;
                    pair.AnimationSpeed = oscSpeed;
                    pair.EnableVerticalAnimation = true;
                    pair.IsMoving = false;
                    pair.HasTargetMovement = useTarget && score >= 20;
                    
                    if (pair.HasTargetMovement)
                    {
                        double moveAmplitude = amplitude * 1.8;
                        moveAmplitude = Math.Min(moveAmplitude, maxAmplitude);
                        bool moveUp = rnd.NextDouble() < 0.5;
                        double targetOffset = moveUp ? -moveAmplitude : moveAmplitude;
                        pair.TargetTopHeight = Math.Clamp(baseTopHeight + targetOffset, minTopHeight, maxTopHeight);
                        pair.IsMoving = true;
                        double baseSpeed = 0.5;
                        double speedMultiplier = 1.0 + (score * 0.01);
                        double moveSpeed = (baseSpeed + rnd.NextDouble() * 0.25) * Math.Min(speedMultiplier, 1.4);
                        pair.TargetMovementSpeed = moveSpeed;
                        double pipeX = Canvas.GetLeft(pair.Top);
                        double birdX = 70;
                        pair.TargetStopX = birdX + PipeSpacing * 1.0;
                    }
                }
                else if (useTarget)
                {
                    pair.IsOscillating = false;
                    pair.HasTargetMovement = false;
                    
                    if (useJumpPattern)
                    {
                        pair.IsJumpPattern = true;
                        double jumpAmplitude = Math.Min(amplitude * 2.0, maxAmplitude);
                        bool jumpUp = rnd.NextDouble() < 0.5;
                        double jumpOffset = jumpUp ? -jumpAmplitude : jumpAmplitude;
                        double jumpHeight = Math.Clamp(baseTopHeight + jumpOffset, minTopHeight, maxTopHeight);
                        pair.JumpTargetHeight = jumpHeight;
                        pair.TargetTopHeight = jumpHeight;
                        pair.TargetMovementStage = 1;
                        double baseSpeed = 2.0;
                        double speedMultiplier = 1.0 + (score * 0.01);
                        double jumpSpeed = (baseSpeed + rnd.NextDouble() * 0.5) * Math.Min(speedMultiplier, 1.5);
                        pair.TargetMovementSpeed = jumpSpeed;
                        pair.EnableVerticalAnimation = true;
                        pair.IsMoving = true;
                    }
                    else
                    {
                        pair.IsJumpPattern = false;
                        double moveAmplitude = amplitude * 2.5;
                        moveAmplitude = Math.Min(moveAmplitude, maxAmplitude);
                        bool moveUp = rnd.NextDouble() < 0.5;
                        double targetOffset = moveUp ? -moveAmplitude : moveAmplitude;
                        pair.TargetTopHeight = Math.Clamp(baseTopHeight + targetOffset, minTopHeight, maxTopHeight);
                        pair.IsMoving = true;
                        double baseSpeed = 0.5;
                        double speedMultiplier = 1.0 + (score * 0.01);
                        double moveSpeed = (baseSpeed + rnd.NextDouble() * 0.25) * Math.Min(speedMultiplier, 1.4);
                        pair.TargetMovementSpeed = moveSpeed;
                        double pipeX = Canvas.GetLeft(pair.Top);
                        double birdX = 70;
                        pair.TargetStopX = birdX + PipeSpacing * 1.0;
                        pair.EnableVerticalAnimation = true;
                        pair.HasTargetMovement = true;
                    }
                }
                else
                {
                    pair.EnableVerticalAnimation = false;
                    pair.IsOscillating = false;
                    pair.IsMoving = false;
                    pair.HasTargetMovement = false;
                    pair.IsJumpPattern = false;
                }
            }
            else
            {
                pair.EnableVerticalAnimation = false;
                pair.IsOscillating = false;
                pair.IsMoving = false;
                pair.HasTargetMovement = false;
                pair.IsJumpPattern = false;
                pair.AnimationDelayFrames = 0;
                pair.AnimationFrameCount = 0;
            }
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
            // Mỗi pipe trong group có animation riêng, không cần follow leader
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
            frameCount++; // Tăng frame counter để tối ưu collision detection
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

                // Bỏ qua pipes trong group (chỉ xử lý leader, các pipes khác sẽ follow leader)
                if (pair.GroupId != -1 && !pair.IsGroupLeader)
                {
                    // Pipes trong group di chuyển cùng leader, giữ khoảng cách GroupPipeSpacing
                    var leader = pipePairs.FirstOrDefault(p => p.GroupId == pair.GroupId && p.IsGroupLeader);
                    if (leader != null)
                    {
                        double leaderX = Canvas.GetLeft(leader.Top);
                        double offset = pair.GroupIndex * GroupPipeSpacing;
                        Canvas.SetLeft(top, leaderX + offset);
                        Canvas.SetLeft(bottom, leaderX + offset);
                    }
                    continue;
                }

                ApplyPipeAnimation(pair);

                Canvas.SetLeft(top, Canvas.GetLeft(top) - speed);
                Canvas.SetLeft(bottom, Canvas.GetLeft(bottom) - speed);

                if (Canvas.GetLeft(top) < -PipeWidth)
                {
                    // Nếu là leader của group, xóa tất cả pipes trong group
                    if (pair.GroupId != -1 && pair.IsGroupLeader)
                    {
                        var groupPipes = pipePairs.Where(p => p.GroupId == pair.GroupId).ToList();
                        foreach (var groupPipe in groupPipes)
                        {
                            if (groupPipe != pair)
                            {
                                GameCanvas.Children.Remove(groupPipe.Top);
                                GameCanvas.Children.Remove(groupPipe.Bottom);
                                pipePairs.Remove(groupPipe);
                            }
                        }
                    }
                    
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

                    // Kiểm tra xem có tạo group pipes không
                    // Score 15-49: 45% cơ hội, Score 50+: 65% cơ hội (tăng 15%)
                    double groupChance = score >= 50 ? 0.65 : (score >= 15 ? 0.45 : 0.0);
                    bool shouldCreateGroup = score >= 15 && rnd.NextDouble() < groupChance && pair.GroupId == -1;
                    
                    if (shouldCreateGroup)
                    {
                        // Tạo group pipes: size phụ thuộc vào score
                        // Score 15-40: size 2-3, Score > 40: size 4
                        int groupSize;
                        if (score >= 15 && score <= 40)
                        {
                            groupSize = rnd.Next(2, 4); // 2-3 pipes
                        }
                        else // score > 40
                        {
                            groupSize = 4; // 4 pipes
                        }
                        int currentGroupId = nextGroupId++;
                        
                        // TẤT CẢ group pipes đều dùng pattern cầu thang (staircase)
                        // Chọn loại: tĩnh hoặc có animation
                        bool isAnimatedGroup = rnd.NextDouble() < 0.5; // 50% tĩnh, 50% animated
                        
                        int minTopHeight = 100;
                        int minBottomHeight = 100;
                        int maxTopHeight = 590 - gap - minBottomHeight;
                        List<double> groupHeights = new List<double>();
                        
                        // TẤT CẢ group pipes đều dùng pattern cầu thang (staircase)
                        // QUAN TRỌNG: Tạo hình cầu thang dựa trên BOTTOM height
                        // Cầu thang lên: Bottom pipe 1 < Bottom pipe 2 < Bottom pipe 3 (bottom tăng dần)
                        // Cầu thang xuống: Bottom pipe 1 > Bottom pipe 2 > Bottom pipe 3 (bottom giảm dần)
                        // Gap giữa bottom pipe trước và top pipe sau = gap (180px) - CỐ ĐỊNH
                        bool ascending = rnd.NextDouble() < 0.5; // Ngẫu nhiên lên hoặc xuống
                        // Tĩnh: 50px, Animated: 20px
                        double stepSize = isAnimatedGroup ? 20 : 50; // Chênh lệch bottom giữa các pipes
                            
                        // TẤT CẢ group pipes đều dùng pattern cầu thang
                        double currentTopHeight;
                        if (ascending)
                        {
                            // Cầu thang lên: Bottom tăng dần
                            // Pipe đầu tiên: bottom thấp
                            currentTopHeight = minTopHeight + rnd.Next(50, 150);
                            groupHeights.Add(currentTopHeight);
                            
                            for (int g = 1; g < groupSize; g++)
                            {
                                // Bottom của pipe trước
                                double bottomOfPrevious = currentTopHeight + gap;
                                // Bottom của pipe sau = bottom của pipe trước + stepSize (tăng dần 10px) - ƯU TIÊN
                                double nextBottom = bottomOfPrevious + stepSize;
                                // Top height của pipe sau = nextBottom - gap (đảm bảo gap trong pipe = 180px)
                                double nextTopHeight = nextBottom - gap;
                                
                                // Đảm bảo top height hợp lệ
                                if (nextTopHeight < minTopHeight + 50)
                                {
                                    // Không thể tiếp tục, dừng group
                                    break;
                                }
                                
                                // Đảm bảo bottom không vượt quá màn hình
                                if (nextBottom > 590 - minBottomHeight)
                                {
                                    // Không thể tiếp tục, dừng group
                                    break;
                                }
                                
                                nextTopHeight = Math.Clamp(nextTopHeight, minTopHeight + 50, 590 - gap - minBottomHeight);
                                groupHeights.Add(nextTopHeight);
                                currentTopHeight = nextTopHeight;
                            }
                        }
                        else
                        {
                            // Cầu thang xuống: Bottom giảm dần
                            // Pipe đầu tiên: bottom cao
                            double maxBottom = 590 - minBottomHeight;
                            double firstBottom = maxBottom - rnd.Next(0, 100);
                            currentTopHeight = firstBottom - gap;
                            currentTopHeight = Math.Clamp(currentTopHeight, minTopHeight + 50, 590 - gap - minBottomHeight);
                            groupHeights.Add(currentTopHeight);
                            
                            for (int g = 1; g < groupSize; g++)
                            {
                                // Bottom của pipe trước
                                double bottomOfPrevious = currentTopHeight + gap;
                                // Bottom của pipe sau = bottom của pipe trước - stepSize (giảm dần 10px) - ƯU TIÊN
                                double nextBottom = bottomOfPrevious - stepSize;
                                // Top height của pipe sau = nextBottom - gap (đảm bảo gap trong pipe = 180px)
                                double nextTopHeight = nextBottom - gap;
                                
                                // Đảm bảo top height hợp lệ
                                if (nextTopHeight < minTopHeight + 50)
                                {
                                    // Không thể tiếp tục, dừng group
                                    break;
                                }
                                
                                // Đảm bảo bottom không xuống quá thấp
                                if (nextBottom < gap + minTopHeight + 50)
                                {
                                    // Không thể tiếp tục, dừng group
                                    break;
                                }
                                
                                nextTopHeight = Math.Clamp(nextTopHeight, minTopHeight + 50, 590 - gap - minBottomHeight);
                                groupHeights.Add(nextTopHeight);
                                currentTopHeight = nextTopHeight;
                            }
                        }
                        
                        // Nếu không tạo được ít nhất 2 pipes, không tạo group (quay lại pipe bình thường)
                        if (groupHeights.Count < 2)
                        {
                            pair.GroupId = -1;
                            pair.IsGroupLeader = false;
                            RandomizePipe(pair);
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
                        pair.MinTopHeight = minTopHeight;
                        pair.MinBottomHeight = minBottomHeight;
                        ApplyPipeGeometry(pair, groupHeights[0]);
                        
                        if (isAnimatedGroup)
                        {
                            // Animated group: Chỉ random animation properties, KHÔNG thay đổi height
                            RandomizePipeAnimationOnly(pair);
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
                            double groupPipeX = newX + (g * GroupPipeSpacing);
                            string pipeFile = isNight ? "Pipe-night.png" : "Pipe-day.png";
                            
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
                            
                            var groupPair = new PipePairState(groupTop, groupBottom)
                            {
                                GroupId = currentGroupId,
                                IsGroupLeader = false,
                                GroupIndex = g,
                                BaseTopHeight = groupHeights[g],
                                CurrentTopHeight = groupHeights[g],
                                MinTopHeight = minTopHeight,
                                MinBottomHeight = minBottomHeight
                            };
                            
                            // Set height trước (không thay đổi)
                            groupPair.MinTopHeight = minTopHeight;
                            groupPair.MinBottomHeight = minBottomHeight;
                            ApplyPipeGeometry(groupPair, groupHeights[g]);
                            
                            if (isAnimatedGroup)
                            {
                                // Cầu thang animated: Chỉ random animation properties, KHÔNG thay đổi height
                                RandomizePipeAnimationOnly(groupPair);
                            }
                            else
                            {
                                // Cầu thang tĩnh: Không có animation
                                groupPair.EnableVerticalAnimation = false;
                                groupPair.IsMoving = false;
                                groupPair.IsOscillating = false;
                                groupPair.HasTargetMovement = false;
                                groupPair.IsJumpPattern = false;
                            }
                            
                            pipePairs.Add(groupPair);
                        }
                    }
                    else
                    {
                        // Pipe bình thường
                        pair.GroupId = -1;
                        pair.IsGroupLeader = false;
                        RandomizePipe(pair);
                    }

                    score++;
                    totalPipesPassed++;
                    ScoreText.Text = $"Score: {score}";
                    PlaySfx(sfxPoint, "Point.mp3", 0.6);
                    
                    // Kiểm tra xem có cần spawn NoTouch không
                    // Phase 1 (pipes 1-10): không có NoTouch
                    // Phase 2 (pipes 11-20): random 1 con trong khoảng này (chỉ 1 lần)
                    // Phase 3 (pipes 21-30): random 2 con trong khoảng này (chỉ 1 lần)
                    // Phase 4 (pipes 31-40): random 3 con trong khoảng này (chỉ 1 lần)
                    // ... đến Phase 10 (pipes 91-100): random 9 con trong khoảng này (chỉ 1 lần)
                    if (totalPipesPassed > 10)
                    {
                        int currentPhase = (totalPipesPassed - 1) / 10; // Phase 1, 2, 3, ...
                        int phaseStartPipe = currentPhase * 10 + 1; // Pipe bắt đầu phase (11, 21, 31...)
                        int phaseEndPipe = (currentPhase + 1) * 10; // Pipe kết thúc phase (20, 30, 40...)
                        int noTouchCount = Math.Min(9, currentPhase); // Số lượng NoTouch = phase (tối đa 9)
                        
                        // Chỉ random điểm spawn khi bắt đầu phase mới và chưa spawn trong phase này
                        if (noTouchCount > 0 && currentPhase != lastSpawnedPhase && nextNoTouchSpawnAt == -1)
                        {
                            // Random một điểm trong phase hiện tại
                            nextNoTouchSpawnAt = rnd.Next(phaseStartPipe, phaseEndPipe + 1);
                        }
                        
                        // Kiểm tra xem đã đến điểm spawn chưa và chưa spawn trong phase này
                        if (totalPipesPassed >= nextNoTouchSpawnAt && nextNoTouchSpawnAt > 0 && currentPhase != lastSpawnedPhase)
                        {
                            // Spawn NoTouch ngay sau pipe vừa recycle
                            SpawnNoTouchGroup(noTouchCount, newX + PipeSpacing);
                            
                            // Đánh dấu đã spawn trong phase này
                            lastSpawnedPhase = currentPhase;
                            nextNoTouchSpawnAt = -1;
                        }
                    }
                }

                // Kiểm tra collision với pipes
                if (graceTicksRemaining <= 0 &&
                    (FlappyBird.CollidesWith(top) || FlappyBird.CollidesWith(bottom)))
                {
                    EndGame();
                    return;
                }
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
            
            // Xử lý Gates (cổng)
            for (int i = gates.Count - 1; i >= 0; i--)
            {
                if (i < 0 || i >= gates.Count) break;
                var gateState = gates[i];
                if (gateState == null || gateState.Gate == null || !GameCanvas.Children.Contains(gateState.Gate))
                {
                    if (i < gates.Count)
                        gates.RemoveAt(i);
                    continue;
                }

                double gateX = Canvas.GetLeft(gateState.Gate);
                if (double.IsNaN(gateX) || gateX < -150)
                {
                    // Cổng đã đi qua màn hình, xóa nó
                    if (GameCanvas.Children.Contains(gateState.Gate))
                        GameCanvas.Children.Remove(gateState.Gate);
                    gates.RemoveAt(i);
                    continue;
                }

                // Di chuyển cổng ngang
                Canvas.SetLeft(gateState.Gate, gateX - speed);

                // Kiểm tra collision với chim - chỉ kiểm tra khi cổng gần chim để tối ưu
                if (!gateState.IsActivated && graceTicksRemaining <= 0)
                {
                    // Chỉ kiểm tra collision khi cổng ở gần chim (trong phạm vi 150px) và mỗi 5 frame để giảm lag
                    double birdX = Canvas.GetLeft(FlappyBird);
                    double distance = Math.Abs(gateX - birdX);
                    
                    // Chỉ kiểm tra khi gần và mỗi 5 frame (giảm tần suất kiểm tra từ mỗi frame xuống mỗi 5 frame)
                    if (distance < 150 && (frameCount % 5 == 0))
                    {
                        if (FlappyBird.CollidesWith(gateState.Gate))
                        {
                            gateState.IsActivated = true;
                            // Trigger chuyển đổi ngày/đêm
                            SmoothToggleDayNight();
                            
                            // Thêm hiệu ứng khi vào cổng - làm cổng sáng lên (đơn giản hơn)
                            gateState.Gate.Opacity = 1.0;
                        }
                    }
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
















