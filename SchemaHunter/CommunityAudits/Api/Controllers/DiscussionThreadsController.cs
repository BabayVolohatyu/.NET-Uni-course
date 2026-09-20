using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using SchemaHunter.CommunityAudits.Domain.Documents;
using SchemaHunter.CommunityAudits.Infrastructure.Repositories;

namespace SchemaHunter.CommunityAudits.Api.Controllers;

/// <summary>
/// Manages discussion threads attached to inspection audits.
/// </summary>
[ApiController]
[Route("api/discussion-threads")]
[Produces("application/json")]
public sealed class DiscussionThreadsController : ControllerBase
{
    private readonly DiscussionThreadRepository _threads;

    public DiscussionThreadsController(DiscussionThreadRepository threads) => _threads = threads;

    // GET /api/discussion-threads/by-audit/{auditId}
    /// <summary>Returns all threads for the given audit, newest first.</summary>
    [HttpGet("by-audit/{auditId}")]
    [ProducesResponseType<List<DiscussionThread>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByAudit(string auditId, CancellationToken ct = default)
    {
        var threads = await _threads.GetByAuditAsync(auditId, ct);
        return Ok(threads);
    }

    // GET /api/discussion-threads/{id}
    /// <summary>Returns a single thread by its ObjectId.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<DiscussionThread>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct = default)
    {
        var thread = await _threads.GetByIdAsync(id, ct);
        return thread is null ? NotFound() : Ok(thread);
    }

    // POST /api/discussion-threads
    /// <summary>Opens a new discussion thread on an audit.</summary>
    [HttpPost]
    [ProducesResponseType<DiscussionThread>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateThreadRequest req,
        CancellationToken ct = default)
    {
        var thread = new DiscussionThread
        {
            Id         = ObjectId.GenerateNewId().ToString(),
            AuditId    = req.AuditId,
            TopicTitle = req.TopicTitle,
            CreatedBy  = req.CreatedBy,
            Comments   = new(),
            IsLocked   = false,
            CreatedAt  = DateTime.UtcNow,
            UpdatedAt  = DateTime.UtcNow
        };

        await _threads.InsertAsync(thread, ct);
        return CreatedAtAction(nameof(GetById), new { id = thread.Id }, thread);
    }

    // POST /api/discussion-threads/{id}/comments
    /// <summary>Adds a comment to an existing thread.</summary>
    [HttpPost("{id}/comments")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddComment(
        string id,
        [FromBody] AddCommentRequest req,
        CancellationToken ct = default)
    {
        var thread = await _threads.GetByIdAsync(id, ct);
        if (thread is null) return NotFound();
        if (thread.IsLocked) return Conflict(new { error = "Thread is locked." });

        var comment = new ThreadComment
        {
            CommentId  = ObjectId.GenerateNewId().ToString(),
            AuthorName = req.AuthorName,
            Body       = req.Body,
            PostedAt   = DateTime.UtcNow,
            Replies    = new()
        };

        await _threads.AddCommentAsync(id, comment, ct);
        return NoContent();
    }

    // POST /api/discussion-threads/{id}/lock
    /// <summary>Locks a thread, preventing further comments.</summary>
    [HttpPost("{id}/lock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Lock(string id, CancellationToken ct = default)
    {
        await _threads.LockAsync(id, ct);
        return NoContent();
    }
}

// ─── Request models ───────────────────────────────────────────────────────────

/// <summary>Payload for <c>POST /api/discussion-threads</c>.</summary>
public record CreateThreadRequest(
    string        AuditId,
    string        TopicTitle,
    ThreadCreator CreatedBy);

/// <summary>Payload for <c>POST /api/discussion-threads/{id}/comments</c>.</summary>
public record AddCommentRequest(string AuthorName, string Body);
