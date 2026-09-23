---
name: rts-protocol-analyzer
description: >-
  Use this skill when analyzing, capturing, decoding, or reverse engineering RTS network packets,
  UDP duplex protocol streams, deterministic lockstep frame synchronizations, and binary serialization structs.
---

# RTS Network Protocol & Packet Analysis Skill

This skill guides the agent through inspecting, decoding, and reconstructing binary network protocols and UDP packet streams for multiplayer RTS synchronization.

## 1. Packet Structure & Header Decomposition

Typical RTS UDP binary packet framing follows this layout:

```text
+--------------+--------------+---------------+---------------+-------------------+---------------+
| Magic (2B)   | MsgId (2B)   | FrameTick(4B) | PlayerId (2B) | PayloadLen (2B)   | Payload (NB)  |
+--------------+--------------+---------------+---------------+-------------------+---------------+
| 0x52 0x54    | 0x1001       | 0x0000045A    | 0x0001        | 0x0014            | ...           |
+--------------+--------------+---------------+---------------+-------------------+---------------+
```

### Python HexDump & Byte Slicing Helper
```python
import struct

def parse_packet(raw_bytes: bytes):
    if len(raw_bytes) < 12:
        return None
    magic, msg_id, tick, player_id, payload_len = struct.unpack(">2sHHH H", raw_bytes[:12])
    if magic != b"RT":
        raise ValueError("Invalid magic header")
    payload = raw_bytes[12 : 12 + payload_len]
    return {
        "msg_id": msg_id,
        "tick": tick,
        "player_id": player_id,
        "payload": payload
    }
```

---

## 2. Lockstep Frame Synchronization Mechanics

In deterministic RTS lockstep networking:

1. **Deterministic Tick Increment**:
   - Clients do not send position coordinates directly; they send **Commands** (e.g. `CommandMove { unit_ids, target_x, target_z }`, `CommandAttack { unit_ids, target_unit_id }`).
   - All clients simulate the exact same tick with identical inputs.
2. **Fixed-Point Coordinates**:
   - Floating-point math (`float` / `double`) can produce minute cross-platform drift (IEEE 754 differences between Intel and ARM or compiler flags).
   - Use integer-scaled fixed-point numbers ($x \times 1000$) over the wire:
     ```csharp
     [StructLayout(LayoutKind.Sequential, Pack = 1)]
     public struct NetCommandMove
     {
         public uint UnitId;
         public int TargetX; // world_x * 1000
         public int TargetZ; // world_z * 1000
     }
     ```

---

## 3. High-Performance C# Zero-Copy Packet Parsing

In Godot 4 C#, avoid GC allocations by using `Span<byte>` and `MemoryMarshal`:

```csharp
using System;
using System.Runtime.InteropServices;

public static class PacketCodec
{
    public static bool TryParse<T>(ReadOnlySpan<byte> data, out T result) where T : struct
    {
        if (data.Length < Marshal.SizeOf<T>())
        {
            result = default;
            return false;
        }
        result = MemoryMarshal.Read<T>(data);
        return true;
    }

    public static void Serialize<T>(in T value, Span<byte> destination) where T : struct
    {
        MemoryMarshal.Write(destination, in value);
    }
}
```

---

## 4. Diagnostics & Testing Scripts

- For live duplex UDP packet inspection, utilize the existing workspace script:
  [`scratch/test_udp_duplex.py`](../../scratch/test_udp_duplex.py)
- Capture round-trip latency and verify packet checksum integrity.
