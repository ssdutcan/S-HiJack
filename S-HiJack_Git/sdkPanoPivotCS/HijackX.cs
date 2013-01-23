using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System.Windows.Threading;

namespace HiJack
{
    public delegate void DataReadyEventHandler(object sender, DataReadyEventArgs e);

    public class HijackX : MediaStreamSource
    {
        enum uart_state
        {
            STARTBIT = 0,
            SAMEBIT = 1,
            NEXTBIT = 2,
            STOPBIT = 3,
            STARTBIT_FALL = 4,
            DECODE = 5,
        };
        const int SAMPLESPERBIT = 32;
        const int THRESHOLD = 0;
        const double HIGHFREQ = 1378.125;  //  the frequency 
        const double LOWFREQ = HIGHFREQ / 2;
        const int SHORT = SAMPLESPERBIT / 2 + SAMPLESPERBIT / 4;
        const int LONG = SAMPLESPERBIT + SAMPLESPERBIT / 2;
        const int NUMSTOPBITS = 100;
        const int AMPLITUDE = (1 << 24);

        UInt32 phaseEnc = 0;
        UInt32 nextPhaseEnc = SAMPLESPERBIT;
        Byte uartByteTx = 0x0;
        int uartBitTx = 0;
        Byte state = (Byte)uart_state.STARTBIT;
        float[]uartBitEnc;
        Byte currentBit = 1;
        Byte parityTx = 0;
        bool newByte = false;
        Byte uartByteTransmit;
        int byteCounter = 0;

        // Standard constants
        const int ChannelCount = 1;
        const int BitsPerSample = 16;
        // ying const int BufferSamples = 4096;     // can be changed
        const int BufferSamples = 4410;
        const int BufferSize = ChannelCount * BufferSamples * BitsPerSample / 8;
        Dictionary<MediaSampleAttributeKeys, string> mediaSampleAttributes;
        double angleIncrement;
        int sampleRate;
        long timestamp;
        MediaStreamDescription mediaStreamDescription;
        MemoryStream memoryStream;

        // ying - 1 microphone
/*
        //enum uart_state
        //{
        //    STARTBIT = 0,
        //    SAMEBIT = 1,
        //    NEXTBIT = 2,
        //    STOPBIT = 3,
        //    STARTBIT_FALL = 4,
        //    DECODE = 5,
        //};

        //private int SAMPLESPERBIT; // = 32;
        //const int THRESHOLD = 0;
        //const double HIGHFREQ = 1378.125;  //  the frequency 
        //const double LOWFREQ = HIGHFREQ / 2;
        //const int NUMSTOPBITS = 100;
        //const int AMPLITUDE = (1 << 24);
*/
        private int SAMPLESPERBIT_MICROPHONE; // = 32;
        private int SHORT_MICROPHONE; // = SAMPLESPERBIT / 2 + SAMPLESPERBIT / 4;
        private int LONG_MICROPHONE; // = SAMPLESPERBIT + SAMPLESPERBIT / 2;
        private UInt32 phase2 = 0;
        private UInt32 lastPhase2 = 0;
        private Byte sample = 0;
        private Int32 lastSample = 0;
        private uart_state decState = uart_state.STARTBIT;
        private Byte parityRx = 0;
        private int bitNum = 0;
        private Byte uartByte = 0;

        private Microphone microphone = Microphone.Default;     // Object representing the physical microphone on the device
        private byte[] buffer;                                  // Dynamic buffer to retrieve audio data from the microphone
        //private MemoryStream stream = new MemoryStream();       // Stores the audio data for later playback
        //private SoundEffectInstance soundInstance;              // Used to play back audio
        //private bool soundIsPlaying = false;                    // Flag to monitor the state of sound playback
        // ying - 1 end


        public HijackX(int sampleRate)
        {
            this.sampleRate = sampleRate;
            // ying
            uartBitEnc = new float[SAMPLESPERBIT];
            // Want to play A = 400Hz
            // ying angleIncrement = 2 * Math.PI * 440 / sampleRate;
            angleIncrement = 2 * Math.PI * HIGHFREQ / sampleRate;
            // Create empty mediaSampleAttributes dictionary for OpenReadAsync
            mediaSampleAttributes = new Dictionary<MediaSampleAttributeKeys, string>();

            // Create re-usable MemoryStream for accumulating audio samples
            memoryStream = new MemoryStream();

            // micphone
            DispatcherTimer dt = new DispatcherTimer();
            dt.Interval = TimeSpan.FromMilliseconds(33);
            dt.Tick += new EventHandler(dt_Tick);
            dt.Start();

            SAMPLESPERBIT_MICROPHONE = (int)(microphone.SampleRate / HIGHFREQ + 0.5);
            SHORT_MICROPHONE = SAMPLESPERBIT_MICROPHONE / 2 + SAMPLESPERBIT_MICROPHONE / 4;
            LONG_MICROPHONE = SAMPLESPERBIT_MICROPHONE + SAMPLESPERBIT_MICROPHONE / 2;
            // Event handler for getting audio data when the buffer is full
            microphone.BufferReady += new EventHandler<EventArgs>(microphone_BufferReady);
            microphone.BufferDuration = TimeSpan.FromMilliseconds(1000);
            // Allocate memory to hold the audio data
            buffer = new byte[microphone.GetSampleSizeInBytes(microphone.BufferDuration)];
            // Start recording
            microphone.Start();
        }

        public event DataReadyEventHandler DataReady;

        public int sendByte(Byte byteToSend){

            // test ying 
            //DataReady(this, new DataReadyEventArgs(123));

            if (this.newByte == false) {
                // transmitter ready
                this.uartByteTransmit = byteToSend;
                this.newByte = true;
                return 0;
            }
            else {
                return 1;
            }
        }

        protected override void OpenMediaAsync()
        {
            int byteRate = sampleRate * ChannelCount * BitsPerSample / 8;
            short blockAlign = (short)(ChannelCount * (BitsPerSample / 8));

            // Build string-based wave-format structure
            string waveFormat = "";
            waveFormat += ToLittleEndianString(string.Format("{0:X4}", 1));      // indicates PCM
            waveFormat += ToLittleEndianString(string.Format("{0:X4}", ChannelCount));
            waveFormat += ToLittleEndianString(string.Format("{0:X8}", sampleRate));
            waveFormat += ToLittleEndianString(string.Format("{0:X8}", byteRate));
            waveFormat += ToLittleEndianString(string.Format("{0:X4}", blockAlign));
            waveFormat += ToLittleEndianString(string.Format("{0:X4}", BitsPerSample));
            waveFormat += ToLittleEndianString(string.Format("{0:X4}", 0));

            // Put wave format string in media streams dictionary
            var mediaStreamAttributes = new Dictionary<MediaStreamAttributeKeys, string>();
            mediaStreamAttributes[MediaStreamAttributeKeys.CodecPrivateData] = waveFormat;

            // Make description to add to available streams list
            var availableMediaStreams = new List<MediaStreamDescription>();
            mediaStreamDescription = new MediaStreamDescription(MediaStreamType.Audio, mediaStreamAttributes);
            availableMediaStreams.Add(mediaStreamDescription);

            // Set some appropriate keys in the media source dictionary
            var mediaSourceAttributes = new Dictionary<MediaSourceAttributesKeys, string>();
            mediaSourceAttributes[MediaSourceAttributesKeys.Duration] = "0";
            mediaSourceAttributes[MediaSourceAttributesKeys.CanSeek] = "false";

            // Signal that the open operation is completed
            ReportOpenMediaCompleted(mediaSourceAttributes, availableMediaStreams);
        }

        // For building string-based wave-format structure
        private string ToLittleEndianString(string bigEndianString)
        {
            StringBuilder strBuilder = new StringBuilder();

            for (int i = 0; i < bigEndianString.Length; i += 2)
                strBuilder.Insert(0, bigEndianString.Substring(i, 2));

            return strBuilder.ToString();
        }

        // Provides audio samples from AudioSampleProvider property.
        //  (MediaStreamType parameter will always equal Audio.)
        protected override void GetSampleAsync(MediaStreamType mediaStreamType)
        {

            // Reset MemoryStream object
            memoryStream.Seek(0, SeekOrigin.Begin);

            for (int sample = 0; sample < BufferSamples; sample++)
            {
                //if (sample % 200 == 0){
                //    sendByte(0x10);
                //}
                // ying short amplitude = (short)(short.MaxValue * Math.Sin(angle));  
                // ying memoryStream.WriteByte((byte)(amplitude & 0xFF));
                // ying memoryStream.WriteByte((byte)(amplitude >> 8));
                // ying angle = (angle + angleIncrement) % (2 * Math.PI);
                if (this.phaseEnc >= this.nextPhaseEnc) {
                    if (this.uartBitTx >= NUMSTOPBITS && this.newByte == true) {
                        this.state = (Byte)uart_state.STARTBIT;
                        this.newByte = false;
                    }
                    else {
                        this.state = (Byte)uart_state.NEXTBIT;
                    }
                }
                // ying break UNCOMMENTED is unaccepted
                if ((Byte)uart_state.STARTBIT == this.state) {
                    uartByteTx = this.uartByteTransmit;
                    //printf("uartByteTx: 0x%x\n", uartByteTx);
                    byteCounter += 1;
                    uartBitTx = 0;
                    parityTx = 0;
                    state = (Byte)uart_state.NEXTBIT;
                }

                switch (this.state)
                {
                    /* ying break UNCOMMENTED is unaccepted
                    case (Byte)uart_state.STARTBIT:
                        {
                            uartByteTx = this.uartByteTransmit;
                            //printf("uartByteTx: 0x%x\n", uartByteTx);
                            byteCounter += 1;
                            uartBitTx = 0;
                            parityTx = 0;
                            state = (Byte)uart_state.NEXTBIT;
                            // break;  UNCOMMENTED ON PURPOSE
                        }*/
                    case (Byte)uart_state.NEXTBIT:
                        {
                            Byte nextBit;
                            if (uartByteTx == 0) {
                                // start bit
                                nextBit = 0;
                            }
                            else
                            {
                                if (this.uartBitTx == 9) {
                                    // parity bit
                                    nextBit = (Byte)(parityTx & 0x01);
                                }
                                else if (this.uartBitTx >= 10) {
                                    // stop bit
                                    nextBit = 1;
                                }
                                else {
                                    nextBit = (Byte)((uartByteTx >> (uartBitTx - 1)) & 0x01);
                                    parityTx += nextBit;
                                }
                            }
                            if (nextBit == currentBit) {
                                if (nextBit == 0) {
                                    for (int p = 0; p < SAMPLESPERBIT; p++) {
                                        //uartBitEnc[p] = -sin(M_PI * 2.0f / THIS->hwSampleRate * HIGHFREQ * (p + 1));
                                        uartBitEnc[p] = (float)(short.MaxValue * (-1) * Math.Sin(angleIncrement * (p + 1)));
                                        //angleIncrement = 2 * Math.PI * HIGHFREQ / sampleRate;
                                        //angle = (angle + angleIncrement) % (2 * Math.PI);
                                    }
                                }
                                else {
                                    for (int p = 0; p < SAMPLESPERBIT; p++) {
                                        //uartBitEnc[p] = sin(M_PI * 2.0f / THIS->hwSampleRate * HIGHFREQ * (p + 1));
                                        uartBitEnc[p] = (float)(short.MaxValue * Math.Sin(angleIncrement * (p + 1)));
                                       // angle = (angle + angleIncrement) % (2 * Math.PI);
                                    }
                                }
                            }
                            else {
                                if (nextBit == 0) {
                                    for (int p = 0; p < SAMPLESPERBIT; p++) {
                                        //uartBitEnc[p] = -sin(M_PI * 2.0f / THIS->hwSampleRate * HIGHFREQ * (p + 1));
                                        uartBitEnc[p] = (float)(short.MaxValue * Math.Sin(angleIncrement / 2 * (p + 1)));
                                        //angle = (angle + angleIncrement/2) % (2 * Math.PI);
                                    }
                                }
                                else {
                                    for (int p = 0; p < SAMPLESPERBIT; p++) {
                                        //uartBitEnc[p] = sin(M_PI * 2.0f / THIS->hwSampleRate * HIGHFREQ * (p + 1));
                                        uartBitEnc[p] = (float)(short.MaxValue * (-1) * Math.Sin(angleIncrement / 2 * (p + 1)));
                                        //angle = (angle + angleIncrement/2) % (2 * Math.PI);
                                    }
                                }
                            }

                            currentBit = nextBit;
                            uartBitTx++;
                            state = (Byte)uart_state.SAMEBIT;
                            phaseEnc = 0;
                            nextPhaseEnc = SAMPLESPERBIT;

                            break;
                        }
                    default:
                        break;
                }
                //  ying 注意
                short amplitude = (short)(uartBitEnc[phaseEnc % SAMPLESPERBIT]);  
                memoryStream.WriteByte((byte)(amplitude & 0xFF));
                memoryStream.WriteByte((byte)(amplitude >> 8));
                phaseEnc++;
            }

            // Send out the sample
            ReportGetSampleCompleted(new MediaStreamSample(mediaStreamDescription,
                                                            memoryStream,
                                                            0,
                                                            BufferSize,
                                                            timestamp,
                                                            mediaSampleAttributes));
            // Prepare for next sample
            timestamp += BufferSamples * 10000000L / sampleRate;
        }

        protected override void SeekAsync(long seekToTime)
        {
            ReportSeekCompleted(seekToTime);
        }

        protected override void CloseMedia()
        {
            mediaStreamDescription = null;
        }

        // Shouldn't get a call here because only one MediaStreamDescription is supported
        protected override void SwitchMediaStreamAsync(MediaStreamDescription mediaStreamDescription)
        {
            throw new NotImplementedException();
        }

        protected override void GetDiagnosticAsync(MediaStreamSourceDiagnosticKind diagnosticKind)
        {
            throw new NotImplementedException();
        }

        // microphone
        /// <summary>
        /// The Microphone.BufferReady event handler.
        /// Gets the audio data from the microphone and stores it in a buffer,
        /// then writes that buffer to a stream for later playback.
        /// Any action in this event handler should be quick!
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void microphone_BufferReady(object sender, EventArgs e)
        {
            // Retrieve audio data
            microphone.GetData(buffer);
            // ying - 2
            for (int j = 0; j < buffer.Length; j += 2)
            {
                Int16 val = (Int16)((buffer[1 + j] << (Int16)8) | buffer[j]);

                phase2 += 1;
                if (val < THRESHOLD)
                {
                    sample = 1;// sample = 0;// due to the hardware.
                }
                else
                {
                    sample = 0;// sample = 1;
                }
                if (sample != lastSample)
                {
                    int diff = (int)(phase2 - lastPhase2);
                    switch (decState)
                    {
                        case uart_state.STARTBIT:
                            if (lastSample == 0 && sample == 1)
                            {
                                // low->high transition. Now wait for a long period
                                decState = uart_state.STARTBIT_FALL;
                            }
                            break;
                        case uart_state.STARTBIT_FALL:
                            if ((SHORT_MICROPHONE < diff) && (diff < LONG_MICROPHONE))
                            {
                                // looks like we got a 1->0 transition
                                bitNum = 0;
                                parityRx = 0;
                                uartByte = 0;
                                decState = uart_state.DECODE;
                            }
                            else
                            {
                                decState = uart_state.STARTBIT;
                            }
                            break;
                        case uart_state.DECODE:
                            if ((SHORT_MICROPHONE < diff) && (diff < LONG_MICROPHONE))
                            {   // got a valid sample/
                                if (bitNum < 8)
                                {
                                    uartByte = (Byte)((uartByte >> 1) + (sample << 7));
                                    bitNum++;
                                    parityRx += sample;
                                }
                                else if (bitNum == 8)
                                {
                                    // parity bit
                                    if (sample != (parityRx & 0x01))
                                    {
                                        //bad parity
                                        decState = uart_state.STARTBIT;
                                    }
                                    else
                                    {
                                        // good parity
                                        bitNum++;
                                    }
                                }
                                else
                                {
                                    // the stopbit
                                    if (sample == 1)
                                    {
                                        // a new and valid byte
                                        DataReady(this, new DataReadyEventArgs(uartByte));
                                    }
                                    decState = uart_state.STARTBIT;
                                }
                            }
                            else if (diff > LONG_MICROPHONE)
                            {
                                decState = uart_state.STARTBIT;
                            }
                            else
                            {
                                lastSample = sample;
                                continue;
                            }
                            break;
                        default:
                            break;
                    }
                    lastPhase2 = phase2;
                }
                lastSample = sample;
            }
            // ying - 2 end
            // Store the audio data in a stream
            //stream.Write(buffer, 0, buffer.Length);
        }

        void dt_Tick(object sender, EventArgs e)
        {
            try { FrameworkDispatcher.Update(); }
            catch { }
        }
    }
}
