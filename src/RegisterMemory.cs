namespace GigE_Cam_Simulator
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class RegisterMemory
    {
        private BufferReader data;
        Dictionary<uint, Action<RegisterMemory>> writeRegisterHock = new Dictionary<uint, Action<RegisterMemory>>();

        public bool ReadBit(eBootstrapRegister register, uint index)
        {
            var address = BootstrapRegisterHelper.RegisterByEnum(register).Address;
            return data.GetBit(address, index);
        }
        public bool ReadBit(uint address, uint index)
        {
            return data.GetBit(MapToLowAddress(address), index);
        }

        public byte ReadByte(eBootstrapRegister register, uint index)
        {
            var address = BootstrapRegisterHelper.RegisterByEnum(register).Address + index;
            return data.GetByte(address);
        }

        public void WriteByte(eBootstrapRegister register, uint index, byte value)
        {
            var address = BootstrapRegisterHelper.RegisterByEnum(register).Address + index;
            data.SetByte(address, value);
            this.TriggerWriteHock(address);
        }

        public void WriteBytes(uint address, byte[] values)
        {
            data.SetBytes(MapToLowAddress(address), values, 0, (uint)values.Length);
            this.TriggerWriteHock(address);
        }


        public void WriteIntBE(uint address, int value)
        { 
            data.SetIntBE(MapToLowAddress(address), value);
            this.TriggerWriteHock(address);
        }

        public uint ReadIntBE(uint address)
        {
            return data.GetIntBE(MapToLowAddress(address));
        }

        public uint ReadIntBE(eBootstrapRegister register)
        {
            var reg = BootstrapRegisterHelper.RegisterByEnum(register);
            return data.GetIntBE(reg.Address);
        }
        public void WriteIntBE(eBootstrapRegister register, int value)
        {
            var reg = BootstrapRegisterHelper.RegisterByEnum(register);
            this.WriteIntBE(reg.Address, value);
        }

        public byte[] ReadBytes(uint address, uint length)
        {
            return this.data.GetBytes(MapToLowAddress(address), length);
        }

        public byte[] ReadBytes(eBootstrapRegister register)
        {
            var reg = BootstrapRegisterHelper.RegisterByEnum(register);
            return this.data.GetBytes(reg.Address, reg.Length);
        }


        public void WriteBytes(eBootstrapRegister register, byte[] values)
        {
            var reg = BootstrapRegisterHelper.RegisterByEnum(register);
            var address = reg.Address;

            var l = (uint)Math.Min(values.Length, reg.Length);

            // fill in data
            this.data.SetBytes(address, values, 0, l);
            
            // clear buffer
            this.data.SetNull(address + l, reg.Length - l);

            this.TriggerWriteHock(address);
        }

        public void WriteBit(uint address, uint index, bool value)
        {
            this.data.SetBit(MapToLowAddress(address), index, value);

            this.TriggerWriteHock(address);
        }

        public void WriteBit(eBootstrapRegister register, uint index, bool value)
        {
            WriteBit(BootstrapRegisterHelper.RegisterByEnum(register).Address, index, value);
        }

        public void WriteString(uint address, string value)
        {
            var charData = ASCIIEncoding.ASCII.GetBytes(value);
            WriteBytes(address, charData);
        }

        public void WriteString(eBootstrapRegister register, string value)
        {
            var reg = BootstrapRegisterHelper.RegisterByEnum(register);
            var charData = ASCIIEncoding.ASCII.GetBytes(value);
            if (charData.Length >= reg.Length)
            {
                // force NULL termination
                charData[reg.Length - 1] = 0;
            }
            WriteBytes(register, charData);
        }

        public string ReadString(eBootstrapRegister register)
        {
            var reg = BootstrapRegisterHelper.RegisterByEnum(register);
            return this.data.GetString(reg.Address, reg.Length);
        }

        /// <summary>
        /// Register a callback that is triggered when data is written to a given address
        /// </summary>
        public void AddWriteRegisterHock(uint address, Action<RegisterMemory> callback)
        {
            this.writeRegisterHock.Add(address, callback);
        }

        private void TriggerWriteHock(uint address)
        {
            if (this.writeRegisterHock.TryGetValue(address, out var callback))
            {
                callback(this);
            }
        }

        private uint MapToLowAddress(uint addr)
        {
            //It was expected that addr would be a simple offset into the register memory space. But in the Teledyne
            //Linea camera config at least, register addresses can be very high. Specifically, they are in ranges:
            //  0x0000     - 0x9FFF     : Reserved by the spec for "Bootstrap Registers". Includes "manifest table" @ 0x9000.
            //  0x08000000 - 0x08000053 : "User set " registers
            //  0x08EFB000 - 0x08EFF003 : File access registers
            //  0x08F00000 - variable   : File access buffer
            //  0x10000020 - 0x100000FF : Transfer registers
            //  0x1200000C - 0x120003FF : Various
            //  0x18000010 - 0x18009383 : Device functions
            //  0x20000040 - 0x20003A63 : Acquisition stuff
            //  0x90000000              : pFFCCorrectionSingleOffsetReg
            //  0xA0000000              : pFFCCorrectionSingleGainReg
            //  0xB0000000              : pLUTValue_Reg
            //
            //Currently there's 68MB allocated for registers so addresses should be < 0x04400000.
            //Here we attempt to compact the address space without conflict, while leaving addresses below 0x10000
            //unaffected. But without knowing what addresses other cameras use it's very hard to make it general,
            //so this may well need to be tweaked to support other cameras. Instead of mapping addresses on the
            //fly, we could instead require the user to modify the camera.xml file to ensure all addresses are
            //within 0x04400000, but that is a burden worth trying to avoid.

            if (data.Length == 0x04400000 &&        //check assumption is true, otherwise do nothing
                (addr & 0xFF000000) != 0x01000000)  //and leave 0x01XXXXXX alone for XML_FILE_ADDRESS
            {
                if ((addr & 0xFF0000) == 0)
                    addr = ((addr & 0xFF000000) >> 8) | (addr & 0xFFFF); //Turn 0x12005678 into 0x00125678.
                else
                    addr -= 0x6000000;                                   //Turn 0x08FF5678 into 0x02FF5678.
            }

            return addr;
        }

        public RegisterMemory(int size)
        {
            this.data = new BufferReader(size);
        }
    }
}
