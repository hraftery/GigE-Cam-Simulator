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
        //As per GenICam standard. Set to the number of frames to acquire in MultiFrame mode.
        public static uint acquisitionFrameCount = 10; //Initial value is arbitrary.
        //As per GenICam standard. Set to true to wait for a trigger before acquiring one or more frames.
        public static bool triggerAcquisitionStartModeOn = false; //Start off.
        //As per GenICam standard. Set to true to wait for a trigger before acquiring a frame.
        public static bool triggerFrameStartModeOn = false; //Start off.

        //Specify a maximum frame rate for continuous/multi-frame mode.
        public static uint minimumFramePeriodMilliseconds = 1000;

        //Reference to application server instance, so we can send acquired images
        private static Server? server;
        //Callback that is triggered when ever a new Image need to be acquired.
        private static Func<ImageData>? onAcquiesceImageCallback;

        private static readonly Thread theThread = new(ThreadFunc);
        private static ManualResetEvent acquisitionStartFlag        = new(false);
        private static AutoResetEvent triggerAcquisitionStartFlag   = new(false);
        private static AutoResetEvent triggerFrameStartFlag         = new(false);
        private static bool resetThread = true;
        //Small efficiency improvement - only need to stop before start if we haven't stopped the last start already.
        private static bool startWithoutAStop = false;

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

            if(startWithoutAStop)
                StopAcquisition(); //interrupt the current one

            currentFrameCount = 0;
            currentMode = mode;
            startWithoutAStop = true;

            Console.WriteLine("--- StartAcquisition");
            acquisitionStartFlag.Set();
        }

        internal static void StopAcquisition()
        {
            Console.WriteLine("--- StopAcquisition");
            acquisitionStartFlag.Reset();
            //Now force the thread loop to get back to the start
            resetThread = true;
            triggerAcquisitionStartFlag.Set();
            triggerFrameStartFlag.Set();
            startWithoutAStop = false;
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
                    triggerAcquisitionStartFlag.Reset();
                    triggerFrameStartFlag.Reset();
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
                    Thread.Sleep((int)minimumFramePeriodMilliseconds);
            }
        }
    }
}
