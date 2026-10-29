using System.Linq;
using System.Web.Http;
using Coho.Billing.Core.Runs;

namespace Coho.Billing.Engine.Control
{
    [RoutePrefix("api")]
    public class EngineController : ApiController
    {
        private static BillingEngine Engine
        {
            get { return ControlHost.Engine; }
        }

        private string UserName
        {
            get { return User.Identity.Name; }
        }

        [HttpGet, Route("status"), Operators]
        public EngineStatus Status()
        {
            return Engine.GetStatus();
        }

        [HttpPost, Route("engine/pause"), Operators]
        public EngineStatus Pause()
        {
            Engine.Pause(UserName);
            return Engine.GetStatus();
        }

        [HttpPost, Route("engine/continue"), Operators]
        public EngineStatus Continue()
        {
            Engine.Continue(UserName);
            return Engine.GetStatus();
        }

        [HttpPost, Route("engine/wake"), Operators]
        public EngineStatus Wake()
        {
            Engine.Wake();
            return Engine.GetStatus();
        }

        [HttpGet, Route("runs"), Operators]
        public IHttpActionResult Runs(int days = 60)
        {
            return Ok(Engine.Database.GetRuns(days));
        }

        [HttpGet, Route("runs/{runId:int}"), Operators]
        public IHttpActionResult Run(int runId)
        {
            var run = Engine.Database.GetRun(runId);
            if (run == null) return NotFound();
            return Ok(new
            {
                Run = run,
                Exceptions = Engine.Database.GetExceptions(runId, false),
                Events = Engine.Database.GetRunEvents(runId)
            });
        }

        [HttpPost, Route("runs/{runId:int}/{command:regex(^(hold|resume|rerate|approve|cancel)$)}"), Operators]
        public RunAction RunCommand(int runId, string command)
        {
            switch (command)
            {
                case "hold": Engine.Runs.Hold(runId, UserName); break;
                case "resume": Engine.Runs.Resume(runId, UserName); break;
                case "rerate": Engine.Runs.Rerate(runId, UserName); break;
                case "approve": Engine.Runs.Approve(runId, UserName); break;
                case "cancel": Engine.Runs.Cancel(runId, UserName); break;
            }

            Engine.Wake();
            var run = Engine.Database.GetRun(runId);
            return new RunAction { RunId = runId, Action = command, Stage = run.Stage.ToString() };
        }

        [HttpPost, Route("exceptions/{exceptionId:int}/resolve"), Operators]
        public IHttpActionResult Resolve(int exceptionId, [FromBody] ResolveRequest request)
        {
            if (request == null) return BadRequest("Resolution missing");
            var resolved = Engine.Runs.Resolve(exceptionId, request.Resolution, UserName, request.Note);
            Engine.Wake();
            return Ok(resolved);
        }

        [HttpPost, Route("import/scan"), Operators]
        public EngineStatus Scan()
        {
            Engine.Wake();
            return Engine.GetStatus();
        }

        [HttpGet, Route("imports"), Operators]
        public IHttpActionResult Imports(int days = 14)
        {
            return Ok(Engine.Database.GetImportFiles(days));
        }

        /// <summary>Reprint of an issued bill; called by the bill lookup site on behalf of a customer service user.</summary>
        [HttpPost, Route("reprints"), Operators(AllowReprintCallers = true)]
        public IHttpActionResult Reprint([FromBody] ReprintRequestModel request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.BillNumber)) return BadRequest("Bill number missing");

            var bill = Engine.Database.GetBill(request.BillNumber.Trim());
            if (bill == null) return NotFound();

            var requestedBy = string.IsNullOrWhiteSpace(request.RequestedBy) ? UserName : request.RequestedBy + " via " + UserName;
            var id = Engine.Database.AddReprintRequest(request.BillNumber.Trim(), requestedBy, request.Reason);
            Engine.Wake();
            return Ok(new { RequestId = id });
        }
    }
}
