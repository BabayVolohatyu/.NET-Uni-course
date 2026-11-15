using MongoDB.Bson;
using RiskAssessments.Domain.Common;
using RiskAssessments.Domain.Enums;
using RiskAssessments.Domain.Exceptions;
using RiskAssessments.Domain.ValueObjects;

namespace RiskAssessments.Domain.Entities;

/// <summary>
/// Auditor annotation in the <c>auditNotes</c> collection.
/// Small attachments are embedded; a long discussion is referenced by <see cref="DiscussionId"/>
/// (hybrid model from Lab 01).
/// </summary>
public class AuditNote : BaseEntity
{
    private List<Attachment> _attachments = [];

    public ObjectId RiskAssessmentId { get; private set; }

    public Auditor Author { get; private set; } = null!;

    /// <summary>Flat author id required by the Lab 01 JSON schema.</summary>
    public string AuthorId { get; private set; } = "";

    /// <summary>Flat author name required by the Lab 01 JSON schema.</summary>
    public string AuthorName { get; private set; } = "";

    public string Text { get; private set; } = "";

    public NoteType NoteType { get; private set; }

    public bool IsPinned { get; private set; }

    public bool IsResolved { get; private set; }

    public ObjectId? DiscussionId { get; private set; }

    public IReadOnlyCollection<Attachment> Attachments => _attachments.AsReadOnly();

    private AuditNote()
    {
    }

    public AuditNote(
        ObjectId riskAssessmentId,
        Auditor author,
        string text,
        NoteType noteType,
        bool isPinned = false)
    {
        if (riskAssessmentId == ObjectId.Empty)
            throw new DomainException("Risk assessment id is required.");

        ArgumentNullException.ThrowIfNull(author);

        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Audit note text is required.");

        RiskAssessmentId = riskAssessmentId;
        Author = author;
        AuthorId = author.UserId;
        AuthorName = author.DisplayName;
        Text = text.Trim();
        NoteType = noteType;
        IsPinned = isPinned;
    }

    public void AddAttachment(Attachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _attachments.Add(attachment);
        Touch();
    }

    public void AttachDiscussion(ObjectId discussionId)
    {
        if (discussionId == ObjectId.Empty)
            throw new DomainException("Discussion id is required.");

        DiscussionId = discussionId;
        Touch();
    }

    public void Resolve()
    {
        if (IsResolved)
            throw new DomainException("Audit note is already resolved.");

        IsResolved = true;
        Touch();
    }
}
