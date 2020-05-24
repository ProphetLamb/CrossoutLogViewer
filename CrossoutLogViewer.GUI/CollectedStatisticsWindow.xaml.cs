﻿using ControlzEx.Theming;

using CrossoutLogView.Common;
using CrossoutLogView.Database;
using CrossoutLogView.Database.Collection;
using CrossoutLogView.Database.Connection;
using CrossoutLogView.Database.Data;
using CrossoutLogView.Database.Events;
using CrossoutLogView.GUI.Controls;
using CrossoutLogView.GUI.Core;
using CrossoutLogView.GUI.Events;
using CrossoutLogView.GUI.Models;
using CrossoutLogView.Statistics;

using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace CrossoutLogView.GUI
{
    /// <summary>
    /// Interaction logic for CollectedStatisticsWindow.xaml
    /// </summary>
    public partial class CollectedStatisticsWindow : MetroWindow
    {
        private CollectedStatisticsWindowViewModel viewModel;

        private bool forceClose = false;

        public CollectedStatisticsWindow()
        {
            Logging.WriteLine<CollectedStatisticsWindow>("Loading CollectedStatisticsWindow", true);
            InitializeComponent();
        }
        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            var controller = await this.ShowProgressAsync("Starting", "Preparing views.\r\nPlease stand by...", settings: new MetroDialogSettings
            {
                AnimateHide = false,
                AnimateShow = false,
                ColorScheme = MetroDialogOptions.ColorScheme
            });
            controller.SetIndeterminate();
            await Task.Delay(50); //ensure that the wait window loaded, before freezing
            DataContext = viewModel = new CollectedStatisticsWindowViewModel();
            viewModel.PropertyChanged += OnPropertyChanged;

            UserGamesViewGames.User = viewModel.MeUser;
            UserGamesViewGames.DataGridGames.OpenViewModel += GamesOpenGameDoubleClick;

            UserListViewUsers.ItemsSource = viewModel.UserListModels;
            var userListView = (CollectionView)CollectionViewSource.GetDefaultView(UserListViewUsers.ItemsSource);
            userListView.Filter = UserListFilter;

            WeaponListViewWeapons.ItemsSource = viewModel.WeaponModels;

            MapsView.Maps = viewModel.Maps;
            MapsView.PlayerGamesDataGrid.OpenViewModel += GamesOpenGameDoubleClick;

            CollectedStatisticsWindowViewModel.InvalidatedCachedData += OnInvalidateCachedData;

            Title = String.Concat(Title, " (", viewModel.MeUser.Object.Name, ")");
            HamburgerMenuControl.Focus();
            await controller.CloseAsync();

            Logging.WriteLine<CollectedStatisticsWindow>("CollectedStatisticsWindow loaded in {TP}");
        }

        private void OnInvalidateCachedData(object sender, InvalidateCachedDataEventArgs e)
        {
            this.BeginInvoke(delegate
            {
                UserListViewUsers.ItemsSource = null;
                CollectionViewSource.GetDefaultView(UserListViewUsers.ItemsSource = viewModel.UserListModels).Refresh();
                WeaponListViewWeapons.ItemsSource = null;
                WeaponListViewWeapons.ItemsSource = viewModel.WeaponModels;
                UserGamesViewGames.DataGridGames.ItemsSource = null;
                CollectionViewSource.GetDefaultView(UserGamesViewGames.DataGridGames.ItemsSource = viewModel.MeUser.Participations).Refresh();
                MapsView.Maps = null;
                MapsView.Maps = viewModel.Maps;
            });
        }


        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(CollectedStatisticsWindowViewModel.UserNameFilter):
                    CollectionViewSource.GetDefaultView(UserListViewUsers.ItemsSource).Refresh();
                    break;
            }
        }

        #region Confim close
        protected override void OnClosing(CancelEventArgs e)
        {
            if (e.Cancel) return;
            if (!forceClose)
            {
                e.Cancel = true;
                Dispatcher.BeginInvoke(new Action(async delegate
                {
                    await ConfirmClose();
                }));
            }
            else
            {
                base.OnClosing(e);
            }
        }

        private async Task ConfirmClose()
        {
            var settings = new MetroDialogSettings
            {
                AnimateHide = false,
                AnimateShow = false,
                AffirmativeButtonText = "Laucher",
                NegativeButtonText = "Exit",
                FirstAuxiliaryButtonText = "Cancel",
                MaximumBodyHeight = 80,
                ColorScheme = MetroDialogOptions.ColorScheme
            };
            var result = await this.ShowMessageAsync(
                "Exit confirmation",
                "You mean to leave me, or just go back to the launcher?",
                MessageDialogStyle.AffirmativeAndNegativeAndSingleAuxiliary,
                settings);
            switch (result)
            {
                case MessageDialogResult.Affirmative:
                    new LauncherWindow().Show();
                    forceClose = true;
                    break;
                case MessageDialogResult.FirstAuxiliary:
                    forceClose = false;
                    break;
                default:
                    forceClose = true;
                    break;
            }
            if (forceClose) Close();
        }
        #endregion

        private void HamburgerMenuControl_ItemInvoked(object sender, HamburgerMenuItemInvokedEventArgs args)
        {
            if (args.InvokedItem is HamburgerMenuItem hmi && hmi.Label == "Settings")
            {
                //ContentTabControl.SelectedItem = TabItemSettings;
                HamburgerMenuControl.SelectedIndex = ContentTabControl.SelectedIndex;
                new SettingsWindow().ShowDialog();
                args.Handled = true;
            }
            else
            {
                ContentTabControl.SelectedIndex = HamburgerMenuControl.SelectedIndex;
                HamburgerMenuControl.IsPaneOpen = false;
            }
        }

        private void CloseHamburgerMenuPane(object sender, RoutedEventArgs e)
        {
            HamburgerMenuControl.IsPaneOpen = false;
        }

        private void GamesOpenGameDoubleClick(object sender, OpenModelViewerEventArgs e)
        {
            if (e.ViewModel is PlayerGameCompositeModel pgc)
            {
                Logging.WriteLine<CollectedStatisticsWindow>("Open game view.");
                new NavigationWindow(pgc.Game).ShowDialog();
            }
            else if (e.ViewModel is UserListModel ul)
            {
                Logging.WriteLine<CollectedStatisticsWindow>("Open user list.");
                new NavigationWindow(ul).ShowDialog();
            }
        }

        private void UserSelectUser(object sender, SelectionChangedEventArgs e)
        {
            if ((sender as UserDataGrid).SelectedItem is UserModel model)
            {
                UserOverviewUsers.DataContext = new UserModel(model.Object);
            }
        }

        private bool UserListFilter(object obj)
        {
            if (String.IsNullOrEmpty(viewModel.UserNameFilter)) return true;
            if (!(obj is UserModel ul)) return false;
            foreach (var part in viewModel.UserNameFilter.TrimEnd().Split(' ', '-', '_'))
            {
                if (!ul.Object.Name.Contains(part, StringComparison.InvariantCultureIgnoreCase)) return false;
            }
            return true;
        }

        private void UserOpenUserDoubleClick(object sender, OpenModelViewerEventArgs e)
        {
            if (e.ViewModel is UserModel user)
            {
                Logging.WriteLine<CollectedStatisticsWindow>("Open user view.");
                new NavigationWindow(new UserModel(user.Object)).ShowDialog();
            }
        }

        private void WeaponOpenUserDoubleClick(object sender, OpenModelViewerEventArgs e)
        {
            if (e.ViewModel is UserModel weapon)
            {
                Logging.WriteLine<CollectedStatisticsWindow>("Open user view.");
                new NavigationWindow(weapon).ShowDialog();
            }
        }
    }
}