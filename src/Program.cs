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

            var imageData = new ImageData[13];
            for (int i = 0; i < 13; i++)
            {
                imageData[i] = ImageData.FormFile(Path.Combine(dataPath, "left" + i.ToString().PadLeft(2, '0') + ".jpg"));
            }

            var imageIndex = 0;
            server.OnAcquiesceImage(() =>
            {
                imageIndex++;
                return imageData[imageIndex % 13];
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