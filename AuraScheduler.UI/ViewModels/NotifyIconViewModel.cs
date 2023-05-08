using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.DependencyInjection;

namespace AuraScheduler.UI
{
    public partial class NotifyIconViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ShowWindowCommand))]
        [NotifyCanExecuteChangedFor(nameof(HideWindowCommand))]
        private WindowState _windowState;

        public NotifyIconViewModel()
        {
            WindowState = Application.Current?.MainWindow?.WindowState ?? WindowState.Normal;
        }

        [RelayCommand(CanExecute = nameof(CanShowWindow))]
        public virtual void ShowWindow()
        {
            var window = Application.Current.MainWindow;

            if (window is not null)
            {
                window.Show();
                window.WindowState = WindowState.Normal;
                window.Focus();

                WindowState = window.WindowState;
            }
        }


        [RelayCommand(CanExecute = nameof(CanHideWindow))]
        public virtual void HideWindow()
        {
            var window = Application.Current.MainWindow;

            if (window is not null)
            {
                window.WindowState = WindowState.Minimized;
                window.Hide();

                WindowState = window.WindowState;
            }
        }

        [RelayCommand]
        public virtual void ExitApplication()
        {
            Application.Current.Shutdown();
        }

        private bool CanHideWindow()
        {
            var window = Application.Current.MainWindow;

            if (window is not null)
            {
                return window.WindowState == WindowState.Normal;
            }

            return false;
        }

        private bool CanShowWindow()
        {
            var window = Application.Current.MainWindow;

            if (window is not null)
            {
                return window.WindowState == WindowState.Minimized;
            }

            return false;
        }
    }
}
