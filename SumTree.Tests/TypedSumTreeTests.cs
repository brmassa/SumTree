using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TextTree = SumTree.SumTree<char, SumTree.TextSummary, SumTree.TextSummaryOperations>;

namespace SumTree.Tests;

[TestClass]
public class TypedSumTreeTests
{
    [TestMethod]
    public void EmptyTreeUsesTheOperationsIdentityEvenWhenItIsNotDefault()
    {
        var empty = default(SumTree<int, Product, ProductOperations>);
        Assert.AreEqual(1L, empty.Summary.Value);
        Assert.AreEqual(0L, empty.Length);
        Assert.AreEqual(-1L, empty.FindPosition(s => s.Value >= 2));
        var tree = new SumTree<int, Product, ProductOperations>(new[] { 2, 3, 4 });
        Assert.AreEqual(24L, tree.Summary.Value);
        Assert.AreEqual(24L, (empty + tree + empty).Summary.Value);
        Assert.AreEqual(1L, tree.FindPosition(s => s.Value >= 6));
        Assert.AreEqual(1L, tree.Slice(1, 0).Summary.Value);
        Assert.AreEqual(1L, tree.GetRangeSummary(1, 0).Value);
        Assert.AreEqual(12L, tree.GetRangeSummary(1, 2).Value);
        Assert.AreEqual(6L, tree.GetPrefixSummary(2).Value);
    }

    [TestMethod]
    public void CrLfAcrossChunksSplitsAndEditsKeepsCompositeSummaryExact()
    {
        var source = new string('a', 1023) + "\r\n({[]})\rtext\n" + new string('b', 3000);
        var tree = new TextTree(source.AsMemory());
        AssertText(tree, source);
        var (left, right) = tree.SplitAt(1024);
        AssertText(left + right, source);
        AssertText(tree.InsertRange(1024, "x".AsMemory()), source.Insert(1024, "x"));
        AssertText(tree.RemoveAt(1024), source.Remove(1024, 1));
        Assert.AreEqual(1023L, tree.FindPosition(new LineTarget(1)));
        Assert.AreEqual(1023L, tree.FindPosition(s => s.Lines.Lines >= 1));
        Assert.AreEqual(-1L, tree.FindPosition(new LineTarget(100)));
    }

    [TestMethod]
    public void RandomizedEditsSlicesAndRetainedSnapshotsMatchReference()
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
            AssertText(tree, expected);
            var start = random.Next(text.Length + 1);
            var length = random.Next(Math.Min(300, text.Length - start) + 1);
            AssertText(tree.Slice(start, length), expected.Substring(start, length));
            if (text.Length > 0)
            {
                Assert.AreEqual(expected[start % text.Length], tree[start % text.Length]);
            }
        }

        foreach (var snapshot in snapshots)
        {
            AssertText(snapshot.Tree, snapshot.Text);
        }
    }

    [TestMethod]
    public void EnumerationAndCopyMatchIndexedContentAcrossManyLeaves()
    {
        var expected = string.Concat(Enumerable.Repeat("Call(items[0]);\r\n", 300));
        var tree = new TextTree(expected.AsMemory());
        CollectionAssert.AreEqual(expected.ToCharArray(), tree.ToArray());
        CollectionAssert.AreEqual(expected.ToCharArray(), tree.ToList().ToArray());
        var destination = new char[expected.Length + 5];
        tree.CopyTo(destination.AsSpan(5));
        Assert.AreEqual(expected, new string(destination, 5, expected.Length));
        AssertText(TextTree.Empty.Add('a').AddRange("\r\n".AsMemory()).Insert(1, '('), "a(\r\n");
    }

    [TestMethod]
    public void RepeatedJoinsAndTypingStayBoundedAndKeepEarlierSummaries()
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
        Assert.IsTrue(tree.Depth < 46);
        Assert.IsTrue(tree.BufferCount < 200, $"Typing left {tree.BufferCount} leaves");
        var original = string.Concat(Enumerable.Repeat(fragment, 2000));
        AssertText(held, original);
        AssertText(tree, original.Insert(17, new string('x', 2000)));
        Assert.AreEqual(2000, tree.Summary.Brackets.OpenParentheses);
    }

    [TestMethod]
    public void TypedSeekCombinesCachedSubtreesAndScansAtMostOneLeaf()
    {
        var owner = new CountingMemory(new string('a', 100_000));
        using (owner)
        {
            var tree = TextTree.FromBorrowedMemory(owner.Memory);
            owner.SpanRequests = 0;
            Assert.AreEqual(49_999L, tree.FindPosition(new CharacterTarget(50_000)));
            Assert.AreEqual(1, owner.SpanRequests, "A seek should access only the final leaf's memory");
            owner.SpanRequests = 0;
            long count = 0;
            foreach (var _ in tree)
            {
                count++;
            }

            Assert.AreEqual(tree.Length, count);
            Assert.AreEqual(tree.BufferCount, owner.SpanRequests, "Traversal obtains each leaf span once");
        }
    }

    [TestMethod]
    public void TypedOwnershipCopiesMutableInputsAndPreservesCheapSplits()
    {
        var input = new string('a', 100_000).ToCharArray();
        input[1023] = '\r'; input[1024] = '\n';
        var expected = new string(input);
        var tree = new TextTree(input);
        var insertion = "(\n)".ToCharArray();
        var edited = tree.InsertRange(1000, insertion);
        Array.Fill(input, 'z');
        Array.Fill(insertion, 'z');
        AssertText(tree, expected);
        AssertText(edited, expected.Insert(1000, "(\n)"));

        _ = new TextTree("abc".AsMemory()).SplitAt(1);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var split = tree.SplitAt(54_321);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.IsTrue(allocated < 30_000, $"Split allocated {allocated} bytes");
        AssertText(split.Left + split.Right, expected);
    }

    [TestMethod]
    public void InvalidRangesAndShortCopyDestinationsAreRejected()
    {
        var tree = new TextTree("abc".AsMemory());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tree.SplitAt(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tree.InsertRange(4, ReadOnlyMemory<char>.Empty));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tree.RemoveRange(1, long.MaxValue));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tree.Slice(0, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => tree.ElementAt(3));
        Assert.ThrowsExactly<ArgumentException>(() => tree.CopyTo(new char[2]));
        Assert.AreEqual("", TextTree.Empty.ToString());
    }

    [TestMethod]
    public async Task TypedPublishedVersionsRemainReadableDuringFurtherEdits()
    {
        var initial = new TextTree("first\r\n".AsMemory());
        var store = new SumTreeSnapshotStore<char, TextSummary, TextSummaryOperations>(initial);
        var held = store.Current;
        var reader = Task.Run(() =>
        {
            for (var i = 0; i < 200; i++)
            {
                var snapshot = store.Current;
                AssertText(snapshot, snapshot.ToString());
                AssertText(held, "first\r\n");
            }
        });
        var tree = initial;
        for (var i = 0; i < 200; i++)
        {
            store.Publish(tree = tree.AddRange("({[]})\r\n".AsMemory()));
        }

        await reader;
        Assert.AreEqual(201, store.Current.Summary.Lines.Lines);
        AssertText(held, "first\r\n");
    }

    [TestMethod]
    public void BuilderShouldBatchAddsAndMatchDirectConstruction()
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

        Assert.AreEqual(expected.Length, builder.Length);
        var tree = builder.ToTree();
        Assert.IsTrue(expected.SequenceEqual(tree));
        Assert.AreEqual(TextSummaryOperations.SummarizeSpan(expected), tree.Summary);
        Assert.AreEqual(expected.Length, builder.Length);
    }

    [TestMethod]
    public void BuilderShouldAppendToExistingTreeInOrder()
    {
        var initial = new TextTree("abc".AsMemory());
        var builder = new SumTreeBuilder<char, TextSummary, TextSummaryOperations>(initial);
        builder.Add('d');
        builder.AddRange("ef".AsMemory());
        builder.AddRange(new TextTree("gh".AsMemory()));
        builder.AddRange(new List<char> { 'i', 'j' });

        Assert.IsFalse(builder.IsEmpty);
        Assert.AreEqual(10, builder.Length);
        AssertText(builder.ToTree(), "abcdefghij");
    }

    [TestMethod]
    public void EmptyBuilderShouldProduceEmptyTree()
    {
        var builder = new SumTreeBuilder<char, TextSummary, TextSummaryOperations>();

        Assert.IsTrue(builder.IsEmpty);
        AssertText(builder.ToTree(), string.Empty);
    }

    [TestMethod]
    public void BuilderAddRangeShouldJoinTypedTreeFromEnumerable()
    {
        var builder = new SumTreeBuilder<char, TextSummary, TextSummaryOperations>();
        builder.AddRange(new List<char> { 'a', 'b', 'c' });
        IEnumerable<char> tail = new TextTree("def".AsMemory());
        builder.AddRange(tail);

        Assert.AreEqual(6, builder.Length);
        AssertText(builder.ToTree(), "abcdef");
    }

    [TestMethod]
    public void TypedTreeEqualityShouldCompareElementsNotStructure()
    {
        var a = new TextTree("hello world".AsMemory());
        var b = TextTree.Empty.Add('h').AddRange("ello world".AsMemory());

        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        Assert.IsFalse(a != b);
        Assert.IsTrue(a.Equals((object)b));

        var longer = new TextTree("hello world!".AsMemory());
        Assert.IsTrue(a != longer);
        Assert.IsFalse(a.Equals(longer));
        Assert.IsFalse(a.Equals("hello world"));

        var emptyA = TextTree.Empty;
        var emptyB = new TextTree(ReadOnlyMemory<char>.Empty);
        Assert.IsTrue(emptyA == emptyB);
        Assert.AreEqual(emptyA.GetHashCode(), emptyB.GetHashCode());
    }

    [TestMethod]
    public void EqualityShouldIgnoreLeafBoundaries()
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

        Assert.AreEqual(oneShot.Length, split.Length);
        Assert.IsTrue(oneShot.Equals(split));
        Assert.IsTrue(oneShot == split);
        Assert.IsFalse(oneShot != split);
        Assert.AreEqual(oneShot.GetHashCode(), split.GetHashCode());

        var modifiedContent = (char[])content.Clone();
        modifiedContent[750] = modifiedContent[750] == 'z' ? 'y' : 'z';
        var modified = new TextTree(modifiedContent.AsMemory());
        Assert.IsTrue(oneShot != modified);
    }

    static void AssertText(TextTree tree, string expected)
    {
        tree.ValidateStructure();
        Assert.AreEqual(expected, tree.ToString());
        Assert.AreEqual(expected.Length, tree.Length);
        Assert.AreEqual(TextSummaryOperations.SummarizeSpan(expected), tree.Summary);
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
