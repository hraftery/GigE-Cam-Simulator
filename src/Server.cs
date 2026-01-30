namespace GigE_Cam_Simulator
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Threading;
    using GigE_Cam_Simulator.Commads;
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
        /// Callback that is triggere when ever a new Image need to be acquire
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
            this.registers.WriteIntBE(RegisterTypes.Version, 0x0100);

            this.registers.WriteBit(RegisterTypes.Device_Mode, 0, true);
            this.registers.WriteBit(RegisterTypes.Device_Mode, 1, true);
            this.registers.ReadByte(RegisterTypes.Device_Mode, 0, 1);
            this.registers.ReadByte(RegisterTypes.Device_Mode, 2, 1);

            // set MAC
            var ipInfo = this.GetIpInfo();
            var gatewayInfo = this.GetGatewayInfo();
            byte[] INVALID_IP = { 0, 0, 0, 0 }; //Not sure what to use if the calls above fail. This is a placeholder.
            var macAddress = this.iface?.GetPhysicalAddress().GetAddressBytes() ?? new byte[] { 0, 0, 0, 0, 0, 0 };
            for (var i = 0; i < 2; i++)
            {
                this.registers.ReadByte(RegisterTypes.Device_MAC_address_High_Network_interface_0, i + 2, macAddress[i]);
            }
            for (var i = 2; i < 6; i++)
            {
                this.registers.ReadByte(RegisterTypes.Device_MAC_address_Low_Network_interface_0, i - 2, macAddress[i]);
            }

            // set IP and network addresses
            this.registers.WriteBytes(RegisterTypes.Current_IP_address_Network_interface_0, ipInfo?.Address.GetAddressBytes() ?? INVALID_IP);
            this.registers.WriteBytes(RegisterTypes.Current_subnet_mask_Network_interface_0, ipInfo?.IPv4Mask.GetAddressBytes() ?? INVALID_IP);
            this.registers.WriteIntBE(RegisterTypes.Number_of_network_interfaces, ipInfo != null ? 1 : 0);
            this.registers.WriteBytes(RegisterTypes.Primary_Application_IP_address, ipInfo?.Address.GetAddressBytes() ?? INVALID_IP); //set IP
            this.registers.WriteBytes(RegisterTypes.Current_default_Gateway_Network_interface_0, gatewayInfo?.Address.GetAddressBytes() ?? INVALID_IP);

            // Store the xml device description file in device memory somewhere, so it can be read out over
            // the wire with READMEM by default. The URLs can also be specified in memory.xml, which is
            // loaded into preSetMemory and will overwrite these defaults. That can be useful to provide
            // an Internet URL or local file path, instead of relying on device memory. Note that the URLs
            // can also be specified in a manifest table instead, in which case these URLs are ignored.
            // The file can be large (eg. 600kB - ZIP support not implemented), so pick somewhere with clear air.
            const int XML_FILE_ADDRESS = 0x01000000; //Spec says address must be aligned to 32-bit boundary.
            this.registers.WriteString(RegisterTypes.XML_Device_Description_File_First_URL,
                "Local:camera.xml;" + ToHexString(XML_FILE_ADDRESS) + ";" + ToHexString(this.xml.Length));
            // If the first fails, the second is used. But we have no other option! So just try the same.
            this.registers.WriteString(RegisterTypes.XML_Device_Description_File_Second_URL,
                "Local:camera.xml;" + ToHexString(XML_FILE_ADDRESS) + ";" + ToHexString(this.xml.Length));
            this.registers.WriteBytes(XML_FILE_ADDRESS, this.xml); //Store the file.

            //Finally, write memory.xml values over the top.
            foreach (var property in preSetMemory.Properties)
            {
                if (property.IsString)
                {
                    this.registers.WriteString(property.Register, property.StringValue!);
                }
                else if (property.IsBits)
                {
                    foreach(var bitIndex in property.Bits!)
                    {
                        this.registers.WriteBit(property.RegisterAddress, bitIndex, true);
                    }
                }
                else if (property.IsInt)
                {
                    this.registers.WriteIntBE(property.RegisterAddress, property.IntValue);
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

        private void IncommingMessage(IAsyncResult res)
        {
            var server = (UdpClient?)res.AsyncState;
            if(server == null)
            {
                Console.WriteLine("!!! Incoming Message with invalid AsyncState.");
                return;
            }

            IPEndPoint? endpoint = new IPEndPoint(IPAddress.Any, 0);
            var msg = new BufferReader(server.EndReceive(res, ref endpoint));

            var identifier = msg.ReadByte();
            if (identifier != 0x42)
            {
                server.BeginReceive(new AsyncCallback(this.IncommingMessage), server);
                return;
            }

            var flags = msg.ReadByte();
            var command = (PackageCommandType)msg.ReadWordBE();

            // check if Endpoint fits - we only response to requests directed to us
            if (command != PackageCommandType.DISCOVERY_CMD &&
                endpoint != null && IsDirectAddress(endpoint)) //Redundant null check to satisfy compiler.
            {
                server.BeginReceive(new AsyncCallback(this.IncommingMessage), server);
                return;
            }

            var length = msg.ReadWordBE();
            var req_id = msg.ReadWordBE();
            var data = new BufferReader(msg.ReadBytes((int)length));

            BufferReader? result = null;

            switch (command)
            {
                case PackageCommandType.DISCOVERY_CMD:
                    Console.WriteLine("DISCOVERY by: " + endpoint);
                    var discovery = new DiscoveryAck(req_id, this.registers);
                    result = discovery.ToBuffer();
                    break;
                case PackageCommandType.READREG_CMD:
                    Console.Write(PadTo("READREG by: " + endpoint));
                    var readReg = new ReadRegAck(req_id, this.registers, data);
                    result = readReg.ToBuffer();
                    break;
                case PackageCommandType.READMEM_CMD:
                    Console.Write(PadTo("READMEM by: " + endpoint));
                    var readMem = new ReadMemAck(req_id, this.registers, data);
                    result = readMem.ToBuffer();
                    break;
                case PackageCommandType.WRITEREG_CMD:
                    Console.Write(PadTo("WRITEREG by: " + endpoint));
                    var writeReg = new WriteRegAck(req_id, this.registers, data);
                    result = writeReg.ToBuffer();
                    break;
                    
                default:
                    Console.WriteLine("!!! Unknown GigE Command: " + command);
                    break;
            }

            server.BeginReceive(new AsyncCallback(this.IncommingMessage), server);

            if (result != null)
            {
                server.Send(result.Buffer, result.Buffer.Length, endpoint);
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
            this.server.BeginReceive(new AsyncCallback(this.IncommingMessage), this.server);
        }


        private UnicastIPAddressInformation? GetIpInfo()
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

        private string ToHexString(int num)
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
                // 
                var ipProperties = iface.GetIPProperties();
                foreach (var ip in ipProperties.UnicastAddresses)
                {
                    if ((iface.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback) && (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                    {
                        if (address == "0.0.0.0")
                        {
                            this.iface = iface;
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

        public void OnRegisterChanged(RegisterTypes register, Action<RegisterMemory> callback)
        {
            var reg = RegisterTypeHelper.RegisterByType(register);
            this.registers.AddWriteRegisterHock(reg.Address, callback);
        }


        public void OnRegisterChanged(int address, Action<RegisterMemory> callback)
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

            var ip = this.registers.ReadBytes(RegisterTypes.Stream_Channel_Destination_Address_0);
            var port = this.registers.ReadIntBE( RegisterTypes.Stream_Channel_Port_0);
            var packetSize = this.registers.ReadIntBE(RegisterTypes.Stream_Channel_Packet_Size_0);
            this.streamClient.Send(imageData, new IPAddress(ip), port, (int)packetSize);


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
    }
}
