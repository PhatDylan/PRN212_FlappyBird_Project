namespace FlappyBird.Business.Models
{
    public class Account
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty; // Path to avatar image or base64 string
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int HighScore { get; set; } = 0;

        public Account() { }

        public Account(string email, string password, string name, string avatar = "")
        {
            Email = email;
            Password = password;
            Name = name;
            Avatar = avatar;
            CreatedAt = DateTime.Now;
        }
    }
}

