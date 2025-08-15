# SumTree

![logo](logo.png)

[![Build Status](https://github.com/brmassa/SumTree/actions/workflows/continuous-integration.yml/badge.svg)](https://github.com/brmassa/SumTree/actions)
[![License](https://img.shields.io/github/license/brmassa/SumTree.svg)](https://github.com/brmassa/SumTree/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/com.BrunoMassa.SumTree.svg)](https://www.nuget.org/packages/BrunoMassa.SumTree)
[![downloads](https://img.shields.io/nuget/dt/com.BrunoMassa.SumTree)](https://www.nuget.org/packages/BrunoMassa.SumTree)
[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/C0C0AE5SC)

## What is this?

SumTree is a high-performance, immutable data structure that combines the efficiency of Rope with powerful summary dimensions. It's designed for applications that need fast text editing, querying, and manipulation with computed summaries like line numbers, bracket counting, and custom metrics.

## Why use this?

* **Performance**: Get the benefits of functional programming without the overhead of copying data on edits
* **Rich Querying**: Built-in support for line/column tracking, bracket matching, and custom summary dimensions
* **Memory Efficient**: Structural sharing means edits don't copy entire data structures
* **Thread Safe**: Immutable by design, safe for concurrent access
* **Versatile**: Works with any type `T`, not just text

**Replace these types with better performance:**

* `string` (for text editing scenarios)
* `T[]` (for immutable arrays with fast edits)
* `List<T>` (for functional-style list operations)
* `ImmutableList<T>` (with much better performance)
* `StringBuilder` (for complex text construction)

## How does it work?

SumTree is a C# implementation that directly integrates Rope structure with summary dimensions. Based on the paper [Ropes: an Alternative to Strings](https://www.cs.rit.edu/usr/local/pub/jeh/courses/QUARTERS/FP/Labs/CedarRope/rope-paper.pdf), but enhanced with:

* **Summary Dimensions**: Efficiently track computed properties (line numbers, bracket counts, custom metrics)
* **Zero-Copy Operations**: Structural sharing means edits create new views without copying data
* **Cache-Friendly**: Arrays of elements with subdivision only on edits for better CPU cache performance
* **Automatic Balancing**: Tree rebalances using Fibonacci heuristics for optimal performance

## How do I use it?

```powershell
dotnet add package BrunoMassa.SumTree
```

### Basic Usage

```csharp
using SumTree;

// Create a SumTree from text - no memory allocation
SumTree<char> text = "Hello, World!".ToSumTree();

// Or create from any array/collection
SumTree<int> numbers = new[] { 1, 2, 3, 4, 5 }.ToSumTree();
SumTree<string> words = new[] { "Hello", "World" }.ToSumTree();

// String-like operations for SumTree<char>
Console.WriteLine(text.ToString()); // "Hello, World!"
Console.WriteLine(text.Length);     // 13

// Efficient concatenation (no copying)
SumTree<char> combined = text + " How are you?".ToSumTree();
Console.WriteLine(combined.ToString()); // "Hello, World! How are you?"
```

### Text Editing with Line Tracking

```csharp
// Create text with line number tracking
SumTree<char> document = @"Line 1
Line 2
Line 3".ToSumTreeWithLines();

// Get line/column information
var (line, column) = document.GetLineAndColumn(10); // line 2, column 4
Console.WriteLine($"Position 10 is at line {line}, column {column}");

// Find start of a specific line
long lineStart = document.FindLineStart(2); // Position 7
Console.WriteLine($"Line 2 starts at position {lineStart}");

// Get content of a specific line
string lineContent = document.GetLineContent(1); // "Line 1"
Console.WriteLine($"Line 1 content: {lineContent}");

// Insert text at specific line/column
SumTree<char> modified = document.InsertAtLineColumn(2, 1, "Modified ");
Console.WriteLine(modified.ToString());
// Output:
// Line 1
// Modified Line 2
// Line 3
```

### Bracket Matching and Code Analysis

```csharp
// Track both lines and bracket counts
SumTree<char> code = @"function example() {
    if (condition) {
        return [1, 2, 3];
    }
}".ToSumTreeWithLinesAndBrackets();

// Check if brackets are balanced
bool balanced = code.AreBracketsBalanced(code.Length); // true
Console.WriteLine($"Brackets balanced: {balanced}");

// Find specific bracket occurrences
long firstParen = code.FindNthOpenBracket('(', 1);    // Position of first '('
long secondBrace = code.FindNthOpenBracket('{', 2);   // Position of second '{'

// Get bracket summary
var bracketSummary = code.GetSummary<BracketCountSummary>();
Console.WriteLine($"Open parens: {bracketSummary.OpenParentheses}");
Console.WriteLine($"Open braces: {bracketSummary.OpenCurlyBraces}");
Console.WriteLine($"Open brackets: {bracketSummary.OpenSquareBrackets}");
```

### Powerful Search Operations

```csharp
SumTree<char> text = "The quick brown fox jumps over the lazy dog".ToSumTree();

// Find patterns
long pos1 = text.IndexOf("quick".ToSumTree());           // 4
long pos2 = text.IndexOf("the".ToSumTree(), 10);        // 31 (second occurrence)
long pos3 = text.LastIndexOf("o".ToSumTree());          // 40 (last 'o')

// Check containment
bool hasQuick = text.Contains("quick".ToSumTree());      // true
bool hasSlow = text.Contains("slow".ToSumTree());        // false

// Case-insensitive search with custom comparer
var caseInsensitive = EqualityComparer<char>.Create(
    (x, y) => char.ToLower(x) == char.ToLower(y),
    c => char.ToLower(c).GetHashCode());

long pos4 = text.IndexOf("QUICK".ToSumTree(), caseInsensitive); // 4
```

### Advanced Operations

```csharp
// Efficient slicing (no copying)
SumTree<char> text = "Hello, World!".ToSumTree();
SumTree<char> slice = text.Slice(7, 5); // "World"

// Remove ranges efficiently
SumTree<char> modified = text.RemoveRange(5, 2); // "Hello World!"

// Replace elements
SumTree<char> replaced = text.Replace(' ', '_'); // "Hello,_World!"

// Insert at any position
SumTree<char> inserted = text.Insert(5, '!'); // "Hello!, World!"

// Combine multiple SumTrees
var parts = new[] { 
    "Hello".ToSumTree(), 
    " ".ToSumTree(), 
    "World".ToSumTree() 
};
SumTree<char> combined = parts.Combine(); // "Hello World"
```

### Custom Summary Dimensions

```csharp
// Create a custom dimension that counts vowels
public class VowelCountDimension : SummaryDimensionBase<char, int>
{
    public override int Identity => 0;

    public override int SummarizeElement(char element)
    {
        return "aeiouAEIOU".Contains(element) ? 1 : 0;
    }

    public override int Combine(int left, int right)
    {
        return left + right;
    }
}

// Use the custom dimension
var vowelDimension = new VowelCountDimension();
SumTree<char> text = "Hello World".ToSumTree(vowelDimension);

int vowelCount = text.GetSummary<int>(); // 3 vowels (e, o, o)
Console.WriteLine($"Vowel count: {vowelCount}");
```

### Working with Any Type

```csharp
// SumTree works with any type
SumTree<int> numbers = new[] { 1, 2, 3, 4, 5 }.ToSumTree();
SumTree<int> doubled = numbers.Select(x => x * 2); // { 2, 4, 6, 8, 10 }

// Efficient sorted insertion
SumTree<int> sorted = SumTree<int>.Empty;
sorted = sorted.InsertSorted(5, Comparer<int>.Default);
sorted = sorted.InsertSorted(2, Comparer<int>.Default);
sorted = sorted.InsertSorted(8, Comparer<int>.Default);
// Result: { 2, 5, 8 }

// Filter operations
SumTree<int> evens = numbers.Where(x => x % 2 == 0); // { 2, 4 }
```

### Constructors and Building

```csharp
// Multiple ways to create SumTrees
SumTree<char> empty = SumTree<char>.Empty;
SumTree<char> single = new SumTree<char>('A');
SumTree<char> fromArray = new SumTree<char>("Hello".ToCharArray());
SumTree<char> fromMemory = new SumTree<char>("Hello".AsMemory());

// Concatenation constructor
SumTree<char> left = "Hello".ToSumTree();
SumTree<char> right = " World".ToSumTree();
SumTree<char> combined = new SumTree<char>(left, right);

// Balance trees when needed
SumTree<char> balanced = unbalancedTree.Balanced();
```

## Key Features

### Performance Benefits

* **O(log n)** random access and edits
* **O(log n)** concatenation and splitting
* **O(n)** sequential iteration with excellent cache locality
* **Zero-copy** operations through structural sharing
* **Automatic balancing** maintains performance over time

### Rich Summary System

* **Line/Column Tracking**: Built-in support for text editor scenarios
* **Bracket Matching**: Track parentheses, brackets, and braces automatically
* **Custom Dimensions**: Define your own summary computations
* **Efficient Queries**: Summary data maintained incrementally during edits

### Developer Experience

* **String-like API**: `SumTree<char>` behaves like `string` but with better performance
* **LINQ Integration**: Full support for `Select`, `Where`, `Aggregate`, etc.
* **Value Semantics**: Structural equality and hash codes work as expected
* **Thread Safe**: Immutable design means safe concurrent access

## Comparison with .NET Built-in Types

| Operation       | SumTree\<T>  | Rope\<T> | String | StringBuilder    | List\<T> | ImmutableList\<T> |
| --------------- | ------------ | -------- | ------ | ---------------- | -------- | ----------------- |
| Concat          | **O(log n)** | O(log n) | O(n)   | Amortized O(1)\* | O(n)     | O(log n)          |
| Insert          | **O(log n)** | O(log n) | O(n)   | O(n)             | O(n)     | O(log n + k)†     |
| Remove          | **O(log n)** | O(log n) | O(n)   | O(n)             | O(n)     | O(log n + k)†     |
| IndexOf         | **O(n)**     | O(n)     | O(n)   | O(n)             | O(n)     | O(n)              |
| Random Access   | **O(log n)** | O(log n) | O(1)   | O(1)             | O(1)     | O(log n)          |
| Memory Usage    | **Low**\*    | Low\*    | High   | Medium           | High     | High              |
| Immutable       | **✓**        | ✓        | ✓      | ✗                | ✗        | ✓                 |
| Summary Queries | **✓**        | ✗        | ✗      | ✗                | ✗        | ✗                 |

* Low: Lower than flat arrays for edits (no full-copy), but has per-node pointer/struct overhead.
* StringBuilder concat is amortized O(1) for appends but still O(n) when converting to string.
† k = size of the modified leaf chunk, which can make it slightly slower than pure log time.

## Advanced Examples

### Text Editor Implementation

```csharp
public class SimpleTextEditor
{
    private SumTree<char> _document;

    public SimpleTextEditor(string initialText = "")
    {
        _document = initialText.ToSumTreeWithLines();
    }

    public void InsertText(int line, int column, string text)
    {
        _document = _document.InsertAtLineColumn(line, column, text);
    }

    public void DeleteLine(int lineNumber)
    {
        long lineStart = _document.FindLineStart(lineNumber);
        long nextLineStart = lineNumber < GetLineCount() 
            ? _document.FindLineStart(lineNumber + 1)
            : _document.Length;
        
        _document = _document.RemoveRange(lineStart, nextLineStart - lineStart);
    }

    public string GetLine(int lineNumber)
    {
        return _document.GetLineContent(lineNumber);
    }

    public int GetLineCount()
    {
        var summary = _document.GetSummary<LineNumberSummary>();
        return summary.Lines + 1; // Lines are 0-based, add 1 for total count
    }

    public (int line, int column) GetPosition(long index)
    {
        return _document.GetLineAndColumn(index);
    }

    public override string ToString()
    {
        return _document.ToString();
    }
}
```

### Code Analysis Tool

```csharp
public class CodeAnalyzer
{
    public static CodeMetrics Analyze(string sourceCode)
    {
        var code = sourceCode.ToSumTreeWithLinesAndBrackets();
        
        var linesSummary = code.GetSummary<LineNumberSummary>();
        var bracketsSummary = code.GetSummary<BracketCountSummary>();
        
        return new CodeMetrics
        {
            LineCount = linesSummary.Lines + 1,
            TotalCharacters = linesSummary.TotalCharacters,
            AverageLineLength = linesSummary.Lines > 0 
                ? (double)linesSummary.TotalCharacters / (linesSummary.Lines + 1) 
                : 0,
            
            ParenthesesCount = bracketsSummary.OpenParentheses,
            BracketsCount = bracketsSummary.OpenSquareBrackets,
            BracesCount = bracketsSummary.OpenCurlyBraces,
            IsBalanced = bracketsSummary.IsBalanced,
            
            FunctionCount = CountFunctions(code),
            MaxNestingLevel = CalculateMaxNesting(code)
        };
    }

    private static int CountFunctions(SumTree<char> code)
    {
        // Count occurrences of "function" keyword
        int count = 0;
        long pos = 0;
        var pattern = "function".ToSumTree();
        
        while ((pos = code.IndexOf(pattern, pos)) != -1)
        {
            count++;
            pos += pattern.Length;
        }
        
        return count;
    }

    private static int CalculateMaxNesting(SumTree<char> code)
    {
        int maxNesting = 0;
        int currentNesting = 0;
        
        foreach (char c in code)
        {
            if (c == '{')
            {
                currentNesting++;
                maxNesting = Math.Max(maxNesting, currentNesting);
            }
            else if (c == '}')
            {
                currentNesting--;
            }
        }
        
        return maxNesting;
    }
}

public class CodeMetrics
{
    public int LineCount { get; set; }
    public long TotalCharacters { get; set; }
    public double AverageLineLength { get; set; }
    public int ParenthesesCount { get; set; }
    public int BracketsCount { get; set; }
    public int BracesCount { get; set; }
    public bool IsBalanced { get; set; }
    public int FunctionCount { get; set; }
    public int MaxNestingLevel { get; set; }
}
```

## Performance

SumTree is designed for high-performance scenarios where traditional string/list operations become bottlenecks:

* **Text Editors**: Handle large documents with efficient line-based operations
* **Code Analysis**: Parse and analyze source code with built-in bracket tracking
* **Data Processing**: Work with large immutable collections without copying overhead
* **Collaborative Editing**: Share data structures safely across threads/processes

## License and Acknowledgements

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgements

* Original Rope paper by Boehm, Atkinson, and Plass
* Inspired by modern text editor data structures like those in VS Code and Xi Editor
* Built on the foundation of efficient immutable data structures
* C# [Rope](https://github.com/FlatlinerDOA/Rope) implementation by Andrew Chisholm

**Author:** Bruno Massa  
**Repository:** https://github.com/brmassa/SumTree  
**Package:** https://www.nuget.org/packages/com.BrunoMassa.SumTree