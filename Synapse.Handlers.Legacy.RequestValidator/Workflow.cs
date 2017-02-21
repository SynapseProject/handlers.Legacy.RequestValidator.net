using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

using Synapse.Core;

namespace Synapse.Handlers.Legacy.RequestValidator
{
	public class Workflow
	{
		WorkflowParameters _wfp = null;
		bool _globalCancel = false;
        //TODO : Replace With Synapse Enterprise Client When Available
		SynapseEnterpriseApiClient _apiClient = null;

        public Action<string, string, LogLevel, Exception> OnLogMessage;
        public Func<string, string, StatusType, long, int, bool, Exception, bool> OnProgress;

        /// <summary>
        /// Default ctor
        /// </summary>
        public Workflow() { }

		/// <summary>
		/// Initializes parameters.
		/// </summary>
		/// <param name="parameters">Initializes Parameters.</param>
		public Workflow(WorkflowParameters parameters)
		{
			_wfp = parameters;
		}

		public void Initialize(HandlerStartInfo startInfo)
		{
			_wfp.RequestNumber = startInfo.RequestNumber;
			_wfp.PackageAdapterInstance = startInfo.InstanceId + "";

            // TODO : Replace With Synapse Enterprise Client When Available
			_apiClient = new SynapseEnterpriseApiClient(WebMessageFormatType.Json );
		}

		/// <summary>
		/// Gets or sets the parameters for the Workflow.  Set ahead of ExecuteAction.
		/// </summary>
		public WorkflowParameters Parameters { get { return _wfp; } set { _wfp = value as WorkflowParameters; } }

		/// <summary>
		/// Executes the main workflow of: Backup, UpdateConfigValues, CopyContent, MoveToNext.
		/// </summary>
		public void ExecuteAction(HandlerStartInfo startInfo)
		{
			string context = "ExecuteAction";

			string msg = Utils.GetHeaderMessage(
				string.Format( "Synapse, Legacy RequestValidator Adapter. {0}, Entering Main Workflow.", Utils.GetBuildDateVersion() ) );
			if( OnStepStarting( context, msg ) )
			{
				return;
			}

            OnStepProgress(context, Utils.CompressXml(startInfo.Parameters));

            Stopwatch clock = new Stopwatch();
            clock.Start();

            bool ok = true;
            Exception ex = null;
            try
            {
                ok = ValidateParameters();
                if (ok)
                {
                    ok = ValidateRequest();
                    if (startInfo.IsDryRun)
                    {
                        OnStepProgress("ExecuteAction", "IsDryRun Flag is set.  Request is presumed to be valid.");
                        ok = true;
                    }
                }
            }
            catch (Exception exception)
            {
                ex = exception;
            }

            ok = ok && ex == null;
            msg = Utils.GetHeaderMessage(string.Format("End Main Workflow: {0}, Total Execution Time: {1}",
                ok ? "Complete." : "One or more steps failed.", clock.ElapsedSeconds()));
            OnProgress(context, msg, ok ? StatusType.Complete : StatusType.Failed, 0, int.MaxValue, false, ex);

        }

		#region Validate Parameters
		bool ValidateParameters()
		{
			string context = "ExecuteAction";
			const int padding = 50;

			OnStepProgress( context, Utils.GetHeaderMessage( "Begin [PrepareAndValidate]" ) );

			_wfp.PrepareAndValidate();


			OnStepProgress( context, Utils.GetMessagePadRight( "WorkflowParameters.IsValid",
				string.Format( "{0} [RequestNumber != null] && [RequestTypeOptions.Count > 0]", _wfp.IsValid ), padding ) );
			OnStepProgress( context, Utils.GetHeaderMessage( "End [PrepareAndValidate]" ) );

			return _wfp.IsValid;
		}
		#endregion


		#region ValidateRequest
		bool ValidateRequest()
		{
			string context = "ValidateRequest";
			string msg = Utils.GetHeaderMessage( "Beginning validation." );
			if( OnStepStarting( context, msg ) )
			{
				return false;
			}

			Stopwatch clock = new Stopwatch();
			clock.Start();

			#region the little dictionary that could, just a helper
			//todo: push this down into config, build the dict dynamically
			const string crqFormat = @"^CRQ[\d]{12}$";
			const string incFormat = @"^INC[\d]{12}$";
			const string tskFormat = @"^TAS[\d]{12}$";
			Dictionary<RequestType, string> requestNumberFormats = new Dictionary<RequestType, string>();
			requestNumberFormats.Add( RequestType.Change, crqFormat );
			requestNumberFormats.Add( RequestType.Incident, incFormat );
			requestNumberFormats.Add( RequestType.Task, tskFormat );
			#endregion

			string requestNumberFormat = "Unknown Format";
			bool requestRequiresApproval = true;//default to true, override value from wfp data

			bool isValidRequestNumberFormat = false;
			foreach( RequestType rt in _wfp.requestTypeToRequiresApproval.Keys )
			{
				isValidRequestNumberFormat = Regex.IsMatch( _wfp.RequestNumber, requestNumberFormats[rt], RegexOptions.IgnoreCase );
				if( isValidRequestNumberFormat )
				{
					requestNumberFormat = requestNumberFormats[rt];
					requestRequiresApproval = _wfp.requestTypeToRequiresApproval[rt];
					break;
				}
			}
			OnStepProgress( context,
				string.Format( "RequestNumber Format: Pass: [{1}], Condition: [{2}], RequestNumber [{0}]",
				_wfp.RequestNumber, isValidRequestNumberFormat, requestNumberFormat ) );

            //TODO : Replace With Synapse Enterprise Client When Available
            PackageRecord package = null;
			PackageAdapterInstanceRecord pair = _apiClient.GetPackageAdapterInstance( _wfp.PackageAdapterInstance );
			List<PackageRecord> packages =
				_apiClient.GetPackages( null, null, pair.PackageKey, null, null, null, null, null, null, null, null, null );
			if( packages.Count > 0 )
			{
				package = packages[0];
			}
			if( package == null )
			{
				throw new Exception( string.Format( "Could not resolve Package from PackageAdapterInstance [{0}]", _wfp.PackageAdapterInstance ) );
			}

			bool foundPackage = false;
            //TODO : Replace With Synapse Enterprise Client When Available
            RequestRecord request = new RequestRecord()
			{
				Id = 0,
				RequestNumber = "Invalid",
				ApplicationName = "Unknown",
				StartDateTime = DateTime.MinValue,
				EndDateTime = DateTime.MinValue,
				ApprovedDateTime = DateTime.MinValue,
				IsApproved = false,
				IsComplete = true
			};
			try
			{
				request = _apiClient.GetRequestByRequestNumber( _wfp.RequestNumber );
			}
			catch( System.Runtime.Serialization.SerializationException serex )
			{
				string err = string.Empty;
				if( serex.Data != null && serex.Data.Count > 0 )
				{
					foreach( object o in serex.Data.Values )
					{
						err = string.Format( "{0} :: {1}", err, o.ToString() );
					}
					err.TrimEnd( ' ', ':' );
				}
				OnStepProgress( context, string.Format( "Error:  Could not retrieve Request [{0}].{1}", _wfp.RequestNumber, err ) );
			}
			catch(Exception ex)
			{
				OnStepProgress( context, string.Format( "Error:  Could not retrieve Request [{0}].  Unhandled exception.", _wfp.RequestNumber ), ex );
			}

			foreach( RequestPackageRecord rpr in request.Packages )
			{
				if( rpr.PackageId == package.Id )
				{
					foundPackage = true;
					break;
				}
			}
			OnStepProgress( context, string.Format( "Package Association:  Pass: [{1}], Condition: Package [{0}] to Request [{2}]", package.Name, foundPackage, _wfp.RequestNumber ) );

			bool datesOk = request.StartDateTime <= DateTime.Now && DateTime.Now <= request.EndDateTime;
			OnStepProgress( context, string.Format( "Date Range:           Pass: [{3}], Condition: StartDateTime:[{0}] <= {2} <= EndDateTime:[{1}]",
				request.StartDateTime, request.EndDateTime, DateTime.Now, datesOk ) );

			string approvedText = request.IsApproved.ToString();
			if( !requestRequiresApproval )
			{
				request.IsApproved = true;
				approvedText = "Not Required";
			}

			OnStepProgress( context, string.Format( "IsApproved:           Pass: [{0}], Condition: IsApproved == true", approvedText ) );
			OnStepProgress( context, string.Format( "IsComplete:           Pass: [{0}], Condition: IsComplete == false", !request.IsComplete ) );

			bool ok = request.IsApproved && !request.IsComplete && foundPackage && datesOk;
			OnStepProgress( context, string.Format( "Request is Valid:     Pass: [{0}], Condition: (HasPackageAssociation && InDateRange && IsApproved && !IsComplete)", ok ) );


			clock.Stop();

			msg = Utils.GetHeaderMessage(
				string.Format( "End ValidateRequest: Total Execution Time: {0}", clock.ElapsedSeconds() ) );
			OnStepFinished( context, msg );

			return ok;
		}
		#endregion


		#region NotifyProgress Events
		public int _cheapSequence = 0;

		/// <summary>
		/// Notify of step beginning. If return value is True, then cancel operation.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name.</param>
		/// <param name="message">Descriptive message.</param>
		/// <returns>AdapterProgressCancelEventArgs.Cancel value.</returns>
		bool OnStepStarting(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, 0, _cheapSequence++, false, null);
            return false;
		}

		/// <summary>
		/// Notify of step progress.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name.</param>
		/// <param name="message">Descriptive message.</param>
		void OnStepProgress(string context, string message, Exception ex = null)
		{
            OnProgress(context, message, StatusType.Running, 0, _cheapSequence++, false, ex);
		}

		/// <summary>
		/// Notify of step completion.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name or workflow activty.</param>
		/// <param name="message">Descriptive message.</param>
		void OnStepFinished(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, 0, _cheapSequence++, false, null);
		}
		#endregion
	}
}