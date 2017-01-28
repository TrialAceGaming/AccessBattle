﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccessBattle
{
    public enum GamePhase
    {
        Init,
        Deployment,
        PlayerTurns,
        GameOver
    }

    public enum PlayerAction
    {
        SelectCard,
        MoveSelectedCard,
        TakeSelectedCard,
        PlaceBoost,
        TakeBoost,
        Error404,
    }

    // TODO: Link card must perform infiltration in a separate turn
    //       after it enters the exit field

    public class Game : PropChangeNotifier
    {
        public Player[] Players { get; private set; }
        int _currentPlayer;
        public int CurrentPlayer
        {
            get { return _currentPlayer; }
            set { SetProp(ref _currentPlayer, value); }
        }

        int _winningPlayer;
        public int WinningPlayer
        {
            get { return _winningPlayer; }
            set { SetProp(ref _winningPlayer, value); }
        }

        public Board Board { get; private set; }
        GamePhase _phase;
        public GamePhase Phase
        {
            get { return _phase; }
            set
            {
                if (_phase != value)
                {
                    _phase = value;
                    OnPhaseChanged(); // Should be done before event fires
                    OnPropertyChanged();
                }
            }
        }

        void OnPhaseChanged()
        {
            var phase = _phase;
            if (phase == GamePhase.Init)
            {
                _winningPlayer = 0;
                for (int x = 0; x < 8; ++x)
                    for (int y = 0; y < 10; ++y)
                        Board.Fields[x, y].Card = null;
                // Give each player 8 cards on his stack
                for (int p = 0; p < 2; ++p)
                    for (ushort i = 0; i < 4; ++i)
                    {
                        var pos = (ushort)(p * 8 + i);
                        // TODO: Randomization to prevent cheating ??? 
                        // Links
                        Board.OnlineCards[pos].Owner = Players[p];
                        Board.OnlineCards[pos].Type = OnlineCardType.Link;
                        Board.PlaceCard(i, (ushort)(8 + p), Board.OnlineCards[pos]);
                        // Viruses
                        pos += 4;
                        Board.OnlineCards[pos].Owner = Players[p];
                        Board.OnlineCards[pos].Type = OnlineCardType.Virus;
                        Board.PlaceCard((ushort)(i + 4), (ushort)(8 + p), Board.OnlineCards[pos]);
                    }
            }
            else if (phase == GamePhase.Deployment)
            {
                CurrentPlayer = 1; // UI can start deployment commands
            }
            else if (phase == GamePhase.PlayerTurns)
            {
                // TODO: Random
                /* var rnd = new Random(Guid.NewGuid().GetHashCode());
                CurrentPlayer = rnd.Next(1, 3); */
                CurrentPlayer = 1;
            }
        }

        public Game()
        {
            Players = new Player[]
            {
                new Player(1) { Name = "Player 1"  },
                new Player(2) { Name = "Player 2"  }
            };
            _winningPlayer = 0;
            Board = new Board();
            Phase = GamePhase.Init;
            OnPhaseChanged();
        }

        public string CreateMoveCommand(Vector pos1, Vector pos2)
        {
            return string.Format("mv {0},{1},{2},{3}", pos1.X, pos1.Y, pos2.X, pos2.Y);
        }

        public string CreateSetBoostCommand(Vector pos, bool setBoost)
        {
            return string.Format("bs {0},{1},{2}", pos.X, pos.Y, setBoost ? 1 : 0);
        }

        void PlaceCardOnStack(OnlineCard card)
        {
            if (card == null) return;
            var stacks = Board.GetPlayerStackFields(CurrentPlayer);
            // First 4 fields are reserved for link cards, others for viruses
            // If a card is not revealed it is treated as a link card            
            int stackpos = -1;
            if (card.IsFaceUp && card.Type == OnlineCardType.Virus)
            {
                for (int i = 4; i < stacks.Count; ++i)
                {
                    if (stacks[i].Card != null) continue;
                    stackpos = i;
                    break;
                }
            }
            if (stackpos == -1)
            {
                for (int i = 0; i < 4 && i < stacks.Count; ++i)
                {
                    if (stacks[i].Card != null) continue;
                    stackpos = i;
                    break;
                }
                if (stackpos == -1) // Happens when card.IsFaceUp and card is virus and link stack is full
                {
                    for (int i = 4; i < stacks.Count; ++i)
                    {
                        if (stacks[i].Card != null) continue;
                        stackpos = i;
                        break;
                    }
                }
            }
            // stackpos must have a value by now or stack is full. 
            // Stack can never be full. If more than 6 cards are on the stack, the game is over
            stacks[stackpos].Card = card;
            card.Location.Card = null;
            card.Location = stacks[stackpos];
            //if (CurrentPlayer > 0)
            //    card.Owner = Players[CurrentPlayer-1]; // TODO: Color does not update with this code
            // Check if player won or lost
            int linkCount = 0;
            int virusCount = 0;
            foreach (var field in stacks)
            {
                var c = field.Card as OnlineCard;
                if (c == null) continue;
                if (c.Type == OnlineCardType.Link) ++linkCount;
                if (c.Type == OnlineCardType.Virus) ++virusCount;
                // TODO: Network Clients can have unknown cards. Make sure to reveal them first
            }
            if (virusCount >= 4)
            {
                _winningPlayer = _currentPlayer == 1 ? 2 : 1;
                Phase = GamePhase.GameOver;
            }
            if (linkCount >= 4)
            {
                _winningPlayer = _currentPlayer == 1 ? 1 : 2;
                Phase = GamePhase.GameOver;
            }
        }

        /// <summary>
        /// Command   Syntax             Example
        /// -------------------------------------------
        /// Move      mv x1,y1,x2,y2     mv 0,0,1,0
        /// Boost     bs x1,y1,e         bs 0,0,1
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public bool ExecuteCommand(string command)
        {
            if (string.IsNullOrEmpty(command)) return false;
            var cmdCopy = command;
            string[] split;

            // Move Command
            // Syntax: mv x1,y1,x2,y2
            if (command.StartsWith("mv ", StringComparison.InvariantCulture) && command.Length > 3)
            {
                command = command.Substring(3);
                split = command.Split(new[] { ',' });
                if (split.Length != 4) return false;
                uint x1, x2, y1, y2;
                if (uint.TryParse(split[0], out x1) &&
                    uint.TryParse(split[1], out y1) &&
                    uint.TryParse(split[2], out x2) &&
                    uint.TryParse(split[3], out y2))
                {
                    // Check range
                    if (x1 > 7 || x2 > 7 || y1 > 9 || y2 > 9)
                    {
                        Trace.WriteLine("Game: Move '" + cmdCopy + "' invalid! Out of range.");
                        return false;
                    }
                    // Check if move is allowed
                    var field1 = Board.Fields[x1, y1];
                    var field2 = Board.Fields[x2, y2];
                    var card1 = field1.Card;
                    if (card1 == null || card1.Owner.PlayerNumber != CurrentPlayer)
                    {
                        if (card1 == null)
                            Trace.WriteLine("Game: Move '" + cmdCopy + "' invalid! First field has no card to move.");
                        else
                            Trace.WriteLine("Game: Move '" + cmdCopy + "' invalid! Player '" + CurrentPlayer + "' cannot move cards of his opponent.");
                        return false;
                    }

                    if (field1.Type == BoardFieldType.Stack)
                    {
                        // Moving cards from Stack is only allowed during deployment
                        if (_phase != GamePhase.Deployment)
                        {
                            Trace.WriteLine("Game: Move '" + cmdCopy + "' invalid! Card can only be moved from stack during deployment phase");
                            return false;
                        }
                        // Deployment:
                        // Target field must be empty and on a deployment field
                        if (field2.Card != null || !Board.GetPlayerDeploymentFields(CurrentPlayer).Contains(field2))
                        {
                            Trace.WriteLine("Game: Move '" + cmdCopy + "' invalid! Card can only be moved from stack to an empty deployment field");
                            return false;
                        }
                        field2.Card = field1.Card;
                        field1.Card = null;
                        field2.Card.Location = field2;
                        return true;
                    }
                    if (field1.Type == BoardFieldType.Main || field1.Type == BoardFieldType.Exit)
                    {
                        // Putting card back to stack is allowed during deployment or when card is claimed
                        if (field2.Type == BoardFieldType.Stack)
                        {
                            if (_phase == GamePhase.Deployment)
                            {
                                // Source field must be a deployment field
                                if (field2.Card != null || !Board.GetPlayerDeploymentFields(CurrentPlayer).Contains(field1))
                                {
                                    Trace.WriteLine("Game: Move '" + cmdCopy + "' invalid! Source card is not on deployment field");
                                    return false;
                                }
                                field2.Card = field1.Card;
                                field1.Card = null;
                                field2.Card.Location = field2;
                                return true;
                            }
                            else
                            {
                                // Card claimed
                                // Currently not valid. Game will do that automatically
                            }
                        }
                        // Default movement: Main-Main or exit
                        else if (field2.Type == BoardFieldType.Main || field2.Type == BoardFieldType.Exit)
                        {
                            // GetTargetFields already does some rule checks
                            if (GetTargetFields(field1).Contains(field2))
                            {
                                // Check if opponents card is claimed
                                if (field2.Card != null)
                                {
                                    var card = field2.Card as OnlineCard;
                                    if (card == null || card.Owner.PlayerNumber == CurrentPlayer)
                                    {
                                        Trace.WriteLine("Game: Move '" + cmdCopy + "' invalid! Can only jump onto online cards of opponent");
                                        return false;
                                    }
                                    // Reveal card
                                    card.IsFaceUp = true;
                                    PlaceCardOnStack(card);
                                    // Remove boost if applied
                                    if (card.HasBoost) card.HasBoost = false;
                                }
                                // TODO: 
                                //if (field2.Type == BoardFieldType.Exit)
                                //{
                                //    PlaceCardOnStack(field1.Card as OnlineCard);
                                //    return true;
                                //}
                                field2.Card = field1.Card;
                                field1.Card = null;
                                field2.Card.Location = field2;
                                return true;
                            }
                        }
                    }
                    //if (field1.Type == BoardFieldType.Exit)
                    //{
                    //    // Cards are never stored on an exit field
                    //    Trace.WriteLine("Game: Move '" + cmdCopy + "' invalid! Exit field cannot contain cards");
                    //    return false;
                    //}
                }
            }
            else if (command.StartsWith("bs ", StringComparison.InvariantCulture) && command.Length > 3)
            {
                command = command.Substring(3);
                split = command.Split(new[] { ',' });
                if (split.Length != 3) return false;
                uint x1, y1, enabled;
                if (uint.TryParse(split[0], out x1) &&
                    uint.TryParse(split[1], out y1) &&
                    uint.TryParse(split[2], out enabled))
                {
                    // Check range
                    if (x1 > 7 || y1 > 9 || enabled > 1)
                    {
                        Trace.WriteLine("Game: Move '" + cmdCopy + "' invalid! Out of range.");
                        return false;
                    }
                    var field1 = Board.Fields[x1, y1];
                    var card1 = field1.Card;
                    if (card1 == null || card1.Owner.PlayerNumber != CurrentPlayer)
                    {
                        Trace.WriteLine("Game: Move '" + cmdCopy + "' invalid! Boost can only be placed on own cards.");
                        return false;
                    }

                    // Check if boost is already used
                    var boostedCard = Board.OnlineCards.FirstOrDefault(
                        card => card.HasBoost && card.Owner.PlayerNumber == CurrentPlayer);
                    if (enabled == 1)
                    {
                        if (boostedCard != null)
                        {
                            Trace.WriteLine("Game: Move '" + cmdCopy + "' invalid! Boost is already placed.");
                            return false;
                        }
                        else
                        {
                            // Place boost
                            if (card1 is OnlineCard)
                            {
                                ((OnlineCard)card1).HasBoost = true;
                                return true;
                            }
                            else
                            {
                                Trace.WriteLine("Game: Move '" + cmdCopy + "' invalid! Target is not an online card.");
                                return false;
                            }
                        }
                    }
                    else if (enabled == 0)
                    {
                        if (boostedCard == card1)
                        {
                            boostedCard.HasBoost = false;
                            return true;
                        }
                        else
                        {
                            Trace.WriteLine("Game: Move '" + cmdCopy + "' invalid! Target has no boost.");
                            return false;
                        }
                    }
                }
            }

            Trace.WriteLine("Game: Move '" + cmdCopy + "' invalid!");
            return false;
        }

        public List<BoardField> GetTargetFields(BoardField field)
        {
            var fields = new List<BoardField>();
            if (field == null || field.Card == null || field.Card.Owner.PlayerNumber != CurrentPlayer)
                return fields;

            var card = field.Card as OnlineCard;
            if (card == null) return fields; // Firewall

            var p = field.Position;

            // without boost there are 4 possible locations
            var fs = new BoardField[4];
            if (p.Y > 0 && p.Y <= 7) // Down
                fs[0] = Board.Fields[p.X, p.Y - 1];
            else
            {
                // Special case for exit fields:
                if (p.Y == 0 && field.Type == BoardFieldType.Exit && CurrentPlayer == 2)
                    fs[1] = Board.Fields[4, 10];
            }

            if (p.Y >= 0 && p.Y < 7) // Up
                fs[1] = Board.Fields[p.X, p.Y + 1];
            else
            {
                // Special case for exit fields:
                if (p.Y == 7 && field.Type == BoardFieldType.Exit && CurrentPlayer == 1)
                    fs[1] = Board.Fields[5, 10];
            }

            if (p.X > 0 && p.X <= 7 && p.Y <= 7) // Left;  Mask out stack fields
                fs[2] = Board.Fields[p.X - 1, p.Y];
            if (p.X >= 0 && p.X < 7 && p.Y <= 7) // Right
                fs[3] = Board.Fields[p.X + 1, p.Y];            

            // Moves on opponents cards are allowed, move on own card not
            for (int i = 0; i < 4; ++i)
            {
                if (fs[i] == null) continue;
                if (fs[i].Card != null && fs[i].Card.Owner.PlayerNumber == CurrentPlayer) continue;
                if (fs[i].Card != null && !(fs[i].Card is OnlineCard)) continue; // Can only jump on online cards
                if (fs[i].Type == BoardFieldType.Stack) continue;
                if (fs[i].Type == BoardFieldType.Exit)
                {
                    // Exit field is allowed, but only your opponents
                    if (CurrentPlayer == 1 && fs[i].Position.Y == 0) continue;
                    if (CurrentPlayer == 2 && fs[i].Position.Y == 7) continue;
                }
                fields.Add(fs[i]);
            }

            // Boost can add additional fields
            if (card.HasBoost)
            {
                // The same checks as above apply for all fields
                var additionalfields = new List<BoardField>();
                foreach (var f in fields)
                {
                    // Ignore field if it it an exit field or has an opponents card
                    if (f.Card != null || f.Type == BoardFieldType.Exit) continue;

                    fs = new BoardField[4];
                    p = f.Position;
                    if (p.Y > 0 && p.Y <= 7)
                        fs[0] = Board.Fields[p.X, p.Y - 1];
                    else
                    {
                        // Special case for exit fields:
                        if (p.Y == 0 && f.Type == BoardFieldType.Exit && CurrentPlayer == 2)
                            fs[1] = Board.Fields[4, 10];
                    }

                    if (p.Y >= 0 && p.Y < 7)
                        fs[1] = Board.Fields[p.X, p.Y + 1];
                    else
                    {
                        // Special case for exit fields:
                        if (p.Y == 7 && field.Type == BoardFieldType.Exit && CurrentPlayer == 1)
                            fs[1] = Board.Fields[5, 10];
                    }

                    if (p.X > 0 && p.X <= 7 && p.Y <= 7) // No Stack fields!
                        fs[2] = Board.Fields[p.X - 1, p.Y];
                    if (p.X >= 0 && p.X < 7 && p.Y <= 7) // No Stack fields!
                        fs[3] = Board.Fields[p.X + 1, p.Y];

                    for (int i = 0; i < 4; ++i)
                    {
                        if (fs[i] == null) continue;
                        if (fs[i].Card != null && fs[i].Card.Owner.PlayerNumber == CurrentPlayer) continue;
                        if (fs[i].Type == BoardFieldType.Stack) continue;
                        if (fs[i].Type == BoardFieldType.Exit)
                        {
                            // Exit field is allowed, but only your opponents
                            if (CurrentPlayer == 1 && fs[i].Position.Y == 0) continue;
                            if (CurrentPlayer == 2 && fs[i].Position.Y == 7) continue;
                        }
                        additionalfields.Add(fs[i]);
                    }
                }

                foreach (var f in additionalfields)
                {
                    if (!fields.Contains(f))
                        fields.Add(f);
                }
            }

            return fields;
        }
    }
}
