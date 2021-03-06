﻿using AccessBattle;
using AccessBattle.Plugins;
using AccessBattleAI.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using AccessBattle.Networking.Packets;

// The configuration of the neural network was created uding a genetic algorithm. (TODO)

namespace AccessBattleAI
{
    // Disabled. Not yet working
    //[Export(typeof(IPlugin))]
    //[ExportMetadata("Name", "AccessBattle.AI.Nou")]
    //[ExportMetadata("Description", "Nou 脳 (Brain). A neural network.")]
    //[ExportMetadata("Version", "0.1")]
    public class NouFactory : IArtificialIntelligenceFactory
    {
        public IPluginMetadata Metadata { get; set; }

        public IArtificialIntelligence CreateInstance()
        {
            var n = new Nou();
            if (System.IO.File.Exists("NouAi_0.txt"))
                n.ReadFromFile(0, "NouAi_0.txt");
            if (System.IO.File.Exists("NouAi_1.txt"))
                n.ReadFromFile(1, "NouAi_1.txt");
            return n;
        }
    }

    /// <summary>
    /// Implementation of Nou.
    /// </summary>
    public class Nou : AiBase, ITrainableAI
    {
        protected override string _name => "Nou (neural network)";

        NeuralNetwork _net1;
        NeuralNetwork _net2;

        public const double MutateDelta = 0.01;

        public NeuralNetwork Net1 => _net1;
        public NeuralNetwork Net2 => _net2;

        public int DeploySeed = 0;

        const int InputsNet = 94;
        const int HiddenNet = 10;
        const int OutputsNet1 = 1;
        const int OutputsNet2 = 12;

        /// <summary>
        /// Read neural network definition from file.
        /// </summary>
        /// <param name="index">0 for card selection network. 1 for movement network</param>
        /// <param name="path">File to load.</param>
        /// <returns></returns>
        public bool ReadFromFile(ushort index, string path)
        {
            if (!System.IO.File.Exists(path)) return false;
            if (index > 1) return false;

            var net = NeuralNetwork.ReadFromFile(path);
            if (net == null) return false;

            if (index == 0) _net1 = net;
            else _net2 = net;
            return true;
        }

        class FieldScore
        {
            public double Score { get; set; }
            public BoardField Field { get; set; }
            public FieldScore(BoardField field) { Field = field; }
        }

        public override string PlayTurn()
        {
            // Strategy: There is no strategy. The neurons just do their thing.

            if (_net1 == null)
            {
                _net1 = new NeuralNetwork(InputsNet, HiddenNet, OutputsNet1);
                _net1.Mutate(MutateDelta);
            }
            if (_net2 == null)
            {
                _net2 = new NeuralNetwork(InputsNet, HiddenNet, OutputsNet2);
                _net2.Mutate(MutateDelta);
            }

            // Deployment is random.
            if (Phase == GamePhase.Deployment)
                return Deploy();

            // Action 1: Card selection
            List<FieldScore> scores = new List<FieldScore>();

            // The net knows nothing about the server field
            // If a card is on the exit field, bring it in
            var enterServer = EnterServer();
            if (enterServer != null) return enterServer;

            foreach (var field in MyLinkCards)
            {
                scores.Add(new FieldScore(field));
            }
            foreach (var field in MyVirusCards)
            {
                scores.Add(new FieldScore(field));
            }

            // Randomize list in case the scores are the same:
            List<FieldScore> scoresx = new List<FieldScore>();
            var rnd = new Random();
            while (scores.Count > 0)
            {
                int index = rnd.Next(scores.Count);
                scoresx.Add(scores[index]);
                scores.RemoveAt(index);
            }
            scores = scoresx;

            foreach (var sc in scores)
            {
                ApplyInputs(_net1, sc.Field);
                _net1.ComputeOutputs(false);
                sc.Score = _net1.Outputs[0];
            }
            scores = scores.OrderByDescending(o => o.Score).ToList();

            BoardField chosenField = null;
            List<BoardField> targetMoves = null;
            for (int i = 0; i < scores.Count; ++i)
            {
                targetMoves = Game.GetMoveTargetFields(this, scores[i].Field);
                if (targetMoves.Count == 0)
                    continue;
                chosenField = scores[i].Field;
                break;
            }
            if (chosenField == null)
                return "???";

            // Action 2: Move
            ApplyInputs(_net2, chosenField);
            _net2.ComputeOutputs(false);
            // Mapping is same as Input Array2
            scores.Clear();
            bool hasBoost = (chosenField.Card as OnlineCard)?.HasBoost == true;
            int x = chosenField.X;
            int y = chosenField.Y;
            for (int i = 0; i < 12; ++i)
            {
                var rel = InputArray2[i];
                int absX = x + rel.X;
                int absY = y + rel.Y;
                // Skip fields we cannot move to
                if (!targetMoves.Exists(o => o.X == absX && o.Y == absY))
                    continue;

                scores.Add(new FieldScore(Board[absX, absY]) { Score = _net2.Outputs[i] });
            }
            scores = scores.OrderByDescending(o => o.Score).ToList();

            BoardField targetField = null;
            if (scores.Count > 0) targetField = scores[0].Field;

            if (targetField == null)
                return "???";
            return PlayMove(chosenField, targetField);
        }

        static int SeedBase = Environment.TickCount;
        static readonly object SeedLock = new object();
        string Deploy()
        {
            Random rnd;
            if (DeploySeed != 0) { rnd = new Random(DeploySeed); }
            else lock (SeedLock) { rnd = new Random((++SeedBase).GetHashCode() ^ Environment.TickCount); }
            // Randomize cards:
            var list = new List<char> { 'V', 'V', 'V', 'V', 'L', 'L', 'L', 'L', };
            var n = list.Count;
            while (n > 1)
            {
                --n;
                int i = rnd.Next(n + 1);
                char c = list[i];
                list[i] = list[n];
                list[n] = c;
            }
            string ret = "dp ";
            foreach (char c in list)
            {
                ret += c;
            }
            return ret;
        }

        struct Point
        {
            public int X;
            public int Y;
            public Point(int x, int y) { X = x; Y = y; }
        }

        void ApplyInputs(NeuralNetwork net, BoardField field)
        {
            int x = field.X;
            int y = field.Y;
            // Input 1+2: Opponent link/virus cards (2x40 fields)
            for (int i = 0; i < 40; ++i)
            {
                int nx = x + InputArray1[i].X;
                int ny = y + InputArray1[i].Y;

                // Empty fields get a zero
                if (nx < 0 || nx > 7 || ny < 0 || ny > 7 ||
                    Board[nx, ny].Card == null ||
                    Board[nx, ny].Card.Owner.PlayerNumber == 1 ||
                    !(Board[nx, ny]?.Card is OnlineCard))
                {
                    net.Inputs[i] = 0;
                    net.Inputs[i+40] = 0;
                    continue;
                }
                var card = Board[nx, ny].Card as OnlineCard;
                if (card.Type == OnlineCardType.Link)
                {
                    net.Inputs[i] = 1;
                    net.Inputs[i+40] = 0;
                }
                else if (card.Type == OnlineCardType.Virus)
                {
                    net.Inputs[i] = 0;
                    net.Inputs[i+40] = 1;
                }
                else
                {
                    net.Inputs[i] = 0.5;
                    net.Inputs[i+40] = 0.5;
                }
            }
            // Input 3: Movement fields
            var moveFields = Game.GetMoveTargetFields(this, field);
            for (int i = 0; i < 12; ++i)
            {
                int nx = x + InputArray2[i].X;
                int ny = y + InputArray2[i].Y;

                var mv = moveFields.FirstOrDefault(o => o.X == nx && o.Y == ny);
                if (mv == null ||
                    // Don't allow Virus on Exit field
                    (mv.IsExit && (field.Card as OnlineCard)?.Type == OnlineCardType.Virus))
                {
                    net.Inputs[i + 80] = 0;
                    continue;
                }

                // Field can be moved to. Assign a value between 0.5 and 1.0 depending on how far it is from the server
                var distance = DistanceToExit(mv.X, mv.Y);
                int isExit = (mv.IsExit && mv.Y == 7) ? 5 : 0; // give +5 if exit
                // Largest distance is somewhere between 7 and 9
                double distVal = 1 - (0.7 * distance / 7.0); // Gives value between 0.1 and 1
                distVal *= distVal; // Gives value between 0.01 and 1
                // Second term gives
                net.Inputs[i + 80] = isExit + 2*distVal; // Added a additional factor of 2
            }
            var myCard = field.Card as OnlineCard;
            if (myCard?.Type == OnlineCardType.Link)
            {
                net.Inputs[80 + 12] = 1;
                net.Inputs[80 + 13] = 0;
            }
            else
            {
                net.Inputs[80 + 12] = 0;
                net.Inputs[80 + 13] = 1;
            }
        }

        double DistanceToExit(int x, int y)
        {
            return Distance(x, y, x < 4 ? 3 : 4, 7);
        }

        double DistanceToExit(BoardField field)
        {
            return Distance(field.X, field.Y, field.X < 4 ? 3 : 4, 7);
        }

        double Distance(int x0, int y0, int x1, int y1)
        {
            var dx = x1 - x0;
            var dy = y1 - y0;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        string EnterServer()
        {
            foreach (var link in MyLinkCards)
            {
                var targets = Game.GetMoveTargetFields(this, link);
                var t = targets.FirstOrDefault(o => o.Y == 10);
                if (t != null)
                {
                    return PlayMove(link, t);
                }
            }
            return null;
        }

        // Relative coordinates for Input 1+2
        Point[] InputArray1 = new Point[40]
        {
                                                                               new Point(0, 4),
                                                              new Point(-1, 3),new Point(0, 3),new Point(1, 3),
                                             new Point(-2, 2),new Point(-1, 2),new Point(0, 2),new Point(1, 2),new Point(2, 2),
                            new Point(-3, 1),new Point(-2, 1),new Point(-1, 1),new Point(0, 1),new Point(1, 1),new Point(2, 1),new Point(3, 1),
            new Point(-4,0),new Point(-3, 0),new Point(-2, 0),new Point(-1, 0),                new Point(1, 0),new Point(2, 0),new Point(3, 0),new Point(4,0),
                            new Point(-3,-1),new Point(-2,-1),new Point(-1,-1),new Point(0,-1),new Point(1,-1),new Point(2,-1),new Point(3,-1),
                                             new Point(-2,-2),new Point(-1,-2),new Point(0,-2),new Point(1,-2),new Point(2,-2),
                                                              new Point(-1,-3),new Point(0,-3),new Point(1,-3),
                                                                               new Point(0,-4)
        };
        Point[] InputArray2 = new Point[12]
        {
                                                  new Point(0, 2),
                                 new Point(-1, 1),new Point(0, 1),new Point(1, 1),
                new Point(-2, 0),new Point(-1, 0),                new Point(1, 0),new Point(2, 0),
                                 new Point(-1,-1),new Point(0,-1),new Point(1,-1),
                                                  new Point(0,-2)
        };

        public double Fitness()
        {
            // Score the current board:
            double score = 0;

            // Link cards that reached server give 15 points.
            // Link cards that were capture give -10 points
            // Give 10 - 'Distance to Exit' for all otther Link cards.
            foreach (var field in AllMyLinkCards)
            {
                if (field.Y == 8) // Card reached server
                {
                    score += 15;
                }
                else if (field.Y == 9) // Card was captured
                {
                    score -= 10;
                }
                else if (field.Y < 8) // Card is on the field
                {
                    // Distance to exit
                    score += 10 - DistanceToExit(field);
                }
            } // Best score = 60

            // Virus cards should not enter exit field or server.
            // Give -20 if exit field is entered, -25 for server.
            // Give +10 if bait worked and opponent captured virus.
            // Give 10 - 'Min dist to opponent' for cards on the field.
            foreach (var field in AllMyVirusCards)
            {
                if (field.Y == 8) // Card reached server
                {
                    score -= 25;
                }
                else if (field.Y == 9) // Captured by opponent
                {
                    score += 10;
                }
                else
                {
                    // Calculate min distance to opponent cards
                    double dMin = 7;
                    foreach (var c in TheirOnlineCards)
                    {
                        var dst = Distance(field.X, field.Y, c.X, c.Y);
                        if (dst < dMin) dst = dMin;
                    }
                    score += 10 - dMin;
                }
            } // Best score = 40 (only of opponent is stupid enough)

            // Give +5 bonus for opponent link cards that have been captured.
            // Give -10 penalty for opponent virus cards that have been captured.
            for (int i = 0; i < 8; ++i)
            {
                var card = Board[i, 8].Card as OnlineCard;
                // Ignore own cards. We scored them already
                if (card == null || card.Owner.PlayerNumber == 1) continue;

                // Don't add too much. AI might catch cards by accident
                if (card.Type == OnlineCardType.Link) score += 5;
                // Give higher penalty for capturing virus cards
                if (card.Type == OnlineCardType.Virus) score -= 10;
                // Unknown cards give the sum of both
                else score -= 5; // This case should never be hit
            } // Best score = 20

            // Theoretical best score: 120 (cannot be reached within a normal game)

            return score;
        }

        public void Mutate(double delta, byte mutateFlags = 3)
        {
            if ((mutateFlags & 1) > 0)
                _net1?.Mutate(delta);
            if ((mutateFlags & 2) > 0)
                _net2?.Mutate(delta);
        }

        public static Nou Copy(Nou nou)
        {
            var newNou = new Nou();
            newNou._net1 = NeuralNetwork.ReadFromString(nou._net1.SaveAsString());
            newNou._net2 = NeuralNetwork.ReadFromString(nou._net2.SaveAsString());
            return newNou;
        }

        void ReplaceLettersWithNumbers(ref string[] array)
        {
            for (int i = 0; i < array.Length; ++i)
            {
                array[i] = array[i]
                    .Replace("a", "1")
                    .Replace("b", "2")
                    .Replace("c", "3")
                    .Replace("d", "4")
                    .Replace("e", "5")
                    .Replace("f", "6")
                    .Replace("g", "7")
                    .Replace("h", "8");
            }
        }

        public void Train(GameSync sync, string command)
        {
            Synchronize(sync);

            if (_net1 == null)
            {
                _net1 = new NeuralNetwork(InputsNet, HiddenNet,OutputsNet1);
                _net1.Mutate(MutateDelta);
            }
            if (_net2 == null)
            {
                _net2 = new NeuralNetwork(InputsNet, HiddenNet, OutputsNet2);
                _net2.Mutate(MutateDelta);
            }

            // Deployment is random
            // and net cannot play any cards.
            if (!command.StartsWith("mv", StringComparison.InvariantCultureIgnoreCase)
                || command.Length < 4) return;
            command = command.Substring(3).Trim();
            var split = command.Split(new[] { ',' });
            if (split.Length != 4) return;
            ReplaceLettersWithNumbers(ref split);
            uint x1, x2, y1, y2;
            if (!uint.TryParse(split[0], out x1) ||
                !uint.TryParse(split[1], out y1) ||
                !uint.TryParse(split[2], out x2) ||
                !uint.TryParse(split[3], out y2))
                return;
            // Convert to zero based index:
            --x1; --x2; --y1; --y2;
            if (x1 > 7 || x2 > 7 || (y1 > 7 && y1 != 10) || (y2 > 7 && y2 != 10))
                return;

            // We cannot train net1. It's output has no definite value that could be
            // calculated. We must implement a scoring model first.

            // Train Net 2:
            var field1 = Board[x1, y1];
            ApplyInputs(Net2, field1);

            double[] expectedOutput = new double[12];
            int relX = (int)x2 - (int)x1;
            int relY = (int)y2 - (int)y1;
            int i = 0;
            bool found = false;
            for (; i < 12 && !found; ++i)
            {
                if (InputArray2[i].X == relX && InputArray2[i].Y == relY)
                {
                    found = true;
                    break;
                }
            }
            if (!found) return; // Bad!
            expectedOutput[i] = 1;

            double[][] trainData = new double[1][];
            trainData[0] = new double[InputsNet + OutputsNet2];
            Array.Copy(Net2.Inputs, trainData[0], InputsNet);
            Array.Copy(
                expectedOutput, 0,
                trainData[0], InputsNet,
                OutputsNet2);

            Net2.Train(trainData, 50, 0.05, 0.01);
            Net2.SaveAsFile("NouAi_1.txt");
            //if (!System.IO.File.Exists("NouAi_0.txt"))
            //    Net1.SaveAsFile("NouAi_0.txt");
        }
    }
}
