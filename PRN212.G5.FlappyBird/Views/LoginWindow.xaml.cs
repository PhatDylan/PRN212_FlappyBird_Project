using FlappyBird.Business.Models;
using FlappyBird.Data.Repositories;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PRN212.G5.FlappyBird.Views
{
    public partial class LoginWindow : Window
    {
        private readonly AccountRepo accountRepo = new();
        public Account? LoggedInAccount { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            EmailTextBox.Focus();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            AttemptLogin();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AttemptLogin();
            }
        }

        private void AttemptLogin()
        {
            string email = EmailTextBox.Text.Trim();
            string password = PasswordBox.Password;

            // Validation
            if (string.IsNullOrEmpty(email))
            {
                ShowError("Vui lòng nhập email!");
                EmailTextBox.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Vui lòng nhập mật khẩu!");
                PasswordBox.Focus();
                return;
            }

            if (!IsValidEmail(email))
            {
                ShowError("Email không hợp lệ!");
                EmailTextBox.Focus();
                return;
            }

            // Attempt login
            var account = accountRepo.Login(email, password);

            if (account == null)
            {
                ShowError("❌ Email hoặc mật khẩu không đúng! Vui lòng kiểm tra lại.");
                PasswordBox.Clear();
                PasswordBox.Focus();
                
                // Thêm hiệu ứng rung nhẹ cho TextBox để thu hút sự chú ý
                System.Windows.Media.Animation.DoubleAnimation shakeAnimation = new System.Windows.Media.Animation.DoubleAnimation();
                shakeAnimation.From = 0;
                shakeAnimation.To = 10;
                shakeAnimation.Duration = new System.Windows.Duration(TimeSpan.FromMilliseconds(50));
                shakeAnimation.RepeatBehavior = new System.Windows.Media.Animation.RepeatBehavior(4);
                shakeAnimation.AutoReverse = true;
                
                var translateTransform = new System.Windows.Media.TranslateTransform();
                EmailTextBox.RenderTransform = translateTransform;
                translateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, shakeAnimation);
                
                return;
            }

            // Success - store account and close
            LoggedInAccount = account;
            DialogResult = true;
            Close();
        }

        private void RegisterLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var registerWindow = new RegisterWindow();
            if (registerWindow.ShowDialog() == true)
            {
                // Auto-fill email if registration was successful
                EmailTextBox.Text = registerWindow.RegisteredEmail ?? "";
                PasswordBox.Focus();
            }
        }

        private void ShowError(string message)
        {
            ErrorMessageText.Text = message;
            ErrorMessageText.Visibility = Visibility.Visible;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ErrorMessageText.Visibility = Visibility.Collapsed;
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Can add validation here if needed
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ErrorMessageText.Visibility = Visibility.Collapsed;
        }

        private void PasswordBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Can add validation here if needed
        }
    }
}

