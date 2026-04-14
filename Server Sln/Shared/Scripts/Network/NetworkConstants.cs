namespace MH.Network
{
    /// <summary>
    /// Sentinel peer id for the listen-server host running inside Unity (not a LiteNetLib connection).
    /// </summary>
    public static class NetworkConstants
    {
        public const int HostLocalPeerId = -1;
        public const int DefaultGamePort = 9050;
        public const string ConnectionKey = "SomeConnectionKey";
    }
}
