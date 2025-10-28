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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PRN212.G5.FlappyBird.Views
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer gameTimer = new();
        private readonly GameRepo gameRepo = new();   //Dùng GameRepo để load/save điểm

        private double birdSpeed = 0;
        private int score = 0;
        private int highScore = 0;
        private readonly Random rnd = new();

        private readonly List<Rectangle> pipesTop = new();
        private readonly List<Rectangle> pipesBottom = new();
        private readonly List<Rectangle> clouds = new(); // danh sách đám mây

        private double pipeSpeed = 5; // tốc độ di chuyển ống
        private const int gap = 150;  // khoảng cách giữa ống trên/dưới
        private double cloudSpeed = 2; // tốc độ mây (chậm hơn pipe)

        private SoundPlayer? jumpSound;
        private SoundPlayer? hitSound;

        public MainWindow()
        {
            InitializeComponent();
            this.KeyDown += Window_KeyDown;

            // Nếu có file âm thanh, bật 2 dòng dưới lên
            //jumpSound = new SoundPlayer("jump.wav");
            //hitSound = new SoundPlayer("hit.wav");

            StartGame();
        }

        private void StartGame()
        {
            // Đặt lại vị trí và trạng thái ban đầu
            Canvas.SetTop(FlappyBird, 250);
            birdSpeed = 0;
            score = 0;
            pipeSpeed = 5;
            ScoreText.Text = "Score: 0";

            // Load high score từ repo
            highScore = gameRepo.LoadHighScore();
            HighScoreText.Text = $"High Score: {highScore}";

            // Xóa các ống và mây cũ
            foreach (var p in pipesTop) GameCanvas.Children.Remove(p);
            foreach (var p in pipesBottom) GameCanvas.Children.Remove(p);
            foreach (var c in clouds) GameCanvas.Children.Remove(c);
            pipesTop.Clear();
            pipesBottom.Clear();
            clouds.Clear();

            // Tạo đám mây ngẫu nhiên
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

            // Tạo cặp ống ban đầu
            for (int i = 0; i < 4; i++)
            {
                Rectangle top = new() { Width = 80, Fill = Brushes.Green };
                Rectangle bottom = new() { Width = 80, Fill = Brushes.Green };
                GameCanvas.Children.Add(top);
                GameCanvas.Children.Add(bottom);

                double leftPos = 400 + i * 300;
                Canvas.SetLeft(top, leftPos);
                Canvas.SetLeft(bottom, leftPos);

                RandomizePipe(top, bottom);

                pipesTop.Add(top);
                pipesBottom.Add(bottom);
            }

            // Cấu hình game loop
            gameTimer.Interval = TimeSpan.FromMilliseconds(20);
            gameTimer.Tick += GameLoop;
            gameTimer.Start();
        }

        private void GameLoop(object? sender, EventArgs e)
        {
            double birdTop = Canvas.GetTop(FlappyBird);
            Canvas.SetTop(FlappyBird, birdTop + birdSpeed);
            birdSpeed += 1; // trọng lực

            // tăng tốc độ theo điểm
            double speed = pipeSpeed + score * 0.1;

            // Cập nhật vị trí đám mây
            foreach (var c in clouds)
            {
                Canvas.SetLeft(c, Canvas.GetLeft(c) - cloudSpeed);
                if (Canvas.GetLeft(c) < -150)
                {
                    Canvas.SetLeft(c, 800 + rnd.Next(0, 200));
                    Canvas.SetTop(c, rnd.Next(20, 150));
                }
            }

            // Cập nhật vị trí ống
            for (int i = 0; i < pipesTop.Count; i++)
            {
                Canvas.SetLeft(pipesTop[i], Canvas.GetLeft(pipesTop[i]) - speed);
                Canvas.SetLeft(pipesBottom[i], Canvas.GetLeft(pipesBottom[i]) - speed);

                // Khi ống đi ra khỏi màn hình, dịch lại sang phải
                if (Canvas.GetLeft(pipesTop[i]) < -80)
                {
                    Canvas.SetLeft(pipesTop[i], 1000);
                    Canvas.SetLeft(pipesBottom[i], 1000);
                    RandomizePipe(pipesTop[i], pipesBottom[i]);
                    score++;
                    ScoreText.Text = $"Score: {score}";
                }

                // Kiểm tra va chạm
                if (FlappyBird.CollidesWith(pipesTop[i]) || FlappyBird.CollidesWith(pipesBottom[i]))
                {
                    EndGame();
                    return;
                }
            }

            // Chim rơi khỏi canvas hoặc chạm trần
            if (birdTop < 0 || birdTop + FlappyBird.Height > 500)
            {
                EndGame();
            }
        }

        private void RandomizePipe(Rectangle top, Rectangle bottom)
        {
            double topHeight = rnd.Next(50, 250);
            top.Height = topHeight;
            bottom.Height = 500 - topHeight - gap;

            Canvas.SetTop(top, 0);
            Canvas.SetTop(bottom, top.Height + gap);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                birdSpeed = -10;
                jumpSound?.Play();
            }
        }

        private void EndGame()
        {
            gameTimer.Stop();
            //hitSound?.Play();

            if (score > highScore)
            {
                highScore = score;
                gameRepo.SaveHighScore(highScore); // Save high score qua repo
                MessageBox.Show($"🎉 New High Score: {score}!", "Flappy Bird");
            }
            else
            {
                MessageBox.Show($"Game Over! Your Score: {score}", "Flappy Bird");
                this.Close();
            }

            StartGame();
        }
    }

    // Phần để kiểm tra va chạm giữa các Rectangle
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
