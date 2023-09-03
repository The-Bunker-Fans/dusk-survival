using System.Linq;
using System.Numerics;
using Content.Shared.Administration.Notes;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Configuration;

namespace Content.Client.Administration.UI.Notes;

[GenerateTypedNameReferences]
public sealed partial class AdminNotesControl : Control
{
    [Dependency] private IEntitySystemManager _entitySystem = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public event Action<int, NoteType, string, NoteSeverity?, bool, DateTime?>? NoteChanged;
    public event Action<NoteType, string, NoteSeverity?, bool, DateTime?>? NewNoteEntered;
    public event Action<int, NoteType>? NoteDeleted;

    private AdminNotesLinePopup? _popup;
    private readonly SpriteSystem _sprites;
    private readonly double _noteFreshDays;
    private readonly double _noteStaleDays;

    public AdminNotesControl()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);
        _sprites = _entitySystem.GetEntitySystem<SpriteSystem>();

        // There should be a warning somewhere if fresh > stale
        // I thought about putting it here but then it would spam you every time you open notes
        _noteFreshDays = _cfg.GetCVar(CCVars.NoteFreshDays);
        _noteStaleDays = _cfg.GetCVar(CCVars.NoteStaleDays);

        NewNoteButton.OnPressed += OnNewNoteButtonPressed;
        ShowMoreButton.OnPressed += OnShowMoreButtonPressed;
    }

    private Dictionary<(int noteId, NoteType noteType), AdminNotesLine> Inputs { get; } = new();
    private bool CanCreate { get; set; }
    private bool CanDelete { get; set; }
    private bool CanEdit { get; set; }
    private string PlayerName { get; set; } = "<Error>";

    public void SetPlayerName(string playerName)
    {
        PlayerName = playerName;
    }

    private void OnNewNoteButtonPressed(BaseButton.ButtonEventArgs obj)
    {
        var noteEdit = new NoteEdit(null, PlayerName, CanCreate, CanEdit);
        noteEdit.SubmitPressed += OnNoteSubmitted;
        noteEdit.OpenCentered();
    }

    private void OnNoteSubmitted(int id, NoteType type, string message, NoteSeverity? severity, bool secret, DateTime? expiryTime)
    {
        if (id == 0)
        {
            NewNoteEntered?.Invoke(type, message, severity, secret, expiryTime);
            return;
        }

        NoteChanged?.Invoke(id, type, message, severity, secret, expiryTime);
    }

    private bool NoteClicked(AdminNotesLine line)
    {
        _popup = new AdminNotesLinePopup(line.Note, PlayerName, CanDelete, CanEdit);
        _popup.OnEditPressed += (noteId, noteType) =>
        {
            if (!Inputs.TryGetValue((noteId, noteType), out var input))
            {
                return;
            }

            var noteEdit = new NoteEdit(input.Note, PlayerName, CanCreate, CanEdit);
            noteEdit.SubmitPressed += OnNoteSubmitted;
            noteEdit.OpenCentered();
        };

        _popup.OnDeletePressed += (noteId, noteType) => NoteDeleted?.Invoke(noteId, noteType);
        _popup.OnPopupHide += OnPopupHide;

        var box = UIBox2.FromDimensions(UserInterfaceManager.MousePositionScaled.Position, Vector2.One);
        _popup.Open(box);

        return true;
    }

    private void OnPopupHide()
    {
        if (_popup == null ||
            !Inputs.TryGetValue((_popup.NoteId, _popup.NoteType), out var input))
        {
            return;
        }

        UpdateNoteLineAlpha(input);
    }

    private void NoteMouseEntered(GUIMouseHoverEventArgs args)
    {
        if (args.SourceControl is not AdminNotesLine line)
            return;

        line.Modulate = line.Modulate.WithAlpha(1f);
    }

    private void NoteMouseExited(GUIMouseHoverEventArgs args)
    {
        if (args.SourceControl is not AdminNotesLine line)
            return;

        if (_popup?.NoteId == line.Note.Id && _popup.Visible)
            return;

        UpdateNoteLineAlpha(line);
    }

    private void UpdateNoteLineAlpha(AdminNotesLine input)
    {
        var timeDiff = DateTime.UtcNow - input.Note.CreatedAt;
        float alpha;
        if (_noteFreshDays == 0 || timeDiff.TotalDays <= _noteFreshDays)
        {
            alpha = 1f;
        }
        else if (_noteStaleDays == 0 || timeDiff.TotalDays > _noteStaleDays)
        {
            alpha = 0f;
        }
        else
        {
            alpha = (float) (1 - Math.Clamp((timeDiff.TotalDays - _noteFreshDays) / (_noteStaleDays - _noteFreshDays), 0, 1));
        }

        input.Modulate = input.Modulate.WithAlpha(alpha);
    }

    public void SetNotes(Dictionary<(int, NoteType), SharedAdminNote> notes)
    {
        foreach (var (key, input) in Inputs)
        {
            if (!notes.ContainsKey(key))
            {
                // Yes this is slower than just updating, but new notes get added at the bottom. The user won't notice.
                Notes.RemoveAllChildren();
                Inputs.Clear();
                break;
            }
            Notes.RemoveChild(input);
            Inputs.Remove(key);
        }

        var showMoreButtonVisible = false;
        foreach (var note in notes.Values.OrderByDescending(note => note.CreatedAt))
        {
            if (Inputs.TryGetValue((note.Id, note.NoteType), out var input))
            {
                input.UpdateNote(note);
                continue;
            }

            input = new AdminNotesLine(_sprites, note);
            input.OnClicked += NoteClicked;
            input.OnMouseEntered += NoteMouseEntered;
            input.OnMouseExited += NoteMouseExited;

            UpdateNoteLineAlpha(input);

            if (input.Modulate.A == 0)
            {
                input.Visible = false;
                showMoreButtonVisible = true;
            }

            Notes.AddChild(input);
            Inputs[(note.Id, note.NoteType)] = input;
            ShowMoreButton.Visible = showMoreButtonVisible;
        }
    }

    private void OnShowMoreButtonPressed(BaseButton.ButtonEventArgs obj)
    {
        foreach (var input in Inputs.Values)
        {
            input.Modulate = input.Modulate.WithAlpha(1f);
            input.Visible = true;
        }

        ShowMoreButton.Visible = false;
    }

    public void SetPermissions(bool create, bool delete, bool edit)
    {
        CanCreate = create;
        CanDelete = delete;
        CanEdit = edit;
        NewNoteButton.Visible = create;
        NewNoteButton.Disabled = !create;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        Inputs.Clear();
        NewNoteButton.OnPressed -= OnNewNoteButtonPressed;

        if (_popup != null)
        {
            UserInterfaceManager.PopupRoot.RemoveChild(_popup);
        }

        NoteDeleted = null;
    }
}
