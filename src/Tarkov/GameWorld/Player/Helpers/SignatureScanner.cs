/*
 * Lone EFT DMA Radar
 * MIT License - Copyright (c) 2025 Lone DMA
 */

using System;
using System.Diagnostics;

namespace LoneEftDmaRadar.DMA
{
    public static class SignatureScanner
    {
        /// <summary>
        /// Scans for a byte pattern with wildcards (?) in the specified module.
        /// </summary>
        /// <param name="moduleBase">Base address of the module to scan</param>
        /// <param name="moduleSize">Size of the module</param>
        /// <param name="pattern">Pattern string with spaces and ? for wildcards (e.g., "48 8B 05 ? ? ? ?")</param>
        /// <returns>Relative offset from moduleBase, or 0 if not found</returns>
        public static ulong FindPattern(ulong moduleBase, uint moduleSize, string pattern)
        {
            try
            {
                // Parse pattern
                var patternBytes = ParsePattern(pattern, out var mask);
                
                Debug.WriteLine($"[SigScan] Scanning for pattern: {pattern}");
                Debug.WriteLine($"[SigScan] Module: 0x{moduleBase:X}, Size: 0x{moduleSize:X}");
                
                // Read entire module into memory (or scan in chunks for large modules)
                const uint chunkSize = 0x100000; // 1MB chunks
                var patternLength = patternBytes.Length;
                
                for (ulong offset = 0; offset < moduleSize; offset += chunkSize)
                {
                    // Compute remaining bytes and desired read size as ulong, then take the smaller value
                    ulong remaining = moduleSize - offset;
                    ulong desired = (ulong)chunkSize + (ulong)patternLength;
                    uint readSize = (uint)(desired < remaining ? desired : remaining);
                    var buffer = Memory.ReadBytes(moduleBase + offset, readSize, false);
                    
                    if (buffer == null || buffer.Length == 0)
                        continue;
                    
                    // Search within this chunk
                    for (int i = 0; i < buffer.Length - patternLength; i++)
                    {
                        bool found = true;
                        for (int j = 0; j < patternLength; j++)
                        {
                            if (mask[j] && buffer[i + j] != patternBytes[j])
                            {
                                found = false;
                                break;
                            }
                        }
                        
                        if (found)
                        {
                            ulong result = offset + (ulong)i;
                            Debug.WriteLine($"[SigScan] ? Pattern found at offset: 0x{result:X}");
                            return result;
                        }
                    }
                }
                
                Debug.WriteLine("[SigScan] ? Pattern not found");
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SigScan] Error: {ex}");
                return 0;
            }
        }
        
        /// <summary>
        /// Resolves a RIP-relative address from the pattern result.
        /// </summary>
        /// <param name="address">Absolute address where pattern was found</param>
        /// <param name="offsetToRelAddr">Offset to the 4-byte relative address (usually 3 for "48 8B 05 ? ? ? ?")</param>
        /// <param name="instructionLength">Total instruction length (usually 7)</param>
        /// <returns>Resolved absolute address</returns>
        public static ulong ResolveRIPRelative(ulong address, int offsetToRelAddr, int instructionLength)
        {
            try
            {
                // Read the 4-byte relative offset
                int relativeOffset = Memory.ReadValue<int>(address + (uint)offsetToRelAddr, false);
                
                // Calculate absolute address: instruction_end + relative_offset
                ulong absoluteAddress = address + (ulong)instructionLength + (ulong)relativeOffset;
                
                Debug.WriteLine($"[SigScan] RIP-relative resolution:");
                Debug.WriteLine($"  Instruction at: 0x{address:X}");
                Debug.WriteLine($"  Relative offset: 0x{relativeOffset:X}");
                Debug.WriteLine($"  Resolved address: 0x{absoluteAddress:X}");
                
                return absoluteAddress;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SigScan] Error resolving RIP-relative: {ex}");
                return 0;
            }
        }
        
        private static byte[] ParsePattern(string pattern, out bool[] mask)
        {
            var parts = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var bytes = new byte[parts.Length];
            mask = new bool[parts.Length];
            
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "?" || parts[i] == "??")
                {
                    bytes[i] = 0;
                    mask[i] = false; // Wildcard
                }
                else
                {
                    bytes[i] = Convert.ToByte(parts[i], 16);
                    mask[i] = true; // Must match
                }
            }
            
            return bytes;
        }
    }
}