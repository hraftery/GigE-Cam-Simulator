namespace GigE_Cam_Simulator.Streams
{
    using System;
    using System.Net;
    using System.Net.Sockets;

    internal class StreamClient
    {
        private UdpClient imageSendClient = new UdpClient();
        private int imageSendClientBlockId = 0;


        public bool Send(Byte[] data, IPAddress ip, uint port, bool doNotFragment)
        {
            var endpoint = new IPEndPoint(ip, (int)port);
            Console.WriteLine("--- send: Raw data to " + endpoint);

            imageSendClient.DontFragment = doNotFragment;
            try
            {
                imageSendClient.Send(data, data.Length, endpoint);
            }
            catch(Exception e)
            {
                Console.WriteLine("--- send: Failed to send: " + e.Message);
                return false;
            }
            return true;
        }

        public bool Send(ImageData data, IPAddress ip, uint port, uint packetSize, bool doNotFragment)
        {
            //NOTE: this assumes headers with 16-bit block_id, which was superseded in GigE Vision
            //version 2.0 by 64-bit "block_id64". A GigE Vision 2.x Receiver is not required to support
            //16-bit block_id, so this will only work with 1.x Receivers or bi-mode 2.x Receivers.
            const uint PACKET_OVERHEAD = 20 + 8 + 8; //IP header + UDP header + GVSP header
            if (packetSize <= PACKET_OVERHEAD)
            {
                Console.WriteLine("*** Attempt to send image data with packet size smaller than overhead.");
                return false;
            }

            this.imageSendClientBlockId++;

            var blockId = (uint)this.imageSendClientBlockId;

            var endpoint = new IPEndPoint(ip, (int)port);
            this.imageSendClient.DontFragment = doNotFragment;

            Console.WriteLine("--- send: Lead for " + data.Data.Length + " bytes to " + endpoint);

            try
            {
                // send Lead
                var lead = new DataLeader_ImageData(blockId, PixelFormat.GVSP_PIX_MONO8, (uint)data.Width, (uint)data.Height);
                var leadPackage = lead.ToBuffer();
                this.imageSendClient.Send(leadPackage.Buffer, leadPackage.Buffer.Length, endpoint);

                // send payload
                uint offset = 0;
                var packetId = 1;
                /*
                 Image data formatted as specified in the Data Leader pixel_format field. For Data Payload
                packets, the IP Header + UDP Header + GVSP Header + data must be equal to the packet
                size specified in the Stream Channel Packet Size register of the GVSP transmitter. The
                only exception is the last Data Payload packet which may be smaller than the specified
                packet size. However, it is also possible to pad the last data packet so that all data payload
                packets are exactly the same size.
                 */
                var chunkSize = packetSize - PACKET_OVERHEAD;

                while (offset < data.Data.Length)
                {
                    var payload = new DataPayload_ImageData(blockId, (uint)packetId);
                    var payloadLength = (uint)Math.Min(data.Data.Length - offset, chunkSize);
                    var payloadPackage = payload.ToBuffer(data.Data, (int)offset, payloadLength);
                    this.imageSendClient.Send(payloadPackage.Buffer, payloadPackage.Buffer.Length, endpoint);

                    offset += payloadLength;
                    packetId++;
                }

                Console.WriteLine("--- send: Trailer with packetId: " + packetId);

                // send trailer
                var trailer = new DataTrailer_ImageData(blockId, (uint)packetId, (uint)data.Height);
                var trailerPackage = trailer.ToBuffer();
                this.imageSendClient.Send(trailerPackage.Buffer, trailerPackage.Buffer.Length, endpoint);
            }
            catch (Exception e)
            {
                Console.WriteLine("--- send: Failed to send: " + e.Message);
                return false;
            }
            return true;
        }

    }
}
