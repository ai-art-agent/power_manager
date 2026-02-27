using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace SensorTest;

/// <summary>
/// Минимальный читатель датчиков из HWiNFO Shared Memory (обороты вентиляторов и т.д.).
/// Требуется: HWiNFO запущен с включённой опцией "Shared Memory Support".
/// </summary>
[SupportedOSPlatform("windows")]
public static class HwiNfoReader
{
    private const string MutexName = "Global\\HWiNFO_SM2_MUTEX";
    private const string MapNameLocal = "Global\\HWiNFO_SENS_SM2";
    private const int HWiNFO_STRING_LEN = 128;
    private const int HWiNFO_UNIT_LEN = 16;

    // HWiNFO SensorType enum (ReadingElements.cs)
    private const int SensorTypeFan = 3;

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    private struct SmSensorsSharedMem2
    {
        public readonly uint Signature;
        public readonly uint Version;
        public readonly uint Revision;
        public readonly long PollTime;
        public readonly uint SensorSection_Offset;
        public readonly uint SensorSection_SizeOfElement;
        public readonly uint SensorSection_NumElements;
        public readonly uint ReadingSection_Offset;
        public readonly uint ReadingSection_SizeOfElement;
        public readonly uint ReadingElements_NumElements;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    private struct SmSensorsReadingElement
    {
        public readonly int Type;
        public readonly uint SensorIndex;
        public readonly uint ReadingId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = HWiNFO_STRING_LEN)]
        public readonly string LabelOrig;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = HWiNFO_STRING_LEN)]
        public readonly string LabelUser;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = HWiNFO_UNIT_LEN)]
        public readonly string Unit;
        public readonly double Value;
        public readonly double ValueMin;
        public readonly double ValueMax;
        public readonly double ValueAvg;
    }

    public record FanReading(string Label, string Unit, double ValueRpm, double Min, double Max);

    /// <summary>
    /// Читает датчики вентиляторов (RPM) из HWiNFO. Возвращает пустой список, если HWiNFO не запущен или Shared Memory выключен.
    /// </summary>
    public static List<FanReading> TryReadFanRpm(int mutexTimeoutMs = 1000)
    {
        var result = new List<FanReading>();
        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(false, MutexName);
            if (!mutex.WaitOne(mutexTimeoutMs))
                return result;

            using var mmf = MemoryMappedFile.OpenExisting(MapNameLocal, MemoryMappedFileRights.Read);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            accessor.Read(0, out SmSensorsSharedMem2 hdr);

            int elementSize = (int)hdr.ReadingSection_SizeOfElement;
            if (elementSize <= 0 || elementSize > 4096)
                return result;

            int count = (int)hdr.ReadingElements_NumElements;
            long offset = hdr.ReadingSection_Offset;

            var buffer = new byte[elementSize];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                IntPtr ptr = handle.AddrOfPinnedObject();
                for (int i = 0; i < count; i++)
                {
                    accessor.ReadArray(offset + (long)i * elementSize, buffer, 0, elementSize);
                    var el = (SmSensorsReadingElement)Marshal.PtrToStructure(ptr, typeof(SmSensorsReadingElement))!;
                    if (el.Type == SensorTypeFan)
                        result.Add(new FanReading(
                            string.IsNullOrWhiteSpace(el.LabelUser) ? el.LabelOrig : el.LabelUser,
                            el.Unit ?? "",
                            el.Value,
                            el.ValueMin,
                            el.ValueMax
                        ));
                }
            }
            finally
            {
                handle.Free();
            }
        }
        catch (FileNotFoundException)
        {
            // HWiNFO не запущен или Shared Memory отключён
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }
        finally
        {
            try
            {
                mutex?.ReleaseMutex();
            }
            catch { }
            mutex?.Dispose();
        }

        return result;
    }
}
