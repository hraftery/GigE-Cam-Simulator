using System;
using System.Threading;

namespace GigE_Cam_Simulator
{
    //These are defined in GenICam Standard Features Naming Convention (SFNC)
    public enum eAcquisitionMode
    {
        Continuous,     //Mandatory for GigE Vision cameras
        SingleFrame,    
        MultiFrame
    }

    public static class AcquisitionControl
    {
        //Reference to application server instance, so we can send acquired images
        internal static Server? server;
        //Provide a callback is triggered when ever a new Image need to be acquired.
        public static Func<ImageData>? onAcquiesceImageCallback;
        //Provide a frame rate for continuous/multi-frame mode. Does not affect an active acquisition.
        public static uint interFramePeriodMilliseconds = 1000;
        //Set to the number of frames to acquire in MultiFrame mode
        public static uint acquisitionFrameCount = 10;


        //Use a long living timer and just start/rearm/stop it as required.
        private static Timer acquisitionTimer = new Timer(AcquireFrame, null, Timeout.Infinite, Timeout.Infinite);
        private static uint currentFrameCount;
        private static bool stopAtFrameCount;
        internal static void StartAcquisition(eAcquisitionMode mode)
        {
            currentFrameCount = 0;

            if (mode == eAcquisitionMode.Continuous || mode == eAcquisitionMode.MultiFrame)
            {
                acquisitionTimer.Change(interFramePeriodMilliseconds, interFramePeriodMilliseconds);
                stopAtFrameCount = (mode == eAcquisitionMode.MultiFrame);
            }
            
            //Kick the first frame off right away.
            AcquireFrame(null);
        }

        private static bool frameAcquisitionInProgress = false;
        private static void AcquireFrame(object? state)
        {
            //Provide a very simple throttling mechanism. If the last
            //frame acquisition hasn't completed, ignore any new requests.
            //Also skip if a way to get a frame hasn't been provided.
            if (frameAcquisitionInProgress || onAcquiesceImageCallback == null)
                return;

            frameAcquisitionInProgress = true;

            var imageData = onAcquiesceImageCallback();
            if (imageData == null)
                Console.WriteLine("!!! Could not acquire frame.");
            else if(server == null)
                Console.WriteLine("!!! No server available to send acquired frame.");
            else
                server.SendStreamPacket(imageData);

            currentFrameCount++;
            if (stopAtFrameCount && currentFrameCount >= acquisitionFrameCount)
                StopAcquisition();

            frameAcquisitionInProgress = false;
        }

        public static void StopAcquisition()
        {
            acquisitionTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }
}
