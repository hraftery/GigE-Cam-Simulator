using System;

namespace GigE_Cam_Simulator.Commads
{
    /// <summary>
    /// https://www.visiononline.org/userAssets/aiaUploads/File/GigE_Vision_Specification_2-0-03.pdf
    /// </summary>
    public class ReadMemAck : GvcpAck
    {
        byte[] resultData;
        uint address;

        private static string ByteToString(byte[] data)
        {
            return System.Text.Encoding.ASCII.GetString(data);
        }

        public ReadMemAck(uint req_id, RegisterMemory registers, BufferReader message) :
            base(req_id, GvcpPacketType.GVCP_PACKET_TYPE_ACK, ArvGvcpCommand.GVCP_COMMAND_READ_MEMORY_ACK)
        {
            var address = message.ReadUIntBE();
            var reserved = message.ReadWordBE();
            var count = message.ReadWordBE();

            resultData = registers.ReadBytes(address, count);
            this.address = address;
            var register = BootstrapRegisterHelper.RegisterByAddress(address);

            Console.WriteLine("  read:  0x" + address.ToString("X4") + " (" + register.Name + ") = " + ByteToString(resultData));
        }

        public BufferReader ToBuffer()
        {
            var b = CreateBuffer(4 + resultData.Length);

            b.WriteIntBE(address);
            b.WriteBytes(resultData, (uint)resultData.Length);

            return b;

        }
    }
}
