using System;
using System.Collections.Generic;

namespace Core.Messages
{
	/// <summary>
	/// Send data in reply to a FindVAlue
	/// </summary>
	[Serializable]
	public class FindValueDataResponse : Response
	{
		private IList<object> vals;
		
		/// <summary>
		/// Make a new response.
		/// </summary>
		/// <param name="nodeId"></param>
		/// <param name="request"></param>
		/// <param name="data"></param>
		public FindValueDataResponse(Id nodeId, FindValue request, IList<object> data) : base(nodeId, request)
		{
			vals = data;
		}
		
		/// <summary>
		/// Get the values returned for the key
		/// </summary>
		/// <returns></returns>
		public IList<object> GetValues()
		{
			return vals;
		}
		
		public override string GetName()
		{
			return "FIND_VALUE_RESPONSE_DATA";
		}
	}
}
