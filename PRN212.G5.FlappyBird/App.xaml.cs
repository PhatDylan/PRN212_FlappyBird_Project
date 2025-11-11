using System.Windows;
using PRN212.G5.FlappyBird.Views;

namespace PRN212.G5.FlappyBird
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnMainWindowClose;

            var loginWindow = new LoginWindow();

            MainWindow = loginWindow;
            loginWindow.Show();
        }
    }
}
