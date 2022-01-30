using System.Collections.Immutable;
using System.Text;

namespace WordGenLib
{
    public class CharSet
    {
        private readonly bool[] _available;
        private readonly char _min;

        public CharSet(char min, char max)
        {
            _min = min;
            _available = new bool[1 + (max - min)];
        }

        public CharSet() : this('`', 'z') { }

        public void Add(char c)
        {
            _available[ c - _min ] = true;
        }

        public bool Contains(char c)
        {
            return _available[c - _min ];
        }
    }
    public class GridDictionary<T> where T : notnull, new()
    {
        private readonly T?[,] _values;
        public GridDictionary(int size)
        {
            _values = new T?[size, size];
        }

        public T GetOrAddDefault((int x, int y) kv)
        {
            var x = _values[kv.x, kv.y];
            if (x == null)
            {
                x = new();
                _values[kv.x, kv.y] = x;
            }
            return x;
        }

        public T this[(int x, int y) kv]
        {
            get
            {
                var x = _values[kv.x, kv.y];
                if (x != null) return x;

                throw new NullReferenceException($"No value found at {kv}");
            }
        }

    }

    public class Generator
    {
        private readonly int gridSize;

        private readonly Dictionary<int, ImmutableArray<string>> commonWordsByLength;
        private readonly Dictionary<int, ImmutableArray<string>> obscureWordsByLength;

        private readonly ImmutableArray<ScoredLine> possibleLines;

        public int GridSize => gridSize;

        public static Generator Create(int gridSize)
        {
            return new(
                gridSize,
                GridReader.COMMON_WORDS
                    .RemoveAll(s => s.Length <= 2 || s.Length > gridSize),
                GridReader.OBSCURE_WORDS
                    .RemoveAll(s => s.Length <= 2 || s.Length > gridSize)
                );
        }

        internal Generator(int gridSize, ImmutableArray<string> commonWords, ImmutableArray<string> obscureWords)
        {
            this.gridSize = gridSize;

            commonWordsByLength = commonWords.GroupBy(w => w.Length).ToDictionary(g => g.Key, g => g.ToImmutableArray ());
            obscureWordsByLength = obscureWords.Except(commonWords).GroupBy(w => w.Length).ToDictionary(g => g.Key, g => g.ToImmutableArray());

            possibleLines = AllPossibleLines(gridSize)
                .GroupBy(l => l.Line).Select(grp => grp.First())
                .OrderBy(l => Random.Shared.Next(int.MaxValue))
                .ToImmutableArray()
                .Sort(Reversed<ScoredLine>((x, y) =>
                {
                    if (x.MaxLength > y.MaxLength) return +1; // x is greater than y.
                    else if (x.MaxLength < y.MaxLength) return -1; // x is less than y.

                    var xValue = x.NumLetters + x.NumLettersOfCommonWords;
                    var yValue = y.NumLetters + y.NumLettersOfCommonWords;
                    return xValue - yValue;
                }));
        }

        private record class ScoredLine(string Line, int MaxLength, int NumLetters, int NumLettersOfCommonWords) { }
        private record GridState(ImmutableArray<ImmutableArray<ScoredLine>> Down, ImmutableArray<ImmutableArray<ScoredLine>> Across) {
            public IEnumerable<int> UndecidedDown
                => Down.Select((options, index) => (options, index)).Where(oi => oi.options.Length > 1).Select(oi => oi.index);
            public IEnumerable<int> UndecidedAcross
                => Across.Select((options, index) => (options, index)).Where(oi => oi.options.Length > 1).Select(oi => oi.index);
        }
        private record class FinalGrid(ImmutableArray<ScoredLine> Down, ImmutableArray<ScoredLine> Across)
        {
            public string Repr => string.Join('\n', Down.Select(l => l.Line));
        }

        private ImmutableArray<ScoredLine> AllPossibleLines(int maxLength)
        {
            return AllPossibleLines(maxLength, new Dictionary<int, ImmutableArray<ScoredLine>>());
        }
        private ImmutableArray<ScoredLine> AllPossibleLines(int maxLength, Dictionary<int, ImmutableArray<ScoredLine>> memo)
        {
            if (maxLength > gridSize) throw new Exception($"{nameof(maxLength)} ({maxLength}) cannot be greater than {nameof(gridSize)} {gridSize}");
            if (maxLength < 3) return ImmutableArray<ScoredLine>.Empty;

            if (memo.TryGetValue(maxLength, out var result))
            {
                return result;
                //foreach (var line in result) yield return line;
                //yield break;
            }

            var builder = ImmutableArray.CreateBuilder<ScoredLine>();
            builder.AddRange(commonWordsByLength[maxLength]
                .Select(word => new ScoredLine(word, maxLength, maxLength, maxLength)));

            builder.AddRange(obscureWordsByLength[maxLength]
                .Select(word => new ScoredLine(word, maxLength, maxLength, 0)));

            // recurse into *[ANYTHING], and [ANYTHING]*
            builder.AddRange(AllPossibleLines(maxLength - 1, memo)
                .SelectMany(line => new[] {
                    line with { Line = GenHelper.BLOCKED + line.Line },
                    line with { Line = line.Line + GenHelper.BLOCKED }
                }));

            // recurse into all combination of [ANYTHING]*[ANYTHING]
            //
            // For length 10:
            // 0 1 2 3 4 5 6 7 8 9
            // _ _ _ _ _ _ _ _ _ _
            //       ^     ^
            // Blockage can be anywhere etween idx 3 and len-4.
            for (int i = 3; i < maxLength - 4; ++i)
            {
                int firstLength = i;  // Always >= 3.
                int secondLength = maxLength - (i + 1);  // Always >= 3.

                if (secondLength < 3) throw new Exception($"{nameof(secondLength)} is {secondLength} (i = {i}).");

                foreach (var firstHalf in AllPossibleLines(firstLength, memo))
                {
                    foreach (var secondHalf in AllPossibleLines(secondLength, memo))
                    {
                        builder.Add( new ScoredLine(
                            firstHalf.Line + GenHelper.BLOCKED + secondHalf.Line,
                            Math.Max(firstHalf.MaxLength, secondHalf.MaxLength),
                            firstHalf.NumLetters + secondHalf.NumLetters,
                            firstHalf.NumLettersOfCommonWords + secondHalf.NumLettersOfCommonWords
                            )
                            );
                    }
                }
            }

            var options = builder.DistinctBy(l => l.Line).ToImmutableArray();
            memo.Add(maxLength, options);
            return options;
        }

        static GridState Prefilter(GridState state, Direction direction)
        {
            var toFilter = direction == Direction.Horizontal ? state.Across : state.Down;
            var constraint = direction == Direction.Horizontal ? state.Down : state.Across;

            if (toFilter.Any(r => r.Length == 0) || constraint.Any(c => c.Length == 0)) return state;

            // x and y here are abstracted wlog based on toFilter/constraint, not truly
            // connected to Horizontal vs Vertical.
            GridDictionary<CharSet> available = new(state.Across.Length);
            for (int x = 0; x < constraint.Length; x++)
            {
                var c = constraint[x];
                
                foreach (ScoredLine line in c)
                {
                    var arr = line.Line.ToCharArray();
                    for (int y = 0; y < arr.Length; y++)
                    {
                        var chars = available.GetOrAddDefault((x, y));
                        chars.Add(arr[y]);
                    }
                }
            }

            var filtered = toFilter.Select((possibles, y) => possibles.Where(line => {
                for (int x = 0; x < line.Line.Length; ++x)
                {
                    if (!available[(x, y)].Contains(line.Line[x])) return false;
                }
                return true;
            }).ToImmutableArray()).ToImmutableArray();

            if (direction == Direction.Horizontal)
            {
                return state with { Across = filtered };
            }
            else return state with { Down = filtered };
        }

        private static IEnumerable<FinalGrid> AllPossibleGrids(GridState root)
        {
            // If we are at a point in our tree some row/column is unfillable, prune this tree.
            if (root.Down.Any(options => options.Length == 0)) yield break;
            if (root.Across.Any(options => options.Length == 0)) yield break;

            // Prefilter
            {
                int tries = 0;
                Direction direction = Direction.Horizontal;
                while (tries < 4)
                {
                    ++tries;
                    GridState newState = Prefilter(root, direction);
                    if (!Changed(root, newState) && tries > 1) break;

                    root = newState;
                    direction = direction == Direction.Vertical ? Direction.Horizontal : Direction.Vertical;
                }

                // If we are at a point in our tree some row/column is unfillable, prune this tree.
                if (root.Down.Any(options => options.Length == 0)) yield break;
                if (root.Across.Any(options => options.Length == 0)) yield break;
            }

            ImmutableArray<int> undecidedDown = root.UndecidedDown.ToImmutableArray();
            ImmutableArray<int> undecidedAcross = root.UndecidedAcross.ToImmutableArray();

            if (undecidedDown.IsEmpty && undecidedAcross.IsEmpty)
            {
                yield return new FinalGrid(
                    Down: root.Down.Select(col => col[0]).ToImmutableArray(),
                    Across: root.Across.Select(row => row[0]).ToImmutableArray()
                );
                yield break;
            }

            var dirs = new[] { Direction.Horizontal, Direction.Vertical };
            var parity = Random.Shared.Next(2);

            for (int i = 0; i < dirs.Length; ++i)
            {
                var dir = dirs[(parity + i) % 2];
                var undecided = dir == Direction.Horizontal ? undecidedAcross : undecidedDown;
                if (undecided.IsEmpty) continue;
                foreach (var final in AllPossibleGrids(root, undecided, dir)) yield return final;

                // After the first successful "search" down the tree, we're done here. The second
                // will be a mirror image.
                yield break;
            }
        }

        private static IEnumerable<FinalGrid> AllPossibleGrids(GridState root, ImmutableArray<int> undecided, Direction dir)
        {
            var optionAxis = (dir == Direction.Horizontal ? root.Across : root.Down);
            var oppositeAxis = (dir == Direction.Horizontal ? root.Down : root.Across);

            // Trim situations where horizontal and vertal words are same.
            for (int i = 0; i < optionAxis.Length; ++i)
            {
                if (optionAxis[i].Length > 1) continue;
                if (oppositeAxis[i].Length > 1) continue;

                if (optionAxis[i][0].Line == oppositeAxis[i][0].Line) yield break;
            }

            foreach (int index in undecided)
            {
                var options = optionAxis[index];

                // The below loop "makes decisions" and recurses. If we already
                // have one attempt, that means it's already pre-decided.
                if (options.Length == 1) continue;

                foreach (var attempt in options)
                {
                    var attemptOpposite = oppositeAxis.ToArray();

                    for (int i = 0; i < attempt.Line.Length; i++)
                    {
                        // WLOG say we dir is Horizontal, and opopsite is Vertical.
                        // we have:
                        //
                        // W O R D
                        // _ _ _ _
                        // _ _ _ _
                        // _ _ _ _
                        //
                        // Then go over each COL (i), filtering s.t. possible lines
                        // only include cases where col[i]'s |attempt|th character == attempt[i].
                        var constriant = attempt.Line[i];

                        attemptOpposite[i] = attemptOpposite[i].RemoveAll(option => option.Line[index] != constriant);
                    }

                    if (attemptOpposite.All(opts => opts.Length > 0))
                    {
                        var oppositeFinal = attemptOpposite.ToImmutableArray();
                        var optionFinal = optionAxis.Select((regular, idx) => idx == index ? ImmutableArray.Create(attempt) : regular).ToImmutableArray();

                        var newRoot = (dir == Direction.Horizontal) ?
                            new GridState(
                                Down: oppositeFinal,
                                Across: optionFinal
                                ) :
                            new GridState(
                                Down: optionFinal,
                                Across: oppositeFinal
                                );

                        foreach (var final in AllPossibleGrids(newRoot)) yield return final;
                    }
                }
            }
        }

        static bool Changed(GridState before, GridState after)
        {
            for (int i = 0; i < before.Down.Length; ++i)
            {
                if (before.Down[i].Length != after.Down[i].Length) return true;
                if (before.Across[i].Length != after.Across[i].Length) return true;
            }
            return false;
        }

        private static Comparison<T> Reversed<T>(Comparison<T> original)
        {
            return (x, y) => original(y, x);
        }

        public IEnumerable<char?[,]> GenerateGrid()
        {
            GridState state = new(
                Down: Enumerable.Range(0, gridSize).Select(_ => possibleLines).ToImmutableArray(),
                Across: Enumerable.Range(0, gridSize).Select(_ => possibleLines).ToImmutableArray()
            );

            int tries = 0;
            Direction direction = Direction.Horizontal;
            while (tries < 4)
            {
                ++tries;
                GridState newState = Prefilter(state, direction);
                if (!Changed(state, newState)) break;

                state = newState;
                direction = direction == Direction.Vertical ? Direction.Horizontal : Direction.Vertical;
            }

            HashSet<string> returned = new();

            foreach (var x in AllPossibleGrids(state))
            {
                if (returned.Add(x.Repr))
                {
                    var grid = new char?[gridSize, gridSize];
                    for (int i = 0; i < gridSize; ++i) GenHelper.Fill(grid, i, Direction.Horizontal, 0, x.Across[i].Line);
                    yield return grid;
                }
            }
        }
    }

    internal static class GenHelper
    {
        public const char BLOCKED = '`';

        public static void Fill(char?[,] grid, int rowOrCol, Direction dir, int start, string word)
        {
            if (dir == Direction.Horizontal)
            {
                for (int i = 0; i < word.Length; ++i)
                {
                    if (word[i] == ' ') continue;
                    grid[start + i, rowOrCol] = word[i];
                }
            }
            else
            {
                for (int i = 0; i < word.Length; ++i)
                {
                    if (word[i] == ' ') continue;
                    grid[rowOrCol, start + i] = word[i];
                }
            }
        }
    }

    public static class GridReader
    {
        internal static ImmutableArray<string> COMMON_WORDS =
            Properties.Resources.words.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
            .Concat(Properties.Resources.phrases.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
            .Select(s => s.Trim().Replace(" ", ""))
            .ToImmutableArray();

        internal static ImmutableArray<string> OBSCURE_WORDS =
            Properties.Resources.phrases.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
            .Concat(Properties.Resources.wikipedia.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
            .Concat(Properties.Resources.from_lexems.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
            .Select(s => s.Trim().Replace(" ", ""))
            .ToImmutableArray();

        internal static ImmutableArray<string> ALL_WORDS = COMMON_WORDS.AddRange(OBSCURE_WORDS);

        public static HashSet<string> AllowedWords()
        {
            return new HashSet<string>(ALL_WORDS);
        }

        public static string[] ReadLine(char?[,] grid, int rowOrCol, Direction dir)
        {
            int gridSize = grid.GetLength(0);
            var sb = new StringBuilder();
            if (dir == Direction.Horizontal)
            {
                for (int i = 0; i < gridSize; ++i)
                {
                    sb.Append(grid[i, rowOrCol] switch
                    {
                        null => ' ',
                        char c => c,
                    });
                }
            }
            else
            {
                for (int i = 0; i < gridSize; ++i)
                {
                    sb.Append(grid[rowOrCol, i] switch
                    {
                        null => ' ',
                        char c => c,
                    });
                }
            }

            return sb.ToString().Split(GenHelper.BLOCKED);
        }
    }

    public enum Direction { Horizontal, Vertical }
}