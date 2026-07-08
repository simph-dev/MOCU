using System;


public class AudioDevice : ISettingsDevice
{
    public event Action DeviceGotChangedByClient;
    public event Action DeviceGotChangedByServer;

    private string? _name;
    private float? _volume;
    private int _minVolume;
    private int _maxVolume;


    public AudioDevice(string? name = null, float? volume = null, int minVolume = 0, int maxVolume = 100)
    {
        _name = name;
        _volume = volume;
        _minVolume = minVolume;
        _maxVolume = maxVolume;
    }


    public string? Name { get => _name; }

    public float? Volume { get => _volume; }

    public void SetNameByClient(string? value)
    {
        if (_name == value) return;

        _name = value;
        DeviceGotChangedByClient?.Invoke();
    }

    public void SetNameByServer(string? value)
    {
        if (_name == value) return;

        _name = value;
        DeviceGotChangedByServer?.Invoke();
    }

    public void SetVolumeByClient(float? value)
    {
        if (_volume == value) return;

        _volume = NormalizeVolume(value);
        DeviceGotChangedByClient?.Invoke();
    }

    public void SetVolumeByServer(float? value)
    {
        if (_volume == value) return;

        // No need to check the server, it determines what is true
        _volume = value;
        DeviceGotChangedByServer?.Invoke();
    }

    private float? NormalizeVolume(float? value)
    {
        if (value == null)
            return null;

        if (value < _minVolume)
            return _minVolume;

        if (value > _maxVolume)
            return _maxVolume;

        return value;
    }
}