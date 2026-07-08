using System.Collections.Generic;
using System;

public class XID_ProtocolParser
{
    public int bytesInDataPacket = 6;

    private Dictionary<string, BitMask> _masks_ProtocolXID = new()
    {
        { "character",  new BitMask("00000000 00000000 00000000 00000000 00000000 11111111") },    // always 'k'
        { "port",       new BitMask("00000000 00000000 00000000 00000000 00001111 00000000") },    // always 0 (0 for keys, 3 for light)
        { "actionFlag", new BitMask("00000000 00000000 00000000 00000000 00010000 00000000") },    // 1-pressed, 0-released
        { "keyCode",    new BitMask("00000000 00000000 00000000 00000000 11100000 00000000") },    // actual answer
        { "timestamp",  new BitMask("11111111 11111111 11111111 11111111 00000000 00000000") }     // from cedrus' inner timer
    };

    public (short cedrusKeyCode, bool keyWasPressed) GetParsedData(byte[] byteArray)
    {
        Array.Resize(ref byteArray, 8);
        var rawData = BitConverter.ToUInt64(byteArray, 0);

        short keyCode = (short)_masks_ProtocolXID["keyCode"].Apply(rawData);
        bool actionFlag = _masks_ProtocolXID["actionFlag"].Apply(rawData) == 1;

        //int timestamp = (int)_masks_ProtocolXID["timestamp"].Apply(rawData);
        //char character = (char)_masks_ProtocolXID["character"].Apply(rawData);
        //int port = (int)_masks_ProtocolXID["port"].Apply(rawData);

        return (cedrusKeyCode: keyCode, keyWasPressed: actionFlag);
    }

    private class BitMask
    {
        private ulong _mask;
        private int _offset;

        public BitMask(string mask)
        {
            if (mask.Length > 64)
                throw new ArgumentException("Max size for mask: ulong (64 bits)");

            var interimString = mask.Replace(" ", "");
            _mask = Convert.ToUInt64(interimString, 2);
            _offset = interimString.Length - interimString.LastIndexOf('1') - 1;

            if (_offset == interimString.Length)
                throw new ArgumentException("Max size for mask: ulong (64 bits)");
        }

        public ulong Apply(ulong data)
        {
            return (_mask & data) >> _offset;
        }

        public ulong Apply(string data)
        {
            return Apply(Convert.ToUInt64(data.Replace(" ", ""), 2));
        }
    }
}