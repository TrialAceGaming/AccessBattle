﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccessBattle
{
    /// <summary>
    /// Indicator for the current game phase.
    /// </summary>
    public enum GamePhase
    {
        /// <summary>Game was just created and the second player has yet to join.</summary>
        WaitingForPlayers,
        /// <summary>A player is joining the game.</summary>
        PlayerJoining,
        /// <summary>In Init phase, the game state is reset and it is decided which player makes the first move.</summary>
        Init,
        /// <summary>Players deploy their cards in this phase.</summary>
        Deployment,
        /// <summary>Main game phase.</summary>
        PlayerTurns,
        /// <summary>Game is over. One of the players won.</summary>
        GameOver
    }

    /// <summary>
    /// Contains the complete state of a game.
    /// </summary>
    public class Game : PropChangeNotifier
    {
        GamePhase _phase;
        /// <summary>
        /// Current game phase.
        /// </summary>
        public GamePhase Phase
        {
            get { return _phase; }
            private set
            {
                SetProp(_phase, value, () =>
                {
                     _phase = value;
                     OnPhaseChanged(); // Should be done before prop change event fires
                });
            }
        }

        PlayerState[] _players;
        /// <summary>
        /// Player related data.
        /// </summary>
        public PlayerState[] Players { get { return _players; } }

        private object _locker = new object();

        /// <summary>
        /// Starts joining a player. Player 1 must accept. Then player 2 must accept after waiting.
        /// </summary>
        /// <param name="player">Player to join.</param>
        /// <returns></returns>
        public bool BeginJoinPlayer(IPlayer player)
        {
            var result = false;
            lock (_locker)
            {
                if (Phase == GamePhase.WaitingForPlayers)
                {
                    Phase = GamePhase.PlayerJoining;
                    Players[1].Player = player;
                    result = true;
                }
            }
            return result;
        }

        /// <summary>
        /// Confirms the joining process and starts Game Init phase.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="accepted"></param>
        /// <returns></returns>
        public bool JoinPlayer(IPlayer player, bool accepted)
        {
            var result = false;
            lock (_locker)
            {
                if (Phase == GamePhase.PlayerJoining && Players[1].Player == player)
                {
                    if (accepted)
                        Phase = GamePhase.Init;                        
                    else
                    {
                        Players[1].Player = null;
                        Phase = GamePhase.WaitingForPlayers;
                    }
                    result = true;
                }
            }
            return result;
        }

        uint _uid;
        /// <summary>
        /// This game's ID on the server.
        /// </summary>
        public uint UID { get { return _uid; } }

        string _name;
        /// <summary>
        /// This game's name.
        /// </summary>
        public string Name
        {
            get { return _name; }
            set { SetProp(ref _name, value); }
        }

        /// <summary>
        /// Changes the UID. Should only be used for network play when player joined a game.
        /// </summary>
        /// <param name="uid">New UID.</param>
        public void SetUid(uint uid)
        {
            SetProp(ref _uid, uid);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="uid">ID of this game (mainly used for server).</param>
        public Game(uint uid = 0)
        {
            _uid = uid;
            _players = new PlayerState[]
            {
                new PlayerState(1) { Name = "Player 1"  },
                new PlayerState(2) { Name = "Player 2"  }
            };
            _phase = GamePhase.WaitingForPlayers;
            OnPhaseChanged();
        }

        void OnPhaseChanged()
        {

        }
    }
}
