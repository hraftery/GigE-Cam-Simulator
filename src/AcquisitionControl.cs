using System;
using System.Threading;

namespace GigE_Cam_Simulator
{

    public class AcquisitionControl
    {
        /// <summary>
        /// Callback that is triggered when ever a new Image need to be acquire
        /// </summary>
        private Func<ImageData>? onAcquiesceImageCallback;

        private Timer? acquisitionTimer;

        private bool acquisitionRunning = false;
        internal void StartAcquisition(int interval, Server server)
        {
            if (this.acquisitionTimer == null)
            {
                this.acquisitionTimer = new Timer(OnAcquisitionCallback, null, Timeout.Infinite, Timeout.Infinite);
            }

            //Now this can be called from a timer, provide a very simple throttling mechanism.
            //If the last request hasn't completed, ignore any new requests.
            if (!acquisitionRunning)
            {
                acquisitionRunning = true;
                OnAcquisitionCallback(server);
                acquisitionRunning = false;
            }
        }

        private void OnAcquisitionCallback(object? server)
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

            ((Server)server!).SendStreamPacket(imageData);

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
