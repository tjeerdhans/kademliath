using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Web;
using Core;

namespace Kademliath.Core
{
    /// <summary>
    /// Represents a number of <see cref="IdLengthInBits"/> bits which is used as a nodeId and as a key for the DHT.
    /// Ids are immutable.
    /// </summary>
    [Serializable]
    public class Id : IComparable
    {
        public const int IdLengthInBits = IdLengthInBytes * 8;
        private const int IdLengthInBytes = 20;
        private readonly byte[] _data;

        // We want to be able to generate random Ids without timing issues.
        private static readonly Random Rnd = new Random();

        // We need to have a mutex to control access to the hash-based host Id.
        // Once one process on the machine under the current user gets it, no others can.
        private static Mutex _mutex;

        /// <summary>
        /// Constructor, produces a new random id.
        /// </summary>
        public Id()
        {
            _data = new byte[IdLengthInBytes];
            Rnd.NextBytes(_data);
        }

        /// <summary>
        /// Make a new Id from a byte array.
        /// </summary>
        /// <param name="data">An array of exactly <see cref="IdLengthInBytes"/> bytes.</param>
        private Id(byte[] data)
        {
            if (data.Length == IdLengthInBytes)
            {
                _data = new byte[IdLengthInBytes];
                data.CopyTo(_data, 0); // Copy the array into us.
            }
            else
            {
                throw new Exception("An Id must be exactly " + IdLengthInBytes + " bytes.");
            }
        }

        /// <summary>
        /// Hash a string to produce an Id
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static Id Hash(string key)
        {
            HashAlgorithm hashAlgorithm = new SHA1CryptoServiceProvider();
            return new Id(hashAlgorithm.ComputeHash(key.ToByteArray()));
        }

        /// <summary>
        /// Hash an object to produce an Id
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static Id Hash(object key)
        {
            HashAlgorithm hashAlgorithm = new SHA1CryptoServiceProvider();
            return new Id(hashAlgorithm.ComputeHash(key.ToByteArray()));
        }

        /// <summary>
        /// XOR operator.
        /// Distance metric in the DHT.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Id operator ^(Id a, Id b)
        {
            var xoredData = new byte[IdLengthInBytes];
            // Do each byte in turn
            for (int i = 0; i < IdLengthInBytes; i++)
            {
                xoredData[i] = (byte) (a._data[i] ^ b._data[i]);
            }

            return new Id(xoredData);
        }

        public static bool operator <(Id a, Id b)
        {
            for (var i = 0; i < IdLengthInBytes; i++)
            {
                if (a._data[i] < b._data[i])
                {
                    return true; // If first mismatch is a < b, a < b
                }

                if (a._data[i] > b._data[i])
                {
                    return false; // If first mismatch is a > b, a > b
                }
            }

            return false; // No mismatches
        }

        public static bool operator >(Id a, Id b)
        {
            for (var i = 0; i < IdLengthInBytes; i++)
            {
                if (a._data[i] < b._data[i])
                {
                    return false; // If first mismatch is a < b, a < b
                }

                if (a._data[i] > b._data[i])
                {
                    return true; // If first mismatch is a > b, a > b
                }
            }

            return false; // No mismatches
        }

        // We're a value, so we override all these
        public static bool operator ==(Id a, Id b)
        {
            if (ReferenceEquals(a, null))
            {
                return ReferenceEquals(b, null);
            }

            if (ReferenceEquals(b, null))
            {
                return false;
            }

            for (var i = 0; i < IdLengthInBytes; i++)
            {
                if (a._data[i] != b._data[i])
                {
                    // Find the first difference
                    return false;
                }
            }

            return true;
        }

        public static bool operator !=(Id a, Id b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            // Algorithm from http://stackoverflow.com/questions/16340/how-do-i-generate-a-hashcode-from-a-byte-array-in-c/425184#425184
            int hash = 0;
            for (var i = 0; i < IdLengthInBytes; i++)
            {
                unchecked
                {
                    hash *= 31;
                }

                hash ^= _data[i];
            }

            return hash;
        }

        public override bool Equals(object obj)
        {
            if (obj is Id id)
            {
                return this == id;
            }

            return false;
        }

        /// <summary>
        /// Determines the least significant bit at which the given Id differs from this one, from 0 through 8 * ID_LENGTH - 1.
        /// PRECONDITION: Ids do not match.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int DifferingBit(Id other)
        {
            Id differingBits = this ^ other;
            int differAt = 8 * IdLengthInBytes - 1;

            // Subtract 8 for every zero byte from the right
            int i = IdLengthInBytes - 1;
            while (i >= 0 && differingBits._data[i] == 0)
            {
                differAt -= 8;
                i--;
            }

            // Subtract 1 for every zero bit from the right
            int j = 0;
            // 1 << j = pow(2, j)
            while (j < 8 && (differingBits._data[i] & (1 << j)) == 0)
            {
                j++;
                differAt--;
            }

            return differAt;
        }

        /// <summary>
        /// Return a copy of ourselves that differs from us at the given bit and is random beyond that.
        /// </summary>
        /// <param name="bit"></param>
        /// <returns></returns>
        public Id RandomizeBeyond(int bit)
        {
            var randomized = new byte[IdLengthInBytes];
            _data.CopyTo(randomized, 0);

            FlipBit(randomized, bit); // Invert pivot bit

            // And randomly flip the rest
            for (int i = bit + 1; i < 8 * IdLengthInBytes; i++)
            {
                if (Rnd.NextDouble() < 0.5)
                {
                    FlipBit(randomized, i);
                }
            }

            return new Id(randomized);
        }

        /// <summary>
        /// Flips the given bit in the byte array.
        /// Byte array must be ID_LENGTH long.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="bit"></param>
        private static void FlipBit(byte[] data, int bit)
        {
            int byteIndex = bit / 8;
            int byteBit = bit % 8;
            byte mask = (byte) (1 << byteBit);

            data[byteIndex] = (byte) (data[byteIndex] ^ mask); // Use a mask to flip the bit
        }

        /// <summary>
        /// Get an Id that will be the same between different calls on the 
        /// same machine by the same app run by the same user.
        /// If that Id is taken, returns a random Id.
        /// </summary>
        /// <returns></returns>
        public static Id HostId()
        {
            // If we already have a mutex handle, we're not the first.
            if (_mutex != null)
            {
                Console.WriteLine("Using random Id");
                return new Id();
            }

            // We might be the first
            var assemblyName = Assembly.GetEntryAssembly().GetName().Name;
            var libName = Assembly.GetExecutingAssembly().GetName().Name;
            var mutexName = libName + "-" + assemblyName + "-ID";
            try
            {
                _mutex = Mutex.OpenExisting(mutexName);
                // If that worked, we're not the first
                Console.WriteLine("Using random Id");
                return new Id();
            }
            catch (Exception)
            {
                // We're the first!
                _mutex = new Mutex(true, mutexName);
                Console.WriteLine("Using host Id");
                // todo dispose mutex?                
            }

            var fullName = Assembly.GetEntryAssembly().GetName().FullName;
            var userName = Environment.UserName;
            var machine = Environment.MachineName + " " + Environment.OSVersion.VersionString;
            var allMacAddresses = string.Join("",
                NetworkInterface.GetAllNetworkInterfaces().Select(i => i.GetPhysicalAddress()));

            return Hash(fullName + userName + machine + allMacAddresses);
        }

        public override string ToString()
        {
            return Convert.ToBase64String(_data);
        }

        /// <summary>
        /// Returns this Id represented as a path-safe string.
        /// </summary>
        /// <returns></returns>
        public string ToPathString()
        {
            var result = HttpUtility.UrlEncode(_data);

            foreach (var c in Path.GetInvalidFileNameChars().Union(Path.GetInvalidFileNameChars()))
            {
                result = result.Replace(c, '-');
            }

            return result;
        }

        public int CompareTo(object obj)
        {
            if (obj is Id id)
            {
                // Compare as Id.
                if (this < id)
                {
                    return -1;
                }

                if (this == id)
                {
                    return 0;
                }
            }

            return 1;
        }
    }
}