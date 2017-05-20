﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Globalization;
using AccessBattle;
using System.Windows.Media.Animation;

// TODO: Make board fixed size and put it in a ViewBox to auto-zoom
// TODO: Own cards blink when clicked while they are on opponents stack

// TODO: Middle of stack area: Show Popup Menu for placing boost or firewall

namespace AccessBattleWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        StoryboardAsyncWrapper _blinkStoryBoard;
        StoryboardAsyncWrapper _lineBoostStoryBoard;
        Storyboard _buttonPanelStoryboardFadeIn = new Storyboard();
        Storyboard _buttonPanelStoryboardFadeOut = new Storyboard();

        public MainWindow()
        {
            InitializeComponent();
            _blinkStoryBoard = new StoryboardAsyncWrapper(
                new Storyboard
                {
                    Duration = TimeSpan.FromSeconds(2),
                    RepeatBehavior = RepeatBehavior.Forever
                }, this);
            _lineBoostStoryBoard = new StoryboardAsyncWrapper(
                new Storyboard
                {
                    Duration = TimeSpan.FromSeconds(6),
                    RepeatBehavior = RepeatBehavior.Forever
                }, this);            

            var mainFields = new BoardFieldView[,] // X,Y
            {
                // Ignore board orientation for stack fields. First 4 fields are links
                { A1, A2, A3, A4, A5, A6, A7, A8, StackLink1P1, StackLink1P2 }, // 0,0=A1 / 0,7=A8
                { B1, B2, B3, B4, B5, B6, B7, B8, StackLink2P1, StackLink2P2 }, // 1,0=B1 / 1,7=B8
                { C1, C2, C3, C4, C5, C6, C7, C8, StackLink3P1, StackLink3P2 }, // ...
                { D1, D2, D3, D4, D5, D6, D7, D8, StackLink4P1, StackLink4P2 },
                { E1, E2, E3, E4, E5, E6, E7, E8, StackVirus1P1, StackVirus1P2 },
                { F1, F2, F3, F4, F5, F6, F7, F8, StackVirus2P1, StackVirus2P2 },
                { G1, G2, G3, G4, G5, G6, G7, G8, StackVirus3P1, StackVirus3P2 },
                { H1, H2, H3, H4, H5, H6, H7, H8, StackVirus4P1, StackVirus4P2 },
            };
            for (int x = 0; x<8;++x)
            {
                for (int y = 0; y < 10; ++y)
                {
                    mainFields[x, y].Initialize(new BoardFieldViewModel(ViewModel.Game.Board.Fields[x, y]), _blinkStoryBoard, _lineBoostStoryBoard);
                    // Screw MVVM. Im not going to write 64+16 command bindings
                    mainFields[x, y].Clicked += (s,e) => ViewModel.FieldClicked(e.Field);
                }
            }

            LineBoostField.Initialize(new BoardFieldViewModel(ViewModel.Game.Board.Fields[0, 10]), _blinkStoryBoard, _lineBoostStoryBoard);
            LineBoostField.Clicked += (s, e) => ViewModel.FieldClicked(e.Field);
            FirewallField.Initialize(new BoardFieldViewModel(ViewModel.Game.Board.Fields[1, 10]), _blinkStoryBoard, _lineBoostStoryBoard);
            FirewallField.Clicked += (s, e) => ViewModel.FieldClicked(e.Field);
            VirusCheckField.Initialize(new BoardFieldViewModel(ViewModel.Game.Board.Fields[2, 10]), _blinkStoryBoard, _lineBoostStoryBoard);
            VirusCheckField.Clicked += (s, e) => ViewModel.FieldClicked(e.Field);
            NotFound404Field.Initialize(new BoardFieldViewModel(ViewModel.Game.Board.Fields[3, 10]), _blinkStoryBoard, _lineBoostStoryBoard);
            NotFound404Field.Clicked += (s, e) => ViewModel.FieldClicked(e.Field);

            DeploymentControl.Initialize(_blinkStoryBoard);
            
            // Server field for entering server
            ServerP2Field.Initialize(new BoardFieldViewModel(ViewModel.Game.Board.Fields[5, 10]), _blinkStoryBoard, _lineBoostStoryBoard);
            ServerP2Field.Clicked += (s, e) => ViewModel.FieldClicked(e.Field);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(.5)) { BeginTime = TimeSpan.FromSeconds(0) };
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(.5)) { BeginTime = TimeSpan.FromSeconds(0) };
            Storyboard.SetTarget(fadeIn, P1ButtonPanel);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath("Opacity"));
            Storyboard.SetTarget(fadeOut, P1ButtonPanel);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath("Opacity"));
            _buttonPanelStoryboardFadeIn.Children.Add(fadeIn);
            _buttonPanelStoryboardFadeOut.Children.Add(fadeOut);

            WeakEventManager<Game, PropertyChangedEventArgs>.AddHandler(ViewModel.Game, "PropertyChanged", Game_PropertyChanged);

            SwitchCardsCtrl.Yes += SwitchCardsCtrl_Yes;
            SwitchCardsCtrl.No += SwitchCardsCtrl_No;

            Loaded += MainWindow_Loaded;
        }

        private void SwitchCardsCtrl_No(object sender, EventArgs e)
        {
            ViewModel.ExecuteError404(false);
        }

        private void SwitchCardsCtrl_Yes(object sender, EventArgs e)
        {
            ViewModel.ExecuteError404(true);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Assigns the WPF dispatcher as context for UI synchronization
            UiSyncHelper.Context = SynchronizationContext.Current ?? new SynchronizationContext();
        }

        void Game_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Phase" || e.PropertyName == "CurrentPlayer")
            {
                if (_showingP1Buttons)
                {
                    if (ViewModel.Game.CurrentPlayer != 1 || ViewModel.Game.Phase != GamePhase.PlayerTurns)
                    {
                        HideP1ButtonPanel();
                    }
                }
            }
        }

        void ShowP1ButtonPanel()
        {
            if (ViewModel.Game.CurrentPlayer != 1 || ViewModel.Game.Phase != GamePhase.PlayerTurns) return;
            _showingP1Buttons = true;
            _buttonPanelStoryboardFadeIn.Begin(P1ButtonPanel);
        }

        void HideP1ButtonPanel()
        {
            _showingP1Buttons = false;
            _buttonPanelStoryboardFadeOut.Begin(P1ButtonPanel);
        }

        #region TODO
        // TODO: Style so that a button with command  can be used
        bool _gameOverClickStarted;
        private void GameOverMessage_MouseLeave(object sender, MouseEventArgs e)
        {
            if (GameOverMessage.IsMouseCaptured) GameOverMessage.ReleaseMouseCapture(); // Solves problems with Window not closing after click
            _gameOverClickStarted = false;
        }

        private void GameOverMessage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (GameOverMessage.CaptureMouse())
            {
                _gameOverClickStarted = true;
            }
        }

        private void GameOverMessage_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (GameOverMessage.IsMouseCaptured) GameOverMessage.ReleaseMouseCapture();
            if (_gameOverClickStarted)
            {
                ViewModel.Game.Phase = GamePhase.Init;
            }
        }
        #endregion

        #region Actions
        bool _actionsClickStarted;
        bool _showingP1Buttons;
        Brush _darkBrush = new SolidColorBrush(Color.FromArgb(0xff, 0x60, 0x60, 0x60));
        private void P1ActionsField_MouseEnter(object sender, MouseEventArgs e)
        {
            P1ActionsField.Background = _darkBrush;
        }

        private void P1ActionsField_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (P1ActionsField.IsMouseCaptured) P1ActionsField.ReleaseMouseCapture();
            if (_actionsClickStarted)
            {
                if (_showingP1Buttons)
                    HideP1ButtonPanel();
                else
                    ShowP1ButtonPanel();
            }
        }

        private void P1ActionsField_MouseLeave(object sender, MouseEventArgs e)
        {
            P1ActionsField.Background = Brushes.Black;
        }

        private void P1ActionsField_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (P1ActionsField.CaptureMouse())
            {
                _actionsClickStarted = true;
            }
        }
        #endregion
    }
}