using Silk.NET.OpenAL;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace SilkNETDemo
{
    internal class Program
    {
        public static unsafe void Main()
        {
            string path = "sample.wav";
            ReadOnlySpan<byte> file = File.ReadAllBytes(path);

            int cur = 0;
            if (Encoding.ASCII.GetString(file.Slice(cur, 4)) != "RIFF")
            {
                throw new Exception($"Given file is not in RIFF format");
            }
            cur += 4;

            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(cur, 4));
            cur += 4;

            if (Encoding.ASCII.GetString(file.Slice(cur, 4)) != "WAVE")
            {
                throw new Exception($"Given file is not in WAVE format");
            }
            cur += 4;

            short numChannels = -1;
            int sampleRate = -1;
            int byteRate = -1;
            short blockAlign = -1;
            short bitsPerSample = -1;
            BufferFormat format = 0;

            ALContext alc = ALContext.GetApi();
            AL al = AL.GetApi();
            Device* device = alc.OpenDevice("");

            if (device == null)
            {
                throw new AudioDeviceException("Could not open audio device");
            }

            Context* context = alc.CreateContext(device, null);
            alc.MakeContextCurrent(context);

            AudioError err = al.GetError();
            if (err != AudioError.NoError)
            {
                throw new IOException($"AudioError: {err}");
            }

            uint source = al.GenSource();
            uint buffer = al.GenBuffer();

            al.SetSourceProperty(source, SourceBoolean.Looping, true);

            while (cur + 4 < file.Length)
            {
                string identifier = Encoding.ASCII.GetString(file.Slice(cur, 4));
                cur += 4;
                var size = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(cur, 4));
                cur += 4;
                if (identifier == "fmt ")
                {
                    if (size != 16)
                    {
                        throw new IOException($"Unknown Audio Format with subchink1 size {size}");
                    }
                    else
                    {
                        short audioFormat = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(cur, 2));
                        cur += 2;
                        if (audioFormat != 1)
                        {
                            throw new AudioException($"Unknown audio format with ID {audioFormat}");
                        }

                        numChannels = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(cur, 2));
                        cur += 2;
                        sampleRate = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(cur, 4));
                        cur += 4;
                        byteRate = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(cur, 4));
                        cur += 4;
                        blockAlign = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(cur, 2));
                        cur += 2;
                        bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(file.Slice(cur, 2));
                        cur += 2;

                        format = numChannels switch
                        {
                            1 => bitsPerSample switch
                            {
                                8 => BufferFormat.Mono8,
                                16 => BufferFormat.Mono16,
                                _ => throw new AudioException($"Can't play mono with {bitsPerSample} bits per sample.")
                            },
                            2 => bitsPerSample switch
                            {
                                8 => BufferFormat.Stereo8,
                                16 => BufferFormat.Stereo16,
                                _ => throw new AudioException($"Can't play stereo with {bitsPerSample} bits per sample.")
                            },
                            _ => throw new AudioException($"Can't play audio with {numChannels} channels.")
                        };                       
                    }
                }
                else if (identifier == "data")
                {
                    var data = file.Slice(44, size);
                    cur += size;
                    fixed (byte* pData = data)
                        al.BufferData(buffer, format, pData, size, sampleRate);
                    Console.WriteLine($"Read {size} bytes of data into buffer");
                }
                else if (identifier == "JUNK")
                {
                    cur += size;
                }
                else if (identifier == "iXML")
                {
                    ReadOnlySpan<byte> v = file.Slice(cur, size);
                    string str = Encoding.ASCII.GetString(v);
                    Console.WriteLine($"iXML Chunk: {str}");
                    cur += size;
                }
                else
                {
                    Console.WriteLine($"Unknown section: {identifier}");
                    cur += size;
                }
            }

            Console.WriteLine($"Success. Detected RIFF-WAVE audio file, PCM encoding. {numChannels} channels, {sampleRate} sample rate, {byteRate} byte rate, {blockAlign} block align, {bitsPerSample} bits per sample.");

            al.SetSourceProperty(source, SourceInteger.Buffer, buffer);
            al.SourcePlay(source);

            Console.WriteLine(
                "Playing audio in loop.\n" +
                "Press enter to exit...");
            Console.ReadLine();

            al.SourceStop(source);
            al.DeleteSource(source);
            al.DeleteBuffer(buffer);
            alc.DestroyContext(context);
            alc.CloseDevice(device);
            al.Dispose();
            alc.Dispose();
        }
    }
}