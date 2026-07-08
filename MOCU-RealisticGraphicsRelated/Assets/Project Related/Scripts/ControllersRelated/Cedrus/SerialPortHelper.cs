using System.IO.Ports;
using System;


public class SerialPortHelper
{
    private SerialPort _serialPort;
    private string _portName;
    private int _baudRate;
    private int _checkPortConnectionReadTimeout;


    public void SetParameters(string portName, int baudRate, int readTimeout)
    {
        _portName = portName;
        _baudRate = baudRate;
        _checkPortConnectionReadTimeout = readTimeout;
    }

    public byte[]? ReadData(int numberOfBytes)
    {
        try
        {
            int bytesToRead = _serialPort.BytesToRead;
            if (bytesToRead < numberOfBytes) return null;

            byte[] buffer = new byte[numberOfBytes];
            _serialPort.Read(buffer, 0, numberOfBytes);

            return buffer;
        }
        catch
        {
            return null;
        }
    }

    public bool Connect()
    {
        try
        {
            if (string.IsNullOrEmpty(_portName))
                return false;

            _serialPort = new SerialPort(_portName)
            {
                ReadTimeout = _checkPortConnectionReadTimeout,  // Is needed only for port checking. doesn't affect reading when there are bytes to read
                BaudRate = _baudRate,                           // The speed of data transmission, specifies how many bits of data are transmitted per second (bits per second)
                DataBits = 8,                                   // The number of data bits in each data packet (usually 7 or 8, 8 in this case)
                Parity = Parity.None,                           // A parity check method used to detect errors in the transmitted data (None means no parity check is used)
                StopBits = StopBits.One,                        // The number of stop bits used to indicate the end of a data packet (One means one stop bit)
                Handshake = Handshake.None,                     // The flow control protocol (Handshake.None means no flow control is used)
                DtrEnable = true                                // Maybe it can help to improve connection speed...
            };

            _serialPort.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Close()
    {
        if (_serialPort == null)
            return;

        try { _serialPort.Close(); } catch { }
        try { _serialPort.Dispose(); } catch { }
    }

    public bool CheckPortConnection()
    {
        try { if (_serialPort.BytesToRead == 0) _serialPort.ReadLine(); }   // on purpose tries to cause an exception
        catch (TimeoutException) { return true; }                           // Device is connected but didn't send anything, it's ok
        catch { return false; }                                             // if not "TimeoutException"-- device disconected

        return true;
    }
}