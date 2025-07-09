using Microsoft.VisualStudio.TestTools.UnitTesting;
using SumTree.Cursors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SumTree.Tests;

[TestClass]
public class CursorDebugTests
{
    [TestMethod]
    public void Debug_EmptyTree_CursorState()
    {
        var tree = SumTree<char>.Empty.WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        Console.WriteLine($"Tree.IsEmpty: {tree.IsEmpty}");
        Console.WriteLine($"Tree.Length: {tree.Length}");
        Console.WriteLine($"Cursor.IsAtEnd: {cursor.IsAtEnd}");
        Console.WriteLine($"Cursor.IsAtStart: {cursor.IsAtStart}");
        Console.WriteLine($"Cursor.Item: '{cursor.Item}'");
        Console.WriteLine($"Cursor.Item is null: {cursor.Item is '\0'}");
        Console.WriteLine($"Cursor.Item type: {cursor.Item.GetType()}");

        // Let's also check what happens after Start()
        cursor.Start();
        Console.WriteLine($"After Start() - IsAtEnd: {cursor.IsAtEnd}");
        Console.WriteLine($"After Start() - IsAtStart: {cursor.IsAtStart}");
        Console.WriteLine($"After Start() - Item: '{cursor.Item}'");
        Console.WriteLine($"After Start() - Item is null: {cursor.Item is '\0'}");
    }

    [TestMethod]
    public void Debug_SingleChar_CursorState()
    {
        var tree = new SumTree<char>('a').WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        Console.WriteLine($"Tree.IsEmpty: {tree.IsEmpty}");
        Console.WriteLine($"Tree.Length: {tree.Length}");
        Console.WriteLine($"Cursor.IsAtEnd: {cursor.IsAtEnd}");
        Console.WriteLine($"Cursor.IsAtStart: {cursor.IsAtStart}");
        Console.WriteLine($"Cursor.Item: '{cursor.Item}'");
        Console.WriteLine($"Cursor.Item is null: {cursor.Item is '\0'}");

        cursor.Start();
        Console.WriteLine($"After Start() - IsAtEnd: {cursor.IsAtEnd}");
        Console.WriteLine($"After Start() - IsAtStart: {cursor.IsAtStart}");
        Console.WriteLine($"After Start() - Item: '{cursor.Item}'");
        Console.WriteLine($"After Start() - Item is null: {cursor.Item is '\0'}");

        var moved = cursor.Next();
        Console.WriteLine($"After Next() - moved: {moved}");
        Console.WriteLine($"After Next() - IsAtEnd: {cursor.IsAtEnd}");
        Console.WriteLine($"After Next() - Item: '{cursor.Item}'");
        Console.WriteLine($"After Next() - Item is null: {cursor.Item is '\0'}");
    }

    [TestMethod]
    public void Debug_String_CursorState()
    {
        var tree = "hello".ToSumTree().WithDimension(LineNumberDimension.Instance);
        using var cursor = tree.LineCursor();

        Console.WriteLine($"Tree.IsEmpty: {tree.IsEmpty}");
        Console.WriteLine($"Tree.Length: {tree.Length}");
        Console.WriteLine($"Initial - IsAtEnd: {cursor.IsAtEnd}");
        Console.WriteLine($"Initial - IsAtStart: {cursor.IsAtStart}");
        Console.WriteLine($"Initial - Item: '{cursor.Item}'");

        cursor.Start();
        Console.WriteLine($"After Start() - IsAtEnd: {cursor.IsAtEnd}");
        Console.WriteLine($"After Start() - IsAtStart: {cursor.IsAtStart}");
        Console.WriteLine($"After Start() - Item: '{cursor.Item}'");

        var chars = new List<char>();
        while (!cursor.IsAtEnd)
        {
            chars.Add(cursor.Item);
            var moved = cursor.Next();
            Console.WriteLine($"After Next() - moved: {moved}, IsAtEnd: {cursor.IsAtEnd}, Item: '{cursor.Item}'");
        }

        Console.WriteLine($"Final chars: {string.Join("", chars)}");
    }
}
