using System;
using System.Threading;

namespace GigE_Cam_Simulator
{
    //These are defined in GenICam Standard Features Naming Convention (SFNC)
    public enum eAcquisitionMode
    {
        Continuous  = 0,    //Mandatory for GigE Vision cameras
        SingleFrame = 1,    //Values seem typical, but not guaranteed.
        MultiFrame  = 2
    }

    public static class AcquisitionThread
    {
        //Set to the number of frames to acquire in MultiFrame mode
        public static uint acquisitionFrameCount = 10; //As per GenICam standard
        //Set to true to wait for a trigger before acquiring one or more frames
        public static bool triggerAcquisitionStartModeOn = false; //As per GenICam standard
        //Set to true to wait for a trigger before acquiring a frame
        public static bool triggerFrameStartModeOn = false; //As per GenICam standard

        //Specify a maximum frame rate for continuous/multi-frame mode.
        public static uint minimumFramePeriodMilliseconds = 1000;

        //Reference to application server instance, so we can send acquired images
        private static Server? server;
        //Callback that is triggered when ever a new Image need to be acquired.
        private static Func<ImageData>? onAcquiesceImageCallback;

        private static Thread theThread = new Thread(ThreadFunc);
        private static ManualResetEvent acquisitionStartFlag = new ManualResetEvent(false);
        private static AutoResetEvent triggerAcquisitionStartFlag = new AutoResetEvent(false);
        private static AutoResetEvent triggerFrameStartFlag = new AutoResetEvent(false);
        private static bool resetThread = true;

        private static eAcquisitionMode currentMode;
        private static uint currentFrameCount;

        internal static void Create(Server theServer, Func<ImageData> theAcquiesceImageCallback)
        {
            server = theServer;
            onAcquiesceImageCallback = theAcquiesceImageCallback;

            theThread.IsBackground = true; //Process can still end even if thread is running.
            theThread.Start();
        }

        internal static void TriggerAcquisitionStart()
        {
            triggerAcquisitionStartFlag.Set();
        }

        internal static void TriggerFrameStart()
        {
            triggerFrameStartFlag.Set();
        }
        internal static void StartAcquisition(eAcquisitionMode mode)
        {
            if(!theThread.IsAlive)
            {
                Console.WriteLine("!!! Acquisition Thread Must be created first.");
                return;
            }

            StopAcquisition(); //interrupt the current one, if any

            currentFrameCount = 0;
            currentMode = mode;

            acquisitionStartFlag.Set();
        }

        internal static void StopAcquisition()
        {
            acquisitionStartFlag.Reset();
            //Now force the thread loop to get back to the start
            resetThread = true;
            triggerAcquisitionStartFlag.Set();
            triggerFrameStartFlag.Set();
        }

        //
        //           ThreadFunc Flow Chart
        //
        //                  (START)
        //                     |_____________________
        //                     |                     |
        //            [Wait for StartAcq]            |
        //                     |                     |
        //    [Wait for Trigger: AcquisitionStart]   |
        //                     |_____________________|______
        //                     |                     |      |
        //       [Wait for Trigger: FrameStart]      |      |
        //                     |                     ^      |
        //         [Acquire Frame, Transport]        |      |
        //                     |                     |      |
        //               /   Single  \               |      |
        //              /      or     \______________|      ^
        //              \  completed  /      Yes            |
        //               \MultiFrame?/                      |
        //                     |                            |
        //       [Delay minimum frame period]               |
        //                     |                            |
        //                     |____________________________|
        //
        // Triggers are only waited on if they are enabled.
        // And at any point, "StopAcquisition()" will return the thread to the top.
        // To avoid gotos, this is done in one big loop, by checking for "resetThread" at
        // every step. Steps are skipped based on "resetThread" to satisfy the diagram.


        private static void ThreadFunc()
        {
            while (true)
            {
                if (resetThread)
                {
                    resetThread = false; //thread is now reset
                    acquisitionStartFlag.WaitOne();

                    if (!resetThread && triggerAcquisitionStartModeOn)
                        triggerAcquisitionStartFlag.WaitOne();
                }

                if (!resetThread && triggerFrameStartModeOn)
                    triggerFrameStartFlag.WaitOne();


                if (!resetThread)
                {
                    var imageData = onAcquiesceImageCallback!();
                    if (imageData == null)
                    {
                        Console.WriteLine("!!! Could not acquire frame.");
                    }
                    else
                    {
                        server!.SendStreamPacket(imageData);
                        currentFrameCount++;
                        if (currentMode == eAcquisitionMode.SingleFrame ||
                            (currentMode == eAcquisitionMode.MultiFrame && currentFrameCount >= acquisitionFrameCount))
                            StopAcquisition();
                    }
                }

                if (!resetThread)
                    Thread.Sleep((int)minimumFramePeriodMilliseconds); //TODO: rename to "minimum" period
            }
        }

    }
}
