using Crossword.Proto;

namespace WordGenLib
{
    public class IO
    {
        private static void Write(Pack pack, string path)
        {
            using var fileStream = File.OpenWrite(path);
            using var output = new Google.Protobuf.CodedOutputStream(fileStream);

            pack.WriteTo(output);
        }

        private static Pack Read(string path)
        {
            using var fileStream = File.OpenRead(path);
            using var input = new Google.Protobuf.CodedInputStream(fileStream);

            return Pack.Parser.ParseFrom(input);
        }

        private record struct LoadedPack(string Path, Pack FileState, Pack RuntimeState)
        {
            public bool IsDirty => !(FileState.Equals(RuntimeState));
        }
        private readonly Dictionary<Guid, LoadedPack> _packs = new();

        public (Guid guid, Pack pack) Load(string path)
        {
            var pack = Read(path);
            var guid = Guid.NewGuid();

            _packs[guid] = new(path, pack.Clone(), pack);
            return (guid, pack);
        }

        public void Save(Guid guid)
        {
            var loaded = _packs[guid];
            Write(loaded.RuntimeState, loaded.Path);

            _packs[guid] = loaded with { FileState = loaded.RuntimeState.Clone() };
        }

        public bool IsDirty(Guid g)
        {
            return _packs[g].IsDirty;
        }
    }
}
