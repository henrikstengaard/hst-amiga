namespace Hst.Amiga.Vm.Libraries.Dos;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileSystem;
using Structs;

public partial class DosLibrary
{
    private const byte ApfItsWild = 1 << 1;
    private const byte ApfDoDir = 1 << 2;
    private const byte ApfDidDir = 1 << 3;

    // achain offsets
    private const int AChainParent = 4;
    private const int AChainLock = 8;
    private const int AChainSize = 280;

    private readonly Dictionary<uint, MatchState> matches = new();

    private class MatchLevel
    {
        public AmigaPath Directory;
        public string RelativePath;
        public string[] Entries;
        public int Index;
        public AmigaPath CurrentPath;
        public string CurrentRelativeName;
        public uint AChain;
    }

    private class MatchState
    {
        public AmigaPattern Pattern;
        public bool IsWild;
        public readonly Stack<MatchLevel> Levels = new();
    }

    private void RegisterPatternFunctions()
    {
        Register("ParsePattern", () => D0 = ParsePattern(D1, D2, (int)D3));
        Register("ParsePatternNoCase", () => D0 = ParsePattern(D1, D2, (int)D3));
        Register("MatchPattern", () =>
            D0 = new AmigaPattern(Ram.ReadCString(D1), false).IsMatch(Ram.ReadCString(D2))
                ? unchecked((uint)DosTrue)
                : 0);
        Register("MatchPatternNoCase", () =>
            D0 = new AmigaPattern(Ram.ReadCString(D1), true).IsMatch(Ram.ReadCString(D2))
                ? unchecked((uint)DosTrue)
                : 0);
        Register("MatchFirst", () => D0 = (uint)MatchFirst(Ram.ReadCString(D1), D2));
        Register("MatchNext", () => D0 = (uint)MatchNext(D1));
        Register("MatchEnd", () => MatchEnd(D1));
        Register("FilePart", () =>
        {
            var path = Ram.ReadCString(D1) ?? string.Empty;
            D0 = D1 + (uint)Ram.GetBytes(path.Substring(0, FilePartIndex(path))).Length;
        });
        Register("PathPart", () =>
        {
            var path = Ram.ReadCString(D1) ?? string.Empty;
            var index = FilePartIndex(path);
            if (index > 0 && path[index - 1] == '/')
            {
                index--;
            }

            D0 = D1 + (uint)index;
        });
        Register("AddPart", () =>
        {
            var result = AddPart(Ram.ReadCString(D1) ?? string.Empty, Ram.ReadCString(D2) ?? string.Empty);
            if (WriteString(D1, (int)D3, result))
            {
                D0 = unchecked((uint)DosTrue);
                return;
            }

            SetIoErr(DosErrors.LineTooLong);
            D0 = 0;
        });
        Register("SplitName", () =>
        {
            var name = Ram.ReadCString(D1) ?? string.Empty;
            var separator = (char)(D2 & 0xFF);
            var position = (int)D4;
            var size = (int)D5;
            if (position >= name.Length)
            {
                WriteString(D3, size, string.Empty);
                D0 = 0xFFFFFFFF;
                return;
            }

            var end = name.IndexOf(separator, position);
            var part = end < 0 ? name.Substring(position) : name.Substring(position, end - position);
            WriteString(D3, size, part);
            D0 = end < 0 ? 0xFFFFFFFF : (uint)(end + 1);
        });
    }

    /// <summary>
    /// Index of file part in path after last slash or colon.
    /// </summary>
    private static int FilePartIndex(string path)
    {
        var index = Math.Max(path.LastIndexOf('/'), path.LastIndexOf(':'));
        return index + 1;
    }

    /// <summary>
    /// Add file or path to directory.
    /// </summary>
    public static string AddPart(string directory, string file)
    {
        if (file.IndexOf(':') >= 0)
        {
            return file;
        }

        var result = directory;
        while (file.StartsWith("/"))
        {
            file = file.Substring(1);
            var trimmed = result.EndsWith("/") ? result.Substring(0, result.Length - 1) : result;
            if (trimmed.Length == 0 || trimmed.EndsWith(":") || trimmed.EndsWith("/"))
            {
                // no directory name to move up from, keep parent reference
                result += "/";
                continue;
            }

            var separator = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf(':'));
            result = separator < 0
                ? string.Empty
                : trimmed.Substring(0, trimmed[separator] == ':' ? separator + 1 : separator);
        }

        if (file.Length == 0)
        {
            return result;
        }

        if (result.Length > 0 && !result.EndsWith(":") && !result.EndsWith("/"))
        {
            result += "/";
        }

        return result + file;
    }

    /// <summary>
    /// Parse pattern by storing pattern in buffer, which is used by match pattern.
    /// </summary>
    /// <returns>1 if pattern has wildcards, 0 if not and -1 if buffer is too small.</returns>
    private uint ParsePattern(uint source, uint buffer, int length)
    {
        var pattern = Ram.ReadCString(source) ?? string.Empty;
        if (!WriteString(buffer, length, pattern))
        {
            return 0xFFFFFFFF;
        }

        return AmigaPattern.IsWild(pattern) ? 1u : 0u;
    }

    private int MatchFirst(string pattern, uint anchor)
    {
        MatchEnd(anchor);
        pattern ??= string.Empty;

        var fileIndex = FilePartIndex(pattern);
        var directoryPart = pattern.Substring(0, fileIndex);
        var filePattern = pattern.Substring(fileIndex);
        var isWild = AmigaPattern.IsWild(filePattern);

        var flags = (byte)(Ram.ReadByte(anchor + AnchorPathStruct.Flags) & ~(ApfItsWild | ApfDidDir));
        if (isWild)
        {
            flags |= ApfItsWild;
        }

        Ram.WriteByte(anchor + AnchorPathStruct.Flags, flags);

        if (!isWild)
        {
            // single object
            var path = ResolveOrFail(pattern);
            if (path == null || !Exists(path))
            {
                SetIoErr(DosErrors.ObjectNotFound);
                return DosErrors.ObjectNotFound;
            }

            // matched name is pattern as given, e.g. "sys:" for volume root
            var state = new MatchState { IsWild = false };
            var parent = volumes.GetParent(path) ?? path;
            var level = new MatchLevel
            {
                Directory = parent,
                RelativePath = directoryPart,
                Entries = new[] { path.Name },
                Index = 1,
                AChain = CreateAChain(anchor, parent, 0)
            };
            state.Levels.Push(level);
            matches[anchor] = state;
            FillMatch(anchor, level, path, pattern);
            return 0;
        }

        var directory = directoryPart.Length == 0 ? CurrentDirectory : ResolveOrFail(directoryPart);
        if (directory == null || !Directory.Exists(directory.HostPath))
        {
            SetIoErr(DosErrors.DirNotFound);
            return DosErrors.DirNotFound;
        }

        var wildState = new MatchState
        {
            Pattern = new AmigaPattern(filePattern, true),
            IsWild = true
        };
        wildState.Levels.Push(new MatchLevel
        {
            Directory = directory,
            RelativePath = directoryPart,
            Entries = GetDirectoryEntries(directory),
            AChain = CreateAChain(anchor, directory, 0)
        });
        matches[anchor] = wildState;
        return MatchNext(anchor);
    }

    private uint CreateAChain(uint anchor, AmigaPath directory, uint parent)
    {
        var aChain = Context.Allocator.Allocate(AChainSize);
        if (aChain == 0)
        {
            return 0;
        }

        Ram.WriteLong(aChain + AChainParent, parent);
        Ram.WriteLong(aChain + AChainLock, CreateLock(directory, SharedLock));
        if (parent != 0)
        {
            Ram.WriteLong(parent, aChain);
        }
        else
        {
            Ram.WriteLong(anchor + AnchorPathStruct.Base, aChain);
        }

        Ram.WriteLong(anchor + AnchorPathStruct.Last, aChain);
        return aChain;
    }

    private void FreeAChain(uint aChain)
    {
        if (aChain == 0)
        {
            return;
        }

        FreeLock(Ram.ReadLong(aChain + AChainLock));
        Context.Allocator.Free(aChain);
    }

    private void FillMatch(uint anchor, MatchLevel level, AmigaPath path, string relativeName)
    {
        level.CurrentPath = path;
        level.CurrentRelativeName = relativeName;
        FillFileInfoBlock(anchor + AnchorPathStruct.Info, path);
        Ram.WriteLong(anchor + AnchorPathStruct.Last, level.AChain);
        if (level.AChain != 0)
        {
            Ram.Copy(anchor + AnchorPathStruct.Info, level.AChain + 12, FileInfoBlockStruct.Size);
        }

        var length = (short)Ram.ReadWord(anchor + AnchorPathStruct.Strlen);
        if (length > 0)
        {
            if (!WriteString(anchor + AnchorPathStruct.Buf, length, relativeName))
            {
                SetIoErr(DosErrors.BufferOverflow);
            }
        }
    }

    private int MatchNext(uint anchor)
    {
        if (!matches.TryGetValue(anchor, out var state))
        {
            SetIoErr(DosErrors.NoMoreEntries);
            return DosErrors.NoMoreEntries;
        }

        var breakBits = Ram.ReadLong(anchor + AnchorPathStruct.BreakBits);
        if (breakBits != 0)
        {
            var received = exec.SignalsReceived & breakBits;
            if (received != 0)
            {
                exec.SignalsReceived &= ~received;
                Ram.WriteLong(anchor + AnchorPathStruct.FoundBreak, received);
                SetIoErr(DosErrors.Break);
                return DosErrors.Break;
            }
        }

        var flags = Ram.ReadByte(anchor + AnchorPathStruct.Flags);

        // enter directory, if requested for last matched directory
        if (state.Levels.Count > 0 && (flags & ApfDoDir) != 0 && (flags & ApfDidDir) == 0)
        {
            var current = state.Levels.Peek();
            if (current.CurrentPath != null)
            {
                var directory = current.CurrentPath;
                if (Directory.Exists(directory.HostPath))
                {
                    var relativeName = current.CurrentRelativeName;
                    Ram.WriteByte(anchor + AnchorPathStruct.Flags, (byte)(flags & ~ApfDoDir));
                    state.Levels.Push(new MatchLevel
                    {
                        Directory = directory,
                        RelativePath = relativeName.Length == 0 || relativeName.EndsWith(":") ||
                                       relativeName.EndsWith("/")
                            ? relativeName
                            : relativeName + "/",
                        Entries = GetDirectoryEntries(directory),
                        AChain = CreateAChain(anchor, directory, current.AChain)
                    });
                }
            }
        }

        flags = (byte)(Ram.ReadByte(anchor + AnchorPathStruct.Flags) & ~ApfDidDir);
        Ram.WriteByte(anchor + AnchorPathStruct.Flags, flags);

        while (state.Levels.Count > 0)
        {
            var level = state.Levels.Peek();
            while (level.Index < level.Entries.Length)
            {
                var name = level.Entries[level.Index++];
                if (state.Pattern != null && !state.Pattern.IsMatch(name))
                {
                    continue;
                }

                var path = Child(level.Directory, name);
                if (!Exists(path))
                {
                    continue;
                }

                FillMatch(anchor, level, path, level.RelativePath + name);
                return 0;
            }

            if (state.Levels.Count == 1)
            {
                break;
            }

            // leaving directory returns directory again with did dir flag set
            state.Levels.Pop();
            FreeAChain(level.AChain);
            var parentLevel = state.Levels.Peek();
            if (parentLevel.AChain != 0)
            {
                Ram.WriteLong(parentLevel.AChain, 0);
            }

            Ram.WriteByte(anchor + AnchorPathStruct.Flags,
                (byte)((Ram.ReadByte(anchor + AnchorPathStruct.Flags) | ApfDidDir) & ~ApfDoDir));
            FillMatch(anchor, parentLevel, parentLevel.CurrentPath, parentLevel.CurrentRelativeName);
            return 0;
        }

        SetIoErr(DosErrors.NoMoreEntries);
        return DosErrors.NoMoreEntries;
    }

    private void MatchEnd(uint anchor)
    {
        if (!matches.TryGetValue(anchor, out var state))
        {
            return;
        }

        foreach (var level in state.Levels)
        {
            FreeAChain(level.AChain);
        }

        matches.Remove(anchor);
        Ram.WriteLong(anchor + AnchorPathStruct.Base, 0);
        Ram.WriteLong(anchor + AnchorPathStruct.Last, 0);
    }
}
