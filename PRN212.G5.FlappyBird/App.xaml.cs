using System.Configuration;
using System.Data;
using System.Windows;
using PRN212.G5.FlappyBird.Views;

namespace PRN212.G5.FlappyBird
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set ShutdownMode to OnExplicitShutdown so app doesn't close when login window closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Show login window first
            var loginWindow = new LoginWindow();
            bool? dialogResult = loginWindow.ShowDialog();
            
            if (dialogResult == true && loginWindow.LoggedInAccount != null)
            {
                try
                {
                    // Open main window with logged in account
                    var mainWindow = new MainWindow(loginWindow.LoggedInAccount);
                    
                    // Set as main window so app doesn't close when this window closes
                    MainWindow = mainWindow;
                    ShutdownMode = ShutdownMode.OnMainWindowClose;
                    
                    mainWindow.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi mở màn hình game: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                }
            }
            else
            {
                // User closed login window without logging in
                Shutdown();
            }
        }
    }

}
