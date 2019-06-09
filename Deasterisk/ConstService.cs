using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deasterisk
{
    internal static class ConstSignature
    {
        // 2019/6/1
        // 4.5
        public static readonly Dictionary<SignatureType, SigRecord> SignatureLib =
            new Dictionary<SignatureType, SigRecord>
            {
                [SignatureType.MainFunctionRouter] = new SigRecord("488D9940190000488BF9488BCBE8????????488B0D", SignatureType.MainFunctionRouter),
            };

        public static readonly Dictionary<PointerType, PtrRecord> PointerLib =
            new Dictionary<PointerType, PtrRecord>
            {
                [PointerType.LogFilter] = new PtrRecord(new int[] { 0, 0x2BF0 }, 0x10, 0x10),
            };

        internal class SigRecord
        {
            public SigRecord(string pattern, SignatureType selfType, int offset = 0, bool asmSignature = true)
            {
                AsmSignature = asmSignature;
                Pattern = pattern;
                Offset = offset;
                Signature = ConstHelper.ConvertPattern(pattern, out Mask);
                Length = Signature.Length;
                SelfType = selfType;
            }

            public readonly string Pattern;
            public readonly byte[] Signature;
            public readonly bool[] Mask;
            public readonly int Length;
            public readonly int Offset;
            public readonly bool AsmSignature;
            public readonly SignatureType SelfType;
        }

        internal class PtrRecord
        {
            public PtrRecord(int[] offsets, int finalOffset, int length, int dtStep = 0, bool enableCache = false)
            {
                Offsets = offsets;
                FinalOffset = finalOffset;
                Length = length;
                DtStep = dtStep;
                EnableCache = enableCache;
            }

            public readonly int[] Offsets;
            public readonly int FinalOffset;
            public readonly int Length;
            public readonly int DtStep;
            public readonly bool EnableCache;
        }

        public enum SignatureType
        {
            MainFunctionRouter = 0x0000,

            Invalid = 0xFF00,
        }

        public enum PointerType
        {
            LogFilter = SignatureType.MainFunctionRouter | Indexer.Index01,

            NotSupported = SignatureType.Invalid | Indexer.IndexFF,
        }

        private enum Indexer
        {
            Index01 = 0x01,
            Index02 = 0x02,
            Index03 = 0x03,
            Index04 = 0x04,
            Index05 = 0x05,
            Index06 = 0x06,
            Index07 = 0x07,
            Index08 = 0x08,
            Index09 = 0x09,
            Index0A = 0x0A,
            Index0B = 0x0B,
            Index0C = 0x0C,
            Index0D = 0x0D,
            Index0E = 0x0E,
            Index0F = 0x0F,
            IndexFF = 0xFF,
        }
    }

    internal static class ConstHelper
    {
        #region ConvertTable

        private static readonly byte[] FromHexTable = {
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 0, 1,
            2, 3, 4, 5, 6, 7, 8, 9, 255, 255,
            255, 255, 255, 255, 255, 10, 11, 12, 13, 14,
            15, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 10, 11, 12,
            13, 14, 15
        };

        private static readonly byte[] FromHexTable16 = {
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 0, 16,
            32, 48, 64, 80, 96, 112, 128, 144, 255, 255,
            255, 255, 255, 255, 255, 160, 176, 192, 208, 224,
            240, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 160, 176, 192,
            208, 224, 240
        };

        #endregion

        public static unsafe byte[] ConvertPattern(string source, out bool[] mask)
        {
            if (string.IsNullOrEmpty(source) || source.Length % 2 == 1)
                throw new ArgumentException();

            var len = source.Length >> 1;

            fixed (char* sourceRef = source)
            {
                var result = new byte[len];
                mask = new bool[len];

                fixed (byte* hiRef = FromHexTable16)
                fixed (byte* lowRef = FromHexTable)
                fixed (byte* resultRef = result)
                fixed (bool* maskRef = mask)
                {
                    var s = &sourceRef[0];
                    var r = resultRef;
                    var m = maskRef;

                    while (*s != 0)
                    {
                        byte add;
                        *m++ = (*r = hiRef[*s++]) != 255 & (add = lowRef[*s++]) != 255;
                        *r++ += add;
                    }
                    return result;
                }
            }
        }
    }
}
