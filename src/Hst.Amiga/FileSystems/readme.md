# File systems

## Protection bits

Files in Amiga file systems have a series of protection bits which controls their attributes. 

The protection bits are represented by letters:
- `H` The file should be held resident in memory after it has been used.
- `S` The file is a script.
- `P` The file is a pure command and can be made resident.
- `A` The file has been archived.
- `R` The file can be read.
- `W` The file can be written to (altered).
- `E` The file is executable (a program).
- `D` The file can be deleted.

Protection bits are represented by following bits:

| Bit | Protection    |
|-----|---------------|
| 128 | Held Resident |
| 64  | Script        |
| 32  | Pure          |
| 16  | Archive       |
| 8   | Read          |
| 4   | Write         |
| 2   | Execute       |
| 1   | Delete        |

By default a file has Read, Write, Execute and Delete protection bits when set to 0.

Protection bit examples:

| Value | Bits       | Attributes |
|-------|------------|------------|
| 0     | `00000000` | `----RWED` |
| 1     | `00000001` | `----RWE-` |
| 4     | `00000100` | `----R-ED` |
| 6     | `00000110` | `----R--D` |
| 64    | `01000000` | `-S--RWED` |


