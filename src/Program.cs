using System;
using System.IO;

namespace GigE_Cam_Simulator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var dataPath = (args.Length > 0) ? args[0] : "data";
            var cameraXml = Path.Combine(dataPath, "camera.xml");
            var memoryXml = Path.Combine(dataPath, "memory.xml");

            var preSetMemory = new RegisterConfig(memoryXml);

            var server = new Server(cameraXml, preSetMemory);


            server.OnRegisterChanged(eBootstrapRegister.Stream_Channel_Packet_Size_0, (regMem) =>
            {
                //The spec is bit unclear on handling unsupported packet sizes. It says:
                //  "When a GVSP transmitter cannot support the requested packet_size, then it MUST NOT fire
                //   a test packet when requested to do so. Also, it MUST round down the packet_size to the
                //   nearest supported value and update the register."
                //I now believe we have to *first* decide whether the requested packet size is "supported",
                //and only then consider the fire test packet bit. If it is not supported, we must "round
                //down to the nearest supported value". Since we don't actually know what the *network* will
                //support, we can only act on what *we* support. Alas, as a simulator, we don't have a way
                //to specify that, unless we add config functionality and get the user to specify.
                //So for now, use the Min/Max values hard-coded into the Teledyne DALSA Symphony camera.xml:
                bool isSupportedPacketSize = true; //by default
                var regVal = regMem.ReadIntBE(eBootstrapRegister.Stream_Channel_Packet_Size_0);
                var packetSize = regVal & 0x0000FFFF;
                var roundedPacketSize = Math.Max(512, Math.Min(packetSize, 9216)); //Min/Max for GevSCPSPacketSize
                if(roundedPacketSize != packetSize)
                {
                    isSupportedPacketSize = false;
                    uint newVal = (regVal & 0xFFFF0000) | roundedPacketSize; //Packet size is in lower two bytes.
                    //Quietly write it back - the application must then read to see the result
                    regMem.WriteIntBE(eBootstrapRegister.Stream_Channel_Packet_Size_0, (int)newVal);
                }

                const uint FIRE_TEST_PACKET_BIT = 0;
                bool F = regMem.ReadBit(eBootstrapRegister.Stream_Channel_Packet_Size_0, FIRE_TEST_PACKET_BIT);
                if (F) //then fire one test packet of size provided in register (if valid), and auto-clear
                {
                    //Note the send function can return false and the packet can fail to send if it's too big
                    //to fit in the MTU. But there's I can't see a way to know what will work without sending
                    //at least one successful test packet! We could round down to the maximum safe UDP
                    //payload: https://stackoverflow.com/a/35697810/3697870
                    //But in the spec section 11.5 "Packet Size Negotiation", the expectation seems to be that
                    //the client tries various sizes until one gets through. So all we have to do is let it
                    //succeed or quietly fail.
                    if (isSupportedPacketSize)
                        server.SendStreamPacket(null);
                    
                    regMem.WriteBit(eBootstrapRegister.Stream_Channel_Packet_Size_0, FIRE_TEST_PACKET_BIT, false);
                }
            });

            // on TriggerSoftware
            server.OnRegisterChanged(0x30c, (mem) =>
            {
                if (mem.ReadIntBE(0x124) == 1)
                {
                    Console.WriteLine("--- StartAcquisition");
                    server.StartAcquisition(100);
                }
                else
                {
                    Console.WriteLine("--- StopAcquisition");
                    server.StopAcquisition();
                }
            });

            //The order returned is "not guaranteed" but always seems to be alphabetical. Getting a logical ordering
            //like used in any modern file browser doesn't look easy, or depends on Windows specific libraries.
            //Ref: https://learn.microsoft.com/en-us/windows/win32/api/shlwapi/nf-shlwapi-strcmplogicalw
            //So make do with alphabetical and user can just zero pad the frame number (frame01, frame02, etc.).
            string[] imageFiles = Directory.GetFiles(dataPath, "frame*");
            var imageData = new ImageData[imageFiles.Length];

            var numValidImages = 0;
            foreach (var imageFile in imageFiles)
            {
                //Console.WriteLine("Processing: " + imageFile);
                var theData = ImageData.FromFile(imageFile);
                if (theData != null)
                    imageData[numValidImages++] = theData;
            }
            Console.WriteLine("Loaded " + numValidImages.ToString() + " frame images.");

            var imageIndex = -1;
            server.OnAcquiesceImage(() =>
            {
                imageIndex = (imageIndex+1) % numValidImages;
                return imageData[imageIndex];
            });

            server.Run();
            var ipInfo = server.GetIpInfo();

            if(ipInfo == null)
                Console.WriteLine("Camera Server is running, but did not get an IP.");
            else
                Console.WriteLine("Camera Server is running on " + ipInfo.Address.ToString() + "...");

            if (!Console.IsInputRedirected)
                Console.ReadLine();
            else
                //When running in docker without an interactive TTY attached (ie. without "-it" flags),
                //there is no stdin to wait on. So instead, wait until we're killed.
                System.Threading.Thread.Sleep(System.Threading.Timeout.InfiniteTimeSpan);

            Console.WriteLine("Exiting...");
        }
    }
}