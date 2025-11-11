namespace FlappyBird.Business.Models
{
    public class Account
    {
        public string Name { get; set; } = string.Empty;
        public int HighScore { get; set; } = 0;

        public Account() { }

        public Account(string name)
        {
            Name = name;
        }
    }
}

