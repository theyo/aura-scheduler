using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.UI.Xaml;

namespace AuraScheduler.UI
{
    public partial class NotifyIconViewModel : ObservableObject
    {
        private MainWindow? _window;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ShowWindowCommand))]
        [NotifyCanExecuteChangedFor(nameof(HideWindowCommand))]
        public partial bool IsWindowVisible { get; set; } = true;

        public void SetWindow(MainWindow window)
        {
            _window = window;
            IsWindowVisible = true;
        }

        [RelayCommand(CanExecute = nameof(CanShowWindow))]
        public virtual void ShowWindow()
        {
            if (_window is null)
                return;

            _window.AppWindow.Show();
            _window.Activate();
            IsWindowVisible = true;
        }

        [RelayCommand(CanExecute = nameof(CanHideWindow))]
        public virtual void HideWindow()
        {
            if (_window is null)
                return;

            _window.AppWindow.Hide();
            IsWindowVisible = false;
        }

        [RelayCommand]
        public virtual async Task ExitApplication()
        {
            if (Application.Current is App app)
                await app.ExitAsync();
            else
                Application.Current.Exit();
        }

        private bool CanShowWindow() => !IsWindowVisible;
        private bool CanHideWindow() => IsWindowVisible;
    }
}
