using System;
using System.Collections.Generic;

namespace FlappyBird.Business.Models
{
    public class GameEngine
    {
        public Bird Bird { get; private set; }
        public List<Pipe> Pipes { get; private set; } = new();
        public int Score { get; private set; } = 0;
        public bool IsGameOver { get; private set; } = false;

        private readonly Random rnd = new();
        private double pipeTimer = 0;

        private const double PipeSpawnInterval = 1.5;  // Giây
        private const double PipeWidth = 80;
        private const double PipeSpeed = 150;          // pixel/s
        private const int Gap = 150;
        private const double CanvasHeight = 500;
        private const double CanvasWidth = 800;
        private const double BirdSize = 30;            // kích thước mặc định của chim

        public GameEngine()
        {
            Reset();
        }

        public void Update(double dt)
        {
            if (IsGameOver) return;

            // Cập nhật chuyển động của chim
            Bird.Update(dt);

            // Sinh thêm ống
            pipeTimer += dt;
            if (pipeTimer > PipeSpawnInterval)
            {
                pipeTimer = 0;
                AddPipePair();
            }

            // Di chuyển ống
            foreach (var pipe in Pipes)
            {
                pipe.X -= PipeSpeed * dt;
            }

            // Xóa ống ra ngoài màn hình
            Pipes.RemoveAll(p => p.X + PipeWidth < 0);

            // Cập nhật điểm
            foreach (var pipe in Pipes)
            {
                if (!pipe.Passed && pipe.X + pipe.Width < Bird.X)
                {
                    pipe.Passed = true;
                    Score++;
                }
            }

            // Kiểm tra va chạm
            CheckCollision();
        }

        private void AddPipePair()
        {
            double topHeight = rnd.Next(50, 250);
            double bottomY = topHeight + Gap;
            double bottomHeight = CanvasHeight - bottomY;

            // Ống trên
            Pipes.Add(new Pipe
            {
                X = CanvasWidth,
                Y = 0,
                Width = PipeWidth,
                Height = topHeight
            });

            // Ống dưới
            Pipes.Add(new Pipe
            {
                X = CanvasWidth,
                Y = bottomY,
                Width = PipeWidth,
                Height = bottomHeight
            });
        }

        private void CheckCollision()
        {
            // Chim chạm trần hoặc sàn
            if (Bird.Y < 0 || Bird.Y + BirdSize > CanvasHeight)
            {
                IsGameOver = true;
                return;
            }

            // Chim chạm ống
            foreach (var pipe in Pipes)
            {
                if (Bird.X + BirdSize > pipe.X &&
                    Bird.X < pipe.X + pipe.Width &&
                    Bird.Y + BirdSize > pipe.Y &&
                    Bird.Y < pipe.Y + pipe.Height)
                {
                    IsGameOver = true;
                    return;
                }
            }
        }

        public void Jump()
        {
            if (!IsGameOver)
                Bird.Jump();
        }

        public void Reset()
        {
            Bird = new Bird(100, 250);
            Pipes.Clear();
            Score = 0;
            IsGameOver = false;
            pipeTimer = 0;
        }
    }
}
