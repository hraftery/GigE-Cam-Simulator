using System;
using System.Threading;

namespace GigE_Cam_Simulator
{

    public static class AcquisitionControl
    {
        //Reference to application server instance, so we can send acquired images
        internal static Server? server;
        //Provide a callback is triggered when ever a new Image need to be acquired.
        public static Func<ImageData>? onAcquiesceImageCallback;

        private static Timer? acquisitionTimer;

        private static bool acquisitionRunning = false;
        internal static void StartAcquisition(int interval)
        {
            if (acquisitionTimer == null)
            {
                acquisitionTimer = new Timer(OnAcquisitionCallback, null, Timeout.Infinite, Timeout.Infinite);
            }

            //Now this can be called from a timer, provide a very simple throttling mechanism.
            //If the last request hasn't completed, ignore any new requests.
            if (!acquisitionRunning)
            {
                acquisitionRunning = true;
                OnAcquisitionCallback(null);
                acquisitionRunning = false;
            }
        }

        private static void OnAcquisitionCallback(object? source)
        {
            if (onAcquiesceImageCallback == null)
            {
                return;
            }

            var imageData = onAcquiesceImageCallback();
            if (imageData == null)
            {
                return;
            }

            server?.SendStreamPacket(imageData);

            return;

            // enqueue next call
            var timer = acquisitionTimer;
            if (timer != null)
            {
                timer.Change(100, Timeout.Infinite);
            }

        }

        public static void StopAcquisition()
        {
            var timer = acquisitionTimer;
            acquisitionTimer = null;
            if (timer == null)
            {
                return;
            }

            timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }
}
