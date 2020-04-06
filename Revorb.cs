using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static RevorbStd.Native;

namespace RevorbStd
{
    public static class Revorb
    {
        #region ReAllocHGlobal
        public static IntPtr ReAllocHGlobal(IntPtr pv, ulong cb)
        {
            return Marshal.ReAllocHGlobal(pv, (IntPtr)cb);
        }

        private static readonly ReAlloc _reAlloc = new ReAlloc(ReAllocHGlobal);
        #endregion

        public static RevorbStream Jiggle(Stream inputFile)
        {
            byte[] raw = new byte[inputFile.Length];
            long pos = inputFile.Position;
            inputFile.Position = 0;
            inputFile.Read(raw, 0, raw.Length);
            inputFile.Position = pos;
            return Jiggle(raw);
        }

        public static RevorbStream Jiggle(byte[] raw)
        {
            var handle = GCHandle.Alloc(raw, GCHandleType.Pinned);
            var rawPtr = handle.AddrOfPinnedObject();

            REVORB_FILE input = new REVORB_FILE {
                start = rawPtr,
                size = raw.Length
            };
            input.cursor = input.start;

            IntPtr ptr = Marshal.AllocHGlobal(4096);

            REVORB_FILE output = new REVORB_FILE
            {
                start = ptr,
                size = 4096
            };
            output.cursor = output.start;

            int result = revorb(ref input, ref output, _reAlloc);

            handle.Free();
            if (result != REVORB_ERR_SUCCESS)
            {
                Marshal.FreeHGlobal(output.start);
                throw new Exception($"Expected success, got {result} -- refer to RevorbStd.Native");
            }

            // output.start to be freed by the stream
            return new RevorbStream(output);
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
