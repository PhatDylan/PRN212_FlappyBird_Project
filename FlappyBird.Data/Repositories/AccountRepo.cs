using FlappyBird.Business.Models;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FlappyBird.Data.Repositories
{
    public class AccountRepo
    {
        private readonly string accountsFilePath = "accounts.json";
        private List<Account>? _accounts;

        private List<Account> LoadAccounts()
        {
            if (_accounts != null) return _accounts;

            if (!File.Exists(accountsFilePath))
            {
                _accounts = new List<Account>();
                return _accounts;
            }

            try
            {
                string json = File.ReadAllText(accountsFilePath);
                _accounts = JsonSerializer.Deserialize<List<Account>>(json) ?? new List<Account>();
                return _accounts;
            }
            catch
            {
                _accounts = new List<Account>();
                return _accounts;
            }
        }

        private void SaveAccounts()
        {
            if (_accounts == null) return;

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_accounts, options);
                File.WriteAllText(accountsFilePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi khi lưu tài khoản: {ex.Message}");
            }
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(password);
                byte[] hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        public bool Register(Account account)
        {
            var accounts = LoadAccounts();

            // Kiểm tra email đã tồn tại chưa
            if (accounts.Any(a => a.Email.Equals(account.Email, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // Hash password trước khi lưu
            account.Password = HashPassword(account.Password);
            accounts.Add(account);
            SaveAccounts();

            return true;
        }

        public Account? Login(string email, string password)
        {
            var accounts = LoadAccounts();
            string hashedPassword = HashPassword(password);

            return accounts.FirstOrDefault(a =>
                a.Email.Equals(email, StringComparison.OrdinalIgnoreCase) &&
                a.Password == hashedPassword);
        }

        public Account? GetAccountByEmail(string email)
        {
            var accounts = LoadAccounts();
            return accounts.FirstOrDefault(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        }

        public bool UpdateAccount(Account account)
        {
            var accounts = LoadAccounts();
            var existingAccount = accounts.FirstOrDefault(a => a.Email.Equals(account.Email, StringComparison.OrdinalIgnoreCase));

            if (existingAccount == null) return false;

            // Giữ nguyên password nếu không thay đổi
            if (string.IsNullOrEmpty(account.Password) || account.Password == existingAccount.Password)
            {
                account.Password = existingAccount.Password;
            }
            else
            {
                account.Password = HashPassword(account.Password);
            }

            int index = accounts.IndexOf(existingAccount);
            accounts[index] = account;
            SaveAccounts();

            return true;
        }

        public bool UpdateHighScore(string email, int score)
        {
            var account = GetAccountByEmail(email);
            if (account == null) return false;

            if (score > account.HighScore)
            {
                account.HighScore = score;
                return UpdateAccount(account);
            }

            return true;
        }

        public List<Account> GetTopScores(int topCount = 10)
        {
            var accounts = LoadAccounts();
            return accounts
                .Where(a => a.HighScore > 0)
                .OrderByDescending(a => a.HighScore)
                .ThenBy(a => a.CreatedAt)
                .Take(topCount)
                .ToList();
        }
    }
}

