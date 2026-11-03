namespace ConferenceBooking.Services.BookingAPI.Enums
{
    public enum HallLocationType : byte { Central = 1, Remote = 2 }
    public enum FacilityCategory : byte { Equipment = 1, Recording = 2, Connectivity = 3, Other = 4 }
    public enum VCNetworkType : byte { Intranet = 1, Internet = 2 }

    public enum BookingType : byte { Normal = 1, VCTest = 2 }
    public enum BookingApprovalStatus : byte { Pending = 1, Approved = 2, Rejected = 3, Cancelled = 4 }
    public enum MeetingStatus : byte { NotStarted = 0, InProgress = 1, Completed = 2, NoShow = 3 }

    public enum ParticipantType : byte { Intranet = 1, Internet = 2, External = 3 }
    public enum ParticipantAttendanceStatus : byte { Unknown = 0, Attended = 1, Absent = 2 }

    public enum RecurrencePattern : byte { Daily = 1, Weekly = 2, Monthly = 3 }
    public enum ReservationStatus : byte { Active = 1, Cancelled = 2, Expired = 3 }
    public enum ReservationOccurrenceStatus : byte { Active = 1, Cancelled = 2, Expired = 3, Booked = 4 }

    public enum ModificationStatus : byte { Pending = 1, Approved = 2, Rejected = 3, Withdrawn = 4, Expired = 5 }
    public enum SlotHoldStatus : byte { Active = 1, Released = 2, Expired = 3 }

    public enum RemoteBusySource : byte { User = 1, Admin = 2 }
    public enum RemoteBusyActionType : byte { Created = 1, Updated = 2, Removed = 3 }

    public enum MeetingOperationType : byte { Start = 1, Stop = 2, NoShow = 3 }
    public enum RecordingStatus : byte { Requested = 1, InProgress = 2, Ready = 3, Collected = 4, Cancelled = 5 }

    public enum VCTestStatus : byte { Scheduled = 1, InProgress = 2, Completed = 3, Cancelled = 4 }
    public enum VCTestResult : byte { Success = 1, Failed = 2, Partial = 3 }
}
