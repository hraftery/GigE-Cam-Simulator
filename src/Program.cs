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


            server.OnRegisterChanged(RegisterTypes.Stream_Channel_Packet_Size_0, (mem) =>
            {
                // mem.WriteIntBE(0x128, 17301505); // PixelFormatRegister 
                // mem.WriteIntBE(0x104, 500); // HeightRegister 
               
                //if (mem.ReadIntBE(RegisterTypes.Stream_Channel_Packet_Size_0) != 2080)
                //{
                //    mem.WriteIntBE(RegisterTypes.Stream_Channel_Packet_Size_0, 2080);
                // }
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

            Console.WriteLine("Camera Server is running...");

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