using System.Collections.Immutable;
using System.Text;

namespace WordGenLib
{
    public record class FinalGrid(ImmutableArray<string> Down, ImmutableArray<string> Across)
    {
        public string Repr => string.Join('\n', Down);
    }

    public class CharSet
    {
        private readonly bool[] _available;
        private readonly char _min;
        private int _ct;

        public CharSet(char min, char max)
        {
            _min = min;
            _available = new bool[1 + (max - min)];
            _ct = 0;
        }

        public CharSet() : this('`', 'z') { }

        public void Add(char c)
        {
            if (!_available[c - _min])
            {
                _ct += 1;
                _available[ c - _min ] = true;
            }
        }

        public bool Contains(char c)
        {
            return _available[c - _min ];
        }

        public bool IsFull => _ct == _available.Length;
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
                .OrderByDescending(l => Random.Shared.Next(10_000) + (l.NumLetters + l.NumLettersOfCommonWords) * 10_000 + l.MaxLength * 100_000)
                .ToImmutableArray();
        }

        private record class ScoredLine(string Line, int MaxLength, int NumLetters, int NumLettersOfCommonWords) { }
        private record GridState(ImmutableArray<ImmutableArray<ScoredLine>> Down, ImmutableArray<ImmutableArray<ScoredLine>> Across) {
            public int SideLength => Down.Length;
            public int Area => SideLength * SideLength;

            public int? UndecidedDown
                => Down.Select((options, index) => (options, index)).Where(oi => oi.options.Length > 1).OrderBy(oi => oi.options.Length).Select(oi => (int?)oi.index).FirstOrDefault();
            public int? UndecidedAcross
                => Across.Select((options, index) => (options, index)).Where(oi => oi.options.Length > 1).OrderBy(oi => oi.options.Length).Select(oi => (int?)oi.index).FirstOrDefault();
        }
        interface IPruneStrategy
        {
            record class PruneStepState(int MaxLength) { }
            bool ShouldKeepCommonWord(PruneStepState state);
            bool ShouldKeepObscureWord(PruneStepState state);
            bool ShouldKeepFinalSet(PruneStepState state, int builderLength);
        }
        class DontPrune : IPruneStrategy
        {
            public bool ShouldKeepCommonWord(IPruneStrategy.PruneStepState _) => true;
            public bool ShouldKeepObscureWord(IPruneStrategy.PruneStepState _) => true;
            public bool ShouldKeepFinalSet(IPruneStrategy.PruneStepState _, int _2) => true;
        }
        class PruneRoots : IPruneStrategy
        {
            public bool ShouldKeepCommonWord(IPruneStrategy.PruneStepState s) => Random.Shared.Next(s.MaxLength) > 1;
            public bool ShouldKeepObscureWord(IPruneStrategy.PruneStepState s) => Random.Shared.Next(s.MaxLength) > Math.Min(2, s.MaxLength - 2);
            public bool ShouldKeepFinalSet(IPruneStrategy.PruneStepState _, int _2) => true;
        }
        class PruneAggressive : IPruneStrategy
        {
            public bool ShouldKeepCommonWord(IPruneStrategy.PruneStepState s) => Random.Shared.Next(s.MaxLength) > 1;
            public bool ShouldKeepObscureWord(IPruneStrategy.PruneStepState s) => Random.Shared.Next(s.MaxLength) > Math.Min(3, s.MaxLength - 2);
            public bool ShouldKeepFinalSet(IPruneStrategy.PruneStepState _, int builderLength) => Random.Shared.Next(3) > 0 && Random.Shared.Next(builderLength) >= Math.Sqrt(builderLength);
        }


        private ImmutableArray<ScoredLine> AllPossibleLines(int maxLength)
        {
            return AllPossibleLines(maxLength, new Dictionary<int, ImmutableArray<ScoredLine>>(), new DontPrune());
        }
        private ImmutableArray<ScoredLine> AllPossibleLines(int maxLength, Dictionary<int, ImmutableArray<ScoredLine>> memo, IPruneStrategy prune)
        {
            if (maxLength > gridSize) throw new Exception($"{nameof(maxLength)} ({maxLength}) cannot be greater than {nameof(gridSize)} {gridSize}");
            if (maxLength < 3) return ImmutableArray<ScoredLine>.Empty;

            if (memo.TryGetValue(maxLength, out var result))
            {
                return result;
            }
            var pruneState = new IPruneStrategy.PruneStepState(maxLength);

            var builder = ImmutableArray.CreateBuilder<ScoredLine>();
            builder.AddRange(commonWordsByLength[maxLength]
                .Select(word => new ScoredLine(word, maxLength, maxLength, maxLength))
                .Where(_ => prune.ShouldKeepCommonWord(pruneState))
                );

            builder.AddRange(obscureWordsByLength[maxLength]
                .Select(word => new ScoredLine(word, maxLength, maxLength, 0))
                .Where(_ => prune.ShouldKeepObscureWord(pruneState))
                );

            // recurse into *[ANYTHING], and [ANYTHING]*
            builder.AddRange(AllPossibleLines(maxLength - 1, memo, prune)
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
            for (int i = 3; i <= maxLength - 4; ++i)
            {
                int firstLength = i;  // Always >= 3.
                int secondLength = maxLength - (i + 1);  // Always >= 3.

                if (secondLength < 3) throw new Exception($"{nameof(secondLength)} is {secondLength} (i = {i}).");

                foreach (var firstHalf in AllPossibleLines(firstLength, memo, prune))
                {
                    var firstHalfWords = firstHalf.Line.Trim(GenHelper.BLOCKED).Split(GenHelper.BLOCKED);

                    foreach (var secondHalf in AllPossibleLines(secondLength, memo, prune))
                    {
                        if (firstHalfWords.Any(word => secondHalf.Line.Contains(word))) continue;

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

            var options = builder.DistinctBy(l => l.Line).Where(_ => prune.ShouldKeepFinalSet(pruneState, builder.Count)).ToImmutableArray();
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
                    bool allFull = true;

                    for (int y = 0; y < arr.Length; y++)
                    {
                        var chars = available.GetOrAddDefault((x, y));
                        chars.Add(arr[y]);
                        allFull = allFull && chars.IsFull;
                    }

                    if (allFull) break;
                }
            }

            var filtered = toFilter.Select((possibles, y) =>
            {
                if (Enumerable.Range(0, toFilter.Length)
                    .All(x => available[(x, y)].IsFull)) return possibles;

                return possibles.Where(line =>
                {
                    for (int x = 0; x < line.Line.Length; ++x)
                    {
                        if (!available[(x, y)].Contains(line.Line[x])) return false;
                    }
                    return true;
                }).ToImmutableArray();
            }).ToImmutableArray();

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

            // If board is > 35% blocked, it's not worth iterating in it.
            int numDefinitelyBlocked = root.Down
                .Where(options => options.Length == 1)
                .Sum(options => options[0].Line.Count(c => c == GenHelper.BLOCKED) );
            if (numDefinitelyBlocked > (root.Area * 0.35) )
            {
                yield break;
            }

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

            int? undecidedDown = root.UndecidedDown;
            int? undecidedAcross = root.UndecidedAcross;

            if (undecidedDown == null && undecidedAcross == null)
            {
                yield return new FinalGrid(
                    Down: root.Down.Select(col => col[0].Line).ToImmutableArray(),
                    Across: root.Across.Select(row => row[0].Line).ToImmutableArray()
                );
                yield break;
            }

            var possibleGrids = (undecidedDown, undecidedAcross) switch
            {
                (int uD, null) => AllPossibleGrids(root, uD, Direction.Vertical),
                (int uD, int uA) when root.Down[uD].Length < root.Across[uA].Length => AllPossibleGrids(root, uD, Direction.Vertical),
                (null, int uA) => AllPossibleGrids(root, uA, Direction.Horizontal),
                (int uD, int uA) when root.Down[uD].Length >= root.Across[uA].Length => AllPossibleGrids(root, uA, Direction.Horizontal),
                _ => Enumerable.Empty<FinalGrid>(),
            };
            foreach (var final in possibleGrids) yield return final;
        }

        private static IEnumerable<FinalGrid> AllPossibleGrids(GridState root, int index, Direction dir)
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

            var options = optionAxis[index];

            // The below loop "makes decisions" and recurses. If we already
            // have one attempt, that means it's already pre-decided.
            if (options.Length == 1) yield break;

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

        static bool Changed(GridState before, GridState after)
        {
            for (int i = 0; i < before.Down.Length; ++i)
            {
                if (before.Down[i].Length != after.Down[i].Length) return true;
                if (before.Across[i].Length != after.Across[i].Length) return true;
            }
            return false;
        }

        public IEnumerable<FinalGrid> PossibleGrids()
        {
            GridState state = new(
                Down: Enumerable.Range(0, gridSize).Select(_ => possibleLines).ToImmutableArray(),
                Across: Enumerable.Range(0, gridSize).Select(_ => possibleLines).ToImmutableArray()
            );
            return AllPossibleGrids(state).DistinctBy(x => x.Repr);
        }

        public IEnumerable<FinalGrid> PossibleGridWithConstraints(char[,] constraints)
        {
            if (constraints == null) throw new ArgumentNullException(nameof(constraints));
            if (constraints.GetLength(0) != gridSize || constraints.GetLength(1) != gridSize) throw new ArgumentException($"{nameof(constraints)} should have size {GridSize} x {GridSize}");

            string[] downTemplates = Enumerable.Range(0, gridSize).Select(y => string.Join("", Enumerable.Range(0, gridSize).Select(x => constraints[x, y]))).ToArray();
            string[] acrossTemplates = Enumerable.Range(0, gridSize).Select(x => string.Join("", Enumerable.Range(0, gridSize).Select(y => constraints[x, y]))).ToArray();

            GridState state = new(
                Down: Enumerable.Range(0, gridSize).Select(i => CompatibleLines(downTemplates[i]).ToImmutableArray()).ToImmutableArray(),
                Across: Enumerable.Range(0, gridSize).Select(i => CompatibleLines(acrossTemplates[i]).ToImmutableArray()).ToImmutableArray()
            );
            return AllPossibleGrids(state).DistinctBy(x => x.Repr);
        }

        private IEnumerable<ScoredLine> CompatibleLines(string template)
        {
            if (template.All(x => x == ' ')) return possibleLines;
            if (template.All(x => x != ' ')) return ImmutableArray.Create(new ScoredLine(template, template.Length, template.Length, template.Length));
            return possibleLines.Where(x => x.Line.Zip(template).All(chars => chars.First == chars.Second || chars.Second == ' '));
        }

        public  IEnumerable<string> CompatibleLineStrings(string template)
        {
            return CompatibleLines(template).Select(x => x.Line);
        }
    }

    internal static class GenHelper
    {
        public const char BLOCKED = '`';

        public static void FillRow(char?[,] grid, int row, string word)
        {
            for (int i = 0; i < word.Length; ++i)
            {
                if (word[i] == ' ') continue;
                grid[i, row] = word[i];
            }
        }
    }

    public static class GridReader
    {
        internal static ImmutableArray<string> COMMON_WORDS =
            Properties.Resources.words.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
            .Concat(Properties.Resources.phrases.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
            .Select(s => s.Trim().Replace(" ", ""))
            .Distinct()
            .ToImmutableArray();

        internal static ImmutableArray<string> OBSCURE_WORDS =
            Properties.Resources.phrases.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
            .Concat(Properties.Resources.wikipedia.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
            .Concat(Properties.Resources.from_lexems.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
            .Select(s => s.Trim().Replace(" ", ""))
            .Distinct()
            .Except(COMMON_WORDS)
            .ToImmutableArray();

        internal static ImmutableArray<string> ALL_WORDS = COMMON_WORDS.AddRange(OBSCURE_WORDS);

        public static HashSet<string> AllowedWords()
        {
            return new HashSet<string>(ALL_WORDS);
        }

        public static string[] DownWords(FinalGrid grid)
        {
            return grid.Down.SelectMany(s => s.Split(GenHelper.BLOCKED)).Where(s => s.Length > 0).ToArray();
        }

        public static string[] AcrossWords(FinalGrid grid)
        {
            return grid.Across.SelectMany(s => s.Split(GenHelper.BLOCKED)).Where(s => s.Length > 0).ToArray();
        }
    }

    public enum Direction { Horizontal, Vertical }
}