using System.Reflection;
using AIGuard.Api.Auth;
using AIGuard.Api.Review;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace AIGuard.IntegrationTests.DataProtection;

/// <summary>
/// A5: GAB 3.3.8 erişim matrisinin uç bazında uygulanışı (K-32). Uygulamadaki her uç bu tabloda olmalı; yeni bir
/// uç eklenince ya da bir ucun yetkisi değişince bu test kırılır ve tablo bilerek güncellenir.
/// </summary>
/// <remarks>
/// Rol kontrolü burada; kayıt düzeyindeki kural (kendi oturumu, atandığı sınav) servis testlerinde
/// (SessionAccess, ReviewApiTests) sınanıyor.
/// </remarks>
public class AccessMatrixTests
{
    // "Giriş yapmış herkes": yalnızca K-20'deki varsayılan kural; servis kayıt düzeyinde ayrıca süzüyor.
    private const string SignedIn = "SignedIn";

    private const string Anonymous = "Anonymous";

    private static readonly Dictionary<string, string> Expected = new()
    {
        // Herkese açık uçlar; gerekçeleri kodda (K-20).
        ["POST api/auth/login"] = Anonymous,
        ["POST api/auth/refresh"] = Anonymous,
        ["POST api/auth/logout"] = Anonymous,
        ["POST api/auth/set-password"] = Anonymous,
        ["POST api/auth/forgot-password"] = Anonymous,
        ["POST api/auth/reset-password"] = Anonymous,
        ["POST api/agent/pair"] = Anonymous,
        ["POST api/agent/signal"] = Anonymous,
        ["GET api/evidence/{id:guid}"] = Anonymous,

        ["POST api/auth/change-password"] = SignedIn,
        ["GET api/exams"] = SignedIn,
        ["GET api/exams/{id:guid}"] = SignedIn,
        ["GET api/consent-texts/{type}"] = SignedIn,
        ["GET api/consent-texts/{type}/{version}"] = SignedIn,
        // K-49: aday da okur (alternatif yöntem metni, ajan indirme adresi); bildirimler rolüne göre süzülür.
        ["GET api/platform-settings"] = SignedIn,
        ["GET api/notifications"] = SignedIn,

        // Atandığı sınavı çözme, kendi rızasını verme ve geri alma: Aday.
        ["GET api/me/biometric"] = AccessPolicies.TakeExam,
        ["POST api/me/photo"] = AccessPolicies.TakeExam,
        ["DELETE api/me/biometric-consent"] = AccessPolicies.TakeExam,
        ["POST api/exams/{examId:guid}/sessions"] = AccessPolicies.TakeExam,
        ["GET api/sessions/{id:guid}"] = AccessPolicies.TakeExam,
        ["POST api/sessions/{id:guid}/answers"] = AccessPolicies.TakeExam,
        ["POST api/sessions/{id:guid}/submit"] = AccessPolicies.TakeExam,
        ["POST api/sessions/{sessionId:guid}/consent"] = AccessPolicies.TakeExam,
        ["POST api/sessions/{sessionId:guid}/verify"] = AccessPolicies.TakeExam,
        ["POST api/sessions/{sessionId:guid}/events"] = AccessPolicies.TakeExam,
        ["POST api/sessions/{sessionId:guid}/frames"] = AccessPolicies.TakeExam,
        ["POST api/sessions/{sessionId:guid}/audio"] = AccessPolicies.TakeExam,
        ["POST api/sessions/{sessionId:guid}/permission"] = AccessPolicies.TakeExam,
        ["POST api/sessions/{sessionId:guid}/agent-code"] = AccessPolicies.TakeExam,
        ["GET api/sessions/{sessionId:guid}/agent"] = AccessPolicies.TakeExam,
        ["POST api/sessions/{sessionId:guid}/agent/continue"] = AccessPolicies.TakeExam,

        // Kanıt karelerini ve olayları inceleme: atandığı sınavda Gözetmen, Yönetici.
        ["GET api/review/exams/{examId:guid}/sessions"] = AccessPolicies.ReviewRead,
        ["GET api/review/sessions/{id:guid}"] = AccessPolicies.ReviewRead,
        ["GET api/reports/exams/{examId:guid}/sessions/{sessionId:guid}"] = AccessPolicies.ReviewRead,
        ["GET api/reports/exams/{examId:guid}"] = AccessPolicies.ReviewRead,

        // Yanlış alarm işaretleme ve karar kaydetme: yalnızca atandığı sınavda Gözetmen.
        ["POST api/review/sessions/{id:guid}/decision"] = $"{AccessPolicies.ReviewRead}+{AccessPolicies.ReviewDecide}",
        ["POST api/review/sessions/{id:guid}/notes"] = $"{AccessPolicies.ReviewRead}+{AccessPolicies.ReviewDecide}",
        ["POST api/events/{id:guid}/false-alarm"] = $"{AccessPolicies.ReviewRead}+{AccessPolicies.ReviewDecide}",

        // Kullanıcı, sınav, ayar ve saklama süresi yönetimi: Yönetici.
        ["GET api/risk-settings"] = AccessPolicies.Manage,
        ["GET api/users"] = AccessPolicies.Manage,
        ["POST api/users"] = AccessPolicies.Manage,
        ["PATCH api/users/{id:guid}"] = AccessPolicies.Manage,
        ["POST api/users/{id:guid}/invite"] = AccessPolicies.Manage,
        ["PUT api/platform-settings"] = AccessPolicies.Manage,
        ["GET api/exams/{examId:guid}/risk-settings"] = AccessPolicies.Manage,
        ["PUT api/exams/{examId:guid}/risk-settings"] = AccessPolicies.Manage,
        ["DELETE api/exams/{examId:guid}/risk-settings"] = AccessPolicies.Manage,
        ["POST api/exams"] = AccessPolicies.Manage,
        ["PUT api/exams/{id:guid}"] = AccessPolicies.Manage,
        ["DELETE api/exams/{id:guid}"] = AccessPolicies.Manage,
        ["GET api/exams/{id:guid}/questions"] = AccessPolicies.Manage,
        ["POST api/exams/{id:guid}/questions"] = AccessPolicies.Manage,
        ["GET api/exams/{id:guid}/assignments"] = AccessPolicies.Manage,
        ["POST api/exams/{id:guid}/assignments"] = AccessPolicies.Manage,
        ["PUT api/questions/{id:guid}"] = AccessPolicies.Manage,
        ["DELETE api/questions/{id:guid}"] = AccessPolicies.Manage,
        ["DELETE api/assignments/{id:guid}"] = AccessPolicies.Manage,

        // Denetim kaydını görüntüleme: Yönetici, yalnızca okuma.
        ["GET api/audit-logs"] = AccessPolicies.ReadAuditLog,
    };

    [Fact]
    public void Every_endpoint_matches_the_access_matrix()
    {
        var actual = Endpoints().ToDictionary(e => e.Key, e => e.Access);

        // Fark tek mesajda görünsün: eksik, fazla ve farklı uçlar.
        var problems = Expected.Keys.Union(actual.Keys)
            .Where(k => Expected.GetValueOrDefault(k) != actual.GetValueOrDefault(k))
            .Select(k => $"{k}: beklenen {Expected.GetValueOrDefault(k) ?? "(tabloda yok)"}, kodda {actual.GetValueOrDefault(k) ?? "(uç yok)"}")
            .ToList();
        Assert.True(problems.Count == 0, "Erişim matrisi ile kod uyuşmuyor:\n" + string.Join('\n', problems));
    }

    [Fact]
    public void Endpoints_use_named_policies_instead_of_role_lists()
    {
        var withRoles = ApiTypes()
            .SelectMany(t => t.GetMethods().Cast<MemberInfo>().Prepend(t))
            .Where(m => m.GetCustomAttributes<AuthorizeAttribute>().Any(a => a.Roles is not null))
            .Select(m => m is Type t ? t.Name : $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();
        Assert.Empty(withRoles);
    }

    [Fact]
    public void Review_hub_uses_the_review_read_policy()
    {
        var attribute = typeof(ReviewHub).GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal(AccessPolicies.ReviewRead, attribute?.Policy);
    }

    /// <summary>
    /// Matristeki "referans yüz vektörünü görme veya indirme: kimse" satırı. Hiçbir yanıt tipinde vektör, şablon
    /// ya da sayı dizisi alanı olmamalı (A5 kartı: "hiçbir uç referans yüz vektörünü döndürmez").
    /// </summary>
    [Fact]
    public void No_endpoint_returns_a_face_vector()
    {
        var offending = new List<string>();
        var seen = new HashSet<Type>();
        foreach (var type in ResponseTypes())
        {
            Inspect(type, type.Name, seen, offending);
        }

        Assert.Empty(offending);
    }

    private static IEnumerable<Type> ResponseTypes() =>
        ApiTypes()
            .Where(t => t != typeof(ReviewHub))
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .Where(m => m.IsDefined(typeof(HttpMethodAttribute)))
            .SelectMany(m => m.GetCustomAttributes<ProducesResponseTypeAttribute>().Select(a => a.Type).Append(m.ReturnType));

    private static void Inspect(Type type, string path, HashSet<Type> seen, List<string> offending)
    {
        // Task<T>, IReadOnlyList<T> gibi sarmalayıcıların içine bakılıyor.
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                Inspect(argument, path, seen, offending);
            }
        }

        if (type == typeof(float[]) || type == typeof(double[]) || type == typeof(byte[]))
        {
            offending.Add($"{path}: {type.Name}");
            return;
        }

        if (type.Namespace?.StartsWith("AIGuard", StringComparison.Ordinal) != true || type.IsEnum || !seen.Add(type))
        {
            return;
        }

        foreach (var property in type.GetProperties())
        {
            var name = $"{path}.{property.Name}";
            if (property.Name.Contains("Vector", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Embedding", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Template", StringComparison.OrdinalIgnoreCase))
            {
                offending.Add(name);
            }

            Inspect(property.PropertyType, name, seen, offending);
        }
    }

    private static IEnumerable<Type> ApiTypes() =>
        typeof(AccessPolicies).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) || t == typeof(ReviewHub));

    private static IEnumerable<(string Key, string Access)> Endpoints()
    {
        foreach (var controller in typeof(AccessPolicies).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            var prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template;
            foreach (var action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var http in action.GetCustomAttributes<HttpMethodAttribute>())
                {
                    var template = http.Template switch
                    {
                        null or "" => prefix,
                        var t when prefix is null => t,
                        var t => $"{prefix}/{t}",
                    };
                    foreach (var method in http.HttpMethods)
                    {
                        yield return ($"{method} {template}", Access(controller, action));
                    }
                }
            }
        }
    }

    private static string Access(Type controller, MethodInfo action)
    {
        if (controller.IsDefined(typeof(AllowAnonymousAttribute)) || action.IsDefined(typeof(AllowAnonymousAttribute)))
        {
            return Anonymous;
        }

        // Sınıf ve metot düzeyindeki policy'lerin hepsi sağlanmalı (ASP.NET Core birleştiriyor).
        var policies = controller.GetCustomAttributes<AuthorizeAttribute>()
            .Concat(action.GetCustomAttributes<AuthorizeAttribute>())
            .Select(a => a.Policy)
            .OfType<string>()
            .Distinct()
            .ToList();
        return policies.Count == 0 ? SignedIn : string.Join('+', policies);
    }
}
