using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace HexGo
{
    enum State
    {
        Empty,
        Red,
        Yellow
    }

    class Cell
    {
        public byte Index;
        public State State = State.Empty;
        public Board Board;

        public Cell(Board board, byte index)
        {
            Board = board;
            Index = index;
        }

        public void CloneFrom(Board board)
        {
            var source = board.Cells[Index];
            this.State = source.State;
        }

        public Coord Coord
        {
            get { return Coord.AllCoords[Index]; }
        }

        public IEnumerable<Move> ValidMoves
        {
            get { return Coord.ValidDestinations.Where(c => Board[c].State == State.Empty).Select(c => new Move(Coord, c)); }
        }

        public override string ToString()
        {
            return (State == State.Empty ? "." : State == State.Red ? "R" : "Y") + " " + this.Coord.ToString();
        }
    }

    class Coord : IEquatable<Coord>
    {
        public static Dictionary<Coord, List<Coord>> Connections = new Dictionary<Coord, List<Coord>>();
        public static Dictionary<Coord, List<Coord>> Reach = new Dictionary<Coord, List<Coord>>();
        public static List<Coord> AllCoords = new List<Coord>();
        public static Dictionary<Coord, byte> Indices;

        static Coord()
        {
            AllCoords.Add(new Coord(4, 8));
            AllCoords.Add(new Coord(5, 7));
            AllCoords.Add(new Coord(5, 9));
            AllCoords.Add(new Coord(6, 6));
            AllCoords.Add(new Coord(6, 8));
            AllCoords.Add(new Coord(6, 10));
            AllCoords.Add(new Coord(7, 5));
            AllCoords.Add(new Coord(7, 7));
            AllCoords.Add(new Coord(7, 9));
            AllCoords.Add(new Coord(7, 11));
            for (byte row = 8; row <= 17; row += 2)
            {
                for (byte col = 4; col <= 11; col += 2)
                {
                    AllCoords.Add(new Coord(row, col));
                    AllCoords.Add(new Coord((byte)(row + 1), (byte)(col + 1)));
                }
                AllCoords.Add(new Coord(row, 12));
            }
            AllCoords.Add(new Coord(18, 6));
            AllCoords.Add(new Coord(18, 8));
            AllCoords.Add(new Coord(18, 10));
            AllCoords.Add(new Coord(19, 7));
            AllCoords.Add(new Coord(19, 9));
            AllCoords.Add(new Coord(20, 8));

            Indices = Enumerable.Range(0, AllCoords.Count).ToDictionary(i => AllCoords[i], i => (byte)i);

            foreach (var coord in AllCoords)
            {
                var cxns = new[]
                {
                    new Coord(coord.Row - 2, coord.Col),
                    new Coord(coord.Row - 1, coord.Col - 1),
                    new Coord(coord.Row - 1, coord.Col + 1),
                    new Coord(coord.Row + 1, coord.Col - 1),
                    new Coord(coord.Row + 1, coord.Col + 1),
                    new Coord(coord.Row + 2, coord.Col)
                };
                var rch = new[]
                {
                    new Coord(coord.Row - 4, coord.Col),
                    new Coord(coord.Row - 3, coord.Col - 1),
                    new Coord(coord.Row - 3, coord.Col + 1),
                    new Coord(coord.Row - 2, coord.Col - 2),
                    new Coord(coord.Row - 2, coord.Col + 2),
                    new Coord(coord.Row, coord.Col - 2),
                    new Coord(coord.Row, coord.Col + 2),
                    new Coord(coord.Row + 2, coord.Col - 2),
                    new Coord(coord.Row + 2, coord.Col + 2),
                    new Coord(coord.Row + 3, coord.Col - 1),
                    new Coord(coord.Row + 3, coord.Col + 1),
                    new Coord(coord.Row + 4, coord.Col)
                };

                Connections[coord] = cxns.Intersect(AllCoords).ToList();
                Reach[coord] = rch.Intersect(AllCoords).ToList();
            }
        }

        public byte Row, Col;

        public Coord(int row, int col)
        {
            Row = (byte)row;
            Col = (byte)col;
        }

        public IEnumerable<Coord> ValidDestinations
        {
            get { return Connections[this].Concat(Reach[this]); }
        }

        public bool Equals(Coord other)
        {
            return Row == other.Row && Col == other.Col;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Coord);
        }

        public override int GetHashCode()
        {
            return (Row << 8) + Col;
        }

        public override string ToString()
        {
            return string.Format("({0},{1})", Row, Col);
        }
    }

    class Board
    {
        static object Locker = new object();
        static Stack<int> Freed = new Stack<int>();
        static int TopBoard = 0;
        static int BoardsAvailable = 0;
        static int MaxBoards = 2500000;
        static List<Board> AllBoards = new List<Board>(MaxBoards);
        static AutoResetEvent Reset = new AutoResetEvent(false);

        static Board()
        {
            var thread = new Thread(() =>
            {
                do
                {
                    for (; BoardsAvailable < MaxBoards; BoardsAvailable += 1000)
                    {
                        for (int i = 0; i < 1000; i++)
                        {
                            AllBoards.Add(new Board(BoardsAvailable + i));
                        }
                    }
                    //Reset.WaitOne();
                } while (false);
            });
            thread.Start();
        }

        private static BoardAccessor Accessor = new BoardAccessor();
        public class BoardAccessor
        {
            public Board Get()
            {
                if (Freed.Any())
                {
                    return AllBoards[Freed.Pop()];
                }
                else
                {
                    while (TopBoard >= BoardsAvailable)
                    {
                        if (TopBoard >= MaxBoards)
                        {
                            while (!Freed.Any())
                            {
                                Thread.Sleep(200);
                            }
                            return AllBoards[Freed.Pop()];
                        }
                    }
                    return AllBoards[TopBoard++];
                }
            }

            public void Return(Board board)
            {
                Freed.Push(board.Index);
            }
        }

        public static void Access(Action<BoardAccessor> act)
        {
            lock (Locker)
            {
                act(Accessor);
            }
        }

        public static T Access<T>(Func<BoardAccessor, T> act)
        {
            lock (Locker)
            {
                return act(Accessor);
            }
        }

        public Cell[] Cells;
        public int Index;

        private Board(int index)
        {
            Index = index;
            Cells = Enumerable.Range(0, Coord.AllCoords.Count).Select(i => new Cell(this, (byte)i)).ToArray();
        }

        public Cell this[byte row, byte col]
        {
            get { return this[new Coord(row, col)]; }
        }

        public Cell this[Coord c]
        {
            get { return Cells[Coord.Indices[c]]; }
        }

        public Board Clone(BoardAccessor access)
        {
            var clone = access.Get();
            foreach (var cell in clone.Cells)
            {
                cell.CloneFrom(this);
            }
            return clone;
        }

        public bool Move(Move move)
        {
            var fromCoord = move.From;
            var toCoord = move.To;
            if (!Coord.Connections[fromCoord].Contains(toCoord) && !Coord.Reach[fromCoord].Contains(toCoord)) return false;
            var from = Cells[Coord.Indices[fromCoord]];
            var to = Cells[Coord.Indices[toCoord]];
            if (from.State == State.Empty) return false;
            if (to.State != State.Empty) return false;
            to.State = from.State;
            if (Coord.Reach[fromCoord].Contains(toCoord)) from.State = State.Empty;
            foreach (var cxn in Coord.Connections[toCoord].Select(c => Cells[Coord.Indices[c]]))
            {
                if (cxn.State != State.Empty) cxn.State = to.State;
            }
            return true;
        }

        public IEnumerable<Cell> RedCells
        {
            get { return GetStateCells(State.Red); }
        }

        public IEnumerable<Cell> YellowCells
        {
            get { return GetStateCells(State.Yellow); }
        }

        public IEnumerable<Cell> EmptyCells
        {
            get { return GetStateCells(State.Empty); }
        }

        public IEnumerable<Cell> GetStateCells(State state)
        {
            return Cells.Where(c => c.State == state);
        }

        public int GetScore(State team)
        {
            return Cells.Count(c => c.State == team);
        }

        public int GetDifferential(State team)
        {
            return GetScore(team) - GetScore(team == State.Red ? State.Yellow : State.Red);
        }

        public void Print()
        {
            var dict = new Dictionary<State, string>();
            dict[State.Empty] = " . ";
            dict[State.Red] = " R ";
            dict[State.Yellow] = " Y ";

            Console.Write("   ");
            for (byte col = 4; col <= 12; col++)
            {
                Console.Write(col.ToString("D2") + " ");
            }
            Console.WriteLine();
            for (byte row = 4; row <= 20; row++)
            {
                Console.Write(row.ToString("D2") + " ");
                for (byte col = 4; col <= 12; col++)
                {
                    Coord c = new Coord(row, col);
                    if (!Coord.AllCoords.Contains(c))
                        Console.Write("   ");
                    else
                        Console.Write(dict[Cells[Coord.Indices[c]].State]);
                }
                Console.WriteLine();
            }
        }
    }

    class Move
    {
        public Coord From;
        public Coord To;

        public static Move New(string cmd)
        {
            var coords = cmd.Split(',').Select(x => int.Parse(x.Trim())).ToArray();
            return new Move((byte)coords[0], (byte)coords[1], (byte)coords[2], (byte)coords[3]);
        }

        public Move(Coord from, Coord to)
        {
            From = from;
            To = to;
        }

        public Move(byte fromRow, byte fromCol, byte toRow, byte toCol)
        {
            From = new Coord(fromRow, fromCol);
            To = new Coord(toRow, toCol);
        }

        public override string ToString()
        {
            return From.ToString() + "-" + To.ToString();
        }
    }

    class Trial
    {
        public Board Board;
        public State Turn;
        public Move Move;
        public byte Score;
        public bool EndGame;
        public Trial BestResponse;

        public void Return()
        {
            Board.Access(a =>
            {
                Return(a);
            });
        }

        public void Return(Board.BoardAccessor a)
        {
            if (BestResponse != null)
            {
                BestResponse.Return(a);
            }
            a.Return(Board);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var board = Board.Access(a => a.Get());
            board[4, 8].State = State.Red;
            board[8, 4].State = State.Yellow;
            board[8, 12].State = State.Yellow;
            board[16, 4].State = State.Red;
            board[16, 12].State = State.Red;
            board[20, 8].State = State.Yellow;

            //board.Move(new Move(20, 8, 18, 8));
            //board.Move(new Move(16, 4, 14, 4));

            Console.WriteLine("Beginning...");
            while (true)
            {
                var best = GetBestMoveFor(State.Yellow, board, 6, true);
                board.Move(best.Move);
                board.Print();
                Console.WriteLine("Make: {0}", best.Move.ToString());
                var nextResponse = best.BestResponse;
                while (nextResponse != null)
                {
                    Console.WriteLine("\t{0}", nextResponse.Move.ToString());
                    nextResponse = nextResponse.BestResponse;
                }
                Console.Write("Their: ");
                string next = Console.ReadLine();
                board.Move(Move.New(next));
                best.Return();
            }
        }

        static Trial GetBestMoveFor(State turn, Board board, byte depth, bool parallel = false, int maxTrials = 10)
        {
            var moves = board.GetStateCells(turn)
                .SelectMany(c => c.ValidMoves);
            var trials = Board.Access(a => moves
                .Where(m => board[m.To].State == State.Empty)
                .Select(m => new Trial()
                {
                    Board = board.Clone(a),
                    Turn = turn,
                    Move = m
                }).ToList());
            if (!trials.Any())
            {
                return new Trial()
                {
                    Board = board,
                    Turn = turn,
                    Score = (byte)board.GetScore(turn),
                    EndGame = true
                };
            }
            foreach (var trial in trials)
            {
                trial.Board.Move(trial.Move);
                trial.Score = (byte)trial.Board.GetDifferential(turn);
            }

            if (depth > 0)
            {
                var subTrials = trials.OrderByDescending(t => t.Score).Take(maxTrials);
                Action<Trial> act = trial =>
                {
                    var bestMove = GetBestMoveFor(turn == State.Red ? State.Yellow : State.Red, trial.Board, (byte)(depth - 1));
                    trial.Score = bestMove.Score;
                    trial.BestResponse = bestMove;
                };
                if (parallel)
                {
                    subTrials.AsParallel().ForAll(act);
                }
                else
                {
                    foreach (var trial in subTrials)
                    {
                        act(trial);
                    }
                }
            }

            var winner = trials.OrderByDescending(t => t.Score).First();
            Board.Access(a =>
            {
                foreach (var trial in trials)
                {
                    if (trial != winner) a.Return(trial.Board);
                }
            });
            return winner;
        }
    }
}
