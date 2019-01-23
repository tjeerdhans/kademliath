using System;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web;

namespace Core
{
   	/// <summary>
	/// Represents a 160-bit number which is used both as a nodeID and as a key for the DHT.
	/// The number is stored big-endian (most-significant-byte first).
	/// IDs are immutable.
	/// </summary>
	[Serializable]
	public class Id : IComparable
	{
		public const int IdLength = 20; // This is how long IDs should be, in bytes.
		private readonly byte[] _data;
		
		// We want to be able to generate random IDs without timing issues.
		private static readonly Random Rnd = new Random();
		
		// We need to have a mutex to control access to the hash-based host ID.
		// Once one process on the machine under the current user gets it, no others can.
		private static Mutex _mutex;
		
		/// <summary>
		/// Make a new ID from a byte array.
		/// </summary>
		/// <param name="data">An array of exactly 20 bytes.</param>
		public Id(byte[] data)
		{
			if(data.Length == IdLength) {
				_data = new byte[IdLength];
				data.CopyTo(_data, 0); // Copy the array into us.
			} else {
				throw new Exception("An ID must be exactly " + IdLength + " bytes.");
			}
		}
		
		/// <summary>
		/// Hash a string to produce an ID
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public static Id Hash(string key)
		{
			HashAlgorithm hashAlgorithm = new SHA1CryptoServiceProvider(); 
            return new Id(hashAlgorithm.ComputeHash(key.ToByteArray()));
		}

        /// <summary>
        /// Hash an object to produce an ID
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
		/// This is our distance metric in the DHT.
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static Id operator^(Id a, Id b)
		{
			byte[] xoredData = new byte[IdLength];
			// Do each byte in turn
			for(int i = 0; i < IdLength; i++) {
				xoredData[i] = (byte) (a._data[i] ^ b._data[i]);
			}
			return new Id(xoredData);
		}
		
		// We need to compare these when measuring distance
		public static bool operator<(Id a, Id b)
		{
			for(int i = 0; i < IdLength; i++) {
				if(a._data[i] < b._data[i]) {
					return true; // If first mismatch is a < b, a < b
				} else if (a._data[i] > b._data[i]) {
					return false; // If first mismatch is a > b, a > b
				}
			}
			return false; // No mismatches
		}
		
		public static bool operator>(Id a, Id b) {
			for(int i = 0; i < IdLength; i++) {
				if(a._data[i] < b._data[i]) {
					return false; // If first mismatch is a < b, a < b
				} else if (a._data[i] > b._data[i]) {
					return true; // If first mismatch is a > b, a > b
				}
			}
			return false; // No mismatches
		}
		
		// We're a value, so we override all these
		public static bool operator==(Id a, Id b) {
			// Handle null
			if(ReferenceEquals(a, null)) {
				return ReferenceEquals(b, null);
			}
			if(ReferenceEquals(b, null)) {
				return false;
			}
			
			// Actually check
			for(int i = 0; i < IdLength; i++) {
				if(a._data[i] != b._data[i]) { // Find the first difference
					return false;
				}
			}
			return true; // Must match
		}
		
		public static bool operator!=(Id a, Id b) {
			return !(a == b); // Already have that
		}
		
		public override int GetHashCode()
		{
			// Algorithm from http://stackoverflow.com/questions/16340/how-do-i-generate-a-hashcode-from-a-byte-array-in-c/425184#425184
			int hash = 0;
			for(int i = 0; i < IdLength; i++) {
				unchecked {
					hash *= 31;
				}
				hash ^= _data[i];
			}
			return hash;
		}
		
		public override bool Equals(object obj)
		{
			if(obj is Id) {
				return this == (Id) obj;
			} else {
				return false;
			}
		}
		
		/// <summary>
		/// Determines the least significant bit at which the given ID differs from this one, from 0 through 8 * ID_LENGTH - 1.
		/// PRECONDITION: IDs do not match.
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public int DifferingBit(Id other)
		{
			Id differingBits = this ^ other;
			int differAt = 8 * IdLength - 1;
			
			// Subtract 8 for every zero byte from the right
			int i = IdLength - 1;
			while(i >= 0 && differingBits._data[i] == 0) {
				differAt -= 8;
				i--;
			}
			
			// Subtract 1 for every zero bit from the right
			int j = 0;
			// 1 << j = pow(2, j)
			while(j < 8 && (differingBits._data[i] & (1 << j)) == 0) {
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
			byte[] randomized = new byte[IdLength];
			_data.CopyTo(randomized, 0);
			
			FlipBit(randomized, bit); // Invert pivot bit
			
			// And randomly flip the rest
			for(int i = bit + 1; i < 8 * IdLength; i++) {
				if(Rnd.NextDouble() < 0.5) {
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
		/// Produce a random ID.
		/// TODO: Make into a constructor?
		/// </summary>
		/// <returns></returns>
		public static Id RandomId()
		{
			byte[] data = new byte[IdLength];
			Rnd.NextBytes(data);
			return new Id(data);
		}
		
		/// <summary>
		/// Get an ID that will be the same between different calls on the 
		/// same machine by the same app run by the same user.
		/// If that ID is taken, returns a random ID.
		/// </summary>
		/// <returns></returns>
		public static Id HostId()
		{
			// If we already have a mutex handle, we're not the first.
			if(_mutex != null) {
				Console.WriteLine("Using random ID");
				return RandomId();
			}
			
			// We might be the first
			string assembly = Assembly.GetEntryAssembly().GetName().Name;
			string libname = Assembly.GetExecutingAssembly().GetName().Name;
			string mutexName = libname + "-" + assembly + "-ID";
			try {
				_mutex = Mutex.OpenExisting(mutexName);
				// If that worked, we're not the first
				Console.WriteLine("Using random ID");
				return RandomId();
			} catch(Exception) {
				// We're the first!
				_mutex = new Mutex(true, mutexName);
				Console.WriteLine("Using host ID");
				// TODO: Close on assembly unload?
			}
			
			// Still the first! Calculate hashed ID.
			string app = Assembly.GetEntryAssembly().GetName().FullName;
			string user = Environment.UserName;
			string machine = Environment.MachineName + " " + Environment.OSVersion.VersionString;
			
			// Get macs
			string macs = "";
			foreach(NetworkInterface i in NetworkInterface.GetAllNetworkInterfaces()) {
				macs += i.GetPhysicalAddress() + "\n";
			}
			return Hash(app + user + machine + macs);
		}
		
		/// <summary>
		/// Turn this ID into a string.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return Convert.ToBase64String(_data);
		}
		
		/// <summary>
		/// Returns this ID represented as a path-safe string.
		/// </summary>
		/// <returns></returns>
		public string ToPathString()
		{
			return HttpUtility.UrlEncode(_data);			
		}
		
		/// <summary>
		/// Compare ourselves to an object
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public int CompareTo(object obj)
		{
			if(obj is Id) {
				// Compare as ID.
				if(this < (Id) obj) {
					return -1;
				} else if(this == (Id) obj) {
					return 0;
				} else {
					return 1;
				}
			} else {
				return 1; // We're bigger than random crap
			}
		}
	}
}