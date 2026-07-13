using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using testdaemon.Service;

namespace testdaemon.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public SettingsService SettingsService => SettingsService.Instance;
    public InstallSettings InstallSettings => InstallSettings.Instance;

    [ObservableProperty] private string _installStatus = "";
    [ObservableProperty] private bool   _confirmRestart;

    [RelayCommand]
    void GoHome() => NavigationService.Instance.GoHome();

    [RelayCommand]
    async Task PickAppdFolderAsync()
    {
        var folder = await DialogService.PickFolderAsync(
            "Выберите папку, где лежит appd.exe (обычно target\\release)");
        if (folder == null) return;

        InstallSettings.AppdFolder = folder;
        InstallStatus = $"Папка сохранена: {folder}";
    }

    [RelayCommand]
    async Task InstallServiceAsync()
    {
        if (string.IsNullOrEmpty(InstallSettings.AppdFolder))
        {
            InstallStatus = "Сначала выбери папку с appd.exe";
            return;
        }

        InstallStatus = "Устанавливаю службу (подтверди запрос UAC)...";

        var folder = InstallSettings.AppdFolder;
        var (ok, error) = await Task.Run(() =>
        {
            var success = ServiceInstaller.Install(folder, out var err);
            return (success, err);
        });

        if (!ok) { InstallStatus = $"Ошибка установки: {error}"; return; }

        InstallStatus = "Служба установлена, запускаю...";

        var (started, startErr) = await Task.Run(() =>
        {
            var success = ServiceInstaller.Start(out var err);
            return (success, err);
        });

        InstallStatus = started
            ? "Служба установлена и запущена"
            : $"Установлена, но не запустилась: {startErr}";
    }

    [RelayCommand]
    async Task UninstallServiceAsync()
    {
        if (string.IsNullOrEmpty(InstallSettings.AppdFolder))
        {
            InstallStatus = "Папка с appd.exe не выбрана";
            return;
        }

        var folder = InstallSettings.AppdFolder;
        var (ok, error) = await Task.Run(() =>
        {
            var success = ServiceInstaller.Uninstall(folder, out var err);
            return (success, err);
        });

        InstallStatus = ok ? "Служба удалена" : $"Ошибка удаления: {error}";
    }

    [RelayCommand(CanExecute = nameof(CanRestart))]
    async Task RestartComputerAsync()
    {
        InstallStatus = "Перезагрузка компьютера...";
        var (ok, error) = await Task.Run(() =>
        {
            var success = ServiceInstaller.Restart(out var err);
            return (success, err);
        });
        if (!ok) InstallStatus = $"Не удалось перезагрузить: {error}";
        // при успехе компьютер уйдёт в перезагрузку раньше, чем что-то ещё выполнится
    }

    bool CanRestart() => ConfirmRestart;

    partial void OnConfirmRestartChanged(bool value) => RestartComputerCommand.NotifyCanExecuteChanged();
}