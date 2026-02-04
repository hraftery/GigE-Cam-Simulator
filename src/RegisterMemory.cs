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
            data.SetBytes(address, values, 0, values.Length);
            this.TriggerWriteHock(address);
        }

        public void WriteIntBE(uint address, int value)
        { 
            data.SetIntBE(address, value);
            this.TriggerWriteHock(address);
        }

        public uint ReadIntBE(uint address)
        {
            return data.GetIntBE(address);
        }

        public uint ReadIntBE(eBootstrapRegister register)
        {
            var reg = BootstrapRegisterHelper.RegisterByEnum(register);
            return this.ReadIntBE(reg.Address);
        }
        public void WriteIntBE(eBootstrapRegister register, int value)
        {
            var reg = BootstrapRegisterHelper.RegisterByEnum(register);
            this.WriteIntBE(reg.Address, value);
        }

        public byte[] ReadBytes(int address, int lenght)
        {
            return this.data.GetBytes(address, lenght);
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
            this.data.SetBit(address, index, value);

            this.TriggerWriteHock(address);
        }

        public void WriteBit(eBootstrapRegister register, uint index, bool value)
        {
            WriteBit(BootstrapRegisterHelper.RegisterByEnum(register).Address, index, value);
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

        public RegisterMemory(int size)
        {
            this.data = new BufferReader(size);
        }
    }
}
