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

        public uint GetUIntBE(uint address)
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


        public void SetBytes(uint address, byte[] data, int dataOffset, uint dataLength)
        {
            if (data == null)
            {
                Array.Fill(this.buffer, (byte)0, (int)address, (int)dataLength);
                return;
            }

            var l = (uint)Math.Min(dataLength, data.Length);
            Array.Copy(data, dataOffset, this.buffer, address, l);

            var left = dataLength - l;
            if (left > 0)
            {
                Array.Fill(this.buffer, (byte)0, (int)(address + l), (int)left);
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

        //Note the GigE Vision spec says "the leftmost bit is the most significant bit" as you'd
        //expect, especially (but not only) for big-endian. But it then denotes it as the "0" bit!
        //That's super confusing, because the 2^0 bit is then the 31st bit (in a 4 byte register).
        //The GenICam standard also allows registers to be defined "LittleEndian" in the XML, which
        //actually refers to the bits, not the bytes! We'll ignore that for now, and stick with the
        //bits in a register starting at 0 for the MSb, as appears in the spec. Pictorially:
        // Byte:       |    0    |     1     |      2     |      3      |
        // Bit:        |0       7|8        15|16        23|24         31|
        // Bit & byte:   <-- most significant     least significant -->
        private bool GetOrSetBitHelper(uint offset, uint index, bool? value)
        {
            uint byteIndex = index / 8;
            uint bitIndex  = index % 8;
            byte bitField = (byte)(0b10000000u >> (int)bitIndex);

            if (value != null) //setter
            {
                bool newVal = value.Value;
                int newByte = newVal ? buffer[offset + byteIndex] | bitField     //set bit
                                     : buffer[offset + byteIndex] & (~bitField); //clear bit

                buffer[offset + byteIndex] = (byte)newByte;
                return newVal; //redundant return value
            }
            else //getter
                return (buffer[offset + byteIndex] & bitField) != 0;
        }

        public void SetBit(uint offset, uint index, bool value)
        {
            GetOrSetBitHelper(offset, index, value);
        }

        public bool GetBit(uint offset, uint index)
        {
            return GetOrSetBitHelper(offset, index, null);
        }


    }
}
