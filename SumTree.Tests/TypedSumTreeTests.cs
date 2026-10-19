using TextTree = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

namespace SumTree.Tests;
public class TypedSumTreeTests
{
    [Test]
    public async Task EmptyTreeUsesTheOperationsIdentityEvenWhenItIsNotDefault()
    {
        var empty = default(SumTree<int, Product, ProductOperations>);
        await Assert.That(empty.Summary.Value).IsEqualTo(1L);
        await Assert.That(empty.Length).IsEqualTo(0L);
        await Assert.That(empty.FindPosition(s => s.Value >= 2)).IsEqualTo(-1L);
        var tree = new SumTree<int, Product, ProductOperations>(new[] { 2, 3, 4 });
        await Assert.That(tree.Summary.Value).IsEqualTo(24L);
        await Assert.That((empty + tree + empty).Summary.Value).IsEqualTo(24L);
        await Assert.That(tree.FindPosition(s => s.Value >= 6)).IsEqualTo(1L);
        await Assert.That(tree.Slice(1, 0).Summary.Value).IsEqualTo(1L);
        await Assert.That(tree.GetRangeSummary(1, 0).Value).IsEqualTo(1L);
        await Assert.That(tree.GetRangeSummary(1, 2).Value).IsEqualTo(12L);
        await Assert.That(tree.GetPrefixSummary(2).Value).IsEqualTo(6L);
    }

    [Test]
    public async Task CrLfAcrossChunksSplitsAndEditsKeepsCompositeSummaryExact()
    {
        var source = new string('a', 1023) + "\r\n({[]})\rtext\n" + new string('b', 3000);
        var tree = new TextTree(source.AsMemory());
        await AssertText(tree, source);
        var (left, right) = tree.SplitAt(1024);
        await AssertText(left + right, source);
        await AssertText(tree.InsertRange(1024, "x".AsMemory()), source.Insert(1024, "x"));
        await AssertText(tree.RemoveAt(1024), source.Remove(1024, 1));
        await Assert.That(tree.FindPosition(new LineTarget(1))).IsEqualTo(1023L);
        await Assert.That(tree.FindPosition(s => s.Lines.Lines >= 1)).IsEqualTo(1023L);
        await Assert.That(tree.FindPosition(new LineTarget(100))).IsEqualTo(-1L);
    }

    [Test]
    public async Task RandomizedEditsSlicesAndRetainedSnapshotsMatchReference()
    {
        var text = new StringBuilder(string.Concat(Enumerable.Repeat("Call(items[0]);\r\n", 300)));
        var tree = new TextTree(text.ToString().AsMemory());
        var snapshots = new List<(TextTree Tree, string Text)>();
        var random = new Random(317);
        var fragments = new[] { "x", "\r", "\n", "\r\n", "([])", new string('z', 1200), "" };
        for (var edit = 0; edit < 500; edit++)
        {
            if (edit % 37 == 0)
            {
                snapshots.Add((tree, text.ToString()));
            }

            var position = random.Next(text.Length + 1);
            if (edit % 3 == 0 && position < text.Length)
            {
                var count = random.Next(1, Math.Min(100, text.Length - position) + 1);
                text.Remove(position, count);
                tree = tree.RemoveRange(position, count);
            }
            else
            {
                var insert = fragments[random.Next(fragments.Length)];
                text.Insert(position, insert);
                tree = edit % 2 == 0 ? tree.InsertRange(position, insert.AsMemory()) :
                    tree.InsertRange(position, new TextTree(insert.AsMemory()));
            }

            var expected = text.ToString();
            await AssertText(tree, expected);
            var start = random.Next(text.Length + 1);
            var length = random.Next(Math.Min(300, text.Length - start) + 1);
            await AssertText(tree.Slice(start, length), expected.Substring(start, length));
            if (text.Length > 0)
            {
                await Assert.That(tree[start % text.Length]).IsEqualTo(expected[start % text.Length]);
            }
        }

        foreach (var snapshot in snapshots)
        {
            await AssertText(snapshot.Tree, snapshot.Text);
        }
    }

    [Test]
    public async Task EnumerationAndCopyMatchIndexedContentAcrossManyLeaves()
    {
        var expected = string.Concat(Enumerable.Repeat("Call(items[0]);\r\n", 300));
        var tree = new TextTree(expected.AsMemory());
        await Assert.That(tree.ToArray()).IsEquivalentTo(expected.ToCharArray());
        await Assert.That(tree.ToList().ToArray()).IsEquivalentTo(expected.ToCharArray());
        var destination = new char[expected.Length + 5];
        tree.CopyTo(destination.AsSpan(5));
        await Assert.That(new string(destination, 5, expected.Length)).IsEqualTo(expected);
        await AssertText(TextTree.Empty.Add('a').AddRange("\r\n".AsMemory()).Insert(1, '('), "a(\r\n");
    }

    [Test]
    public async Task RepeatedJoinsAndTypingStayBoundedAndKeepEarlierSummaries()
    {
        var fragment = "Call(items[0]);\r\n";
        var prepared = new TextTree(fragment.AsMemory());
        var tree = TextTree.Empty;
        for (var i = 0; i < 2000; i++)
        {
            tree += prepared;
        }
        var held = tree;
        for (var i = 0; i < 2000; i++)
        {
            tree = tree.Insert(17 + i, 'x');
        }
        await Assert.That(tree.Depth < 46).IsTrue();
        await Assert.That(tree.BufferCount < 200).IsTrue().Because($"Typing left {tree.BufferCount} leaves");
        var original = string.Concat(Enumerable.Repeat(fragment, 2000));
        await AssertText(held, original);
        await AssertText(tree, original.Insert(17, new string('x', 2000)));
        await Assert.That(tree.Summary.Brackets.OpenParentheses).IsEqualTo(2000);
    }

    [Test]
    public async Task TypedSeekCombinesCachedSubtreesAndScansAtMostOneLeaf()
    {
        var owner = new CountingMemory(new string('a', 100_000));
        using (owner)
        {
            var tree = TextTree.FromBorrowedMemory(owner.Memory);
            owner.SpanRequests = 0;
            await Assert.That(tree.FindPosition(new CharacterTarget(50_000))).IsEqualTo(49_999L);
            await Assert.That(owner.SpanRequests).IsEqualTo(1).Because("A seek should access only the final leaf's memory");
            owner.SpanRequests = 0;
            long count = 0;
            foreach (var _ in tree)
            {
                count++;
            }

            await Assert.That(count).IsEqualTo(tree.Length);
            await Assert.That(owner.SpanRequests).IsEqualTo(tree.BufferCount).Because("Traversal obtains each leaf span once");
        }
    }

    [Test]
    public async Task TypedOwnershipCopiesMutableInputsAndPreservesCheapSplits()
    {
        var input = new string('a', 100_000).ToCharArray();
        input[1023] = '\r'; input[1024] = '\n';
        var expected = new string(input);
        var tree = new TextTree(input);
        var insertion = "(\n)".ToCharArray();
        var edited = tree.InsertRange(1000, insertion);
        Array.Fill(input, 'z');
        Array.Fill(insertion, 'z');
        await AssertText(tree, expected);
        await AssertText(edited, expected.Insert(1000, "(\n)"));

        _ = new TextTree("abc".AsMemory()).SplitAt(1);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var split = tree.SplitAt(54_321);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(allocated < 30_000).IsTrue().Because($"Split allocated {allocated} bytes");
        await AssertText(split.Left + split.Right, expected);
    }

    [Test]
    public async Task InvalidRangesAndShortCopyDestinationsAreRejected()
    {
        var tree = new TextTree("abc".AsMemory());
        await Assert.That(() => tree.SplitAt(-1)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => tree.InsertRange(4, ReadOnlyMemory<char>.Empty)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => tree.RemoveRange(1, long.MaxValue)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => tree.Slice(0, -1)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => tree.ElementAt(3)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => tree.CopyTo(new char[2])).ThrowsExactly<ArgumentException>();
        await Assert.That(TextTree.Empty.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task TypedPublishedVersionsRemainReadableDuringFurtherEdits()
    {
        var initial = new TextTree("first\r\n".AsMemory());
        var store = new SumTreeSnapshotStore<char, TextSummary, TextSummaryOperations>(initial);
        var held = store.Current;
        var reader = Task.Run(async () =>
        {
            for (var i = 0; i < 200; i++)
            {
                var snapshot = store.Current;
                await AssertText(snapshot, snapshot.ToString());
                await AssertText(held, "first\r\n");
            }
        });
        var tree = initial;
        for (var i = 0; i < 200; i++)
        {
            store.Publish(tree = tree.AddRange("({[]})\r\n".AsMemory()));
        }

        await reader;
        await Assert.That(store.Current.Summary.Lines.Lines).IsEqualTo(201);
        await AssertText(held, "first\r\n");
    }

    [Test]
    public async Task BuilderShouldBatchAddsAndMatchDirectConstruction()
    {
        var expected = new char[5000];
        for (var i = 0; i < expected.Length; i++)
        {
            expected[i] = (char)('a' + i % 26);
        }

        var builder = new SumTreeBuilder<char, TextSummary, TextSummaryOperations>();
        builder.AddRange(expected.AsSpan(0, 3));
        var index = 3;
        while (index < expected.Length)
        {
            builder.Add(expected[index++]);
            if (index % 700 == 0 && index < expected.Length)
            {
                var take = Math.Min(13, expected.Length - index);
                builder.AddRange(expected.AsSpan(index, take));
                index += take;
            }
        }

        await Assert.That(builder.Length).IsEqualTo(expected.Length);
        var tree = builder.ToTree();
        await Assert.That(expected.SequenceEqual(tree)).IsTrue();
        await Assert.That(tree.Summary).IsEqualTo(TextSummaryOperations.SummarizeSpan(expected));
        await Assert.That(builder.Length).IsEqualTo(expected.Length);
    }

    [Test]
    public async Task BuilderShouldAppendToExistingTreeInOrder()
    {
        var initial = new TextTree("abc".AsMemory());
        var builder = new SumTreeBuilder<char, TextSummary, TextSummaryOperations>(initial);
        builder.Add('d');
        builder.AddRange("ef".AsMemory());
        builder.AddRange(new TextTree("gh".AsMemory()));
        builder.AddRange(new List<char> { 'i', 'j' });

        await Assert.That(builder.IsEmpty).IsFalse();
        await Assert.That(builder.Length).IsEqualTo(10);
        await AssertText(builder.ToTree(), "abcdefghij");
    }

    [Test]
    public async Task EmptyBuilderShouldProduceEmptyTree()
    {
        var builder = new SumTreeBuilder<char, TextSummary, TextSummaryOperations>();

        await Assert.That(builder.IsEmpty).IsTrue();
        await AssertText(builder.ToTree(), string.Empty);
    }

    [Test]
    public async Task BuilderAddRangeShouldJoinTypedTreeFromEnumerable()
    {
        var builder = new SumTreeBuilder<char, TextSummary, TextSummaryOperations>();
        builder.AddRange(new List<char> { 'a', 'b', 'c' });
        IEnumerable<char> tail = new TextTree("def".AsMemory());
        builder.AddRange(tail);

        await Assert.That(builder.Length).IsEqualTo(6);
        await AssertText(builder.ToTree(), "abcdef");
    }

    [Test]
    public async Task TypedTreeEqualityShouldCompareElementsNotStructure()
    {
        var a = new TextTree("hello world".AsMemory());
        var b = TextTree.Empty.Add('h').AddRange("ello world".AsMemory());

        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a == b).IsTrue();
        await Assert.That(b.GetHashCode()).IsEqualTo(a.GetHashCode());
        await Assert.That(a != b).IsFalse();
        await Assert.That(a.Equals((object)b)).IsTrue();

        var longer = new TextTree("hello world!".AsMemory());
        await Assert.That(a != longer).IsTrue();
        await Assert.That(a.Equals(longer)).IsFalse();
        await Assert.That(a.Equals("hello world")).IsFalse();

        var emptyA = TextTree.Empty;
        var emptyB = new TextTree(ReadOnlyMemory<char>.Empty);
        await Assert.That(emptyA == emptyB).IsTrue();
        await Assert.That(emptyB.GetHashCode()).IsEqualTo(emptyA.GetHashCode());
    }

    [Test]
    public async Task EqualityShouldIgnoreLeafBoundaries()
    {
        var content = new char[1500];
        for (var i = 0; i < content.Length; i++)
        {
            content[i] = (char)('a' + i % 26);
        }

        // One shot: leaves 1024 + 476. Split: leaves 600 + 900.
        var oneShot = new TextTree(content.AsMemory());
        var split = new TextTree(content.AsSpan(0, 600).ToArray())
            .AddRange(content.AsSpan(600).ToArray());

        await Assert.That(split.Length).IsEqualTo(oneShot.Length);
        await Assert.That(oneShot.Equals(split)).IsTrue();
        await Assert.That(oneShot == split).IsTrue();
        await Assert.That(oneShot != split).IsFalse();
        await Assert.That(split.GetHashCode()).IsEqualTo(oneShot.GetHashCode());

        var modifiedContent = (char[])content.Clone();
        modifiedContent[750] = modifiedContent[750] == 'z' ? 'y' : 'z';
        var modified = new TextTree(modifiedContent.AsMemory());
        await Assert.That(oneShot != modified).IsTrue();
    }

    static async Task AssertText(TextTree tree, string expected)
    {
        tree.ValidateStructure();
        await Assert.That(tree.ToString()).IsEqualTo(expected);
        await Assert.That(tree.Length).IsEqualTo(expected.Length);
        await Assert.That(tree.Summary).IsEqualTo(TextSummaryOperations.SummarizeSpan(expected));
    }

    readonly struct LineTarget(int lines) : ISummarySeek<TextSummary>
    {
        public bool IsReached(in TextSummary summary) => summary.Lines.Lines >= lines;
    }

    readonly struct CharacterTarget(int characters) : ISummarySeek<TextSummary>
    {
        public bool IsReached(in TextSummary summary) => summary.Lines.TotalCharacters >= characters;
    }

    readonly record struct Product(long Value);
    readonly struct ProductOperations : ISummaryOperations<int, Product>
    {
        public static Product Identity => new(1);
        public static Product SummarizeElement(int element) => new(element);
        public static Product SummarizeSpan(ReadOnlySpan<int> elements)
        {
            var value = 1L;
            foreach (var element in elements)
            {
                value *= element;
            }

            return new(value);
        }
        public static Product Combine(in Product left, in Product right) => new(left.Value * right.Value);
    }

    sealed class CountingMemory(string text) : MemoryManager<char>
    {
        readonly char[] _text = text.ToCharArray();
        public int SpanRequests { get; set; }
        public override Span<char> GetSpan() { SpanRequests++; return _text; }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) { }
    }
}