﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Serialization;
using System.IO;

using Synapse.Handlers.Legacy.RequestValidator;

using Synapse.Core;

public class RequestValidatorHandler : HandlerRuntimeBase
{
    int seqNo = 0;
    override public ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        XmlSerializer ser = new XmlSerializer(typeof(WorkflowParameters));
        WorkflowParameters wfp = new WorkflowParameters();
        TextReader reader = new StringReader(startInfo.Parameters);
        wfp = (WorkflowParameters)ser.Deserialize(reader);

        Workflow wf = new Workflow(wfp);
        wf.Initialize(startInfo);

        wf.OnLogMessage = this.OnLogMessage;
        wf.OnProgress = this.OnProgress;

        seqNo = 0;
        OnProgress("Execute", "Starting", StatusType.Running, startInfo.InstanceId, seqNo++);
        wf.ExecuteAction(startInfo.IsDryRun);

        return new ExecuteResult() { Status = StatusType.Complete };
    }
}
