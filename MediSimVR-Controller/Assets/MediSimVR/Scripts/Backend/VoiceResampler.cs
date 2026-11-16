using System.Collections.Generic;

namespace MediSimVR
{
    // Streaming linear resampler: microphone rate (often 44.1/48 kHz) -> 16 kHz voice frames.
    public class VoiceResampler
    {
        private readonly double step;
        private readonly List<float> buffer = new List<float>();
        private double position;

        public VoiceResampler(int inputRate, int outputRate)
        {
            step = (double)inputRate / outputRate;
        }

        public void Push(float[] samples)
        {
            buffer.AddRange(samples);
        }

        public void Clear()
        {
            buffer.Clear();
            position = 0;
        }

        // Fills 'frame' with output samples when enough input is buffered.
        public bool TryPull(float[] frame)
        {
            double needed = position + step * frame.Length + 1;

            if (buffer.Count < needed)
            {
                return false;
            }

            for (int i = 0; i < frame.Length; i++)
            {
                int index = (int)position;
                float t = (float)(position - index);
                float a = buffer[index];
                float b = index + 1 < buffer.Count ? buffer[index + 1] : a;
                frame[i] = a + (b - a) * t;
                position += step;
            }

            int consumed = (int)position;
            buffer.RemoveRange(0, consumed);
            position -= consumed;
            return true;
        }
    }
}
