using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Kademliath.Core
{
    public static class ObjectUtils
    {
        public static object ByteArrayToObject(byte[] byteArray)
        {
            MemoryStream memStream = new MemoryStream();
            BinaryFormatter binForm = new BinaryFormatter();
            memStream.Write(byteArray, 0, byteArray.Length);
            memStream.Seek(0, SeekOrigin.Begin);
            object obj = binForm.Deserialize(memStream);
            return obj;
        }
        public static byte[] ToByteArray(this object obj)
        {
            if (obj == null)
                return null;
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            bf.Serialize(ms, obj);
            return ms.ToArray();
        }
    }
}