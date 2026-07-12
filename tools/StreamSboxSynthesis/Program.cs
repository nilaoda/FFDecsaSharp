using System.Globalization;

const ushort All = ushort.MaxValue;
int maxCost = args.Length == 1 && int.TryParse(args[0], out int parsed) ? parsed : 4;
if (maxCost is < 1 or > 7)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/StreamSboxSynthesis [max-cost: 1..7]");
    return 1;
}

List<List<Node>> byCost = BuildCatalog(maxCost);
Dictionary<ushort, Node> best = byCost.SelectMany(static nodes => nodes)
    .ToDictionary(static node => node.Value);

Console.WriteLine($"catalog_nodes={best.Count} max_formula_cost={maxCost}");
for (int sbox = 0; sbox < 7; sbox++)
{
    ushort[] targets = GetCofactors(sbox);
    Console.WriteLine($"sbox={sbox + 1} cofactors={string.Join(',', targets.Select(static value => value.ToString("X4", CultureInfo.InvariantCulture)))}");

    foreach (Candidate candidate in FindCandidates(targets, best).Take(8))
    {
        Console.WriteLine($"  total={candidate.TotalCost} shared={candidate.Shared.Expr} shared_cost={candidate.Shared.Cost} branches={candidate.Description}");
    }
}

return 0;

static List<List<Node>> BuildCatalog(int maxCost)
{
    var byCost = Enumerable.Range(0, maxCost + 1).Select(static _ => new List<Node>()).ToList();
    var best = new Dictionary<ushort, Node>();

    AddSource(0x0000, "0");
    AddSource(All, "1");
    AddSource(0xAAAA, "fa");
    AddSource(0xCCCC, "fb");
    AddSource(0xF0F0, "fc");
    AddSource(0xFF00, "fd");

    for (int cost = 1; cost <= maxCost; cost++)
    {
        for (int leftCost = 0; leftCost < cost; leftCost++)
        {
            int rightCost = cost - leftCost - 1;
            if (rightCost < leftCost)
            {
                continue;
            }

            foreach (Node left in byCost[leftCost])
            {
                foreach (Node right in byCost[rightCost])
                {
                    if (leftCost == rightCost && left.Value > right.Value)
                    {
                        continue;
                    }

                    Add((ushort)(left.Value & right.Value), $"({left.Expr}&{right.Expr})", left, right);
                    Add((ushort)(left.Value | right.Value), $"({left.Expr}|{right.Expr})", left, right);
                    Add((ushort)(left.Value ^ right.Value), $"({left.Expr}^{right.Expr})", left, right);
                    Add((ushort)(left.Value & ~right.Value), $"({left.Expr}&~{right.Expr})", left, right);
                    Add((ushort)(right.Value & ~left.Value), $"({right.Expr}&~{left.Expr})", right, left);
                }
            }
        }
    }

    return byCost;

    void AddSource(ushort value, string expr)
    {
        if (best.TryAdd(value, new Node(value, expr, 0, null, null)))
        {
            byCost[0].Add(best[value]);
        }
    }

    void Add(ushort value, string expr, Node left, Node right)
    {
        if (best.ContainsKey(value))
        {
            return;
        }

        var node = new Node(value, expr, left.Cost + right.Cost + 1, left, right);
        best.Add(value, node);
        byCost[node.Cost].Add(node);
    }
}

static IEnumerable<Candidate> FindCandidates(IReadOnlyList<ushort> targets, IReadOnlyDictionary<ushort, Node> best)
{
    int baseline = targets.Sum(target => best.TryGetValue(target, out Node? node) ? node.Cost : 99);
    foreach (Node shared in best.Values.Where(static node => node.Cost > 0))
    {
        int total = shared.Cost;
        var descriptions = new List<string>(targets.Count);
        int sharedUseCount = 0;

        foreach (ushort target in targets)
        {
            Branch? branch = FindBestBranch(shared, target, best);
            if (branch is null)
            {
                total = int.MaxValue;
                break;
            }

            total += branch.Cost;
            if (branch.UsesShared)
            {
                sharedUseCount++;
            }
            descriptions.Add(branch.Description);
        }

        if (total < baseline && sharedUseCount >= 2)
        {
            yield return new Candidate(shared, total, string.Join("; ", descriptions));
        }
    }
}

static Branch? FindBestBranch(Node shared, ushort target, IReadOnlyDictionary<ushort, Node> best)
{
    var candidates = new List<Branch>();
    if (best.TryGetValue((ushort)(target ^ shared.Value), out Node? xor) && !xor.Contains(shared.Value))
    {
        candidates.Add(new Branch(xor.Cost + 1, true, $"shared^{xor.Expr}"));
    }

    if ((target & ~shared.Value) == 0 && best.TryGetValue(target, out Node? and) && !and.Contains(shared.Value))
    {
        candidates.Add(new Branch(and.Cost + 1, true, $"shared&{and.Expr}"));
        ushort mask = (ushort)(shared.Value & ~target);
        if (best.TryGetValue(mask, out Node? andNot) && !andNot.Contains(shared.Value))
        {
            candidates.Add(new Branch(andNot.Cost + 1, true, $"shared&~{andNot.Expr}"));
        }
    }

    if ((shared.Value & ~target) == 0 && best.TryGetValue(target, out Node? or) && !or.Contains(shared.Value))
    {
        candidates.Add(new Branch(or.Cost + 1, true, $"shared|{or.Expr}"));
    }

    if (best.TryGetValue(target, out Node? direct))
    {
        candidates.Add(new Branch(direct.Cost, false, direct.Expr));
    }

    return candidates.Count == 0 ? null : candidates.MinBy(static candidate => candidate.Cost);
}

static ushort[] GetCofactors(int sbox)
{
    ushort fa = 0xAAAA;
    ushort fb = 0xCCCC;
    ushort fc = 0xF0F0;
    ushort fd = 0xFF00;
    (ushort zeroA, ushort zeroB) = Evaluate(sbox, 0, fa, fb, fc, fd);
    (ushort oneA, ushort oneB) = Evaluate(sbox, All, fa, fb, fc, fd);
    return [zeroA, oneA, zeroB, oneB];
}

static (ushort A, ushort B) Evaluate(int sbox, ushort fe, ushort fa, ushort fb, ushort fc, ushort fd)
{
    ushort U(int value) => (ushort)value;
    ushort tmp0;
    ushort tmp1;
    ushort tmp2;
    ushort tmp3;

    switch (sbox)
    {
        case 0:
            tmp0 = U(fa ^ (fb ^ ((((fa | fb) ^ fc) | (fc ^ fd)) ^ All)));
            tmp1 = U((fa | fb) ^ ((fc & (fa | (fb ^ fd))) ^ All));
            tmp2 = U(fa ^ ((fb & fd) ^ ((fa & fd) | fc)));
            tmp3 = U((fa & fc) ^ (fa ^ ((fa & fb) | fd)));
            return (U(tmp0 ^ (fe & tmp1)), U(tmp2 ^ (fe & tmp3)));
        case 1:
            tmp0 = U(fa ^ ((fb & (fc | fd)) ^ (fc ^ (fd ^ All))));
            tmp1 = U((fa & (fb ^ fd)) | ((fa | fb) & fc));
            tmp2 = U((fb & fd) ^ ((fa & fd) | (fb ^ (fc ^ All))));
            tmp3 = U((fa & fd) | (fa ^ (fb ^ (fc & fd))));
            return (U(tmp0 ^ (fe & tmp1)), U(tmp2 ^ (fe & tmp3)));
        case 2:
            tmp0 = U(fa ^ (fb ^ ((fc & (fa | fd)) ^ fd)));
            tmp1 = U((fa & fc) ^ ((fa ^ fd) | ((fb | fc) ^ (fd ^ All))));
            tmp2 = U(fa ^ (((fb ^ fc) & fd) ^ fc));
            return (U(tmp0 ^ ((fe ^ All) & tmp1)), U(tmp2 ^ fe));
        case 3:
            tmp0 = U(fa ^ ((fc & (fa ^ fd)) | (fb ^ (fc | (fd ^ All)))));
            tmp1 = U((fa & fb) ^ (fb ^ (((fa | fc) & fd) ^ fc)));
            tmp2 = U(fa ^ ((fb & fc) | (((fa & (fb ^ fd)) | fc) ^ fd)));
            ushort first = U(tmp0 ^ (fe & U(tmp1 ^ tmp0)));
            return (first, U((first ^ tmp2) ^ fe));
        case 4:
            tmp0 = U(((fa & (fb | fc)) ^ fb) | (((fa ^ fc) | fd) ^ All));
            tmp1 = U(fb ^ ((fc ^ fd) & (fc ^ (fb | (fa ^ fd)))));
            tmp2 = U((fa & fc) ^ (fb ^ ((fb | (fa ^ fc)) & fd)));
            tmp3 = U(((fa ^ fb) & (fc ^ All)) | fd);
            return (U(tmp0 ^ (fe & tmp1)), U(tmp2 ^ (fe & tmp3)));
        case 5:
            tmp0 = U(((fa & fc) & fd) ^ ((fb & (fa | fd)) ^ fc));
            tmp1 = U(((fa ^ fc) & fd) ^ All);
            tmp2 = U((fa & (fb | fc)) ^ (fb ^ ((fb & fc) | fd)));
            tmp3 = U(fc & ((fa & (fb ^ fd)) ^ (fb | fd)));
            return (U(tmp0 ^ (fe & tmp1)), U(tmp2 ^ (fe & tmp3)));
        case 6:
            tmp0 = U(fb ^ ((fc & fd) | (fa ^ (fc ^ fd))));
            tmp1 = U((fb | fd) & ((fa & fc) | (fb ^ (fc ^ fd))));
            tmp2 = U((fa | fb) ^ ((fc & (fb | fd)) ^ fd));
            tmp3 = U(fd | ((fa & fc) ^ All));
            return (U(tmp0 ^ (fe & tmp1)), U(tmp2 ^ (fe & tmp3)));
        default:
            throw new ArgumentOutOfRangeException(nameof(sbox));
    }
}

sealed record Node(ushort Value, string Expr, int Cost, Node? Left, Node? Right)
{
    public bool Contains(ushort value) => Value == value || (Left?.Contains(value) ?? false) || (Right?.Contains(value) ?? false);
}
sealed record Branch(int Cost, bool UsesShared, string Description);
sealed record Candidate(Node Shared, int TotalCost, string Description);
