using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using RiskAssessments.Domain.Common;
using RiskAssessments.Domain.Entities;
using RiskAssessments.Domain.ValueObjects;

namespace RiskAssessments.Infrastructure.Persistence;

/// <summary>
/// BSON maps live here so domain classes stay free of MongoDB attributes.
/// Registered once, before the first database call.
/// </summary>
public static class MongoMappings
{
    private static int _registered;

    public static void Register()
    {
        if (Volatile.Read(ref _registered) == 1)
            return;

        lock (Gate)
        {
            if (_registered == 1)
                return;

            RegisterCore();
            Volatile.Write(ref _registered, 1);
        }
    }

    private static readonly object Gate = new();

    private static void RegisterCore()
    {

        ConventionRegistry.Register(
            "riskAssessmentCamelCase",
            new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
                new EnumRepresentationConvention(BsonType.String),
                new PreferParameterlessConstructorConvention()
            },
            _ => true);

        if (!BsonClassMap.IsClassMapRegistered(typeof(BaseEntity)))
        {
            BsonClassMap.RegisterClassMap<BaseEntity>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                if (cm.IdMemberMap is null && cm.GetMemberMap(nameof(BaseEntity.Id)) is { } id)
                    cm.SetIdMember(id);
                cm.GetMemberMap(nameof(BaseEntity.UpdatedAt))?.SetIgnoreIfNull(true);
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(Auditor)))
        {
            BsonClassMap.RegisterClassMap<Auditor>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(a => new Auditor(a.UserId, a.DisplayName));
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(Money)))
        {
            BsonClassMap.RegisterClassMap<Money>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(m => new Money(m.Amount, m.Currency));
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(Attachment)))
        {
            BsonClassMap.RegisterClassMap<Attachment>(cm =>
            {
                cm.AutoMap();
                cm.MapCreator(a => new Attachment(a.Name, a.Url));
            });
        }

        RegisterEntity<TriggeredFlag>(cm =>
        {
            IgnoreIfNull(cm, nameof(TriggeredFlag.AffectedField));
            IgnoreIfNull(cm, nameof(TriggeredFlag.ContractorId));
            IgnoreIfNull(cm, nameof(TriggeredFlag.ContractorName));
            IgnoreIfNull(cm, nameof(TriggeredFlag.ContractorActive));
            IgnoreIfNull(cm, nameof(TriggeredFlag.CompanyAgeDays));
            IgnoreIfNull(cm, nameof(TriggeredFlag.AuthorizedCapital));
        });

        RegisterEntity<RiskAssessment>(cm =>
        {
            UnmapReadOnly(cm, nameof(RiskAssessment.TriggeredFlags));
            UnmapReadOnly(cm, nameof(RiskAssessment.AuditNoteIds));
            cm.MapField("_triggeredFlags").SetElementName("triggeredFlags");
            cm.MapField("_auditNoteIds").SetElementName("auditNoteIds");
            IgnoreIfNull(cm, nameof(RiskAssessment.ExpectedBudget));
            IgnoreIfNull(cm, nameof(RiskAssessment.UpdatedAt));
        });

        RegisterEntity<AuditNote>(cm =>
        {
            UnmapReadOnly(cm, nameof(AuditNote.Attachments));
            cm.MapField("_attachments").SetElementName("smallAttachments");
            IgnoreIfNull(cm, nameof(AuditNote.DiscussionId));
            IgnoreIfNull(cm, nameof(AuditNote.UpdatedAt));
        });
    }

    private static void RegisterEntity<T>(Action<BsonClassMap<T>> extra)
        where T : class
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(T)))
            return;

        BsonClassMap.RegisterClassMap<T>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            UsePrivateParameterlessConstructor(cm);
            extra(cm);
        });
    }

    /// <summary>
    /// Runs when the class map is frozen, after the default conventions have
    /// registered the public constructor. Deserialization must use the private
    /// parameterless constructor so embedded collections are applied to fields.
    /// </summary>
    private sealed class PreferParameterlessConstructorConvention : IClassMapConvention
    {
        public string Name => nameof(PreferParameterlessConstructorConvention);

        public void Apply(BsonClassMap classMap)
        {
            if (!typeof(BaseEntity).IsAssignableFrom(classMap.ClassType) || classMap.ClassType == typeof(BaseEntity))
                return;

            foreach (var ctor in classMap.ClassType.GetConstructors(BindingFlags.Instance | BindingFlags.Public))
                classMap.UnmapConstructor(ctor);

            var parameterless = classMap.ClassType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                Type.EmptyTypes,
                modifiers: null);

            if (parameterless is not null)
                classMap.MapConstructor(parameterless);
        }
    }

    private static void UsePrivateParameterlessConstructor<T>(BsonClassMap<T> cm)
    {
        foreach (var ctor in typeof(T).GetConstructors(BindingFlags.Instance | BindingFlags.Public))
            cm.UnmapConstructor(ctor);

        var parameterless = typeof(T).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);

        if (parameterless is not null)
            cm.MapConstructor(parameterless);
    }

    private static void UnmapReadOnly<T>(BsonClassMap<T> cm, string propertyName)
    {
        if (cm.GetMemberMap(propertyName) is not null)
            cm.UnmapProperty(propertyName);
    }

    private static void IgnoreIfNull<T>(BsonClassMap<T> cm, string memberName)
    {
        cm.GetMemberMap(memberName)?.SetIgnoreIfNull(true);
    }
}
