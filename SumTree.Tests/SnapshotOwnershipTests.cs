using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SumTree.Tests;

[TestClass]
public class SnapshotOwnershipTests
{
    [TestMethod]
    [DataRow(8)]
    [DataRow(4096)]
    public void MutableInputCannotChangeSnapshotOrItsSummaries(int length)
    {
        var input = new string('a', length).ToCharArray();
        input[1] = '\n';
        var expected = new string(input);
        var dimensions = new Dictionary<Type, object> { [typeof(LineNumberSummary)] = LineNumberDimension.Instance };
        var tree = new SumTree<char>(input.AsMemory(), dimensions);
        var fast = input.ToFastSumTree();
        var fromArray = input.ToSumTree();
        dimensions.Clear();
        Array.Fill(input, 'z');

        Assert.AreEqual(expected, tree.ToString());
        Assert.AreEqual(expected, fromArray.ToString());
        Assert.AreEqual(expected, fast.ToStringFast());
        Assert.AreEqual(1, tree.GetSummary<LineNumberSummary>().Lines);
        Assert.AreEqual(1L, tree.FindPosition<LineNumberSummary>(s => s.Lines >= 1));
        var (left, right) = tree.SplitAt(length / 2);
        Assert.AreEqual(expected, (left + right).ToString());
    }

    [TestMethod]
    public void InsertAppendAndSpanConversionsCopyCallerStorage()
    {
        var input = "x\ny".ToCharArray();
        var original = "a\nb".ToSumTreeWithLines();
        var inserted = original.InsertRange(1, input.AsMemory());
        var appended = original.AddRange(input);
        var fromSpan = ((ReadOnlySpan<char>)input).ToSumTree();
        var fastAppended = "a\nb".ToFastSumTree().AddRange(input);
        input[1] = '!';

        Assert.AreEqual("ax\ny\nb", inserted.ToString());
        Assert.AreEqual("a\nbx\ny", appended.ToString());
        Assert.AreEqual("x\ny", fromSpan.ToString());
        Assert.AreEqual("a\nbx\ny", fastAppended.ToStringFast());
        Assert.AreEqual(2, inserted.GetSummary<LineNumberSummary>().Lines);
        Assert.AreEqual(2, appended.GetSummary<LineNumberSummary>().Lines);
        Assert.AreEqual("a\nb", original.ToString());
    }

    [TestMethod]
    public void ConstructorsCopyMemorySlicesBeforeOwnerIsDisposed()
    {
        using var owner = new TestMemory("prefix:a\nb:suffix");
        var memory = owner.Memory.Slice(7, 3);
        var tree = new SumTree<char>(memory);
        var fast = new FastSumTree<char>(memory);
        owner.Release();

        Assert.AreEqual("a\nb", tree.ToString());
        Assert.AreEqual("a\nb", fast.ToStringFast());
    }

    [TestMethod]
    public void BorrowedFactoriesShareValidMemoryAndCopyDimensionDictionary()
    {
        using var owner = new TestMemory("a\nb");
        var dimensions = new Dictionary<Type, object> { [typeof(LineNumberSummary)] = LineNumberDimension.Instance };
        var tree = SumTree<char>.FromBorrowedMemory(owner.Memory, dimensions);
        var fast = FastSumTree<char>.FromBorrowedMemory(owner.Memory);
        dimensions.Clear();
        owner.SpanRequests = 0;

        Assert.AreEqual("a\nb", tree.ToString());
        Assert.AreEqual("a\nb", fast.ToStringFast());
        Assert.IsTrue(owner.SpanRequests >= 2, "Borrowed trees should read their retained memory owner");
        Assert.AreEqual(1, tree.GetSummary<LineNumberSummary>().Lines);
    }

    [TestMethod]
    public void ReadOnlyListConversionDoesNotRetainReturnedPoolStorage()
    {
        var input = Enumerable.Range(0, 73).ToArray();
        IReadOnlyList<int> list = new ReadOnlyCollection<int>(input);
        var tree = ((IEnumerable<int>)list).ToSumTree();
        Array.Fill(input, -1);
        var rented = ArrayPool<int>.Shared.Rent(73);
        try
        {
            Array.Fill(rented, -2);
            CollectionAssert.AreEqual(Enumerable.Range(0, 73).ToArray(), tree.ToArray());
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rented);
        }
    }

    [TestMethod]
    public void SplittingLargeOwnedBuffersDoesNotCopyTheirText()
    {
        var input = new string('a', 1_000_000).ToCharArray();
        var tree = input.ToSumTree();
        var fast = input.ToFastSumTree();
        // Warm up split code independently of the measured large document.
        _ = "abc".ToSumTree().SplitAt(1);
        _ = "abc".ToFastSumTree().SplitAt(1);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var parts = tree.SplitAt(456_789);
        var fastParts = fast.SplitAt(456_789);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.IsTrue(allocated < 100_000, $"Splits allocated {allocated} bytes; backing buffers should be shared");
        Assert.AreEqual(456_789L, parts.Left.Length);
        Assert.AreEqual(543_211L, parts.Right.Length);
        Assert.AreEqual(parts.Left.Length, fastParts.Left.Length);
        Assert.AreEqual(parts.Right.Length, fastParts.Right.Length);
    }

    [TestMethod]
    public async Task PublishedSnapshotsStayCompleteWhileOldVersionsRemainReadable()
    {
        var original = "x\n".ToSumTreeWithLines();
        var store = new SumTreeSnapshotStore<char>(original);
        using var ready = new CountdownEvent(3);
        using var finished = new ManualResetEventSlim();
        var readers = Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
        {
            var retained = store.Current;
            ready.Signal();
            do
            {
                var snapshot = store.Current;
                var text = snapshot.ToString();
                Assert.AreEqual(snapshot.Length, (long)text.Length);
                Assert.AreEqual(text.Length / 2, snapshot.GetSummary<LineNumberSummary>().Lines);
                for (var i = 0; i < text.Length; i++)
                {
                    Assert.AreEqual(i % 2 == 0 ? 'x' : '\n', text[i]);
                }
            } while (!finished.IsSet);

            Assert.AreEqual("x\n", retained.ToString());
            Assert.AreEqual(1, retained.GetSummary<LineNumberSummary>().Lines);
        })).ToArray();

        try
        {
            Assert.IsTrue(ready.Wait(TimeSpan.FromSeconds(10)), "Readers did not start");
            var tree = original;
            for (var edit = 0; edit < 200; edit++)
            {
                tree = tree.AddRange("x\n".AsMemory());
                store.Publish(tree);
            }
        }
        finally
        {
            finished.Set();
        }

        await Task.WhenAll(readers);
        Assert.AreEqual(402L, store.Current.Length);
        Assert.AreEqual(201, store.Current.GetSummary<LineNumberSummary>().Lines);
        Assert.AreEqual("x\n", original.ToString());
    }

    sealed class TestMemory(string text) : MemoryManager<char>
    {
        readonly char[] _buffer = text.ToCharArray();
        bool _disposed;
        public int SpanRequests { get; set; }
        public override Span<char> GetSpan()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SpanRequests++;
            return _buffer;
        }

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) => _disposed = true;
        public void Release() => ((IDisposable)this).Dispose();
    }
}
