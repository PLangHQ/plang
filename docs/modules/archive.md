# Archive Module

Compression settings for archive operations. Controls maximum decompressed file size and compression level.

## Settings

The archive module exposes configuration through the settings system:

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| Max | long | 100 MB | Maximum decompressed size in bytes |
| Level | CompressionLevel | Optimal | Compression level: Fastest, Optimal, NoCompression, SmallestSize |

These settings are resolved through the PLang settings scope chain — you can override them at the app or system level.

## Usage

Archive functionality is used when compressing or decompressing files:

```plang
/ Compress a file
- compress 'report.txt' to 'report.zip'

/ Compress a directory
- compress directory 'logs' to 'logs-archive.zip'

/ Decompress
- uncompress 'archive.zip' to './extracted/', overwrite
```

The `Max` setting protects against zip bombs — if the decompressed content would exceed the limit, the operation fails.
