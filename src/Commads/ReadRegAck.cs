using System;

namespace GigE_Cam_Simulator.Commads
{

    /// <summary>
    /// https://aravisproject.github.io/docs/aravis-0.4/aravis-gvcp.html
    /// </summary>
    public class ReadRegAck : GvcpAck
    {
        BufferReader resultData;

        public ReadRegAck(uint req_id, RegisterMemory registers, BufferReader message) :
            base(req_id, GvcpPacketType.GVCP_PACKET_TYPE_ACK, ArvGvcpCommand.GVCP_COMMAND_READ_REGISTER_ACK)
        {
            //The READREG command contains one or more 4-byte register addresses, and we must respond with
            //a 4-byte register value for each address. So the result is the same size as the request.
            var resultData = new BufferReader(message.Length);
            while (!message.Eof)
            {
                var address = message.ReadUIntBE();
                var register = BootstrapRegisterHelper.RegisterByAddress(address);
                Console.WriteLine("  read:  0x" + address.ToString("X4") + " (" + register.Name + ") = " + registers.ReadIntBE(address));

                var data = registers.ReadBytes(address, 4);
                resultData.WriteBytes(data, 4);
            }

            this.resultData = resultData;
        }

        public BufferReader ToBuffer()
        {
            var b = CreateBuffer(resultData.Length);

            b.WriteBytes(resultData.Buffer, (uint)resultData.Length);

            return b;

        }
    }
}
