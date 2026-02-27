using System.Windows;
using System.Windows.Threading;

namespace PowerManagerWidget;

public partial class App : Application
{
    private void App_Startup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.ToString(), "Ошибка виджета", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                MessageBox.Show(ex.ToString(), "Ошибка виджета", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        try
        {
            var main = new MainWindow();
            main.Show();
            main.Activate();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Ошибка запуска виджета", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
