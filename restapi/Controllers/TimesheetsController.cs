using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using restapi.Models;

namespace restapi.Controllers
{
    [Route("[controller]")]
    public class TimesheetsController : Controller
    {
        [HttpGet]
        [Produces(ContentTypes.Timesheets)]
        [ProducesResponseType(typeof(IEnumerable<Timecard>), 200)]
        public IEnumerable<Timecard> GetAll()
        {
            return Database
                .All
                .OrderBy(t => t.Opened);
        }

        [HttpGet("{id}")]
        [Produces(ContentTypes.Timesheet)]
        [ProducesResponseType(typeof(Timecard), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetOne(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null) 
            {
                return Ok(timecard);
            }
            else
            {
                return NotFound();
            }
        }

        // Remove (DELETE) a draft or cancelled timecard
        [HttpDelete("{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(InvalidStateError), 409)]
        public IActionResult DeleteTimecard(string id, Deletion deletion)
        {
            Timecard timecard = Database.Find(id);

            if (timecard == null)
            {
                return NotFound();
            }

            if (timecard.CanBeDeleted() == false)
            {
                return StatusCode(409, new InvalidStateError() { });
            }

            // This verifies that timecard deleter is timecard resource
            if (deletion.Resource != timecard.Resource)
            {
                return StatusCode(409, new ResourceDoesNotManageError());
            }

            Database.Delete(timecard);
            return Ok();
        }

        // Replace (POST) a complete line item
        [HttpPost("{id}/lines/{lineId}")]
        [ProducesResponseType(typeof(AnnotatedTimecardLine), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(InvalidStateError), 409)]
        public IActionResult ReplaceLine(string id, Guid lineId, [FromBody] TimecardLine line)
        {
            Timecard timeCard = Database.Find(id);
            // if timecard not found, or line not in timecard, return not found
            if (timeCard == null || !timeCard.HasLine(lineId))
            {
                return NotFound();
            }
            // if timecard isn't a draft, it can't be edited
            else if (timeCard.Status != TimecardStatus.Draft)
            {
                return StatusCode(409, new InvalidStateError() { });
            }
            else if (!timeCard.HasLine(lineId))
            {
                return NotFound();
            }

            // This now completely replaces the existing line with
            // all of the input from POST parameter, so everything except
            // the ID changes. User must remember to change all parameters
            // since unincluded parameters are set to 0.
            TimecardLine resultLine = timeCard.ReplaceLine(lineId, line);
            return Ok(resultLine);
        }

        // Update (PATCH) a line item
        [HttpPatch("{id}/lines/{lineId}")]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(AnnotatedTimecardLine), 200)]
        [ProducesResponseType(typeof(InvalidStateError), 409)]
        public IActionResult UpdateLine(string id, Guid lineId, [FromBody] JObject line)
        {
            Timecard timeCard = Database.Find(id);
            // if timecard not found, or line not in timecard, return not found
            if (timeCard == null || !timeCard.HasLine(lineId))
            {
                return NotFound();
            }
            // if timecard isn't a draft, it can't be edited
            else if (timeCard.Status != TimecardStatus.Draft)
            {
                return StatusCode(409, new InvalidStateError() { });
            }
            else if (!timeCard.HasLine(lineId))
            {
                return NotFound();
            }

            // This now tries to update only the parts of the line that are in
            // the PATCH parameters while trying to leave other fields unchanged
            // but if there's an error while parsing then return a 409.
            TimecardLine resultLine = new TimecardLine();
            try
            {
                resultLine = timeCard.ReplaceLine(lineId, line);
            }
            catch (Exception e)
            {
                return StatusCode(409, new InvalidStateError() { });
            }
            return Ok(resultLine);
        }

        // When approving, timecard approver cannot be same as timecard resource
        [HttpPost("{id}/approval")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(InvalidStateError), 409)]
        [ProducesResponseType(typeof(EmptyTimecardError), 409)]
        [ProducesResponseType(typeof(CannotApproveSelfError), 409)]
        public IActionResult Approve(string id, [FromBody] Approval approval)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                if (timecard.Status != TimecardStatus.Submitted)
                {
                    return StatusCode(409, new InvalidStateError() { });
                }

                // This verifies that timecard approver is not timecard resource
                if (approval.Resource == timecard.Resource)
                {
                    return StatusCode(409, new CannotApproveSelfError());
                }

                var transition = new Transition(approval, TimecardStatus.Approved);
                timecard.Transitions.Add(transition);
                return Ok(transition);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost]
        [Produces(ContentTypes.Timesheet)]
        [ProducesResponseType(typeof(Timecard), 200)]
        public Timecard Create([FromBody] DocumentResource resource)
        {
            var timecard = new Timecard(resource.Resource);

            var entered = new Entered() { Resource = resource.Resource };

            timecard.Transitions.Add(new Transition(entered));

            Database.Add(timecard);

            return timecard;
        }

        [HttpGet("{id}/lines")]
        [Produces(ContentTypes.TimesheetLines)]
        [ProducesResponseType(typeof(IEnumerable<AnnotatedTimecardLine>), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetLines(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                var lines = timecard.Lines
                    .OrderBy(l => l.WorkDate)
                    .ThenBy(l => l.Recorded);

                return Ok(lines);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost("{id}/lines")]
        [Produces(ContentTypes.TimesheetLine)]
        [ProducesResponseType(typeof(AnnotatedTimecardLine), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(InvalidStateError), 409)]
        public IActionResult AddLine(string id, [FromBody] TimecardLine timecardLine)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                if (timecard.Status != TimecardStatus.Draft)
                {
                    return StatusCode(409, new InvalidStateError() { });
                }

                var annotatedLine = timecard.AddLine(timecardLine);

                return Ok(annotatedLine);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet("{id}/transitions")]
        [Produces(ContentTypes.Transitions)]
        [ProducesResponseType(typeof(IEnumerable<Transition>), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetTransitions(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                return Ok(timecard.Transitions);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost("{id}/submittal")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(InvalidStateError), 409)]
        [ProducesResponseType(typeof(EmptyTimecardError), 409)]
        public IActionResult Submit(string id, [FromBody] Submittal submittal)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                if (timecard.Status != TimecardStatus.Draft)
                {
                    return StatusCode(409, new InvalidStateError() { });
                }

                if (timecard.Lines.Count < 1)
                {
                    return StatusCode(409, new EmptyTimecardError() { });
                }

                // For consistency, only resource can submit a timecard for that resource
                if (timecard.Resource != submittal.Resource)
                {
                    return StatusCode(409, new InconsistentResourceError() { });
                }

                var transition = new Transition(submittal, TimecardStatus.Submitted);
                timecard.Transitions.Add(transition);
                return Ok(transition);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet("{id}/submittal")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(MissingTransitionError), 409)]
        public IActionResult GetSubmittal(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                if (timecard.Status == TimecardStatus.Submitted)
                {
                    var transition = timecard.Transitions
                                        .Where(t => t.TransitionedTo == TimecardStatus.Submitted)
                                        .OrderByDescending(t => t.OccurredAt)
                                        .FirstOrDefault();

                    return Ok(transition);                                        
                }
                else 
                {
                    return StatusCode(409, new MissingTransitionError() { });
                }
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost("{id}/cancellation")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(InvalidStateError), 409)]
        [ProducesResponseType(typeof(EmptyTimecardError), 409)]
        public IActionResult Cancel(string id, [FromBody] Cancellation cancellation)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                if (timecard.Status != TimecardStatus.Draft && timecard.Status != TimecardStatus.Submitted)
                {
                    return StatusCode(409, new InvalidStateError() { });
                }

                // For consistency, only resource can submit a timecard for that resource
                if (timecard.Resource != cancellation.Resource)
                {
                    return StatusCode(409, new InconsistentResourceError() { });
                }

                var transition = new Transition(cancellation, TimecardStatus.Cancelled);
                timecard.Transitions.Add(transition);
                return Ok(transition);
            }
            else
            {
                return NotFound();
            }
        }


        [HttpGet("{id}/cancellation")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(MissingTransitionError), 409)]
        public IActionResult GetCancellation(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                if (timecard.Status == TimecardStatus.Cancelled)
                {
                    var transition = timecard.Transitions
                                        .Where(t => t.TransitionedTo == TimecardStatus.Cancelled)
                                        .OrderByDescending(t => t.OccurredAt)
                                        .FirstOrDefault();

                    return Ok(transition);                                        
                }
                else 
                {
                    return StatusCode(409, new MissingTransitionError() { });
                }
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost("{id}/rejection")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(InvalidStateError), 409)]
        [ProducesResponseType(typeof(EmptyTimecardError), 409)]
        public IActionResult Close(string id, [FromBody] Rejection rejection)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                if (timecard.Status != TimecardStatus.Submitted)
                {
                    return StatusCode(409, new InvalidStateError() { });
                }

                // For consistency, resource cannot reject own timecard
                if (timecard.Resource == rejection.Resource)
                {
                    return StatusCode(409, new CannotRejectSelfError() { });
                }

                var transition = new Transition(rejection, TimecardStatus.Rejected);
                timecard.Transitions.Add(transition);
                return Ok(transition);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet("{id}/rejection")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(MissingTransitionError), 409)]
        public IActionResult GetRejection(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                if (timecard.Status == TimecardStatus.Rejected)
                {
                    var transition = timecard.Transitions
                                        .Where(t => t.TransitionedTo == TimecardStatus.Rejected)
                                        .OrderByDescending(t => t.OccurredAt)
                                        .FirstOrDefault();

                    return Ok(transition);                                        
                }
                else 
                {
                    return StatusCode(409, new MissingTransitionError() { });
                }
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet("{id}/approval")]
        [Produces(ContentTypes.Transition)]
        [ProducesResponseType(typeof(Transition), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(MissingTransitionError), 409)]
        public IActionResult GetApproval(string id)
        {
            Timecard timecard = Database.Find(id);

            if (timecard != null)
            {
                if (timecard.Status == TimecardStatus.Approved)
                {
                    var transition = timecard.Transitions
                                        .Where(t => t.TransitionedTo == TimecardStatus.Approved)
                                        .OrderByDescending(t => t.OccurredAt)
                                        .FirstOrDefault();

                    return Ok(transition);                                        
                }
                else 
                {
                    return StatusCode(409, new MissingTransitionError() { });
                }
            }
            else
            {
                return NotFound();
            }
        }        
    }
}
