namespace restapi.Models
{
    public class InvalidStateError
    {
        public int ErrorCode { get => 100; }

        public string Message { get => "Transition not valid for current state"; }
    }

    public class EmptyTimecardError
    {
        public int ErrorCode { get => 101; }

        public string Message { get => "Unable to submit timecard with no lines"; }
    }

    public class MissingTransitionError
    {
        public int ErrorCode { get => 102; }

        public string Message { get => "No state transition of requested type present in timecard"; }
    }

    public class CannotApproveSelfError
    {
        public int ErrorCode { get => 103; }

        public string Message { get => "Timecard approver cannot be same as timecard resource"; }
    }

    public class CannotRejectSelfError
    {
        public int ErrorCode { get => 103; }

        public string Message { get => "Timecard rejector cannot be same as timecard resource"; }
    }

    public class ResourceDoesNotManageError
    {
        public int ErrorCode { get => 104; }

        public string Message { get => "Resource does not manage this timecard"; }
    }

    public class InconsistentResourceError
    {
        public int ErrorCode { get => 104; }

        public string Message { get => "Resource can only submit or update own timecard"; }
    }
}