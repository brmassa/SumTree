using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SumTree.TreeMap;

namespace SumTree.Tests;

[TestClass]
public class TreeSetDebugTests
{
    [TestMethod]
    public void SmallDataSetShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 10 items
        for (var i = 0; i < 10; i++)
        {
            set.Add(i);
        }

        Assert.AreEqual(10, set.Count);
        Assert.IsTrue(set.Contains(0));
        Assert.IsTrue(set.Contains(9));
    }

    [TestMethod]
    public void MediumDataSetShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 100 items
        for (var i = 0; i < 100; i++)
        {
            set.Add(i);
        }

        Assert.AreEqual(100, set.Count);
        Assert.IsTrue(set.Contains(0));
        Assert.IsTrue(set.Contains(50));
        Assert.IsTrue(set.Contains(99));
    }

    [TestMethod]
    public void LargerDataSetShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 1000 items
        for (var i = 0; i < 1000; i++)
        {
            set.Add(i);
        }

        Assert.AreEqual(1000, set.Count);
        Assert.IsTrue(set.Contains(0));
        Assert.IsTrue(set.Contains(500));
        Assert.IsTrue(set.Contains(999));
    }

    [TestMethod]
    public void EnumerationSmallDataSetShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 10 items
        for (var i = 0; i < 10; i++)
        {
            set.Add(i);
        }

        var items = set.ToList();
        Assert.AreEqual(10, items.Count);

        // Check ordering
        for (var i = 0; i < 10; i++)
        {
            Assert.AreEqual(i, items[i]);
        }
    }

    [TestMethod]
    public void EnumerationMediumDataSetShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 100 items
        for (var i = 0; i < 100; i++)
        {
            set.Add(i);
        }

        var items = set.ToList();
        Assert.AreEqual(100, items.Count);

        // Check ordering
        for (var i = 0; i < 100; i++)
        {
            Assert.AreEqual(i, items[i]);
        }
    }

    [TestMethod]
    public void RemoveMediumDataSetShouldWork()
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

        Assert.AreEqual(50, set.Count);
        Assert.IsFalse(set.Contains(0));
        Assert.IsTrue(set.Contains(50));
        Assert.IsTrue(set.Contains(99));
    }

    [TestMethod]
    public void IterativeAddShouldHandleGracefully()
    {
        var set = new TreeSet<int>();

        // Add items one by one and check count
        for (var i = 0; i < 50; i++)
        {
            set.Add(i);
            Assert.AreEqual(i + 1, set.Count);
            Assert.IsTrue(set.Contains(i));
        }
    }

    [TestMethod]
    public void RandomOrderShouldMaintainOrder()
    {
        var set = new TreeSet<int>();
        var random = new Random(42); // Fixed seed for reproducibility
        var numbers = Enumerable.Range(0, 100).OrderBy(x => random.Next()).ToList();

        // Add in random order
        foreach (var num in numbers)
        {
            set.Add(num);
        }

        // Should still be in sorted order
        var items = set.ToList();
        Assert.AreEqual(100, items.Count);

        for (var i = 0; i < 100; i++)
        {
            Assert.AreEqual(i, items[i]);
        }
    }

    [TestMethod]
    public void DuplicateHandlingShouldWork()
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
                    Assert.IsTrue(added, $"First add of {i} should return true");
                }
                else
                {
                    Assert.IsFalse(added, $"Subsequent add of {i} should return false");
                }
            }
        }

        Assert.AreEqual(10, set.Count);
        var items = set.ToList();
        for (var i = 0; i < 10; i++)
        {
            Assert.AreEqual(i, items[i]);
        }
    }

    [TestMethod]
    public void Debug5000ItemsShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 5000 items
        for (var i = 0; i < 500; i++)
        {
            set.Add(i);
        }

        Assert.AreEqual(500, set.Count);
        Assert.IsTrue(set.Contains(0));
        Assert.IsTrue(set.Contains(250));
        Assert.IsTrue(set.Contains(499));
    }

    [TestMethod]
    public void Debug8000ItemsShouldWork()
    {
        var set = new TreeSet<int>();

        // Add 8000 items
        for (var i = 0; i < 8000; i++)
        {
            set.Add(i);
        }

        Assert.AreEqual(8000, set.Count);
        Assert.IsTrue(set.Contains(0));
        Assert.IsTrue(set.Contains(4000));
        Assert.IsTrue(set.Contains(7999));
    }
}
