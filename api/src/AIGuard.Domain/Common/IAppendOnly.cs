namespace AIGuard.Domain.Common;

/// <summary>
/// Yalnızca eklenen kayıtlar: denetim kaydı ve rıza metinleri (K-10, K-32). SaveChanges güncelleme ve
/// silmeyi reddediyor; veritabanında da aynı kuralı uygulayan tetikleyici var.
/// </summary>
public interface IAppendOnly;
