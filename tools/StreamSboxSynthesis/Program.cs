using System.Numerics;
using System.Globalization;

const ushort All = ushort.MaxValue;
if (args.Length >= 1 && string.Equals(args[0], "pla", StringComparison.OrdinalIgnoreCase))
{
    int sbox = args.Length > 1 && int.TryParse(args[1], out int parsedSbox) ? parsedSbox : 1;
    if (sbox is < 1 or > 7)
    {
        Console.Error.WriteLine("Usage: pla [sbox: 1..7]");
        return 1;
    }

    ushort[] cofactors = GetCofactors(sbox - 1);
    Console.WriteLine(".i 4");
    Console.WriteLine(".o 4");
    Console.WriteLine(".ilb fa fb fc fd");
    Console.WriteLine(".ob a0 a1 b0 b1");
    for (int row = 0; row < 16; row++)
    {
        string input = Convert.ToString(row, 2).PadLeft(4, '0');
        string output = string.Concat(cofactors.Select(value => (value & (1 << row)) != 0 ? '1' : '0'));
        Console.WriteLine($"{input} {output}");
    }
    Console.WriteLine(".e");
    return 0;
}

if (args.Length >= 1 && string.Equals(args[0], "beam", StringComparison.OrdinalIgnoreCase))
{
    int sboxArg = args.Length > 1 && int.TryParse(args[1], out int parsedSbox) ? parsedSbox : 0;
    int maxGates = args.Length > 2 && int.TryParse(args[2], out int parsedGates) ? parsedGates : 18;
    int beamWidth = args.Length > 3 && int.TryParse(args[3], out int parsedBeam) ? parsedBeam : 64;
    if (sboxArg is < 0 or > 7 || maxGates is < 4 or > 28 || beamWidth is < 4 or > 512)
    {
        Console.Error.WriteLine("Usage: beam [sbox:0=all|1..7] [max-gates:4..28] [beam-width:4..512]");
        return 1;
    }

    int first = sboxArg == 0 ? 0 : sboxArg - 1;
    int last = sboxArg == 0 ? 6 : sboxArg - 1;
    for (int sbox = first; sbox <= last; sbox++)
    {
        RunBeamSearch(sbox, maxGates, beamWidth);
    }

    return 0;
}

bool xorPairMode = args.Length >= 1 && string.Equals(args[0], "xor2", StringComparison.OrdinalIgnoreCase);
int costArgumentIndex = xorPairMode ? 1 : 0;
int maxCost = args.Length > costArgumentIndex && int.TryParse(args[costArgumentIndex], out int parsed) ? parsed : 4;
if (maxCost is < 1 or > 7)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/StreamSboxSynthesis [max-cost: 1..7] | xor2 [max-cost: 1..7] | beam [sbox] [max-gates] [beam]");
    return 1;
}

List<List<Node>> byCost = BuildCatalog(maxCost);
Dictionary<ushort, Node> best = byCost.SelectMany(static nodes => nodes)
    .ToDictionary(static node => node.Value);

Console.WriteLine($"catalog_nodes={best.Count} max_formula_cost={maxCost}");
for (int sbox = 0; sbox < 7; sbox++)
{
    ushort[] targets = GetCofactors(sbox);
    int outputMergeCost = GetOutputMergeCost(targets);
    int sourceGateCost = GetCurrentNetworkGateCount(sbox);
    Console.WriteLine($"sbox={sbox + 1} outputs={GetOutputNames(sbox)} source_gates={sourceGateCost} cofactor_merge_gates={outputMergeCost} cofactors={string.Join(',', targets.Select(static value => value.ToString("X4", CultureInfo.InvariantCulture)))}");

    IEnumerable<Candidate> candidates = xorPairMode
        ? FindXorPairCandidates(targets, best)
        : FindCandidates(targets, best);
    foreach (Candidate candidate in candidates
        .Where(candidate => candidate.TotalCost + outputMergeCost < sourceGateCost)
        .Take(8))
    {
        int completeCost = candidate.TotalCost + outputMergeCost;
        Console.WriteLine($"  complete_total={completeCost} cofactor_total={candidate.TotalCost} shared={candidate.Shared.Expr} shared_cost={candidate.Shared.Cost} branches={candidate.Description}");
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

static IEnumerable<Candidate> FindXorPairCandidates(IReadOnlyList<ushort> targets, IReadOnlyDictionary<ushort, Node> best)
{
    int baseline = targets.Sum(target => best.TryGetValue(target, out Node? node) ? node.Cost : 99);
    List<Node> sharedNodes = best.Values
        .Where(static node => node.Cost is > 0 and <= 3)
        .Where(node => IsUsefulXorShare(node, targets, best))
        .OrderBy(node => XorShareBenefit(node, targets, best))
        .ThenBy(static node => node.Cost)
        .ThenBy(static node => node.Value)
        .Take(256)
        .ToList();

    for (int first = 0; first < sharedNodes.Count; first++)
    {
        Node left = sharedNodes[first];
        for (int second = first + 1; second < sharedNodes.Count; second++)
        {
            Node right = sharedNodes[second];
            if (left.Contains(right.Value) || right.Contains(left.Value))
            {
                continue;
            }

            int total = left.Cost + right.Cost;
            int leftUses = 0;
            int rightUses = 0;
            var descriptions = new List<string>(targets.Count);
            foreach (ushort target in targets)
            {
                XorBranch? branch = FindBestXorBranch(left, right, target, best);
                if (branch is null)
                {
                    total = int.MaxValue;
                    break;
                }

                total += branch.Cost;
                leftUses += branch.LeftUses;
                rightUses += branch.RightUses;
                descriptions.Add(branch.Description);
            }

            if (total < baseline && leftUses >= 2 && rightUses >= 2)
            {
                yield return new Candidate(
                    new Node(0, $"{left.Expr}; {right.Expr}", left.Cost + right.Cost, null, null),
                    total,
                    string.Join("; ", descriptions));
            }
        }
    }
}

static bool IsUsefulXorShare(Node shared, IReadOnlyList<ushort> targets, IReadOnlyDictionary<ushort, Node> best)
{
    return targets.Any(target =>
    {
        if (!best.TryGetValue(target, out Node? direct)
            || !best.TryGetValue((ushort)(target ^ shared.Value), out Node? residual)
            || residual.Contains(shared.Value))
        {
            return false;
        }

        return residual.Cost + 1 < direct.Cost;
    });
}

static int XorShareBenefit(Node shared, IReadOnlyList<ushort> targets, IReadOnlyDictionary<ushort, Node> best)
{
    int benefit = 0;
    foreach (ushort target in targets)
    {
        if (best.TryGetValue(target, out Node? direct)
            && best.TryGetValue((ushort)(target ^ shared.Value), out Node? residual)
            && !residual.Contains(shared.Value))
        {
            benefit += Math.Min(0, residual.Cost + 1 - direct.Cost);
        }
    }

    return benefit;
}

static XorBranch? FindBestXorBranch(Node left, Node right, ushort target, IReadOnlyDictionary<ushort, Node> best)
{
    XorBranch? result = null;
    for (int mask = 0; mask < 4; mask++)
    {
        ushort sharedValue = 0;
        int useCount = 0;
        if ((mask & 1) != 0)
        {
            sharedValue ^= left.Value;
            useCount++;
        }
        if ((mask & 2) != 0)
        {
            sharedValue ^= right.Value;
            useCount++;
        }

        ushort residualValue = (ushort)(target ^ sharedValue);
        if (!best.TryGetValue(residualValue, out Node? residual)
            || residual.Contains(left.Value)
            || residual.Contains(right.Value))
        {
            continue;
        }

        int cost = residualValue == 0
            ? Math.Max(0, useCount - 1)
            : residual.Cost + useCount;
        string description = mask switch
        {
            0 => residual.Expr,
            1 when residualValue == 0 => "shared0",
            2 when residualValue == 0 => "shared1",
            3 when residualValue == 0 => "shared0^shared1",
            1 => $"shared0^{residual.Expr}",
            2 => $"shared1^{residual.Expr}",
            _ => $"shared0^shared1^{residual.Expr}",
        };
        var candidate = new XorBranch(cost, (mask & 1) != 0 ? 1 : 0, (mask & 2) != 0 ? 1 : 0, description);
        if (result is null || candidate.Cost < result.Cost)
        {
            result = candidate;
        }
    }

    return result;
}


static void RunBeamSearch(int sbox, int maxGates, int beamWidth)
{
    ushort[] targets = GetCofactors(sbox);
    int mergeCost = GetOutputMergeCost(targets);
    int sourceGates = GetCurrentNetworkGateCount(sbox);
    Console.WriteLine(
        $"beam sbox={sbox + 1} outputs={GetOutputNames(sbox)} source_gates={sourceGates} merge={mergeCost} max_gates={maxGates} beam={beamWidth} targets={string.Join(',', targets.Select(static value => value.ToString("X4", CultureInfo.InvariantCulture)))}");

    // Seed with constants and inputs. Cost is number of binary gates in the DAG.
    var seeds = new List<DagNode>
    {
        new(0x0000, "0", 0),
        new(All, "1", 0),
        new(0xAAAA, "fa", 0),
        new(0xCCCC, "fb", 0),
        new(0xF0F0, "fc", 0),
        new(0xFF00, "fd", 0),
    };

    var beam = new List<DagState> { new(seeds, 0) };
    int bestCover = int.MaxValue;
    string? bestDescription = null;

    for (int depth = 0; depth <= maxGates; depth++)
    {
        foreach (DagState state in beam)
        {
            int cover = CoverCost(state.Nodes, targets);
            if (cover >= int.MaxValue / 8)
            {
                continue;
            }

            // Only treat exact DAG covers (all targets present) as real complete networks.
            // one-gate residual estimates are used only for beam ranking.
            if (cover != 0)
            {
                continue;
            }

            int complete = state.GateCount + mergeCost;
            if (complete < bestCover)
            {
                bestCover = complete;
                bestDescription = DescribeCover(state.Nodes, targets);
                Console.WriteLine(
                    $"  exact_complete={bestCover} gates={state.GateCount} residual_cover=0 delta={bestCover - sourceGates} cover={bestDescription}");
            }
        }

        if (depth == maxGates)
        {
            break;
        }

        var next = new List<DagState>();
        var seen = new HashSet<string>();
        foreach (DagState state in beam)
        {
            int n = state.Nodes.Count;
            // Expand only pairs involving the most recently introduced non-seed node when available,
            // plus a bounded all-pairs pass over the last few nodes to recover sharing.
            int start = state.GateCount < 4 ? 0 : Math.Max(0, n - 8);
            for (int i = start; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    DagNode left = state.Nodes[i];
                    DagNode right = state.Nodes[j];
                    TryAdd(state, left, right, (ushort)(left.Value & right.Value), $"({left.Expr}&{right.Expr})", next, seen, targets, sourceGates, mergeCost);
                    TryAdd(state, left, right, (ushort)(left.Value | right.Value), $"({left.Expr}|{right.Expr})", next, seen, targets, sourceGates, mergeCost);
                    TryAdd(state, left, right, (ushort)(left.Value ^ right.Value), $"({left.Expr}^{right.Expr})", next, seen, targets, sourceGates, mergeCost);
                    TryAdd(state, left, right, (ushort)(left.Value & ~right.Value), $"({left.Expr}&~{right.Expr})", next, seen, targets, sourceGates, mergeCost);
                }
            }
        }

        // Keep states with lowest (gateCount + residual cover), then complete.
        var ranked = next
            .Select(state => (state, cover: CoverCost(state.Nodes, targets)))
            .OrderBy(item => item.cover)
            .ThenBy(item => item.state.GateCount)
            .ThenBy(item => item.cover + item.state.GateCount)
            .Take(beamWidth)
            .ToList();
        beam = ranked.Select(item => item.state).ToList();
        if (beam.Count == 0)
        {
            break;
        }

        int bestResidual = ranked[0].cover;
        if (bestResidual < 100 || depth == 0 || (depth + 1) % 4 == 0)
        {
            int presentCount = 0;
            foreach (ushort target in targets)
            {
                if (beam[0].Nodes.Any(node => node.Value == target))
                {
                    presentCount++;
                }
            }

            Console.WriteLine($"  depth={depth + 1} beam={beam.Count} best_residual={bestResidual} present={presentCount}/{targets.Length} nodes={beam[0].Nodes.Count}");
        }
    }

    Console.WriteLine(
        bestDescription is null
            ? $"  no_exact_cover under max_gates={maxGates}"
            : $"  final_exact_complete={bestCover} source_gates={sourceGates} delta={bestCover - sourceGates}");
}

static void TryAdd(
    DagState state,
    DagNode left,
    DagNode right,
    ushort value,
    string expr,
    List<DagState> next,
    HashSet<string> seen,
    ushort[] targets,
    int sourceGates,
    int mergeCost) // targets/sourceGates/mergeCost reserved for future pruning
{
    if (state.Nodes.Any(node => node.Value == value))
    {
        return;
    }

    var nodes = new List<DagNode>(state.Nodes.Count + 1);
    nodes.AddRange(state.Nodes);
    nodes.Add(new DagNode(value, expr, state.GateCount + 1));
    int gateCount = state.GateCount + 1;
    // Fingerprint: sorted present values.
    string key = string.Join(',', nodes.Select(static node => node.Value).OrderBy(static value => value));
    if (!seen.Add(key))
    {
        return;
    }

    next.Add(new DagState(nodes, gateCount));
}


static int CoverCost(IReadOnlyList<DagNode> nodes, IReadOnlyList<ushort> targets)
{
    // Exact cover ranks 0. Otherwise guide the beam by Hamming distance to targets
    // and prefer states where a target is one gate away.
    int total = 0;
    foreach (ushort target in targets)
    {
        total += ResidualCost(target, nodes);
    }

    return total;
}

static int ResidualCost(ushort target, IReadOnlyList<DagNode> nodes)
{
    int minHamming = 16;
    for (int i = 0; i < nodes.Count; i++)
    {
        ushort value = nodes[i].Value;
        if (value == target)
        {
            return 0;
        }

        int hamming = BitOperations.PopCount((uint)(value ^ target));
        if (hamming < minHamming)
        {
            minHamming = hamming;
        }
    }

    for (int i = 0; i < nodes.Count; i++)
    {
        ushort left = nodes[i].Value;
        for (int j = 0; j < nodes.Count; j++)
        {
            ushort right = nodes[j].Value;
            if ((ushort)(left ^ right) == target
                || (ushort)(left & right) == target
                || (ushort)(left | right) == target
                || (ushort)(left & ~right) == target)
            {
                return 1;
            }
        }
    }

    // Scale Hamming into a soft residual so nearer functions rank first.
    return 10 + minHamming;
}

static string DescribeCover(IReadOnlyList<DagNode> nodes, IReadOnlyList<ushort> targets)
{
    return string.Join("; ", targets.Select(target => DescribeTarget(target, nodes)));
}

static string DescribeTarget(ushort target, IReadOnlyList<DagNode> nodes)
{
    foreach (DagNode node in nodes)
    {
        if (node.Value == target)
        {
            return node.Expr;
        }
    }

    for (int i = 0; i < nodes.Count; i++)
    {
        for (int j = 0; j < nodes.Count; j++)
        {
            ushort left = nodes[i].Value;
            ushort right = nodes[j].Value;
            string l = nodes[i].Expr;
            string r = nodes[j].Expr;
            if ((ushort)(left ^ right) == target)
            {
                return $"({l}^{r})";
            }

            if ((ushort)(left & right) == target)
            {
                return $"({l}&{r})";
            }

            if ((ushort)(left | right) == target)
            {
                return $"({l}|{r})";
            }

            if ((ushort)(left & ~right) == target)
            {
                return $"({l}&~{r})";
            }
        }
    }

    return $"?{target:X4}";
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

static string GetOutputNames(int sbox)
{
    return sbox switch
    {
        0 => "x[0],z[2]",
        1 => "x[1],z[3]",
        2 => "y[0],x[2]",
        3 => "y[1],x[3]",
        4 => "z[0],y[2]",
        5 => "z[1],y[3]",
        6 => "p,q",
        _ => throw new ArgumentOutOfRangeException(nameof(sbox)),
    };
}

static int GetOutputMergeCost(IReadOnlyList<ushort> cofactors)
{
    return GetMuxCost(cofactors[0], cofactors[1]) + GetMuxCost(cofactors[2], cofactors[3]);
}

static int GetMuxCost(ushort whenFeZero, ushort whenFeOne)
{
    ushort delta = (ushort)(whenFeZero ^ whenFeOne);
    return delta switch
    {
        0 => 0,
        All => 1,
        _ => 2,
    };
}

static int GetCurrentNetworkGateCount(int sbox)
{
    // Count the current portable C# boolean operations, including each output merge.
    // AdvSIMD may lower some muxes to a single BSL, so this is deliberately a
    // cross-platform source-level screen rather than an ISA-specific cycle model.
    return sbox switch
    {
        0 => 23,
        1 => 19,
        2 => 17,
        3 => 20,
        4 => 21,
        5 => 18,
        6 => 19,
        _ => throw new ArgumentOutOfRangeException(nameof(sbox)),
    };
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
sealed record XorBranch(int Cost, int LeftUses, int RightUses, string Description);
sealed record DagNode(ushort Value, string Expr, int IntroducedAtGate);
sealed record DagState(List<DagNode> Nodes, int GateCount);
