namespace SptModSync.Client.Sync
{
    public sealed class SyncProgress
    {
        public int FilesDone;
        public int FilesTotal;
        public long BytesDone;
        public long BytesTotal;
        public string CurrentFile = "";
        public bool Complete;
        public string? Error;
    }
}
