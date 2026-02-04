namespace GigE_Cam_Simulator
{
    using System;
    using System.Collections;
    using System.Text;

    public class BufferReader
    {
        byte[] buffer;
        uint bufferPos;

        public byte[] Buffer => this.buffer;

        public bool Eof => bufferPos >= this.buffer.Length;

        public int Length => this.buffer.Length;

        public BufferReader(int size)
        {
            this.buffer = new byte[size];
            this.bufferPos = 0;
        }
        public BufferReader(byte[] buffer)
        {
            this.buffer = buffer;
        }

        public void WriteWordBE(uint value)
        {
            this.buffer[this.bufferPos++] = (byte)((value >> 8) & 0xFF);
            this.buffer[this.bufferPos++] = (byte)(value & 0xFF);
        }

        public uint ReadWordBE()
        {
            uint b1 = this.buffer[this.bufferPos++];
            uint b2 = this.buffer[this.bufferPos++];

            return (b1 << 8 | b2);
        }

        public void SetIntBE(uint offset, int value)
        {
            this.buffer[offset++] = (byte)((value >> 24) & 0xFF);
            this.buffer[offset++] = (byte)((value >> 16) & 0xFF);
            this.buffer[offset++] = (byte)((value >>  8) & 0xFF);
            this.buffer[offset]   = (byte)((value >>  0) & 0xFF);
        }

        public void WriteIntBE(uint value)
        {
            this.buffer[this.bufferPos++] = (byte)((value >> 24) & 0xFF);
            this.buffer[this.bufferPos++] = (byte)((value >> 16) & 0xFF);
            this.buffer[this.bufferPos++] = (byte)((value >>  8) & 0xFF);
            this.buffer[this.bufferPos++] = (byte)((value >>  0) & 0xFF);
        }

        public uint ReadUIntBE()
        {
            uint b1 = this.buffer[this.bufferPos++];
            uint b2 = this.buffer[this.bufferPos++];
            uint b3 = this.buffer[this.bufferPos++];
            uint b4 = this.buffer[this.bufferPos++];

            return (b1 << 24 | b2 << 16 | b3 << 8 | b4);
        }

        public uint GetIntBE(uint address)
        {
            uint b1 = this.buffer[address++];
            uint b2 = this.buffer[address++];
            uint b3 = this.buffer[address++];
            uint b4 = this.buffer[address++];

            return (b1 << 24 | b2 << 16 | b3 << 8 | b4);
        }

        public void WriteBytes(byte[] data, uint length)
        {
            this.SetBytes(this.bufferPos, data, 0, length);
            this.bufferPos += length;
        }

        public void WriteBytes(byte[] data, uint dataLength, int dataOffset)
        {
            this.SetBytes(this.bufferPos, data, dataOffset, dataLength);
            this.bufferPos += dataLength;
        }

        public byte[] ReadBytes(uint length)
        {
            var b = this.GetBytes(this.bufferPos, length);
            this.bufferPos += length;
            return b;
        }


        public void SetBytes(uint offset, byte[] data, int dataOffset, uint dataLength)
        {
            if (data == null)
            {
                Array.Fill(this.buffer, (byte)0, (int)offset, (int)dataLength);
                return;
            }

            var l = (uint)Math.Min(dataLength, data.Length);
            Array.Copy(data, dataOffset, this.buffer, offset, l);

            var left = dataLength - l;
            if (left > 0)
            {
                Array.Fill(this.buffer, (byte)0, (int)(offset + l), (int)left);
            }
        }

        public byte[] GetBytes(uint address, uint length)
        {
            var result = new byte[length];
            Array.Copy(this.buffer, address, result, 0, length);

            return result;
        }

        public void WriteString(string value, uint length)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
   
            if (bytes.Length >= length)
            {
                // force NULL termination
                bytes[length - 1] = 0;
            }

            WriteBytes(bytes, length); //automatically null fills to length if necessary
        }

        public string GetString(uint offset, uint length)
        {
            return System.Text.Encoding.UTF8.GetString(this.buffer, (int)offset, (int)length);
        }

        public byte GetByte(uint offset)
        {
            return this.buffer[offset];
        }

        public byte ReadByte()
        {
            var b = this.buffer[this.bufferPos];
            this.bufferPos++;
            return b;
        }


        public void SetByte(uint offset, byte value)
        {
            this.buffer[offset] = value;
        }

        public void WriteNull(uint length)
        {
            this.SetNull(this.bufferPos, length);
            this.bufferPos += length;
        }

        public void SetNull(uint offset, uint length)
        {
            for (int i = 0; i < length; i++)
            {
                this.buffer[offset] = 0;
                offset++;
            }
        }

        public void SetBit(int offset, int index, bool value)
        {
            int byteIndex = index >> 3;
            int bitIndex = 7 - (index & 0x7);

            int result;
            if (value)
            {
                result = this.buffer[offset + byteIndex] | (1 << bitIndex);
            }
            else
            {
                result = this.buffer[offset + byteIndex] & (~(1 << bitIndex));
            }

            this.buffer[offset + byteIndex] = (byte)result;
        }

        public void GetBit(int offset, int index, bool value)
        {
            int byteIndex = index >> 3;
            int bitIndex = index & 0x7;

            int result;
            if (value)
            {
                result = this.buffer[offset + byteIndex] | (1 << bitIndex);
            }
            else
            {
                result = this.buffer[offset + byteIndex] & (~(1 << bitIndex));
            }

            this.buffer[offset + byteIndex] = (byte)result;
        }


    }
}
