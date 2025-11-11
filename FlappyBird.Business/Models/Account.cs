using System;

namespace FlappyBird.Business.Models
{
    public class Account
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public int HighScore { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Account() { }

        public Account(string email, string name)
        {
            Email = email;
            Name = name;
        }
    }
}
