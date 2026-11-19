using AIGuard.Domain.Entities;
using AIGuard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AIGuard.UnitTests;

/// <summary>
/// Veritabanı modelindeki güvenlik ve bütünlük kurallarının kontrolü.
/// </summary>
public class ModelSecurityTests
{
    private static IModel Model()
    {
        using var db = TestDbContextFactory.Create();
        return db.Model;
    }

    private static IEntityType Entity<T>() => Model().FindEntityType(typeof(T))!;

    [Fact]
    public void Migrations_are_in_sync_with_model()
    {
        // Model değişip "dotnet ef migrations add" unutulursa bu test kırılır.
        using var db = TestDbContextFactory.Create();
        Assert.False(db.Database.HasPendingModelChanges(), "Model değişmiş ama göç eklenmemiş.");
    }

    [Fact]
    public void ReferenceFaceTemplate_has_no_photo_or_image_column()
    {
        // Şablonda yalnızca şifreli vektör olmalı, fotoğraf alanı olmamalı.
        string[] forbidden = ["photo", "image", "picture", "frame", "jpeg", "png"];
        var properties = Entity<ReferenceFaceTemplate>().GetProperties().Select(p => p.Name.ToLowerInvariant());

        Assert.DoesNotContain(properties, name => forbidden.Any(name.Contains));
    }

    [Fact]
    public void ReferenceFaceTemplate_only_binary_column_is_the_encrypted_vector()
    {
        var binary = Entity<ReferenceFaceTemplate>().GetProperties()
            .Where(p => p.ClrType == typeof(byte[]))
            .Select(p => p.Name);

        Assert.Equal([nameof(ReferenceFaceTemplate.EncryptedVector)], binary);
    }

    [Theory]
    [InlineData(typeof(User), "Password")]
    [InlineData(typeof(User), "InviteToken")]
    [InlineData(typeof(RefreshToken), "Token")]
    [InlineData(typeof(AgentPairing), "PairingCode")]
    [InlineData(typeof(AgentPairing), "SessionKey")]
    public void Secrets_are_never_stored_in_plain_text(Type entity, string secretName)
    {
        // Gizli değerler yalnızca …Hash veya Encrypted… alanlarında tutulmalı.
        var names = Model().FindEntityType(entity)!.GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(secretName, names);
        Assert.Contains(names, n => n.StartsWith(secretName) && n.EndsWith("Hash")
                                    || n.StartsWith("Encrypted") && n.EndsWith(secretName));
    }

    public static TheoryData<Type, string[]> UniqueIndexes => new()
    {
        { typeof(User), [nameof(User.NormalizedEmail)] },
        { typeof(RefreshToken), [nameof(RefreshToken.TokenHash)] },
        { typeof(ExamSession), [nameof(ExamSession.ExamId), nameof(ExamSession.CandidateId)] }, // tek oturum hakkı
        { typeof(AgentSignal), [nameof(AgentSignal.AgentPairingId), nameof(AgentSignal.Nonce)] }, // tekrar oynatma
        { typeof(Answer), [nameof(Answer.ExamSessionId), nameof(Answer.Seq)] },
        { typeof(ExamAssignment), [nameof(ExamAssignment.ExamId), nameof(ExamAssignment.UserId)] },
        { typeof(ReferenceFaceTemplate), [nameof(ReferenceFaceTemplate.UserId)] },
        { typeof(ReviewerDecision), [nameof(ReviewerDecision.ExamSessionId)] }, // 0..1 karar
        { typeof(AgentPairing), [nameof(AgentPairing.ExamSessionId)] },
        { typeof(EvidenceFrame), [nameof(EvidenceFrame.EventId)] }, // 0..1 kanıt
    };

    [Theory]
    [MemberData(nameof(UniqueIndexes))]
    public void Critical_unique_indexes_exist(Type entity, string[] columns)
    {
        var indexes = Model().FindEntityType(entity)!.GetIndexes();

        Assert.Contains(indexes, i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(columns));
    }

    [Fact]
    public void Enums_are_stored_as_text_except_flags()
    {
        // K-04: SessionFlags hariç tüm enum sütunları metin olmalı.
        var enumProperties = Model().GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType).IsEnum)
            .ToList();

        Assert.NotEmpty(enumProperties);
        foreach (var p in enumProperties)
        {
            var providerType = p.GetTypeMapping().Converter?.ProviderClrType ?? p.ClrType;
            if (p.ClrType == typeof(SessionFlags))
            {
                Assert.NotEqual(typeof(string), providerType);
            }
            else
            {
                Assert.True(providerType == typeof(string), $"{p.DeclaringType.DisplayName()}.{p.Name} metin olarak saklanmalı.");
            }
        }
    }

    [Fact]
    public void Nothing_cascades_into_audit_logs()
    {
        // Cascade veya SetNull, başka bir kayıt silinince denetim satırını değiştirebilirdi.
        var auditForeignKeys = Entity<AuditLog>().GetForeignKeys();

        Assert.All(auditForeignKeys, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }

    [Fact]
    public void ExamSession_uses_optimistic_concurrency()
    {
        var version = Entity<ExamSession>().FindProperty(nameof(ExamSession.Version))!;

        Assert.True(version.IsConcurrencyToken);
        Assert.Equal("xmin", version.GetColumnName());
    }

    [Fact]
    public void AuditLog_properties_are_init_only()
    {
        // Kod içinde bir AuditLog nesnesi oluşturulduktan sonra değiştirilemesin.
        var settableAfterInit = typeof(AuditLog).GetProperties()
            .Where(p => p.DeclaringType == typeof(AuditLog) && p.SetMethod is not null)
            .Where(p => !p.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
                .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)))
            .Select(p => p.Name);

        Assert.Empty(settableAfterInit);
    }
}
