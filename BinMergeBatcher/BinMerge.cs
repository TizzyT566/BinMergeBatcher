using System.Text;

namespace BinMergeBatcher
{
    public class CueBin
    {
        internal class Track
        {
            public int Number { get; }
            public TrackType TrackType { get; }

            public List<Index> Indexes;

            public Track(int number, TrackType type)
            {
                Indexes = new();
                Number = number;
                TrackType = type;
            }
        }

        internal class Index
        {
            public int Id;
            public string Stamp;
            public int Offset;
            public Index(int id, string stamp)
            {
                Id = id;
                Stamp = stamp;
                Offset = CuestampToSector(stamp);
            }
        }

        internal class TrackType
        {
            private static readonly TrackType[] trackTypes = {
                new("AUDIO", 2352), new("CDG", 2448),
                new("MODE1/2048", 2048), new("MODE1/2352", 2352),
                new("MODE2/2336", 2336), new("MODE2/2352", 2352),
                new("CDI/2336", 2336), new("CDI/2352", 2352)};
            public static readonly TrackType AUDIO = trackTypes[0];
            public static readonly TrackType CDG = trackTypes[1];
            public static readonly TrackType MODE1_2048 = trackTypes[2];
            public static readonly TrackType MODE1_2352 = trackTypes[3];
            public static readonly TrackType MODE2_2336 = trackTypes[4];
            public static readonly TrackType MODE2_2352 = trackTypes[5];
            public static readonly TrackType CDI_2336 = trackTypes[6];
            public static readonly TrackType CDI_2352 = trackTypes[7];

            private readonly string _name;
            private readonly int _blockSize;

            public TrackType(string name, int blockSize)
            {
                _name = name;
                _blockSize = blockSize;
            }

            public override string ToString() => _name;

            public static implicit operator int(TrackType trackType) => trackType._blockSize;
            public static implicit operator TrackType(string track)
            {
                foreach (TrackType trackType in trackTypes)
                    if (track.Contains(trackType.ToString()))
                        return trackType;
                throw new ArgumentException("Invalid track type.");
            }
        }

        internal class File
        {
            public string Path;
            public List<Track> Tracks;
            public long Size;

            public File(string filePath)
            {
                Path = filePath;
                Tracks = new();
                Size = new FileInfo(filePath).Length;
            }
        }

        private readonly File[] _files;
        private readonly int _tracks;
        private readonly int _blockSize = -1;
        private readonly string _baseName;

        public CueBin(string cuePath)
        {
            Track? crntTrack = null;
            File? crntFile = null;

            string[] lines = System.IO.File.ReadAllLines(cuePath);

            _baseName = Path.GetFileNameWithoutExtension(cuePath);

            List<File> files = new();

            foreach (string line in lines)
            {
                string[] parts = CueLine(line);
                if (parts[0] == "FILE")
                {
                    string dirPath = Path.GetDirectoryName(cuePath) ?? throw new Exception("Invalid Path");
                    string fileName = Path.GetFileName(parts[1][1..^1]);
                    string thisPath = Path.Combine(dirPath, fileName);
                    crntFile = new File(thisPath);
                    files.Add(crntFile);
                }
                else if (parts[0] == "TRACK" && crntFile is not null)
                {
                    crntTrack = new(int.Parse(parts[1]), parts[2]);
                    if (_blockSize == -1) _blockSize = crntTrack.TrackType;
                    crntFile.Tracks.Add(crntTrack);
                    _tracks++;
                }
                else if (parts[0] == "INDEX" && crntTrack is not null)
                {
                    crntTrack.Indexes.Add(new(int.Parse(parts[1]), parts[2]));
                }
            }
            _files = files.ToArray();
        }

        internal static string[] CueLine(string line)
        {
            string[] parts = line.Split(' ');
            if (parts.Length < 3)
                throw new ArgumentException("Invalid Cue format.");
            string[] result = new string[3];
            int first = 0;
            while (true)
            {
                string crnt = parts[first++].Trim();
                if (crnt != "")
                {
                    result[0] = crnt;
                    break;
                }
            }
            int last = parts.Length - 1;
            for (; ; last--)
            {
                string crnt = parts[last].Trim();
                if (crnt != "")
                {
                    result[2] = crnt;
                    break;
                }
            }
            result[1] = string.Join(' ', parts[first..last]).Trim();
            return result;
        }

        internal string MergedCueSheet(string baseName)
        {
            StringBuilder sb = new();
            sb.AppendLine($"FILE \"{baseName}.bin\" BINARY");
            long secPos = 0;
            int tracksDigitLength = (int)Math.Ceiling(Math.Log10(_tracks + 1));
            if (tracksDigitLength < 2)
                tracksDigitLength = 2;
            foreach (File file in _files)
            {
                foreach (Track track in file.Tracks)
                {
                    sb.AppendLine($"  TRACK {track.Number.ToString().PadLeft(tracksDigitLength, '0')} {track.TrackType.ToString()}");
                    int indexesDigitLength = (int)Math.Ceiling(Math.Log10(track.Indexes.Count + 1));
                    if (indexesDigitLength < 2)
                        indexesDigitLength = 2;
                    foreach (Index index in track.Indexes)
                    {
                        sb.AppendLine($"    INDEX {index.Id.ToString().PadLeft(indexesDigitLength, '0')} {SectorToCuestamp(secPos + index.Offset)}");
                    }
                }
                secPos += file.Size / _blockSize;
            }
            return sb.ToString();
        }

        internal async Task ConsolidateCueBin(string outDir)
        {
            string dirPath = Path.Combine(outDir, _baseName);
            string cueFile = Path.Combine(dirPath, $"{_baseName}.cue");
            string binFile = Path.Combine(dirPath, $"{_baseName}.bin");
            using MemoryStream ms = new();
            foreach (File file in _files)
            {
                using FileStream fr = new(file.Path, FileMode.Open, FileAccess.Read, FileShare.None);
                await fr.CopyToAsync(ms, (int)file.Size);
            }
            ms.Position = 0;
            if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
            System.IO.File.WriteAllText(cueFile, MergedCueSheet(_baseName));
            using FileStream fs = new(binFile, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            await ms.CopyToAsync(fs);
            Console.WriteLine($"Done: {_baseName}");
        }

        internal static string SectorToCuestamp(long sectors)
        {
            long min = sectors / 4500;
            long fields = sectors % 4500;
            long sec = fields / 75;
            fields = sectors % 75;
            return $"{min.ToString().PadLeft(2, '0')}:{sec.ToString().PadLeft(2, '0')}:{fields.ToString().PadLeft(2, '0')}";
        }

        internal static int CuestampToSector(string stamp)
        {
            string[] parts = stamp.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Last().Split(':');
            int min = int.Parse(parts[0]);
            int sec = int.Parse(parts[1]);
            int fields = int.Parse(parts[2]);
            return fields + (sec * 75) + (min * 60 * 75);
        }
    }
}
