# Data types

A note about the used data descriptors. All elements are in Motorola byte
order (highest byte first):

| Data type | Description              | Range                                         |
|-----------|--------------------------|-----------------------------------------------|
| APTR      | A memory pointer         | (usually this gets a boolean meaning on disk) |
| BYTE      | A single byte            | -128 .. 127                                   |
| UBYTE     | An unsigned byte         | 0 .. 255                                      |
| WORD      | A signed 16 bit value    | -32768 .. 32767                               |
| UWORD     | An unsigned 16 bit value | 0 .. 65535                                    |
| LONG      | A signed 32 bit value   | -2147483648 .. 2147483647                     |
| ULONG     | An unsigned 32 bit value | 0 .. 4294967295                               |
