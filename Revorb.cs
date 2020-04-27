using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static RevorbStd.Native;

namespace RevorbStd
{
    public static class Revorb
    {
        public static Stream Jiggle(Stream inputFile)
        {
            byte[] raw = new byte[inputFile.Length];
            long pos = inputFile.Position;
            inputFile.Position = 0;
            inputFile.Read(raw, 0, raw.Length);
            inputFile.Position = pos;
            return Jiggle(raw);
        }

        public static Stream Jiggle(byte[] input)
        {
            var hInput = GCHandle.Alloc(input, GCHandleType.Pinned);
            var pInput = hInput.AddrOfPinnedObject();
            REVORB_FILE inputFile = new REVORB_FILE {
                start = pInput,
                cursor = pInput,
                size = input.Length
            };

            var output = new byte[input.Length + 4096];
            var hOutput = GCHandle.Alloc(output, GCHandleType.Pinned);
            var pOutput = hOutput.AddrOfPinnedObject();
            REVORB_FILE outputFile = new REVORB_FILE
            {
                start = pOutput,
                cursor = pOutput,
                size = output.Length
            };

            //int result = revorb(ref input, ref output, _reAlloc);
            int result = revorb(ref inputFile, ref outputFile);

            hInput.Free();
            hOutput.Free();
            if (result <= REVORB_ERR_WRITE_FAIL2 && result > REVORB_ERR_SUCCESS)
            {
                throw new Exception($"Expected success, got {result} -- refer to RevorbStd.Native");
            }

            return new MemoryStream(output, 0, result);
        }

        public unsafe class RevorbStream : UnmanagedMemoryStream
        {
            private REVORB_FILE revorbFile;

            public RevorbStream(REVORB_FILE revorbFile) : base((byte*)revorbFile.start.ToPointer(), revorbFile.size)
            {
                this.revorbFile = revorbFile;
            }
            
            public new void Dispose()
            {
                base.Dispose();
                Marshal.FreeHGlobal(revorbFile.start);
            }
        }

        public static void Main(string[] args)
        {
            if (args.Contains("-t"))
            {
                Test(args[0]);
                return;
            }

            try
            {
                using (Stream file = File.OpenRead(args[0]))
                {
                    using (Stream data = Jiggle(file))
                    {
                        using (Stream outp = File.OpenWrite(args[1]))
                        {
                            data.Position = 0;
                            data.CopyTo(outp);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }
        }

        public static void Test(string path, int count = 10000, int outSize = 4096)
        {
            for (var i = 0; i < count; i++)
            {
                var raw = File.ReadAllBytes(path);
                using (var output = Jiggle(raw))
                {
                    Console.WriteLine($"#{i + 1}: {output.Length}");
                    output.Dispose();
                }
            }
        }
    }
}
