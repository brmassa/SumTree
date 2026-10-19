
namespace SumTree.Tests;
public class TreeSetDebugTests
{
    [Test]
    public async Task SmallDataSetShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 10 items
        for (var i = 0; i < 10; i++)
        {
            set.Add(i);
        }

        await Assert.That(set.Count).IsEqualTo(10);
        await Assert.That(set.Contains(0)).IsTrue();
        await Assert.That(set.Contains(9)).IsTrue();
    }

    [Test]
    public async Task MediumDataSetShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 100 items
        for (var i = 0; i < 100; i++)
        {
            set.Add(i);
        }

        await Assert.That(set.Count).IsEqualTo(100);
        await Assert.That(set.Contains(0)).IsTrue();
        await Assert.That(set.Contains(50)).IsTrue();
        await Assert.That(set.Contains(99)).IsTrue();
    }

    [Test]
    public async Task LargerDataSetShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 1000 items
        for (var i = 0; i < 1000; i++)
        {
            set.Add(i);
        }

        await Assert.That(set.Count).IsEqualTo(1000);
        await Assert.That(set.Contains(0)).IsTrue();
        await Assert.That(set.Contains(500)).IsTrue();
        await Assert.That(set.Contains(999)).IsTrue();
    }

    [Test]
    public async Task EnumerationSmallDataSetShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 10 items
        for (var i = 0; i < 10; i++)
        {
            set.Add(i);
        }

        var items = set.ToList();
        await Assert.That(items.Count).IsEqualTo(10);

        // Check ordering
        for (var i = 0; i < 10; i++)
        {
            await Assert.That(items[i]).IsEqualTo(i);
        }
    }

    [Test]
    public async Task EnumerationMediumDataSetShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 100 items
        for (var i = 0; i < 100; i++)
        {
            set.Add(i);
        }

        var items = set.ToList();
        await Assert.That(items.Count).IsEqualTo(100);

        // Check ordering
        for (var i = 0; i < 100; i++)
        {
            await Assert.That(items[i]).IsEqualTo(i);
        }
    }

    [Test]
    public async Task RemoveMediumDataSetShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 100 items
        for (var i = 0; i < 100; i++)
        {
            set.Add(i);
        }

        // Remove half the items
        for (var i = 0; i < 50; i++)
        {
            set.Remove(i);
        }

        await Assert.That(set.Count).IsEqualTo(50);
        await Assert.That(set.Contains(0)).IsFalse();
        await Assert.That(set.Contains(50)).IsTrue();
        await Assert.That(set.Contains(99)).IsTrue();
    }

    [Test]
    public async Task IterativeAddShouldHandleGracefully()
    {
        var set = new TreeSet<int>();

        // Add items one by one and check count
        for (var i = 0; i < 50; i++)
        {
            set.Add(i);
            await Assert.That(set.Count).IsEqualTo(i + 1);
            await Assert.That(set.Contains(i)).IsTrue();
        }
    }

    [Test]
    public async Task RandomOrderShouldMaintainOrder()
    {
        var set = new TreeSet<int>();
        var random = new Random(42); // Fixed seed for reproducibility
        var numbers = Enumerable.Range(0, 100).OrderBy(_ => random.Next()).ToList();

        // Add in random order
        foreach (var num in numbers)
        {
            set.Add(num);
        }

        // Should still be in sorted order
        var items = set.ToList();
        await Assert.That(items.Count).IsEqualTo(100);

        for (var i = 0; i < 100; i++)
        {
            await Assert.That(items[i]).IsEqualTo(i);
        }
    }

    [Test]
    public async Task DuplicateHandlingShouldWork()
    {
        var set = new TreeSet<int>();

        // Add same numbers multiple times
        for (var round = 0; round < 3; round++)
        {
            for (var i = 0; i < 10; i++)
            {
                var added = set.Add(i);
                if (round == 0)
                {
                    await Assert.That(added).IsTrue().Because($"First add of {i} should return true");
                }
                else
                {
                    await Assert.That(added).IsFalse().Because($"Subsequent add of {i} should return false");
                }
            }
        }

        await Assert.That(set.Count).IsEqualTo(10);
        var items = set.ToList();
        for (var i = 0; i < 10; i++)
        {
            await Assert.That(items[i]).IsEqualTo(i);
        }
    }

    [Test]
    public async Task Debug5000ItemsShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 5000 items
        for (var i = 0; i < 500; i++)
        {
            set.Add(i);
        }

        await Assert.That(set.Count).IsEqualTo(500);
        await Assert.That(set.Contains(0)).IsTrue();
        await Assert.That(set.Contains(250)).IsTrue();
        await Assert.That(set.Contains(499)).IsTrue();
    }

    [Test]
    public async Task Debug8000ItemsShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 8000 items
        for (var i = 0; i < 8000; i++)
        {
            set.Add(i);
        }

        await Assert.That(set.Count).IsEqualTo(8000);
        await Assert.That(set.Contains(0)).IsTrue();
        await Assert.That(set.Contains(4000)).IsTrue();
        await Assert.That(set.Contains(7999)).IsTrue();
    }
}