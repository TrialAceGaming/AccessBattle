﻿using AccessBattle.Wpf.Interfaces;
using System.Windows.Input;

namespace AccessBattle.Wpf.ViewModel
{
    public class GameOverMenuViewModel : MenuViewModelBase
    {
        string _gameOverMessage;
        public string GameOverMessage
        {
            get { return _gameOverMessage; }
            set { SetProp(ref _gameOverMessage, value); }
        }

        public ICommand RematchCommand => new RelayCommand(o=>
        {
            ParentViewModel.CurrentMenu = MenuType.WaitForJoin;
            if (!ParentViewModel.Game.Client.Rematch())
                ParentViewModel.CurrentMenu = MenuType.GameOver;
        }, o=>{ return ParentViewModel.Game.Client.IsJoined == true; });

#pragma warning disable RECS0165 // Asynchronous methods should return a Task instead of void
        public ICommand LeaveCommand => new RelayCommand(async o =>
        {
            await ParentViewModel.Game.Client.ExitGame(ParentViewModel.Game.UID, Networking.Packets.ExitGameReason.PlayerQuit);
            if (ParentViewModel.Game.IsInSinglePlayerMode)
                ParentViewModel.CurrentMenu = MenuType.AISelect;
            else
                ParentViewModel.CurrentMenu = MenuType.NetworkGame;
        });
#pragma warning restore RECS0165 // Asynchronous methods should return a Task instead of void

        public GameOverMenuViewModel(IMenuHolder parent) : base(parent) { }

        public override void Activate()
        {
        }

        public override void Suspend()
        {

        }
    }
}
