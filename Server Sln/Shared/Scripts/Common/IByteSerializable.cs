namespace Shared.Common
{
    /// <summary>Marks a type that can serialize itself to bytes.</summary>
    public interface IByteSerializable
    {
        byte[] ToBytes();
    }
}
