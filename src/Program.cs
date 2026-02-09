using System;
using System.Buffers.Binary;
using System.IO;
using System.Timers;

namespace GigE_Cam_Simulator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var dataPath = (args.Length > 0) ? args[0] : "data";
            var cameraXml = Path.Combine(dataPath, "camera.xml");
            var memoryXml = Path.Combine(dataPath, "memory.xml");

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


            var preSetMemory = new RegisterConfig(memoryXml);
            var server = new Server(cameraXml, preSetMemory);
            var imageIndex = -1;
            AcquisitionThread.Create(server, () =>
                {
                    imageIndex = (imageIndex + 1) % numValidImages;
                    return imageData[imageIndex];
                });


            SetRegisterChangeHandlers(server);

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

        private static void SetRegisterChangeHandlers(Server server)
        {
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
                var regVal = regMem.ReadUIntBE(eBootstrapRegister.Stream_Channel_Packet_Size_0);
                var packetSize = regVal & 0x0000FFFF;
                var roundedPacketSize = Math.Max(512, Math.Min(packetSize, 9216)); //Min/Max for GevSCPSPacketSize
                if (roundedPacketSize != packetSize)
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
                if (mem.ReadUIntBE(0x124) == 1)
                    AcquisitionThread.StartAcquisition(eAcquisitionMode.Continuous);
                else
                    AcquisitionThread.StopAcquisition();
            });

            //AcquisitionStart, at least in Teledyne DALSA Linea.
            server.OnRegisterChanged(0x12000360, (regMem) =>
            {
                //I think the value of the register doesn't matter. We just want to know when it is written too.

                //Too hard to read from trigger settings from registers at the moment, and we don't support
                //LinetStart anyway (see Timer1 event comments below). So this setting is assumed.
                AcquisitionThread.triggerFrameStartModeOn = true;
                var acqMode = BinaryPrimitives.ReverseEndianness(regMem.ReadUIntBE(0x20000040));
                AcquisitionThread.StartAcquisition((eAcquisitionMode)acqMode);
            });

            //AcquisitionStop, at least in Teledyne DALSA Linea.
            server.OnRegisterChanged(0x12000370, (mem) =>
            {
                //I think the value of the register doesn't matter. We just want to know when it is written too.
                AcquisitionThread.StopAcquisition();
            });

            //AcquisitionFrameCount (for MultiFrame AcquisitionMode), at least in Teledyne DALSA Linea.
            server.OnRegisterChanged(0x20000050, (regMem) =>
            {
                var acquisitionFrameCount = BinaryPrimitives.ReverseEndianness(regMem.ReadUIntBE(0x20000050));
                AcquisitionThread.acquisitionFrameCount = acquisitionFrameCount;
            });

            //Very specific to a certain configuration (Timer1 drives line captures) of a certain camera (Teledyne
            //DALSA Linea). Haven't figured out how to make it generic yet. The full timer implementation requires
            //a line/trigger implementation at least, and possible a counter implementation too. Tall order,
            //especially in software. So here we assume that if Timer1 is activated, the intent is to trigger an
            //acquisition.
            Timer? timer1 = null; //Note main() blocks forever so this wont go out of scope
            server.OnRegisterChanged(0x20000450, (regMem) => //Timer1 TimerMode
            {
                //Whether we're activating the timer or turning it off, we start by killing any active timer.
                //Then we either do nothing (if turning it off) or set up a new one (if activating it).
                if (timer1 != null && timer1.Enabled)
                {
                    timer1.Stop();
                    timer1.Dispose();
                }

                //The TimerActive register is typically little endian. But it doesn't matter, != 0 is != 0.
                if (regMem.ReadUIntBE(0x20000450) != 0) //active
                {
                    if (timer1 != null && timer1.Enabled)
                        return; //timer is already active. Nothing else to do.

                    //We'll assume the timer triggers LineStart acquisition on Timer1End. In that case it will
                    //take height number of timer ends to produce a frame. We further assume the client is only
                    //interested in EndOfFrame events, so don't do anything on end of line. Finally, we assume
                    //the timer end occurs after the timer delay and duration.
                    var delay_us = BinaryPrimitives.ReverseEndianness(regMem.ReadUIntBE(0x20000480));
                    var duration_us = BinaryPrimitives.ReverseEndianness(regMem.ReadUIntBE(0x200004A0));
                    var height_lines = BinaryPrimitives.ReverseEndianness(regMem.ReadUIntBE(0x20000090));
                    var endOfTimer_ms = (delay_us + duration_us) * height_lines / 1000; //eg. 5000 lines at 100us per line = 500ms.

                    timer1 = new Timer(endOfTimer_ms);
                    timer1.Elapsed += (sender, e) =>
                    {
                        AcquisitionThread.TriggerFrameStart();

                        //By default the timer is looping (AutoReset is true). If the timerStartSource is not
                        //Timer1End (25), then interrupt that and kill the timer.
                        var timerStartSource = BinaryPrimitives.ReverseEndianness(regMem.ReadUIntBE(0x20000460));
                        if (timerStartSource != 25)
                        {
                            timer1.Stop();
                            timer1.Dispose();
                        }
                    };
                    timer1.Start();
                }
            });
        }
    }
}