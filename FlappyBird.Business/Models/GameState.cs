using FlappyBird.Business.Models;
using System.Collections.Generic;
using System.Drawing;


namespace FlappyBird.Business.Models
{
    public class GameState
    {
        public Bird Bird { get; set; }
        public List<Rectangle> Pipes { get; set; } = new();
        public bool IsGameOver { get; set; }
        public int Score { get; set; }

        public GameState()
        {
            Reset();
        }

        public void Reset()
        {
            Bird = new Bird(100, 200);
            Pipes.Clear();
            IsGameOver = false;
            Score = 0;
        }
    }
}
