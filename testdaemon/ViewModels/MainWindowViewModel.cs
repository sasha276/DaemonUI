using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using testdaemon.Models;
using testdaemon.Service;
using testdaemon.Views;

namespace testdaemon.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public NavigationService Nav => NavigationService.Instance;
}
