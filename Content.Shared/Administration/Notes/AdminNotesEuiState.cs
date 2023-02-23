using Content.Shared.Database;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration.Notes;

[Serializable, NetSerializable]
public sealed class AdminNotesEuiState : EuiStateBase
{
    public AdminNotesEuiState(string notedPlayerName, Dictionary<int, SharedAdminNote> notes, bool canCreate, bool canDelete, bool canEdit)
    {
        NotedPlayerName = notedPlayerName;
        Notes = notes;
        CanCreate = canCreate;
        CanDelete = canDelete;
        CanEdit = canEdit;
    }

    public string NotedPlayerName { get; }
    public Dictionary<int, SharedAdminNote> Notes { get; }
    public bool CanCreate { get; }
    public bool CanDelete { get; }
    public bool CanEdit { get; }
}

public static class AdminNoteEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class Close : EuiMessageBase
    {
    }

    [Serializable, NetSerializable]
    public sealed class CreateNoteRequest : EuiMessageBase
    {
        public CreateNoteRequest(NoteType type, string message, NoteSeverity severity, bool secret, DateTime? expiryTime)
        {
            NoteType = type;
            Message = message;
            NoteSeverity = severity;
            Secret = secret;
            ExpiryTime = expiryTime;
        }

        public NoteType NoteType { get; set; }
        public string Message { get; set; }
        public NoteSeverity NoteSeverity { get; set; }
        public bool Secret { get; set; }
        public DateTime? ExpiryTime { get; set; }
    }

    [Serializable, NetSerializable]
    public sealed class DeleteNoteRequest : EuiMessageBase
    {
        public DeleteNoteRequest(int id)
        {
            Id = id;
        }

        public int Id { get; set; }
    }

    [Serializable, NetSerializable]
    public sealed class EditNoteRequest : EuiMessageBase
    {
        public EditNoteRequest(int id, string message, NoteSeverity severity, bool secret, DateTime? expiryTime)
        {
            Id = id;
            Message = message;
            NoteSeverity = severity;
            Secret = secret;
            ExpiryTime = expiryTime;
        }

        public int Id { get; set; }
        public string Message { get; set; }
        public NoteSeverity NoteSeverity { get; set; }
        public bool Secret { get; set; }
        public DateTime? ExpiryTime { get; set; }
    }
}
