using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeSorterWin;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 兜底：如果 App.OnStartup 的时机仍导致 DataContext 为空，则在窗口构造时补上。
        if (DataContext is null && App.Services is not null)
            DataContext = App.Services.GetRequiredService<ViewModels.MainViewModel>();
    }
}