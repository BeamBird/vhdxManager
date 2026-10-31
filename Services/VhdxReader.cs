using System.Buffers.Binary;
using System.Text;

namespace VhdxManager.Services;

/// <summary>
/// 直接解析 VHDX 文件结构读取虚拟容量。
/// 相比 diskpart，它在磁盘已挂载、被占用时依然可用，也不需要管理员权限。
/// </summary>
public static class VhdxReader
{
    private const long RegionTable1Offset = 0x30000;
    private const long RegionTable2Offset = 0x40000;

    private static readonly Guid MetadataRegionId = new("8b7ca206-4790-4b9a-b8fe-575f050f886e");
    private static readonly Guid VirtualDiskSizeId = new("2fa54224-cd1b-4876-b211-5dbed83bf4b8");

    /// <summary>读取虚拟磁盘的总容量（字节）。不是 VHDX 或结构异常时返回 null。</summary>
    public static long? TryReadVirtualSize(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            if (stream.Length < 0x50000)
            {
                return null;
            }

            Span<byte> identifier = stackalloc byte[8];
            stream.Position = 0;
            stream.ReadExactly(identifier);
            if (!identifier.SequenceEqual("vhdxfile"u8))
            {
                return null;
            }

            foreach (long tableOffset in new[] { RegionTable1Offset, RegionTable2Offset })
            {
                if (TryReadMetadataRegion(stream, tableOffset, out long metadataOffset, out int metadataLength))
                {
                    return TryReadVirtualSizeFromMetadata(stream, metadataOffset, metadataLength);
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryReadMetadataRegion(FileStream stream, long tableOffset, out long offset, out int length)
    {
        offset = 0;
        length = 0;

        if (stream.Length < tableOffset + 16)
        {
            return false;
        }

        byte[] header = new byte[16];
        stream.Position = tableOffset;
        stream.ReadExactly(header, 0, header.Length);

        if (!header.AsSpan(0, 4).SequenceEqual("regi"u8))
        {
            return false;
        }

        int entryCount = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8, 4));
        if (entryCount <= 0 || entryCount > 64)
        {
            return false;
        }

        byte[] entries = new byte[entryCount * 32];
        if (stream.Length < tableOffset + 16 + entries.Length)
        {
            return false;
        }

        stream.ReadExactly(entries, 0, entries.Length);

        for (int i = 0; i < entryCount; i++)
        {
            int baseIndex = i * 32;
            if (ReadGuid(entries.AsSpan(baseIndex, 16)) != MetadataRegionId)
            {
                continue;
            }

            offset = (long)BinaryPrimitives.ReadUInt64LittleEndian(entries.AsSpan(baseIndex + 16, 8));
            length = BinaryPrimitives.ReadInt32LittleEndian(entries.AsSpan(baseIndex + 24, 4));
            return length > 0;
        }

        return false;
    }

    private static long? TryReadVirtualSizeFromMetadata(FileStream stream, long regionOffset, int regionLength)
    {
        long regionEnd = regionOffset + regionLength;
        if (stream.Length < regionEnd || regionLength < 32)
        {
            return null;
        }

        byte[] header = new byte[32];
        stream.Position = regionOffset;
        stream.ReadExactly(header, 0, header.Length);

        if (!header.AsSpan(0, 8).SequenceEqual("metadata"u8))
        {
            return null;
        }

        int entryCount = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(10, 2));
        if (entryCount <= 0 || entryCount > 2047)
        {
            return null;
        }

        // 每个元数据条目固定 32 字节：GUID(16) + Offset(4) + Length(4) + Flags(4) + Reserved(4)
        byte[] entries = new byte[entryCount * 32];
        if (regionLength < 32 + entries.Length || stream.Length < regionOffset + 32 + entries.Length)
        {
            return null;
        }

        stream.ReadExactly(entries, 0, entries.Length);

        for (int i = 0; i < entryCount; i++)
        {
            int baseIndex = i * 32;
            if (ReadGuid(entries.AsSpan(baseIndex, 16)) != VirtualDiskSizeId)
            {
                continue;
            }

            uint dataOffset = BinaryPrimitives.ReadUInt32LittleEndian(entries.AsSpan(baseIndex + 16, 4));
            uint dataLength = BinaryPrimitives.ReadUInt32LittleEndian(entries.AsSpan(baseIndex + 20, 4));

            if (dataLength < 8)
            {
                return null;
            }

            long absolute = regionOffset + dataOffset;
            if (absolute + 8 > stream.Length)
            {
                return null;
            }

            byte[] buffer = new byte[8];
            stream.Position = absolute;
            stream.ReadExactly(buffer, 0, buffer.Length);

            ulong size = BinaryPrimitives.ReadUInt64LittleEndian(buffer);
            if (size < 1024 * 1024 || size > 64UL * 1024 * 1024 * 1024 * 1024)
            {
                return null;
            }

            return (long)size;
        }

        return null;
    }

    private static Guid ReadGuid(ReadOnlySpan<byte> span)
    {
        uint a = BinaryPrimitives.ReadUInt32LittleEndian(span);
        ushort b = BinaryPrimitives.ReadUInt16LittleEndian(span[4..]);
        ushort c = BinaryPrimitives.ReadUInt16LittleEndian(span[6..]);

        return new Guid(
            (int)a, (short)b, (short)c,
            span[8], span[9], span[10], span[11],
            span[12], span[13], span[14], span[15]);
    }
}
