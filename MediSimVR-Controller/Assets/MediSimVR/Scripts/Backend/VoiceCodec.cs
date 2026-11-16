namespace MediSimVR
{
    // Same codec as the Medisim student stations (voice relay on the PC server, UDP 5082).
    // G.711 mu-law: 16-bit speech -> 8 bits per sample (half the bandwidth), no native plugin needed.
    public static class VoiceCodec
    {
        private const int Bias = 0x84;
        private const int Clip = 32635;
        private static readonly byte[] ExponentTable = BuildExponentTable();

        public static byte Encode(float sample)
        {
            int pcm = (int)(sample * 32767f);

            if (pcm > 32767)
            {
                pcm = 32767;
            }
            else if (pcm < -32767)
            {
                pcm = -32767;
            }

            int sign = (pcm >> 8) & 0x80;

            if (sign != 0)
            {
                pcm = -pcm;
            }

            if (pcm > Clip)
            {
                pcm = Clip;
            }

            pcm += Bias;
            int exponent = ExponentTable[(pcm >> 7) & 0xFF];
            int mantissa = (pcm >> (exponent + 3)) & 0x0F;
            return (byte)~(sign | (exponent << 4) | mantissa);
        }

        public static float Decode(byte value)
        {
            int mu = ~value & 0xFF;
            int sign = mu & 0x80;
            int exponent = (mu >> 4) & 0x07;
            int mantissa = mu & 0x0F;
            int pcm = (((mantissa << 3) + Bias) << exponent) - Bias;
            return (sign != 0 ? -pcm : pcm) / 32768f;
        }

        private static byte[] BuildExponentTable()
        {
            byte[] table = new byte[256];

            for (int i = 0; i < 256; i++)
            {
                int value = i;
                byte exponent = 0;

                while (value > 1)
                {
                    value >>= 1;
                    exponent++;
                }

                table[i] = exponent;
            }

            return table;
        }
    }
}
