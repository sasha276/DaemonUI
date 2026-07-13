using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using testdaemon.Models;
using testdaemon.Service;

namespace testdaemon.ViewModels;

public partial class SettingsViewModel:ObservableObject
{
    public SettingsService SettingsService => SettingsService.Instance;
    
    [RelayCommand]
    void GoHome() => NavigationService.Instance.GoHome();
}