using System;
using System.IO;

var lines = File.ReadAllLines(@"C:\Users\RakeshReddy\.gemini\antigravity\brain\1892dbd9-1602-40c9-ba10-bb2a9f07474e\walkthrough.md");
for (int i = 0; i < lines.Length; i++)
{
    var line = lines[i];
    if (line.Contains("sys.objects"))
    {
        Console.WriteLine($"Line {i+1}:");
        foreach (var c in line)
        {
            if (c > 127 || c < 32) Console.WriteLine($"Char: {(int)c}");
        }
    }
}
