#nullable disable
using System;
using System.IO;
using System.Linq;

namespace ReferenceCopAnalyzer.Test
{
    public static class DirectoryFinder
    {
        public static string FindParentDirectoryWith(string directory, string fileToFindSearchPattern)
        {
            if (Directory.EnumerateFiles(directory, searchPattern: fileToFindSearchPattern).Any())
            {
                return directory;
            }

            var parent = Directory.GetParent(directory);
            if (parent is null)
            {
                throw new ArgumentException($"No file matching the pattern {fileToFindSearchPattern} found in search directory", nameof(fileToFindSearchPattern));
            }

            return FindParentDirectoryWith(parent.FullName, fileToFindSearchPattern);
        }
    }
}