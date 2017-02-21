using System;
using System.Collections.Generic;
using io = System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Synapse.Handlers.Legacy.RequestValidator
{
	[Serializable, XmlRoot( "RequestValidator" )]
	public class WorkflowParameters
	{
		public WorkflowParameters() { }

		#region properties
		[XmlElement]
		public string RequestNumber { get; set; }
		[XmlElement]
		public string PackageAdapterInstance { get; set; }

		[XmlArray]
		public List<RequestTypeOption> RequestTypeOptions { get; set; }
		[XmlIgnore]
		internal Dictionary<RequestType, bool> requestTypeToRequiresApproval { get; private set; }

		[XmlIgnore()]
		public bool IsValid { get; internal set; }
		#endregion


		public void PrepareAndValidate()
		{
			IsValid = !string.IsNullOrWhiteSpace( RequestNumber );

			requestTypeToRequiresApproval = new Dictionary<RequestType, bool>();
			foreach(RequestTypeOption rto in RequestTypeOptions)
			{
				requestTypeToRequiresApproval[rto.RequestType] = rto.RequiresApproval;
			}
			IsValid &= requestTypeToRequiresApproval.Count > 0;
		}

		public void Serialize(string filePath)
		{
			XmlSerializer s = new XmlSerializer( typeof( WorkflowParameters ) );
			XmlTextWriter w = new XmlTextWriter( filePath, Encoding.ASCII );
			w.Formatting = Formatting.Indented;
			s.Serialize( w, this );
			w.Close();
		}

		public static WorkflowParameters Deserialize(string filePath)
		{
			using( io.FileStream fs = new io.FileStream( filePath, io.FileMode.Open, io.FileAccess.Read ) )
			{
				XmlSerializer s = new XmlSerializer( typeof( WorkflowParameters ) );
				return (WorkflowParameters)s.Deserialize( fs );
			}
		}

		public static WorkflowParameters Deserialize(XmlElement el)
		{
			XmlSerializer s = new XmlSerializer( typeof( WorkflowParameters ) );
			return (WorkflowParameters)s.Deserialize( new System.IO.StringReader( el.OuterXml ) );
		}

		public WorkflowParameters FromXmlElement(XmlElement el)
		{
			XmlSerializer s = new XmlSerializer( typeof( WorkflowParameters ) );
			return (WorkflowParameters)s.Deserialize( new System.IO.StringReader( el.OuterXml ) );
		}
	}

	public class RequestTypeOption
	{
		[XmlElement]
		public RequestType RequestType { get; set; }
		[XmlElement]
		public bool RequiresApproval { get; set; }
	}
}