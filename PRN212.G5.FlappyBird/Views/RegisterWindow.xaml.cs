using FlappyBird.Business.Models;
using FlappyBird.Data.Repositories;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PRN212.G5.FlappyBird.Views
{
    public partial class RegisterWindow : Window
    {
        private readonly AccountRepo accountRepo = new();
        private string? avatarPath = null;
        public string? RegisteredEmail { get; private set; }

        public RegisterWindow()
        {
            InitializeComponent();
            NameTextBox.Focus();
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            AttemptRegister();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AttemptRegister();
            }
        }

        private void AttemptRegister()
        {
            string name = NameTextBox.Text.Trim();
            string email = EmailTextBox.Text.Trim();
            string password = PasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;

            // Validation
            if (string.IsNullOrEmpty(name))
            {
                ShowError("Vui lòng nhập tên hiển thị!");
                NameTextBox.Focus();
                return;
            }

            if (name.Length < 2)
            {
                ShowError("Tên hiển thị phải có ít nhất 2 ký tự!");
                NameTextBox.Focus();
                return;
            }

            if (string.IsNullOrEmpty(email))
            {
                ShowError("Vui lòng nhập email!");
                EmailTextBox.Focus();
                return;
            }

            if (!IsValidEmail(email))
            {
                ShowError("Email không hợp lệ!");
                EmailTextBox.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Vui lòng nhập mật khẩu!");
                PasswordBox.Focus();
                return;
            }

            if (password.Length < 6)
            {
                ShowError("Mật khẩu phải có ít nhất 6 ký tự!");
                PasswordBox.Focus();
                return;
            }

            if (password != confirmPassword)
            {
                ShowError("Mật khẩu xác nhận không khớp!");
                ConfirmPasswordBox.Focus();
                return;
            }

            // Check if email already exists
            if (accountRepo.GetAccountByEmail(email) != null)
            {
                ShowError("Email này đã được sử dụng!");
                EmailTextBox.Focus();
                return;
            }

            // Convert avatar to base64 if exists
            string avatarBase64 = "";
            if (!string.IsNullOrEmpty(avatarPath) && File.Exists(avatarPath))
            {
                try
                {
                    byte[] imageBytes = File.ReadAllBytes(avatarPath);
                    avatarBase64 = Convert.ToBase64String(imageBytes);
                }
                catch
                {
                    // If conversion fails, continue without avatar
                }
            }

            // Create account
            Account newAccount = new Account(email, password, name, avatarBase64);

            if (accountRepo.Register(newAccount))
            {
                RegisteredEmail = email;
                MessageBox.Show("Đăng ký thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                ShowError("Đăng ký thất bại! Email có thể đã tồn tại.");
            }
        }

        private void SelectAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All files (*.*)|*.*",
                Title = "Chọn ảnh đại diện"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                avatarPath = openFileDialog.FileName;
                LoadAvatarImage(avatarPath);
                RemoveAvatarButton.Visibility = Visibility.Visible;
            }
        }

        private void RemoveAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            avatarPath = null;
            AvatarPreview.Source = null;
            DefaultAvatarText.Visibility = Visibility.Visible;
            RemoveAvatarButton.Visibility = Visibility.Collapsed;
        }

        private void LoadAvatarImage(string path)
        {
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.DecodePixelWidth = 160; // Optimize for display
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                AvatarPreview.Source = bitmap;
                DefaultAvatarText.Visibility = Visibility.Collapsed;
            }
            catch
            {
                ShowError("Không thể tải ảnh!");
            }
        }

        private void LoginLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DialogResult = false;
            Close();
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

