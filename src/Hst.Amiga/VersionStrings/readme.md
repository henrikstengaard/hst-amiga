# Version strings

Version strings directory contains classes to read version strings from Amiga files.

## References

Version strings classes are written with inspiration from following references:
- https://aminet.net/package/util/sys/Ver.
- http://amiga-dev.wikidot.com/file-format:hunk
- exec/resident.h (Resident structure, `RTC_MATCHWORD` 0x4AFC)

## Version string fallback

If a file doesn't contain a `$VER:` version string, `VersionStringReader.Read` reads the file with `DataTypes.Hunks.HunkReader` and returns the id string (`rt_IdString`) from the first resident (romtag) structure found by `DataTypes.Hunks.RomTagReader`, e.g. file system handlers like SmartFilesystem.
