using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using testdaemon.ViewModels;

namespace testdaemon.Views;

public partial class HomeView : UserControl
{
    private const double SidePanelWidth = 400;
    private bool _sidePanelOpen;

    public HomeView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is not HomeViewModel vm) return;

            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(HomeViewModel.AdditionalWindow)) return;

                if (TopLevel.GetTopLevel(this) is not Window window) return;

                if (vm.AdditionalWindow && !_sidePanelOpen)
                {
                    _sidePanelOpen = true;
                    window.Width += SidePanelWidth;
                }
                else if (!vm.AdditionalWindow && _sidePanelOpen)
                {
                    _sidePanelOpen = false;
                    window.Width -= SidePanelWidth;
                }
            };
        };
    }
}