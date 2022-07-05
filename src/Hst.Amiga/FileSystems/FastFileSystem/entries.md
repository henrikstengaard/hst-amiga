# Entries

## Cache entry

| Offset                     | Data type     | Name              | Comment             |
|----------------------------|---------------|-------------------|---------------------|
| 0x000                      | LONG          | Header            |                     |
| 0x004                      | LONG          | Size              | Size of cache entry |
| 0x008                      | LONG          | Protect           | Protection bits     |
| 0x00C                      | WORD          | Days              |                     |
| 0x00E                      | WORD          | Minutes           |                     |
| 0x010                      | WORD          | Ticks             |                     |
| 0x012                      | UNSIGNED CHAR | Type              |                     |
| 0x013                      | CHAR          | Length of name    |                     |
| 0x014                      | CHAR * length | Name              |                     |
| 0x014 + length of name     | CHAR          | Length of comment |                     |
| 0x014 + length of name + 1 | CHAR * length | Comment           |                     |
