using NAudio.CoreAudioApi;

namespace PowerManagerWidget;

public static class VolumeHelper
{
    private static MMDevice? _device;

    public static float? GetVolume()
    {
        try
        {
            var dev = GetDevice();
            return dev?.AudioEndpointVolume.MasterVolumeLevelScalar;
        }
        catch { return null; }
    }

    public static void SetVolume(float level)
    {
        if (level < 0) level = 0;
        if (level > 1f) level = 1f;
        try
        {
            var dev = GetDevice();
            if (dev != null)
                dev.AudioEndpointVolume.MasterVolumeLevelScalar = level;
        }
        catch { }
    }

    private static MMDevice? GetDevice()
    {
        if (_device != null) return _device;
        try
        {
            var enumerator = new MMDeviceEnumerator();
            _device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch { }
        return _device;
    }
}
