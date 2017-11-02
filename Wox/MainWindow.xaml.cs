﻿using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Forms;
using Wox.Core.Plugin;
using Wox.Core.Resource;
using Wox.Helper;
using Wox.Infrastructure.UserSettings;
using Wox.ViewModel;
using Screen = System.Windows.Forms.Screen;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

namespace Wox
{
    public partial class MainWindow
    {

        #region Private Fields

        private readonly Storyboard _progressBarStoryboard = new Storyboard();
        private Settings _settings;
        private NotifyIcon _notifyIcon;
        private MainViewModel _viewModel;

        #endregion

        public MainWindow(Settings settings, MainViewModel mainVM)
        {
            InitializeComponent();
            DataContext = mainVM;
            _viewModel = mainVM;
            _settings = settings;
            SetWindowPosition();
        }
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            _notifyIcon.Visible = false;
            _viewModel.Save();
        }

        private void OnInitialized(object sender, EventArgs e)
        {
            // show notify icon when wox is hided
            InitializeNotifyIcon();
        }

        private void OnLoaded(object sender, RoutedEventArgs _)
        {
            // todo is there a way to set blur only once?
            ThemeManager.Instance.SetBlurForWindow();
            WindowsInteropHelper.DisableControlBox(this);
            InitProgressbarAnimation();
            InitializePosition();
            // since the default main window visibility is visible
            // so we need set focus during startup
            QueryTextBox.Focus();

            _viewModel.PropertyChanged += (o, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.MainWindowVisibility))
                {
                    if (Visibility == Visibility.Visible)
                    {
                        SetWindowPosition();
                        Activate();
                        QueryTextBox.Focus();
                        _settings.ActivateTimes++;
                        if (!_viewModel.LastQuerySelected)
                        {
                            QueryTextBox.SelectAll();
                            _viewModel.LastQuerySelected = true;
                        }
                    }
                    else if (Visibility == Visibility.Hidden)
                    {
                        this._settings.Save();
                    }
                }
            };
            InitializePosition();
        }

        private void InitializePosition()
        {
            Top = WindowTop();
            Left = WindowLeft();
            _settings.WindowTop = Top;
            _settings.WindowLeft = Left;
        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Text = Infrastructure.Constant.Wox,
                Icon = Properties.Resources.app,
                Visible = true
            };
            var menu = new ContextMenuStrip();
            var items = menu.Items;

            var open = items.Add(InternationalizationManager.Instance.GetTranslation("iconTrayOpen"));
            open.Click += (o, e) => Visibility = Visibility.Visible;
            var setting = items.Add(InternationalizationManager.Instance.GetTranslation("iconTraySettings"));
            setting.Click += (o, e) => App.API.OpenSettingDialog();
            var exit = items.Add(InternationalizationManager.Instance.GetTranslation("iconTrayExit"));
            exit.Click += (o, e) => Close();

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.MouseClick += (o, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (menu.Visible)
                    {
                        menu.Close();
                    }
                    else
                    {
                        var p = System.Windows.Forms.Cursor.Position;
                        menu.Show(p);
                    }
                }
            };
        }

        private void InitProgressbarAnimation()
        {
            var da = new DoubleAnimation(ProgressBar.X2, ActualWidth + 100, new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            var da1 = new DoubleAnimation(ProgressBar.X1, ActualWidth, new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            Storyboard.SetTargetProperty(da, new PropertyPath("(Line.X2)"));
            Storyboard.SetTargetProperty(da1, new PropertyPath("(Line.X1)"));
            _progressBarStoryboard.Children.Add(da);
            _progressBarStoryboard.Children.Add(da1);
            _progressBarStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            ProgressBar.BeginStoryboard(_progressBarStoryboard);
            _viewModel.ProgressBarVisibility = Visibility.Hidden;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void OnPreviewMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender != null && e.OriginalSource != null)
            {
                var r = (ResultListBox)sender;
                var d = (DependencyObject)e.OriginalSource;
                var item = ItemsControl.ContainerFromElement(r, d) as ListBoxItem;
                var result = (ResultViewModel)item?.DataContext;
                if (result != null)
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        _viewModel.OpenResultCommand.Execute(null);
                    }
                    else if (e.ChangedButton == MouseButton.Right)
                    {
                        _viewModel.LoadContextMenuCommand.Execute(null);
                    }
                }
            }
        }


        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files[0].ToLower().EndsWith(".wox"))
                {
                    PluginManager.InstallPlugin(files[0]);
                }
                else
                {
                    MessageBox.Show(InternationalizationManager.Instance.GetTranslation("invalidWoxPluginFileFormat"));
                }
            }
            e.Handled = false;
        }

        private void OnPreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void OnContextMenusForSettingsClick(object sender, RoutedEventArgs e)
        {
            App.API.OpenSettingDialog();
        }


        private void OnDeactivated(object sender, EventArgs e)
        {
            if (_settings.HideWhenDeactive)
            {
                Hide();
            }
        }

        private bool _startup = true;
        private void SetWindowPosition()
        {
            if( _settings.RememberLastLaunchLocation || _startup )
            {
                this._startup = false;
                this.Top = this._settings.WindowTop;
                this.Left = this._settings.WindowLeft;
            }
            else
            {
                this.Left = this.WindowLeft();
                this.Top = this.WindowTop();
            }
        }

        /// <summary>
        // used to set correct position on windows first startup
        // since the actual width and actual height will be avaiable after this event
        /// </summary>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if( _settings.RememberLastLaunchLocation )
                return;
            this.Left = this.WindowLeft();
            this.Top = this.WindowTop();
        }

        private void OnLocationChanged( object sender, EventArgs e )
        {
            if (this._settings.RememberLastLaunchLocation)
            {
                this._settings.WindowTop = this.Top;
                this._settings.WindowLeft = this.Left;
            }
        }

        private double WindowLeft()
        {
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip2.X - ActualWidth) / 2 + dip1.X;
            return left;
        }

        private double WindowTop()
        {
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            var top = (dip2.Y - QueryTextBox.ActualHeight) / 4 + dip1.Y;
            return top;
        }

        /// <summary>
        /// Register up and down key
        /// todo: any way to put this in xaml ?
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                _viewModel.SelectNextItemCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                _viewModel.SelectPrevItemCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.PageDown)
            {
                _viewModel.SelectNextPageCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.PageUp)
            {
                _viewModel.SelectPrevPageCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_viewModel.QueryTextCursorMovedToEnd)
            {
                QueryTextBox.CaretIndex = QueryTextBox.Text.Length;
                _viewModel.QueryTextCursorMovedToEnd = false;
            }
        }


    }
}