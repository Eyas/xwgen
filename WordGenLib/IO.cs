using Crossword.Proto;

namespace WordGenLib
{
    public class IO
    {
        public static void Write(Pack pack, string path)
        {
            using var fileStream = File.OpenWrite(path);
            Write(pack, fileStream);
        }

        public static void Write(Pack pack, Stream outputStream)
        {
            using var output = new Google.Protobuf.CodedOutputStream(outputStream);
            pack.WriteTo(output);
        }

        public static Pack Read(string path)
        {
            using var fileStream = File.OpenRead(path);
            using var input = new Google.Protobuf.CodedInputStream(fileStream);

            return Pack.Parser.ParseFrom(input);
        }
    }
}
