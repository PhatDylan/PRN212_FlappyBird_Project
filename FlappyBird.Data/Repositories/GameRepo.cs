using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlappyBird.Data.Repositories
{
    public class GameRepo
    {
        private readonly string filePath = "highscore.txt";

        public int LoadHighScore()
        {
            if (!File.Exists(filePath)) return 0;
            return int.TryParse(File.ReadAllText(filePath), out int score) ? score : 0;
        }

        public void SaveHighScore(int score)
        {
            File.WriteAllText(filePath, score.ToString());
        }
    }
}
