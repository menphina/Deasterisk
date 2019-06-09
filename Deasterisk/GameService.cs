using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace Deasterisk
{
    internal class GameService
    {
        [Obsolete("Use GameSvc.GetProcessList instead")]
        public static IEnumerable<int> GetPids() // Very Expensive
        {
            foreach (var p in Process.GetProcesses())
            {
                if (string.Equals(p.ProcessName, "ffxiv_dx11", StringComparison.Ordinal))
                {
                    yield return p.Id;
                }
                p.Dispose();
            }
        }

        public static IList<Process> GetProcessList()
        {
            return Process.GetProcessesByName("ffxiv_dx11").Where(x => !x.HasExited && x.MainModule != null && x.MainModule.ModuleName == "ffxiv_dx11.exe").ToList();
        }

        public static Process GetProcess(int pid = 0)
        {
                var ffxivProcessList = GetProcessList();
                return pid != 0 ? ffxivProcessList.FirstOrDefault(x => x.Id == pid) : ffxivProcessList.OrderBy(x => x.StartTime).FirstOrDefault(); // Attach to the 'longest lived' session
        }
    }

    internal class GameScanner : IDisposable
    {
        private readonly object SigLock = new object();
        private bool _initialized;
        private Dictionary<ConstSignature.SignatureType, IntPtr> _pointers = new Dictionary<ConstSignature.SignatureType, IntPtr>();
        private MemoryService _reader;

        public GameScanner()
        {
        }

        public void Dispose()
        {
            Deinitialize();
        }

        /// <summary>
        /// Initialize the GameScanner instance with a MemoryReader
        /// </summary>
        /// <param name="memReader">MemoryReader for a game instance</param>
        public void Initialize(MemoryService memReader)
        {
                lock (SigLock)
                {
                    if (_initialized)
                        return;

                    _reader = memReader;
                    _pointers[ConstSignature.SignatureType.Invalid] = new IntPtr(-1L); // To make it pass ValidatePointers

                    var unscanned = new List<ConstSignature.SigRecord>();

                    foreach (ConstSignature.SignatureType signatureType in Enum.GetValues(typeof(ConstSignature.SignatureType)))
                    {
                        if (_pointers.ContainsKey(signatureType)) continue;
                        //var newSigOffsetByType = GetNewSigOffsetByType(signatureType);
                        //if (newSigOffsetByType != IntPtr.Zero)
                        //    _pointers.Add(signatureType, newSigOffsetByType);
                        if (!ConstSignature.SignatureLib.TryGetValue(signatureType, out var pattern)) continue;
                        unscanned.Add(pattern);
                    }

                    var result = Search(ref unscanned);

                    foreach (var kvp in result)
                    {
                        if (kvp.Value != IntPtr.Zero)
                            _pointers.Add(kvp.Key, kvp.Value);
                    }

                    _initialized = true;
                }
        }

        /// <summary>
        /// Deinitialize current GameScanner instance
        /// </summary>
        private void Deinitialize()
        {
                lock (SigLock)
                {
                    if (!_initialized)
                        return;
                    _pointers = new Dictionary<ConstSignature.SignatureType, IntPtr>();
                    _initialized = false;
                }
        }

        /// <summary>
        /// Validate all signatures are found in memory. It not ensures correctness for scan results
        /// </summary>
        /// <returns>True if all signatures are found</returns>
        public bool ValidatePointers()
        {
            try
            {
                lock (SigLock)
                {
                    foreach (ConstSignature.SignatureType key in Enum.GetValues(typeof(ConstSignature.SignatureType)))
                    {

                        if (!_initialized || !_pointers.ContainsKey(key))
                            return false;
                        if (_pointers[key] == IntPtr.Zero)
                            return false;
                    }
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Get absolute pointer for specific target
        /// </summary>
        /// <param name="pointerType">Type of the target</param>
        /// <returns>Absolute pointer pointing to the target</returns>
        public IntPtr GetPointer(ConstSignature.PointerType pointerType)
        {
                if (pointerType == ConstSignature.PointerType.NotSupported)
                    return IntPtr.Zero;

                var key = (ConstSignature.SignatureType)(pointerType & (ConstSignature.PointerType)0xFF00);

                IntPtr sigPointer;
                lock (SigLock)
                {
                    if (!_initialized || !_pointers.TryGetValue(key, out sigPointer))
                        return IntPtr.Zero;

                    if (key == ConstSignature.SignatureType.Invalid && ConstSignature.PointerLib.TryGetValue(pointerType, out var rec))
                        return _reader.TraceTree(IntPtr.Zero, rec.Offsets, rec.FinalOffset);
                }
                return sigPointer == IntPtr.Zero ? IntPtr.Zero : GetPointer(sigPointer, pointerType);
            return IntPtr.Zero;
        }

        /// <summary>
        /// Get absolute pointer from specific relative pointer
        /// </summary>
        /// <param name="sigPointer">Relative pointer</param>
        /// <param name="pointerType">Type of the target</param>
        /// <returns>Absolute pointer pointing to the target</returns>
        private IntPtr GetPointer(IntPtr sigPointer, ConstSignature.PointerType pointerType)
        {
            if (!ConstSignature.PointerLib.ContainsKey(pointerType))
                return IntPtr.Zero;

            var pointerInfo = ConstSignature.PointerLib[pointerType];
            var pointerTree = pointerInfo.Offsets;

            lock (SigLock)
            {
                if (!_initialized || pointerTree == null)
                    return IntPtr.Zero;
                return _reader.TraceTree(sigPointer, pointerTree, pointerInfo.FinalOffset);
            }
        }

        /// <summary>
        /// Search memory pointers for specific signatures
        /// </summary>
        /// <param name="unscanned">Signatures unscanned</param>
        /// <returns>Relative pointers of signatures</returns>
        private Dictionary<ConstSignature.SignatureType, IntPtr> Search(ref List<ConstSignature.SigRecord> unscanned)
        {
            const int sliceLength = 65536;
            var moduleMemorySize = _reader.Info.Process.MainModule.ModuleMemorySize;
            var baseAddress = _reader.Info.Process.MainModule.BaseAddress;

            var mainModuleEnd = IntPtr.Add(baseAddress, moduleMemorySize);
            var sliceStart = baseAddress;
            var scanResults = new Dictionary<ConstSignature.SignatureType, IntPtr>();

            #region Preprocess

            var temp = unscanned;
            var maxLength = -1;

            foreach (var examinee in unscanned)
            {
                maxLength = maxLength < examinee.Length ? examinee.Length : maxLength;
            }

            #endregion

            while (sliceStart.ToInt64() < mainModuleEnd.ToInt64())
            {
                    var bufferLength = (long)sliceLength;

                    var sliceEnd = IntPtr.Add(sliceStart, sliceLength);
                    if (sliceEnd.ToInt64() > mainModuleEnd.ToInt64())
                        bufferLength = mainModuleEnd.ToInt64() - sliceStart.ToInt64();

                    // Assume that we will not meet page fault.
                    var sliceBuffer = _reader.Read(sliceStart, bufferLength);

                    unscanned = temp;

                if(unscanned.Count > 0)
                    {
                    var signature = unscanned.First();
                    var patternLength = signature.Length;
                        var patternOffset = signature.Offset;

                        for (var matchStartPos = 0; matchStartPos < bufferLength - patternLength - patternOffset - 4L + 1L; ++matchStartPos)
                        {
                            var matchedCount = 0;

                            for (var matchingIndex = 0; matchingIndex < patternLength; ++matchingIndex)
                            {
                                if (!signature.Mask[matchingIndex])
                                    ++matchedCount;
                                else if (signature.Signature[matchingIndex] == sliceBuffer[matchStartPos + matchingIndex])
                                    ++matchedCount;
                                else
                                    break;
                            }

                            if (matchedCount != patternLength) continue;

                            long offsetFromBase;
                            if (signature.AsmSignature)
                            {
                                var effectiveAddress = new IntPtr(BitConverter.ToInt32(sliceBuffer,
                                    matchStartPos + patternLength + patternOffset));
                                var realAddress = sliceStart.ToInt64() + matchStartPos + patternLength + 4L +
                                                  effectiveAddress.ToInt64();
                                offsetFromBase = realAddress - baseAddress.ToInt64();
                            }
                            else
                            {
                                var effectiveAddress = new IntPtr(BitConverter.ToInt32(sliceBuffer,
                                    matchStartPos + patternLength + patternOffset));
                                offsetFromBase = effectiveAddress.ToInt64() - baseAddress.ToInt64();
                            }
                            scanResults.Add(signature.SelfType, new IntPtr(offsetFromBase));
                            temp.Remove(signature);
                            break;
                        }
                    }

                    sliceStart = IntPtr.Add(sliceStart, sliceLength - maxLength);
            }

            return scanResults;
        }

        /// <summary>
        /// Search memory pointer for specific signature
        /// </summary>
        /// <param name="signature">Signature to be scanned</param>
        /// <returns>Relative pointers of signatures</returns>
        private KeyValuePair<ConstSignature.SignatureType, IntPtr> SingleSearch(ConstSignature.SigRecord signature)
        {
            var patternBytes = signature.Signature;
            var patternMask = signature.Mask;
            var patternLength = signature.Length;
            var patternOffset = signature.Offset;

            const int sliceLength = 65536;
            var moduleMemorySize = _reader.Info.Process.MainModule.ModuleMemorySize;
            var baseAddress = _reader.Info.Process.MainModule.BaseAddress;

            var mainModuleEnd = IntPtr.Add(baseAddress, moduleMemorySize);
            var sliceStart = baseAddress;

            while (sliceStart.ToInt64() < mainModuleEnd.ToInt64())
            {
                    var bufferLength = (long)sliceLength;

                    var sliceEnd = IntPtr.Add(sliceStart, sliceLength);
                    if (sliceEnd.ToInt64() > mainModuleEnd.ToInt64())
                        bufferLength = mainModuleEnd.ToInt64() - sliceStart.ToInt64();

                    // Assume that we will not meet page fault.
                    var sliceBuffer = _reader.Read(sliceStart, bufferLength);

                    for (var matchStartPos = 0; matchStartPos < bufferLength - patternLength - patternOffset - 4L + 1L; ++matchStartPos)
                    {
                        var matchedCount = 0;
                        for (var matchingIndex = 0; matchingIndex < patternLength; ++matchingIndex)
                        {
                            if (!patternMask[matchingIndex])
                                ++matchedCount;
                            else if (patternBytes[matchingIndex] == sliceBuffer[matchStartPos + matchingIndex])
                                ++matchedCount;
                            else
                                break;
                        }

                        if (matchedCount != patternLength) continue;

                        long offsetFromBase;
                        if (signature.AsmSignature)
                        {
                            var effectiveAddress = new IntPtr(BitConverter.ToInt32(sliceBuffer,
                                matchStartPos + patternLength + patternOffset));
                            var realAddress = sliceStart.ToInt64() + matchStartPos + patternLength + 4L +
                                              effectiveAddress.ToInt64();
                            offsetFromBase = realAddress - baseAddress.ToInt64();
                        }
                        else
                        {
                            var effectiveAddress = new IntPtr(BitConverter.ToInt32(sliceBuffer,
                                matchStartPos + patternLength + patternOffset));
                            offsetFromBase = effectiveAddress.ToInt64() - baseAddress.ToInt64();
                        }
                        return new KeyValuePair<ConstSignature.SignatureType, IntPtr>(signature.SelfType, new IntPtr(offsetFromBase));
                    }

                    sliceStart = IntPtr.Add(sliceStart, sliceLength - patternLength);
            }
            return new KeyValuePair<ConstSignature.SignatureType, IntPtr>(signature.SelfType, IntPtr.Zero);
        }
    }
}
