using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using testdaemon.ViewModels;

namespace testdaemon.Views;

public partial class CanStreamView : Window
{
    public CanStreamView(StreamViewModel svm)
    {
        InitializeComponent();

        this.DataContext = svm;
    }
}