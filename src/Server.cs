namespace GigE_Cam_Simulator
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Threading;
    using GigE_Cam_Simulator.GVCP;
    using GigE_Cam_Simulator.Streams;

    internal class Server
    {
        private static readonly int CONTROL_PORT = 3956;

        string Address { get; set; }

        readonly RegisterMemory registers;
        readonly byte[] xml;
        private NetworkInterface? iface;
        private UdpClient? server;
        private StreamClient streamClient = new StreamClient();

        /// <summary>
        /// Callback that is triggered when ever a new Image need to be acquire
        /// </summary>
        private Func<ImageData>? onAcquiesceImageCallback;

        public Server(string address, string camXmlFileName, RegisterConfig preSetMemory)
        {
            this.Address = address;

            this.GetAllNic(address);


            this.registers = new RegisterMemory(1024 * 1024 * 68); // 68MB Register

            this.xml = File.ReadAllBytes(camXmlFileName);

            this.InitRegisters(preSetMemory);
        }

        private void InitRegisters(RegisterConfig preSetMemory)
        {
            registers.WriteIntBE(eBootstrapRegister.Version, 0x00020002); //2.2

            registers.WriteByte(eBootstrapRegister.Device_Mode, 0, 0b10000000);
            //                                                       ||||||++---- 0: Single Link Configuration
            //                                                       ||||++------ 0: Reserved
            //                                                       |+++-------- 0: Transmitter
            //                                                       +----------- 1: Big-endian device
            registers.WriteByte(eBootstrapRegister.Device_Mode, 1, 0); //Reserved
            registers.WriteByte(eBootstrapRegister.Device_Mode, 2, 0); //Reserved
            registers.WriteByte(eBootstrapRegister.Device_Mode, 3, 2); //ASCII character set

            // set MAC
            var ipInfo = this.GetIpInfo();
            var gatewayInfo = this.GetGatewayInfo();
            byte[] INVALID_IP = { 0, 0, 0, 0 }; //Not sure what to use if the calls above fail. This is a placeholder.
            var macAddress = this.iface?.GetPhysicalAddress().GetAddressBytes() ?? new byte[] { 0, 0, 0, 0, 0, 0 };
            for (uint i = 0; i < 2; i++)
            {
                registers.WriteByte(eBootstrapRegister.Device_MAC_address_High_Network_interface_0, i + 2, macAddress[i]);
            }
            for (uint i = 2; i < 6; i++)
            {
                registers.WriteByte(eBootstrapRegister.Device_MAC_address_Low_Network_interface_0, i - 2, macAddress[i]);
            }

            //Set network capability. PAUSE is assumed unsupported. DHCP is assumed on.
            registers.WriteBit(eBootstrapRegister.Network_interface_capability_0, 0, false); //PAUSE reception not supported
            registers.WriteBit(eBootstrapRegister.Network_interface_capability_0, 1, false); //PAUSE generation not supported
            registers.WriteBit(eBootstrapRegister.Network_interface_capability_0, 29, true);  //Link-local address always supported
            registers.WriteBit(eBootstrapRegister.Network_interface_capability_0, 30, true);  //DHCP always supported
            registers.WriteBit(eBootstrapRegister.Network_interface_capability_0, 31, false); //Persistent IP is not supported

            registers.WriteBit(eBootstrapRegister.Network_interface_configuration_0, 0, false); //Disable PAUSE reception
            registers.WriteBit(eBootstrapRegister.Network_interface_configuration_0, 1, false); //Disable PAUSE generation
            registers.WriteBit(eBootstrapRegister.Network_interface_configuration_0, 29, true);  //Link-local address is activated (always 1)
            registers.WriteBit(eBootstrapRegister.Network_interface_configuration_0, 30, true);  //DHCP is activated (default 1)
            registers.WriteBit(eBootstrapRegister.Network_interface_configuration_0, 31, false); //Persistent IP is not activated (default 0)

            // set IP and network addresses
            registers.WriteBytes(eBootstrapRegister.Current_IP_address_Network_interface_0, ipInfo?.Address.GetAddressBytes() ?? INVALID_IP);
            registers.WriteBytes(eBootstrapRegister.Current_subnet_mask_Network_interface_0, ipInfo?.IPv4Mask.GetAddressBytes() ?? INVALID_IP);
            registers.WriteIntBE(eBootstrapRegister.Number_of_network_interfaces, ipInfo != null ? 1 : 0);
            registers.WriteBytes(eBootstrapRegister.Primary_Application_IP_address, ipInfo?.Address.GetAddressBytes() ?? INVALID_IP); //set IP
            registers.WriteBytes(eBootstrapRegister.Current_default_Gateway_Network_interface_0, gatewayInfo?.Address.GetAddressBytes() ?? INVALID_IP);

            // Message capabilities
            registers.WriteByte(eBootstrapRegister.GVSP_Capability, 0, 0); //SCSPx, legacy block id, SCMBSx and SCEBAx all not supported.
            registers.WriteByte(eBootstrapRegister.Message_channel_Capability, 0, 0); //MCSP, MCCFG and MCEC all not supported
            //GVCP capability expected to be provided in memory.xml, so nothing done here.

            // Timing options
            registers.WriteIntBE(eBootstrapRegister.Heartbeat_timeout, 0x0BB8); //3000ms = 0x0BB8 is factory default
            //Timestamp support is optional, so leave all as zeros for not supported.
            registers.WriteIntBE(eBootstrapRegister.Pending_Timeout, 5000); //Worst case GVCP cmd execution time. No idea. 5s sounds like enough.

            //Control switchover and optional GVSP features not supported.

            // Physical Link
            registers.WriteByte(eBootstrapRegister.Physical_Link_Capability, 3, 0b00000001);
            //                                                                    |||||||+---- 1: Single Link supported
            //                                                                    ||||||+----- 0: Multiple Link not supported
            //                                                                    |||||+------ 0: Static LAG not supported
            //                                                                    ||||+------- 0: Dynamic LAG not supported
            //                                                                    ++++-------- 0: Reserved
            registers.WriteByte(eBootstrapRegister.Physical_Link_Configuration, 3, 0); //0: Single Link configuration

            //IEEE 1588 registers are optional. Control channel not supported. Primary Application and
            //Message channel registers are optional. Stream Channel Port and Dest Address are set by
            //the application. Stream Channel Size is set in memory.xml or the application. Stream
            //Channel delay is ignored because timestamps are disabled. Other Stream Channel registers
            //are optional. Action Group registers are optional. Skip all related registers.


            // Store the xml device description file in device memory somewhere, so it can be read out over
            // the wire with READMEM by default. The URLs can also be specified in memory.xml, which is
            // loaded into preSetMemory and will overwrite these defaults. That can be useful to provide
            // an Internet URL or local file path, instead of relying on device memory. Note that the URLs
            // can also be specified in a manifest table instead, in which case these URLs are ignored.
            // The file can be large (eg. 600kB - ZIP support not implemented), so pick somewhere with clear air.
            const uint XML_FILE_ADDRESS = 0x01000000; //Spec says address must be aligned to 32-bit boundary.
            registers.WriteString(eBootstrapRegister.XML_Device_Description_File_First_URL,
                "Local:camera.xml;" + ToHexString(XML_FILE_ADDRESS) + ";" + ToHexString((uint)this.xml.Length));
            // If the first fails, the second is used. But we have no other option! So just try the same.
            registers.WriteString(eBootstrapRegister.XML_Device_Description_File_Second_URL,
                "Local:camera.xml;" + ToHexString(XML_FILE_ADDRESS) + ";" + ToHexString((uint)this.xml.Length));
            registers.WriteBytes(XML_FILE_ADDRESS, this.xml); //Store the file.

            //Finally, write memory.xml values over the top.
            foreach (var property in preSetMemory.Properties)
            {
                if (property.IsString)
                {
                    //Prefer a known register write, because then length checks can be performed.
                    if (property.Register != eBootstrapRegister.Unknown)
                        registers.WriteString(property.Register, property.StringValue!);
                    else //otherwise just write the string as provided
                        registers.WriteString(property.RegisterAddress, property.StringValue!);
                }
                else if (property.IsBits)
                {
                    foreach (var bitIndex in property.Bits!)
                    {
                        registers.WriteBit(property.RegisterAddress, bitIndex, true);
                    }
                }
                else if (property.IsInt)
                {
                    registers.WriteIntBE(property.RegisterAddress, property.IntValue);
                }
            }
        }


        private bool IsDirectAddress(IPEndPoint endpoint)
        {
            if (server == null || server.Client.LocalEndPoint == null)
                return false;
            return endpoint.Address.Equals(((IPEndPoint)server.Client.LocalEndPoint).Address);
        }

        private string PadTo(string s)
        {
            return s.PadRight(36);
        }

        private void IncomingMessage(IAsyncResult res)
        {
            var server = (UdpClient?)res.AsyncState;
            if (server == null)
            {
                Console.WriteLine("!!! Incoming Message with invalid AsyncState.");
                return;
            }

            IPEndPoint? endpoint = new IPEndPoint(IPAddress.Any, 0);
            var msg = new BufferReader(server.EndReceive(res, ref endpoint));

            var identifier = msg.ReadByte();
            if (identifier != (byte)GvcpPacketType.GVCP_PACKET_TYPE_CMD) //Must start with the GVCP message key value
            {
                server.BeginReceive(new AsyncCallback(this.IncomingMessage), server);
                return;
            }

            var flags = msg.ReadByte();
            var command = (PackageCommandType)msg.ReadWordBE();

            // check if Endpoint fits - we only respond to discovery commands, or requests directed to us
            if (command != PackageCommandType.DISCOVERY_CMD &&
                endpoint != null && IsDirectAddress(endpoint)) //Redundant null check to satisfy compiler.
            {
                server.BeginReceive(new AsyncCallback(this.IncomingMessage), server);
                return;
            }

            var length = msg.ReadWordBE();
            var req_id = msg.ReadWordBE();
            var data = new BufferReader(msg.ReadBytes(length));

            GvcpAck? ack = null;
            switch (command)
            {
                case PackageCommandType.DISCOVERY_CMD:
                    Console.WriteLine("DISCOVERY by: " + endpoint);
                    ack = new DiscoveryAck(req_id, this.registers);
                    break;
                case PackageCommandType.READREG_CMD:
                    Console.Write(PadTo("READREG by: " + endpoint));
                    ack = new ReadRegAck(req_id, this.registers, data);
                    break;
                case PackageCommandType.READMEM_CMD:
                    Console.Write(PadTo("READMEM by: " + endpoint));
                    ack = new ReadMemAck(req_id, this.registers, data);
                    break;
                case PackageCommandType.WRITEREG_CMD:
                    Console.Write(PadTo("WRITEREG by: " + endpoint));
                    ack = new WriteRegAck(req_id, this.registers, data);
                    break;
                    
                default:
                    Console.WriteLine("!!! Unknown GigE Command: " + command);
                    break;
            }

            server.BeginReceive(new AsyncCallback(this.IncomingMessage), server);

            //Only send back the ack if requested to do so. For historical reasons the
            //ack object is always created, because that's where the command is processed.
            if (ack != null && (flags & (byte)GvcpCommandFlag.ACKNOWLEDGE) != 0)
            {
                BufferReader ackPacket = ack.ToBuffer();
                server.Send(ackPacket.Buffer, ackPacket.Buffer.Length, endpoint);
            }
        }

        public void Run()
        {
            //Prevent "an existing connection was forcibly closed by the remote host" error.
            //Ref: https://stackoverflow.com/a/39440399/3697870
            const int SIO_UDP_CONNRESET = -1744830452;

            this.server = new UdpClient();
            this.server.Client.Bind(new IPEndPoint(IPAddress.Any, Server.CONTROL_PORT));
            this.server.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            if (OperatingSystem.IsWindows()) //Only valid on Windows
                this.server.Client.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            this.server.BeginReceive(new AsyncCallback(this.IncomingMessage), this.server);
        }


        public UnicastIPAddressInformation? GetIpInfo()
        {
            if (this.iface == null)
                return null;

            foreach (UnicastIPAddressInformation ips in this.iface.GetIPProperties().UnicastAddresses)
            {
                if (ips.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ips;
                }
            }

            return null;
        }

        private GatewayIPAddressInformation? GetGatewayInfo()
        {
            if (this.iface == null)
                return null;

            foreach (GatewayIPAddressInformation ips in this.iface.GetIPProperties().GatewayAddresses)
            {
                if (ips.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ips;
                }
            }

            return null;
        }

        private string ToHexString(uint num)
        {
            return num.ToString("X");
        }

        public Server(string camXmlFileName, RegisterConfig preSetMemory) :
            this("0.0.0.0", camXmlFileName, preSetMemory)
        {
        }

        private void GetAllNic(string address)
        {
            var ifaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (var iface in ifaces)
            {
                var ipProperties = iface.GetIPProperties();
                foreach (var ip in ipProperties.UnicastAddresses)
                {
                    if ((iface.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback) &&
                        (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                    {
                        if (address == "0.0.0.0")
                        {
                            this.iface = iface;

                            //As long as this interface doesn't have a self-assigned address, don't bother looking for another.
                            byte[] octals = ip.Address.GetAddressBytes();
                            if (octals[0] != 169 || octals[1] != 254)
                                return;
                        }
                        else
                        {
                            if (address == ip.Address.ToString())
                            {
                                this.iface = iface;
                                return;
                            }
                        }
                    }
                }
            }
        }

        public void OnRegisterChanged(eBootstrapRegister regEnum, Action<RegisterMemory> callback)
        {
            var reg = BootstrapRegisterHelper.RegisterByEnum(regEnum);
            this.registers.AddWriteRegisterHock(reg.Address, callback);
        }


        public void OnRegisterChanged(uint address, Action<RegisterMemory> callback)
        {
            this.registers.AddWriteRegisterHock(address, callback);
        }

        private Timer? acquisitionTimer;

        public void StartAcquisition(int interval)
        {
            if (this.acquisitionTimer == null)
            {
                this.acquisitionTimer = new Timer(OnAcquisitionCallback, null, Timeout.Infinite, Timeout.Infinite);
            }

            OnAcquisitionCallback(null);
        }

        private void OnAcquisitionCallback(object? source)
        {
            if (this.onAcquiesceImageCallback == null)
            {
                return;
            }

            var imageData = this.onAcquiesceImageCallback();
            if (imageData == null)
            {
                return;
            }

            Console.WriteLine("--- >> send Image: start");
            SendStreamPacket(imageData);
            Console.WriteLine("--- << send Image: end");

            return;

            // enqueue next call
            var timer = this.acquisitionTimer;
            if (timer != null)
            {
                timer.Change(100, Timeout.Infinite);
            }
          
        }

        public void StopAcquisition()
        {
            var timer = this.acquisitionTimer;
            this.acquisitionTimer = null;
            if (timer == null)
            {
                return;
            }

            timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Set callback for Image acquiring
        /// </summary>
        internal void OnAcquiesceImage(Func<ImageData> callback)
        {
            this.onAcquiesceImageCallback = callback;
        }

        //NULL data means send a test packet (arbitrary data of packet size)
        public bool SendStreamPacket(ImageData? data)
        {
            var ipReg = registers.ReadBytes(eBootstrapRegister.Stream_Channel_Destination_Address_0);
            var ip = new IPAddress(ipReg);
            var port = registers.ReadIntBE(eBootstrapRegister.Stream_Channel_Port_0);
            var packetSizeReg = registers.ReadIntBE(eBootstrapRegister.Stream_Channel_Packet_Size_0);
            var packetSize = (uint)(packetSizeReg & 0xFFFF); //lower 16 bits is packet size

            if (data == null)
            {
                //Data size is packet size minus IP header (20 bytes) and UDP header (8 bytes).
                var testData = new Byte[packetSize - 28];
                //Could fill in some arbitrary data, but not important.
                return streamClient.Send(testData, ip, port, true); //"don't fragment" must be set for test packets
            }
            else
            {
                var doNotFragment = (packetSizeReg & 0x40000000) != 0;
                return streamClient.Send(data, ip, port, packetSize, doNotFragment);
            }
        }
    }
}
