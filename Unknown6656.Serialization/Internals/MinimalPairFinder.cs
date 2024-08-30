namespace Unknown6656.EDS.Internals;


internal class MinimalPairFinder<X, Y>
    where X : notnull
{
    public Func<X, Y, int> Distance { get; }


    public MinimalPairFinder(Func<X, Y, int> distance) => Distance = distance;

    public Dictionary<X, Y> FindMinimalPairs(IEnumerable<X> x, IEnumerable<Y> y) => FindMinimalPairs(x, y, out _);

    public Dictionary<X, Y> FindMinimalPairs(IEnumerable<X> x, IEnumerable<Y> y, out int sum)
    {
        X[] xs = x as X[] ?? x.ToArray();
        Y[] ys = y as Y[] ?? y.ToArray();
        int n = xs.Length;
        int m = ys.Length;
        int[,] matrix = new int[n, m];

        Parallel.For(0, n * m, i => matrix[i % n, i / n] = Distance(xs[i % n], ys[i / n]));

        (int x, int y)[] pairs = FindMinimalPairs(matrix, n, m);
        sum = pairs.Sum(t => matrix[t.x, t.y]);

        return pairs.ToDictionary(t => xs[t.x], t => ys[t.y]);
    }

    private static (int x, int y)[] FindMinimalPairs(int[,] matrix, int N, int M)
    {
        int L = Math.Min(N, M);
        List<(int x, int y)> pairs = new();
        List<int> columns = Enumerable.Range(0, M).ToList();
        List<int> rows = Enumerable.Range(0, N).ToList();

        for (int i = 0; i < L; ++i)
        {
            int min_sum = int.MaxValue;
            int sel_row = -1;
            int sel_col = -1;

            foreach (int row in rows)
                foreach (int column in columns)
                    if (matrix[row, column] + pairs.Sum(t => matrix[t.x, t.y]) is int sum && sum < min_sum)
                    {
                        min_sum = sum;
                        sel_row = row;
                        sel_col = column;
                    }

            pairs.Add((sel_row, sel_col));
            columns.Remove(sel_col);
            rows.Remove(sel_row);
        }

        return pairs.ToArray();
    }
}

internal class MinimalPairFinder<X>
    : MinimalPairFinder<X, X>
    where X : notnull
{
    public MinimalPairFinder(Func<X, X, int> distance)
        : base(distance)
    {
    }
}
