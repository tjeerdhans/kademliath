
using MessagePack;

namespace Kademliath.Core
{
    public static class ObjectUtils
    {
        // public static object ByteArrayToObject(byte[] byteArray)
        // {
        //     return MessagePackSerializer.Deserialize(byteArray);
        //     // using var memStream = new MemoryStream();
        //     // var binForm = new BinaryFormatter();
        //     // memStream.Write(byteArray, 0, byteArray.Length);
        //     // memStream.Seek(0, SeekOrigin.Begin);
        //     // object obj = binForm.Deserialize(memStream);
        //     // return obj;
        // }

        // public static byte[] ToByteArray(this object obj)
        // {
        //     if (obj == null)
        //         return null;
        //     return MessagePackSerializer.Serialize(obj);
        //     // var bf = new BinaryFormatter();
        //     // using var ms = new MemoryStream();
        //     // bf.Serialize(ms, obj);
        //     // return ms.ToArray();
        // }
    }
}