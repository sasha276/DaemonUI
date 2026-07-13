using CommunityToolkit.Mvvm.ComponentModel;

namespace testdaemon.Models;

public partial class Settings : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _desc = string.Empty;
    [ObservableProperty] private bool _cheked ;
}

 