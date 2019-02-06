using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using Kademliath.Core;

namespace Core
{
   /// <summary>
	/// Stores key/value pairs assigned to our node.
	/// Automatically handles persistence to disk.
	/// </summary>
	public class LocalStorage
	{
		[Serializable]
		private class Entry {
			// The existence of an entry implies a file with the data at PathFor(key, hash)
			public DateTime Timestamp;
			public TimeSpan KeepFor;
		}
		
		private readonly SortedList<Id, SortedList<Id, Entry>> _store; // TODO: Replace with real database.
		private readonly Thread _saveThread;
		private readonly string _indexFilename;
		private readonly string _storageRoot;
		private readonly Mutex _mutex;
		
		private const string IndexExtension = ".index";
		private const string DataExtension = ".dat";
		private static readonly IFormatter Coder = new BinaryFormatter(); // For disk storage
		private static readonly TimeSpan SaveInterval = new TimeSpan(0, 10, 0);
		
		/// <summary>
		/// Make a new LocalStorage. 
		/// Uses the executing assembly's name to determine the filename for on-disk storage.
		/// If another LocalStorage on the machine is already using that file, we use a temp directory.
		/// </summary>
		public LocalStorage()
		{
			string assembly = Assembly.GetEntryAssembly().GetName().Name;
			string libname = Assembly.GetExecutingAssembly().GetName().Name;
			
			// Check the mutex to see if we get the disk storage
			string mutexName = libname + "-" + assembly + "-storage";
			try {
				_mutex = Mutex.OpenExisting(mutexName);
				// If that worked, our disk storage has to be in a temp directory
				_storageRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
			} catch(Exception) {
				// We get the real disk storage
				_mutex = new Mutex(true, mutexName);				
				_storageRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/" + libname + "/" + assembly + "/";
			}
			
			Console.WriteLine("Storing data in " + _storageRoot);
			
			// Set a filename for an index file.
			_indexFilename =  Path.Combine(_storageRoot, "index" + IndexExtension);
			
			// Get our store from disk, if possible
			if(File.Exists(_indexFilename)) {
				try {
					// Load stuff from disk
					FileStream fs = File.OpenRead(_indexFilename);
					_store = (SortedList<Id, SortedList<Id, Entry>>) Coder.Deserialize(fs);
					fs.Close();
				} catch (Exception ex) {
					Console.WriteLine("Could not load disk data: " + ex);
				}
			}
			
			// If we need a new store, make it
			if(_store == null) {
				_store = new SortedList<Id, SortedList<Id, Entry>>();
			}
			
			// Start the index autosave thread
			_saveThread = new Thread(BackgroundSave);
			_saveThread.IsBackground = true;
			_saveThread.Start();
		}
		
		/// <summary>
		/// Clean up and close our mutex if needed.
		/// </summary>
		~LocalStorage()
		{
			_saveThread.Abort(); // Stop our autosave thread.
			SaveIndex(); // Make sure our index getw written when we shut down properly.
			_mutex.Close(); // Release our hold on the mutex.
		}
		
		/// <summary>
		/// Create all folders in a path, if missing.
		/// </summary>
		/// <param name="path"></param>
		private static void CreatePath(string path)
		{
			path = path.TrimEnd('/', '\\');
			if(Directory.Exists(path)) {
				return; 
			}

			if(Path.GetDirectoryName(path) != "") {
				CreatePath(Path.GetDirectoryName(path)); // Make up to parent
			}
			Directory.CreateDirectory(path); // Make this one
		}
		
		/// <summary>
		/// Where should we save a particular value?
		/// </summary>
		/// <param name="key"></param>
		/// <param name="hash"></param>
		/// <returns></returns>
		private string PathFor(Id key, Id hash)
		{
			return Path.Combine(Path.Combine(_storageRoot, key.ToPathString()), hash.ToPathString() + DataExtension);
		}
		
		/// <summary>
		/// Save the store in the background.
		/// PRECONSITION: We have the mutex and diskFilename is set.
		/// </summary>
		private void BackgroundSave()
		{
			while(true) {
				SaveIndex();
				Thread.Sleep(SaveInterval);
			}
			// ReSharper disable once FunctionNeverReturns
		}
		
		/// <summary>
		/// Save the index now.
		/// </summary>
		private void SaveIndex() 
		{
			try {
				Console.WriteLine("Saving datastore index...");
				CreatePath(Path.GetDirectoryName(_indexFilename));
				
				// Save
				lock(_store) {
					FileStream fs = File.OpenWrite(_indexFilename);
					Coder.Serialize(fs, _store);
					fs.Close();
				}
				Console.WriteLine("Datastore index saved");
			} catch (Exception ex) { // Report errors so the thread keeps going
				Console.WriteLine("Save error: " + ex);
			}
		}

		/// <summary>
		/// Store a key/value pair published originally at the given UTC timestamp. Value is kept until keepTime past timestamp.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="hash">The hash of the value</param>
		/// <param name="val"></param>
		/// <param name="timestamp"></param>
		/// <param name="keepFor"></param>
		public void Put(Id key, Id hash, object val, DateTime timestamp, TimeSpan keepFor)
		{
			// Write the file
			CreatePath(Path.GetDirectoryName(PathFor(key, hash)));
			File.WriteAllBytes(PathFor(key, hash), val.ToByteArray());
			
			
			// Record its existence
			Entry entry = new Entry();
			entry.Timestamp = timestamp;
			entry.KeepFor = keepFor;
			
			lock(_store) {
				if(!_store.ContainsKey(key)) {
					_store[key] = new SortedList<Id, Entry>();
				}
				_store[key][hash] = entry;
			}
		}
		
		/// <summary>
		/// Change the timing information on an existing entry, if extant.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="hash"></param> 
		/// <param name="newStamp"></param>
		/// <param name="newKeep"></param>
		public void Restamp(Id key, Id hash, DateTime newStamp, TimeSpan newKeep)
		{
			lock(_store) {
				if(_store.ContainsKey(key) && _store[key].ContainsKey(hash)) {
					_store[key][hash].Timestamp = newStamp;
					_store[key][hash].KeepFor = newKeep;
				}
			}
		}
		
		/// <summary>
		/// Do we have any data for the given key?
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public bool ContainsKey(Id key)
		{
			lock (_store)
			{
				return _store.ContainsKey(key);
			}
		}
		
		/// <summary>
		/// Do we have the specified value for the given key?
		/// </summary>
		/// <param name="key"></param>
		/// <param name="hash"></param>
		/// <returns></returns>
		public bool Contains(Id key, Id hash)
		{
			lock(_store) {
				return _store.ContainsKey(key) && _store[key].ContainsKey(hash);
			}
		}
		
		/// <summary>
		/// Get all data values for the given key, or an empty list.
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public List<object> Get(Id key)
		{
            List<object> toReturn = new List<object>();
			lock(_store) {
				if(ContainsKey(key)) {
					foreach(Id hash in _store[key].Keys) {
						// Load the value and add it to the list
						toReturn.Add(ObjectUtils.ByteArrayToObject(File.ReadAllBytes(PathFor(key, hash))));
					}
				}
			}
			return toReturn;
		}
		
		/// <summary>
		/// Get a particular value by key and hash, or null.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="hash"></param>
		/// <returns></returns>
		public object Get(Id key, Id hash)
		{
			lock(_store) {
				if(Contains(key, hash)) {
					ObjectUtils.ByteArrayToObject(File.ReadAllBytes(PathFor(key, hash)));
				}
			}
			return null;
		}

		/// <summary>
		/// Returns when the given value was last inserted by
		/// its original publisher, or null if the value isn't present.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="hash"></param>
		/// <returns></returns>
		public DateTime? GetPublicationTime(Id key, Id hash)
		{
			lock(_store) {
				if(_store.ContainsKey(key) && _store[key].ContainsKey(hash)) {
					return _store[key][hash].Timestamp;
				}
			}
			return null;
		}
		
		/// <summary>
		/// Get all IDs, so we can go through and republish everything.
		/// It's a copy so you can iterate it all you want.
		/// </summary>
		public IList<Id> GetKeys()
		{
			List<Id> toReturn = new List<Id>();
			lock(_store) {
				foreach(Id key in _store.Keys) {
					toReturn.Add(key);
				}
			}
			return toReturn;
		}
		
		/// <summary>
		/// Gets a list of all value hashes for the given key
		/// It's a copy, iterate all you want.
		/// </summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public IList<Id> GetHashes(Id key)
		{
			List<Id> toReturn = new List<Id>();
			lock(_store) {
				if(_store.ContainsKey(key)) {
					foreach(Id hash in _store[key].Keys) {
						toReturn.Add(hash);
					}
				}
			}
			return toReturn;
		}
		
		/// <summary>
		/// Expire old entries
		/// </summary>
		public void Expire()
		{
			lock(_store) {
				for(int i = 0; i < _store.Count; i++) {
					// Go through every value for the key
					SortedList<Id, Entry> vals = _store.Values[i];
					for(int j = 0; j < vals.Count; j++) {
						if(DateTime.Now.ToUniversalTime() 
						   > vals.Values[j].Timestamp + vals.Values[j].KeepFor) { // Too old!
							// Delete file
							string filePath = PathFor(_store.Keys[i], vals.Keys[j]);
							File.Delete(filePath);
							
							// Remove index
							vals.RemoveAt(j);
							j--;
						}
					}
					
					// Don't keep empty value lists around, or their directories
					if(vals.Count == 0) {
						string keyPath = Path.Combine(_storageRoot, _store.Keys[i].ToPathString());
						Directory.Delete(keyPath);
						_store.RemoveAt(i);
						i--;
					}
				}
			}
			// TODO: Remove files that the index does not mention!
		}
	}
}