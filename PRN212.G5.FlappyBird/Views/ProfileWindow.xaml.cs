using FlappyBird.Business.Models;
using FlappyBird.Data.Repositories;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace PRN212.G5.FlappyBird.Views
{
    public partial class ProfileWindow : Window
    {
        private readonly AccountRepo accountRepo = new();
        private Account currentAccount;
        private string? avatarPath = null;

        public Account? UpdatedAccount { get; private set; }

        public ProfileWindow(Account account)
        {
            InitializeComponent();
            currentAccount = account;
            LoadAccountData();
        }

        private void LoadAccountData()
        {
            NameTextBox.Text = currentAccount.Name;
            EmailTextBox.Text = currentAccount.Email;
            HighScoreText.Text = currentAccount.HighScore.ToString();

            // Load avatar if exists
            if (!string.IsNullOrEmpty(currentAccount.Avatar))
            {
                LoadAvatarFromBase64(currentAccount.Avatar);
                RemoveAvatarButton.Visibility = Visibility.Visible;
            }
            else
            {
                DefaultAvatarText.Visibility = Visibility.Visible;
                RemoveAvatarButton.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadAvatarFromBase64(string base64String)
        {
            try
            {
                byte[] imageBytes = Convert.FromBase64String(base64String);
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(imageBytes);
                bitmap.DecodePixelWidth = 240;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                AvatarImage.Source = bitmap;
                DefaultAvatarText.Visibility = Visibility.Collapsed;
            }
            catch
            {
                DefaultAvatarText.Visibility = Visibility.Visible;
            }
        }

        private void ChangeAvatarButton_Click(object sender, RoutedEventArgs e)
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
            AvatarImage.Source = null;
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
                bitmap.DecodePixelWidth = 240;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                AvatarImage.Source = bitmap;
                DefaultAvatarText.Visibility = Visibility.Collapsed;
            }
            catch
            {
                ShowError("Không thể tải ảnh!");
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTextBox.Text.Trim();
            string newPassword = NewPasswordBox.Password;
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

            // Password validation (if provided)
            if (!string.IsNullOrEmpty(newPassword))
            {
                if (newPassword.Length < 6)
                {
                    ShowError("Mật khẩu phải có ít nhất 6 ký tự!");
                    NewPasswordBox.Focus();
                    return;
                }

                if (newPassword != confirmPassword)
                {
                    ShowError("Mật khẩu xác nhận không khớp!");
                    ConfirmPasswordBox.Focus();
                    return;
                }
            }

            // Update account
            currentAccount.Name = name;

            // Update password if provided
            if (!string.IsNullOrEmpty(newPassword))
            {
                currentAccount.Password = newPassword; // Will be hashed in UpdateAccount
            }
            else
            {
                currentAccount.Password = ""; // Keep old password
            }

            // Update avatar if changed
            if (avatarPath != null && File.Exists(avatarPath))
            {
                try
                {
                    byte[] imageBytes = File.ReadAllBytes(avatarPath);
                    currentAccount.Avatar = Convert.ToBase64String(imageBytes);
                }
                catch
                {
                    // Keep old avatar if conversion fails
                }
            }
            else if (avatarPath == null && AvatarImage.Source == null)
            {
                // User removed avatar
                currentAccount.Avatar = "";
            }

            // Save account
            if (accountRepo.UpdateAccount(currentAccount))
            {
                UpdatedAccount = currentAccount;
                ShowSuccess("Đã cập nhật thông tin thành công!");
                NewPasswordBox.Clear();
                ConfirmPasswordBox.Clear();
            }
            else
            {
                ShowError("Không thể cập nhật thông tin!");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void ShowError(string message)
        {
            ErrorMessageText.Text = message;
            ErrorMessageText.Visibility = Visibility.Visible;
            SuccessMessageText.Visibility = Visibility.Collapsed;
        }

        private void ShowSuccess(string message)
        {
            SuccessMessageText.Text = message;
            SuccessMessageText.Visibility = Visibility.Visible;
            ErrorMessageText.Visibility = Visibility.Collapsed;
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ErrorMessageText.Visibility = Visibility.Collapsed;
            SuccessMessageText.Visibility = Visibility.Collapsed;
        }

        private void PasswordBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ErrorMessageText.Visibility = Visibility.Collapsed;
            SuccessMessageText.Visibility = Visibility.Collapsed;
        }
    }
}

